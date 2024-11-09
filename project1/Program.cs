using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text;
using MathNet.Numerics.Data.Matlab;
using MathNet.Numerics.LinearAlgebra;
using CS120.Symbol;
using CS120.Preamble;
using CS120.TxRx;
using CS120.Modulate;
using CS120.Packet;
using System.IO.Pipelines;
using CS120.Utils;
using System.Buffers;
// using System.IO.Pipes;
using System.Runtime.InteropServices;

namespace CS120;

class Program
{
    // static readonly string defaultAudioFileName = "assets/初星学園,ギガP,花海咲季 - Fighting My Way -
    // Instrumental.mp3"; static readonly string defaultRecordFileName = "assets/recorded.wav";

    public static DPSKSymbolOption option =
        new() { NumSymbols = 2, NumRedundant = 1, SampleRate = 48000, Freq = 6_000 };

    public static ChirpSymbolOption chirpOption = new() {
        NumSymbols = 2,
        Duration = 0.005f, // Read config or something
        SampleRate = 48_000,
        FreqA = 3_000, // Read config or something
        FreqB = 10_000 // Read config or something
    };

    public static float corrThreshold = 0.1f;
    public static int maxPeakFalling = chirpOption.NumSamplesPerSymbol;
    public static float smoothedEnergyFactor = 1f / 64f;

    public static int eccNums = 2;
    public static int dataNum = 32;

    public const int magicNum = 1;
    public const int idNum = 2;
    public const int lengthNum = 2;
    public static int dataLengthInByte = dataNum + eccNums + magicNum + lengthNum + idNum;
    // public static int dataLengthInBit = (2 + 60 + 40) * 8;

    static Receiver InitReceiver<TWaveIn>(WaveFormat waveFormat, PipeReader pipe)
        where TWaveIn : IWaveIn, new()
    {
        // pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));

        // var rec = audioManager.Record<TWaveIn>(pipe.Writer.AsStream(), CancellationToken.None);
        var receiver = new Receiver(
            // receivedFormat,
            // new StreamWaveProvider(receivedFormat, pipe.Reader.AsStream()).ToSampleProvider().ToMono(),
            new PreambleDetection(
                pipe,
                waveFormat,
                new ChirpPreamble(chirpOption with { SampleRate = waveFormat.SampleRate }),
                corrThreshold,
                smoothedEnergyFactor,
                maxPeakFalling
            ),
            new DPSKDemodulator(pipe, waveFormat, option with { SampleRate = waveFormat.SampleRate }),
            // new OFDMDemodulator(
            //     pipe,
            //     waveFormat,
            //     [
            //         option with { SampleRate = waveFormat.SampleRate },
            //         option with {
            //             Freq = option.Freq * 2,
            //             NumRedundant = option.NumRedundant * 2,
            //             SampleRate = waveFormat.SampleRate
            //         }
            //     ]
            // ),
            new DemodulateLength.FixedLength(dataLengthInByte)
        );
        return receiver;
    }

    static Transmitter InitTransmitter<TWaveOut>(WaveFormat waveFormat, PipeWriter pipe)
        where TWaveOut : IWavePlayer, new()
    {
        var transmitter = new Transmitter(
            waveFormat,
            pipe,
            new ChirpPreamble(chirpOption with { SampleRate = waveFormat.SampleRate }),
            new DPSKModulator(pipe, option with { SampleRate = waveFormat.SampleRate }),
            // new OFDMModulator(
            //     pipe,
            //     [
            //         option with { SampleRate = waveFormat.SampleRate },
            //         option with {
            //             Freq = option.Freq * 2,
            //             NumRedundant = option.NumRedundant * 2,
            //             SampleRate = waveFormat.SampleRate
            //         }
            //     ]
            // ),
            // sendFormat.ConvertLatencyToByteSize
            0
            // sendFormat.ConvertLatencyToByteSize(1)
        );
        return transmitter;
    }

