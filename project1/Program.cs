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

    // static Channel<CancellationTokenSource> crtlCts;
    public static DPSKSymbolOption option =
        new() { NumSymbols = 2, NumRedundant = 2, SampleRate = 48000, Freq = 4_000 };

    public static ChirpSymbolOption chirpOption = new()
    {
        NumSymbols = 2,
        Duration = 0.005f, // Read config or something
        SampleRate = 48_000,
        FreqA = 3_000, // Read config or something
        FreqB = 10_000 // Read config or something
    };

    public static float corrThreshold = 0.05f;
    public static int maxPeakFalling = 1024;
    public static float smoothedEnergyFactor = 1f / 64f;

    public static int eccNums = 5;
    public static int dataNum = 14;
    public static int dataLengthInBit = (dataNum + eccNums + 2) * 8;
    // public static int dataLengthInBit = (2 + 60 + 40) * 8;


    static Receiver<TPacket> InitReceiver<TWaveIn, TPacket>(AudioManager audioManager, FileInfo? file, out Pipe pipe)
        where TWaveIn : IWaveIn, new()
        where TPacket : IPacket<TPacket>
    {
        WaveFormat receivedFormat;
        if (file is null)
        {
            receivedFormat = audioManager.GetRecorderWaveFormat<TWaveIn>();
        }
        else
        {
            using var reader = new WaveFileReader(file.FullName);
            receivedFormat = reader.WaveFormat;
        }

        pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));

        // var rec = audioManager.Record<TWaveIn>(pipe.Writer.AsStream(), CancellationToken.None);

        Console.WriteLine(receivedFormat.SampleRate);

        var receiver = new Receiver<TPacket>(
            new StreamWaveProvider(receivedFormat, pipe.Reader.AsStream()).ToSampleProvider().ToMono(),
            new PreambleDetection(
                new ChirpPreamble(new ChirpSymbol(chirpOption with { SampleRate = receivedFormat.SampleRate })),
                corrThreshold,
                smoothedEnergyFactor,
                maxPeakFalling
            ),
            // new DPSKDemodulator(new DPSKSymbol(option with { SampleRate = receivedFormat.SampleRate }))
            new OFDMDemodulator(
                option with { SampleRate = receivedFormat.SampleRate },
                option with
                {
                    Freq = option.Freq * 2,
                    NumRedundant = option.NumRedundant * 2,
                    SampleRate = receivedFormat.SampleRate
                }
            )
        );

        return receiver;
    }

    static Transmitter<TPacket> InitTransmitter<TWaveOut, TPacket>(AudioManager audioManager)
        where TWaveOut : IWavePlayer, new()
        where TPacket : IPacket<TPacket>
    {
        var sendFormat = audioManager.GetPlayerWaveFormat<TWaveOut>();

        var transmitter = new Transmitter<TPacket>(
            sendFormat,
            new ChirpPreamble(new ChirpSymbol(chirpOption with { SampleRate = sendFormat.SampleRate })),
            // new DPSKModulator(new DPSKSymbol(option with { SampleRate = sendFormat.SampleRate })),
            new OFDMModulator(
                option with { SampleRate = sendFormat.SampleRate },
                option with
                {
                    Freq = option.Freq * 2,
                    NumRedundant = option.NumRedundant * 2,
                    SampleRate = sendFormat.SampleRate
                }
            ),
            0.005f
        );

        return transmitter;
    }

    [STAThread]
    static void Main(string[] args)
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
        //         Console.WriteLine("Managed Example Byte");
        //         Console.WriteLine("--------------------");
        //         const int dataShardCount = 4;
        //         const int parityShardCount = 2;

        //         // Initialize Reed-Solomon with data shards and parity shards
        //         ReedSolomon rs = new ReedSolomon(dataShardCount, parityShardCount);

        //         // Example data to encode
        //         byte[] data = { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        //         Console.WriteLine("Data:");
        //         Console.WriteLine(string.Join(" ", data));

        //         // Encode the data using ManagedEncode to produce shards
        //         var shards = rs.ManagedEncode(data, dataShardCount, parityShardCount);

        //         Console.WriteLine("Encoded Data:");
        //         foreach (var shard in shards)
        //         {
        //             Console.WriteLine(string.Join(" ", shard));
        //         }

        //         // Simulate loss of one shard
        //         shards[1] = null;

        //         Console.WriteLine("Encoded with missing Data:");
        //         foreach (var shard in shards)
        //         {
        //             if (shard == null)
        //             {
        //                 Console.WriteLine("null");
        //             }
        //             else
        //             {
        //                 Console.WriteLine(string.Join(" ", shard));
        //             }
        //         }

        //         // Decode the remaining shards using ManagedDecode to recover original data
        //         var decodedData = rs.ManagedDecode(shards, dataShardCount, parityShardCount);

        //         Console.WriteLine("Decoded data:");
        //         Console.WriteLine(string.Join(" ", decodedData));
        //     }
        // );
        // foreach (var x in args)
        // {
        //     Console.WriteLine(x);
        // }
        rootCommand.Invoke(args);
    }

    static Command BuildAudioCommand(AudioManager audioManager)
    {
        var command = new Command("audio", "play with audio stuff");

        var playOption = new Option<FileInfo?>(name: "--play",
                                                    description: "The file path to play",
                                                    isDefault: true,
                                                    parseArgument: result => ParseSingleFileInfo(result));

        var recordOption = new Option<FileInfo?>(name: "--record",
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

        var fileArgument = new Argument<FileInfo
            ?>(name: "input", description: "The file path to save", parse: result => ParseSingleFileInfo(result));

        var toWavOption = new Option<FileInfo?>(name: "--to-wav",
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
            async (file, toWav, binaryTxt) => await SendCommandTask(audioManager, file, toWav, binaryTxt),
            fileArgument,
            toWavOption,
            binaryTxtOption
        );
        return command;
    }

    static Command BuildReceiveCommand(AudioManager audioManager)
    {
        var command = new Command("receive", "receive data");

        var fileOption = new Option<FileInfo?>(name: "--file",
                                                    description: "The file path to save, otherwise stdout",
                                                    isDefault: true,
                                                    parseArgument: result => ParseSingleFileInfo(result, false));

        var fromWavOption = new Option<FileInfo?>(name: "--from-wav",
                                                       description: "Import audio data from wav file",
                                                       isDefault: true,
                                                       parseArgument: result => ParseSingleFileInfo(result));

        var binaryTxtOption = new Option<bool>(
            name: "--binary-txt", description: "original txt are binary", getDefaultValue: () => false
        );

        command.AddOption(fromWavOption);
        command.AddOption(fileOption);
        command.AddOption(binaryTxtOption);

        command.SetHandler(async (fromWav, file, binaryTxt) => await ReceiveCommandTask(audioManager, fromWav, file, binaryTxt), fromWavOption, fileOption, binaryTxtOption);
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

        var taskPlay = play switch
        {
            FileInfo => manager.Play<WasapiOut>(play.FullName, cancelToken.Source.Token),
            null => Task.CompletedTask,
        };

        var taskRecord = (record, recordPlayBack) switch
        {
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

        using var transmitter = InitTransmitter<WasapiOut, RSEncodePacket>(audioManager);

        Console.WriteLine("start");
        var exec = transmitter.Execute(cts.Source.Token);

        Console.WriteLine(AudioManager.ListAsioDevice()[0]);
        // var wavePro
        // var play = Task.CompletedTask;
        var play = toWav switch
        {
            null => audioManager.Play<WasapiOut>(transmitter.Samples.ToWaveProvider(), cts.Source.Token),
            // null => audioManager.Play(transmitter.Samples.ToWaveProvider(), "ASIO4ALL v2", cts.Source.Token, 0),
            FileInfo => Task.Run(
                async () =>
                {
                    await exec;
                    var buffer = new float[1024];
                    using var writer = new WaveFileWriter(toWav.FullName, transmitter.Samples.WaveFormat);

                    while (true)
                    {
                        var length = transmitter.Samples.Read(buffer, 0, buffer.Length);
                        if (length == 0)
                        {
                            break;
                        }
                        writer.WriteSamples(buffer, 0, length);
                    }
                    // using var timer = new System.Timers.Timer(
                    //     buffer.Length / (float)transmitter.Samples.WaveFormat.SampleRate
                    // ) { AutoReset = true };
                    // writer.
                    // var tcs = new TaskCompletionSource();
                    // timer.Elapsed += (s, e) =>
                    // {
                    //     var length = transmitter.Samples.Read(buffer, 0, buffer.Length);
                    //     if (length == 0)
                    //     {
                    //         tcs.SetResult();
                    //         return;
                    //     }

                    //     buffer.AsSpan(length).Clear();
                    //     writer.WriteSamples(buffer, 0, buffer.Length);
                    // };
                    // timer.Start();
                    // await tcs.Task;
                    // timer.Close();
                    // writer.Close();
                }
            ),
        };

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

        static bool TryChunkData(
            int chunkSize, bool complete, ref ReadOnlySequence<byte> seq, out ReadOnlySequence<byte> chunk
        )
        {
            if (seq.IsEmpty)
            {
                chunk = seq;
                return false;
            }
            else if (seq.Length < chunkSize)
            {
                chunk = seq;
                seq = seq.Slice(seq.Length);
                return complete;
            }
            else
            {
                // Console.WriteLine(seq.Length);
                chunk = seq.Slice(0, chunkSize);
                seq = seq.Slice(chunkSize);
                // Console.WriteLine(seq.Length);
                return true;
            }
        };

        while (true)
        {
            var read = await dataPipe.Reader.ReadAsync(cts.Source.Token);
            var buffer = read.Buffer;
            // Console.WriteLine(buffer.Length);
            while (TryChunkData(dataNum, read.IsCompleted, ref buffer, out var chunk))
            {
                Console.WriteLine(chunk.Length);
                var dataArray = chunk.ToArray();
                // chunk.CopyTo()
                // foreach (var d in dataArray)
                // {
                //     // d.Span[]
                //     // foreach (var b in d.Span)
                //     // {
                //     //     Console.WriteLine(Convert.ToString(b, 2).PadLeft(8, '0'));
                //     // }
                //     Console.WriteLine(Convert.ToString(d, 2).PadLeft(8, '0'));
                // }
                var packet = new LengthEncodePacket(dataArray, dataNum).Convert<RSEncodePacket>();
                await transmitter.Packets.WriteAsync(packet, cts.Source.Token);
            }

            dataPipe.Reader.AdvanceTo(buffer.Start, buffer.End);

            if (read.IsCompleted)
            {
                break;
            }
            // var data = await stream.ReadAtLeast();
            // await foreach (var data in dataPipe.Reader.ReadAsync(cts.Source.Token))
            // {
            //     // Console.WriteLine(data.Length);
            //     await transmitter.Packets.WriteAsync(new LengthEncodePacket(data, dataNum), cts.Source.Token);
            // }
        }
        // for (int i = 0; i < 100; i++)
        // {
        //     var data = DataHelper.GenerateData((dataNum - 1) * 8);
        //     Console.WriteLine("Data:");
        //     foreach (var d in data)
        //     {
        //         Console.WriteLine(Convert.ToString(d, 2).PadLeft(8, '0'));
        //     }
        //     Console.WriteLine();
        //     var packet = new LengthEncodePacket([.. data], dataNum).Convert<RSEncodePacket>();
        //     // byte[] dataArray = [.. data];
        //     // await transmitter.Packets.WriteAsync(RawPacket.Create(data), cts.Source.Token);
        //     await transmitter.Packets.WriteAsync(packet, cts.Source.Token);
        //     // await Task.Delay(1000, cts.Source.Token);
        // }
        transmitter.Packets.Complete();

        await exec;
        await play;
    }

    static async Task ReceiveCommandTask(AudioManager audioManager, FileInfo? fromWav, FileInfo? file, bool binaryTxt)
    {
        using var cts = new CancelKeyPressCancellationTokenSource(new CancellationTokenSource());
        // var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
        // var transmitter = new Transmitter<DPSKModulator, EmptyPacket, ChirpPreamble>(waveFormat);
        using var receiver = InitReceiver<WasapiCapture, RSDecodePacket>(audioManager, fromWav, out var pipe);

        // var data = GenerateData(10);
        // await transmitter.DataChannel.Writer.WriteAsync(data);

        // var transmitTask = Task.Run(() => transmitter.Execute(CancellationToken.None));
        // var receiveTask = Task.Run(() => receiver.Execute(CancellationToken.None));
        Console.WriteLine("start");

        var exec = receiver.Execute(cts.Source.Token);

        var rec = fromWav switch
        {
            null => audioManager.Record<WasapiCapture>(pipe.Writer.AsStream(), cts.Source.Token),
            FileInfo => Task.Run(
                () =>
                {
                    using var reader = new WaveFileReader(fromWav.FullName);
                    reader.CopyTo(pipe.Writer.AsStream());
                    pipe.Writer.Complete();
                }
            ),
        };
        // PipeStream
        using var stream = file switch
        {
            null => Console.OpenStandardOutput(),
            FileInfo => file.OpenWrite()
        };

        await foreach (var packet in receiver.Packets.ReadAllAsync(cts.Source.Token))
        {
            Console.WriteLine($"Receive a packet: Length {packet.Bytes.Length}, Valid: {packet.Valid}");
            var p = packet.Convert<LengthDecodePacket>();
            if (binaryTxt)
            {
                foreach (var b in p.Bytes)
                {
                    stream.Write(MemoryMarshal.Cast<char, byte>(Convert.ToString(b, 2).PadLeft(8, '0').AsSpan()));
                }

            }
            else
            {
                stream.Write(p.Bytes);
            }
            // await stream.WriteAsync()
            // foreach (var d in packet.Bytes)
            // {
            //     Console.WriteLine(Convert.ToString(d, 2).PadLeft(8, '0'));
            // }
            // Console.WriteLine(packet.Valid);
            // Console.WriteLine(.Bytes.Length);
            // Console.WriteLine();
        }
        Console.WriteLine();

        await exec;
        await rec;
    }
}