    [STAThread]
    static int Main(string[] args)
    {
        // AudioManager.ListAsioDevice().ToList().ForEach(x => Console.WriteLine(x));

        var audioManager = new AudioManager();

        var rootCommand = new RootCommand("CS120 project CLI program, welcome to use!");

        rootCommand.AddCommand(BuildAudioCommand(audioManager));
        rootCommand.AddCommand(BuildSendCommand(audioManager));
        rootCommand.AddCommand(BuildReceiveCommand(audioManager));

        // rootCommand.SetHandler(
        //     () =>
        //     {
        //     }
        // );

        return rootCommand.Invoke(args);
    }

    static Command BuildAudioCommand(AudioManager audioManager)
    {
        var command = new Command("audio", "play with audio stuff");

        var playOption = new Option < FileInfo ? > (name: "--play",
                                                    description: "The file path to play",
                                                    isDefault: true,
                                                    parseArgument: result => ParseSingleFileInfo(result));

        var recordOption = new Option < FileInfo ? > (name: "--record",
                                                      description: "The file path to record",
                                                      isDefault: true,
                                                      parseArgument: result => ParseSingleFileInfo(result, false));

        var recordPlayBackOption = new Option<bool>(
            name: "--playback-record",
            description: "Record and then playback, not saving to file",
            getDefaultValue: () => false
        );

        var durationOption = new Option<int>(
            name: "--duration",
            description: "Time duration of audio operation, in seconds, -1 means infinite until canceled by user",
            isDefault: true,
            parseArgument: result =>
                result.Tokens.Count == 0 ? -1 : (int)(float.Parse(result.Tokens.Single().Value) * 1000)
        );

        command.AddOption(playOption);
        command.AddOption(recordOption);
        command.AddOption(recordPlayBackOption);
        command.AddOption(durationOption);
        command.SetHandler(
            async (FileInfo? play, FileInfo? record, bool recordPlayBack, int duration) =>
            {
            await AudioCommandTask(play, record, recordPlayBack, duration, audioManager);
            },
         playOption, recordOption, recordPlayBackOption, durationOption
        );
        return command;
    }

    static Command BuildSendCommand(AudioManager audioManager)
    {
        var command = new Command("send", "send data");

        var fileArgument = new Argument < FileInfo
            ? > (name: "input", description: "The file path to save", parse: result => ParseSingleFileInfo(result));

        var toWavOption = new Option < FileInfo ? > (name: "--to-wav",
                                                     description: "Export audio data to wav file",
                                                     isDefault: true,
                                                     parseArgument: result => ParseSingleFileInfo(result, false));

        var binaryTxtOption = new Option<bool>(
            name: "--binary-txt", description: "original txt are binary", getDefaultValue: () => false
        );

        command.AddArgument(fileArgument);
        command.AddOption(toWavOption);
        command.AddOption(binaryTxtOption);

        command.SetHandler(
            async (file, toWav, binaryTxt) =>
            { await SendCommandTask(audioManager, file, toWav, binaryTxt); },
            fileArgument,
            toWavOption,
            binaryTxtOption
        );
        return command;
    }

    static Command BuildReceiveCommand(AudioManager audioManager)
    {
        var command = new Command("receive", "receive data");

        var fileOption = new Option < FileInfo ? > (name: "--file",
                                                    description: "The file path to save, otherwise stdout",
                                                    isDefault: true,
                                                    parseArgument: result => ParseSingleFileInfo(result, false));

        var fromWavOption = new Option < FileInfo ? > (name: "--from-wav",
                                                       description: "Import audio data from wav file",
                                                       isDefault: true,
                                                       parseArgument: result => ParseSingleFileInfo(result));

        var binaryTxtOption = new Option<bool>(
            name: "--binary-txt", description: "original txt are binary", getDefaultValue: () => false
        );

        command.AddOption(fromWavOption);
        command.AddOption(fileOption);
        command.AddOption(binaryTxtOption);

        command.SetHandler(
            async (fromWav, file, binaryTxt) => await ReceiveCommandTask(audioManager, fromWav, file, binaryTxt),
            fromWavOption,
            fileOption,
            binaryTxtOption
        );
        return command;
    }

    static FileInfo? ParseSingleFileInfo(ArgumentResult result, bool checkExist = true)
    {
        if (result.Tokens.Count == 0)
        {
            return null;
        }
        string? filePath = result.Tokens.Single().Value;
        if (checkExist && !File.Exists(filePath))
        {
            result.ErrorMessage = "File does not exist";
            return null;
        }
        else
        {
            return new FileInfo(filePath);
        }
    }

    static async Task
    AudioCommandTask(FileInfo? play, FileInfo? record, bool recordPlayBack, int duration, AudioManager manager)
    {
        // var audioCtses = new CancellationTokenSource[2] { new(duration), new(duration) };
        using var cancelToken = new CancelKeyPressCancellationTokenSource(new CancellationTokenSource(duration));
        using var cancelToken1 = new CancelKeyPressCancellationTokenSource(new CancellationTokenSource(), false);

        var taskPlay = play switch {
            FileInfo => manager.Play<WasapiOut>(play.FullName, cancelToken.Source.Token),
            null => Task.CompletedTask,
        };

        var taskRecord = (record, recordPlayBack) switch {
            (_, true) => manager.RecordThenPlay<WasapiCapture, WasapiOut>(new[] { cancelToken, cancelToken1 }.Select(
                cts =>
                {
                    cts.Enable(true);
                    cts.Source.CancelAfter(duration);
                    return cts.Source.Token;
                }
            )),
            (FileInfo, _) => manager.Record<WasapiCapture>(record.FullName, cancelToken.Source.Token),
            _ => Task.CompletedTask,
        };

        var taskUni = Task.WhenAll(taskPlay, taskRecord);

        // using (var cancelToken1 = new CancelKeyPressCancellationTokenSource(new CancellationTokenSource())) using
        //     var cancelToken2 = new CancelKeyPressCancellationTokenSource(new CancellationTokenSource())
        // )
        // {
        //     await((record, recordPlayBack) switch {
        //         (_, true) => manager.RecordThenPlay<WasapiCapture, WasapiOut>(Enumerable.Range(0, 2).Select(
        //             (_, _) =>
        //             { cancelToken1 }
        //         )),
        //         (FileInfo, _) => manager.Record<WasapiCapture>(record.FullName, cancelToken1.Source.),
        //         _ => Task.CompletedTask
        //     });
        // }
        // foreach (var audioCts in audioCtses)
        // {
        //     using var cancelToken = new CancelKeyPressCancellationToken();
        //     // using var cts = new CancellationTokenSource();
        //     var delayTask = Task.Delay(duration, cancelToken.Token);

        //     // ConsoleCancelEventHandler cancelHandler =
        //     //     new((s, e) =>
        //     //         {
        //     //             e.Cancel = true;
        //     //             cts.Cancel();
        //     //         });

        //     // Console.CancelKeyPress += cancelHandler;

        //     if (await Task.WhenAny(taskUni, delayTask) == delayTask)
        //     {
        //         audioCts.Cancel();
        //     }

        //     // Console.CancelKeyPress -= cancelHandler;
        // }
        await taskUni;
    }
    static async Task SendCommandTask(AudioManager audioManager, FileInfo? file, FileInfo? toWav, bool binaryTxt)
    {
        using var cts = new CancelKeyPressCancellationTokenSource(new CancellationTokenSource());

        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));

        // var wavePro
        // var play = Task.CompletedTask;
        var waveFormat = audioManager.GetPlayerWaveFormat<WasapiOut>();
        var provider = new NonBlockingPipeWaveProvider(
            WaveFormat.CreateIeeeFloatWaveFormat(waveFormat.SampleRate, 1), pipe.Reader
        );

        var play = toWav switch {
            null => audioManager.Play<WasapiOut>(provider, cts.Source.Token),
            FileInfo => Task.Run(
                () =>
                {
                    using var writer = new WaveFileWriter(toWav.FullName, provider.WaveFormat);
                    pipe.Reader.AsStream().CopyTo(writer);
                }
            ),
        };

        using var transmitter = InitTransmitter<WasapiOut>(waveFormat, pipe.Writer);

        var exec = transmitter.Execute(cts.Source.Token);

        var dataPipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));
        var readTask = Task.Run(
            () =>
            {
                if (file is not null)
                {
                    if (binaryTxt)
                    {
                        using var fileData = file.OpenText();
                        var txt = fileData.ReadToEnd().ToArray();
                        // foreach (var b in txt)
                        // {
                        //     Console.WriteLine(b);
                        //     // Console.WriteLine(Convert.ToString(b, 2).PadLeft(8, '0'));
                        // }
                        // foreach (var b in w)
                        // {
                        //     Console.WriteLine(Convert.ToString(b, 2).PadLeft(8, '0'));
                        // }
                        dataPipe.Writer.Write(DataHelper.Convert01ToBytes(txt));
                    }
                    else
                    {
                        file.OpenRead().CopyTo(dataPipe.Writer.AsStream());
                    }
                    dataPipe.Writer.Complete();
                }
            }
        );

        var index = 0;
        while (true)
        {
            var read = await dataPipe.Reader.ReadAsync(cts.Source.Token);
            var buffer = read.Buffer;
            // Console.WriteLine(buffer.Length);
            while (DataHelper.TryChunkData(dataNum, read.IsCompleted, ref buffer, out var chunk))
            {
                var dataArray = chunk.ToArray();

                // foreach (var d in dataArray)
                // {
                //     Console.WriteLine(Convert.ToString(d, 2).PadLeft(8, '0'));
                // }
                await transmitter.Packets.WriteAsync(
                    dataArray.IDEncode(index++).LengthEncode(dataNum + idNum).RSEncode(eccNums), cts.Source.Token
                );
            }

            dataPipe.Reader.AdvanceTo(buffer.Start, buffer.End);

            if (read.IsCompleted)
                break;
        }

        transmitter.Packets.Complete();

        await exec;
        await play;
    }

    static async Task ReceiveCommandTask(AudioManager audioManager, FileInfo? fromWav, FileInfo? file, bool binaryTxt)
    {
        using var cts = new CancelKeyPressCancellationTokenSource(new CancellationTokenSource());

        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));

        Task rec;
        WaveFormat waveFormat;

        if (fromWav is null)
        {
            rec = audioManager.Record<WasapiCapture>(pipe.Writer.AsStream(), cts.Source.Token);
            waveFormat = audioManager.GetRecorderWaveFormat<WasapiCapture>();
        }
        else
        {
            rec = Task.CompletedTask;
            using var reader = new WaveFileReader(fromWav.FullName);
            waveFormat = reader.WaveFormat;
            reader.CopyTo(pipe.Writer.AsStream());
            pipe.Writer.Complete();
        }
        Console.WriteLine(waveFormat.SampleRate);

        // PipeStream
        using var receiver = InitReceiver<WasapiCapture>(waveFormat, pipe.Reader);
        var exec = receiver.Execute(cts.Source.Token);

        // var index = 0;
        using var stream = file switch { null => Console.OpenStandardOutput(), FileInfo => file.OpenWrite() };
        await foreach (var packet in receiver.Packets.ReadAllAsync(cts.Source.Token))
        {
            var p = packet.RSDecode(eccNums, out var eccValid).LengthDecode(out var lengthValid).IDDecode(out var id);

            Console.WriteLine(
                $"Receive a packet: Length {p.Length}, eccValid: {eccValid}, lengthValid: {lengthValid}, {id}"
            );

            if (binaryTxt)
            {
                foreach (var b in p)
                {
                    stream.Write(MemoryMarshal.Cast<char, byte>(Convert.ToString(b, 2).PadLeft(8, '0').AsSpan()));
                }
            }
            else
            {
                stream.Write(p);
            }
        }
        await exec;
        await rec;
    }
}