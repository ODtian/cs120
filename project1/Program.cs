using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.CommandLine;
using System.CommandLine.Parsing;
using MathNet.Numerics.Data.Matlab;
using MathNet.Numerics.LinearAlgebra;
using CS120.Symbol;
using CS120.Preamble;
using CS120.TxRx;
using CS120.Modulate;
using CS120.Packet;
using System.IO.Pipelines;
using CS120.Utils;

namespace CS120;

class Program
{
    // static readonly string defaultAudioFileName = "assets/初星学園,ギガP,花海咲季 - Fighting My Way -
    // Instrumental.mp3"; static readonly string defaultRecordFileName = "assets/recorded.wav";

    // static Channel<CancellationTokenSource> crtlCts;
    public static DPSKSymbolOption option =
        new() { NumSymbols = 2, NumRedundant = 4, SampleRate = 48000, Freq = 4_800 };

    public static ChirpSymbolOption chirpOption = new() {
        NumSymbols = 2,
        Duration = 0.001f, // Read config or something
        SampleRate = 48_000,
        FreqA = 3_000, // Read config or something
        FreqB = 10_000 // Read config or something
    };

    public static float corrThreshold = 0.09f;
    public static int maxPeakFalling = 480;
    public static float smoothedEnergyFactor = 1f / 64f;
    public static int dataLengthInBit = 255 * 8;
    // public static int dataLengthInBit = (2 + 60 + 40) * 8;

    static byte[] GenerateData(int length)
    {
        var fragment = new byte[] {
            0b10101010,
            0b01010101,
            0b10101010,
            0b01010101,
            0b10101010,
            0b00000000,
        };

        var data = new List<byte> {
            0b00000000,
            0b00000000,
        };

        for (int i = 0; i < length; i++)
        {
            data.AddRange(fragment);
        }

        var totalLength = (data.Count - 2) * 8;
        Console.WriteLine(totalLength);

        data[0] = (byte)totalLength;
        data[1] = (byte)(totalLength >> 8);

        Console.WriteLine(Convert.ToString(data[0], 2).PadLeft(8, '0'));
        Console.WriteLine(Convert.ToString(data[1], 2).PadLeft(8, '0'));
        for (int i = 0; i < 40; i++)
        {
            data.Add(0b00000000);
        }
        return data.ToArray();
    }

    // static float[] GenerateSamples(byte[] data)
    // {
    //     // var symbols = DFSKSymbol.Get(option);
    //     // var symbols = DPSKSymbolOption.Get(option);
    //     var symbols = new DPSKSymbol(option).Samples;

    //     var samples = new List<float>();

    //     // IPreamble? preamble = ChirpPreamble.Create(WaveFormat.CreateIeeeFloatWaveFormat(option.SampleRate, 1));
    //     var preamble = new ChirpPreamble(new ChirpSymbol(chirpOption));

    //     samples.AddRange(Enumerable.Range(0, 48000).Select(
    //         _ => 0f
    //     ));
    //     samples.AddRange(preamble.Samples);

    //     foreach (var d in data)
    //     {
    //         for (int i = 0; i < 8; i++)
    //         {
    //             samples.AddRange(symbols[(d >> i) & 1]);
    //         }
    //     }

    //     samples.AddRange(Enumerable.Range(0, 48000).Select(
    //         _ => 0f
    //     ));

    //     return samples.ToArray();
    // }

    // static IReceiver GetFileReciver(string filePath)
    // {
    //     using var fileReader = new WaveFileReader(filePath);
    //     var receiver = new Receiver(
    //         fileReader.WaveFormat,
    //         new PreambleDetection(
    //             new ChirpPreamble(new ChirpSymbol(chirpOption with { SampleRate = fileReader.WaveFormat.SampleRate
    //             })), corrThreshold, smoothedEnergyFactor, maxPeakFalling
    //         ),
    //         new DPSKDemodulator(new DPSKSymbol(option with { SampleRate = fileReader.WaveFormat.SampleRate }))
    //     );

    //     using var writer = receiver.StreamIn;

    //     fileReader.CopyTo(writer);
    //     return receiver;
    // }

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
                option with {
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
            // new DPSKModulator(new DPSKSymbol(option with { SampleRate = sendFormat.SampleRate }))
            new OFDMModulator(
                option with { SampleRate = sendFormat.SampleRate },
                option with {
                    Freq = option.Freq * 2, NumRedundant = option.NumRedundant * 2, SampleRate = sendFormat.SampleRate
                }
            ),
            0.5f
        );

        return transmitter;
    }

    static void WriteSamplesToFile(string filePath, float[] samples)
    {
        using var writer = new WaveFileWriter(filePath, WaveFormat.CreateIeeeFloatWaveFormat(48000, 1));
        writer.WriteSamples(samples, 0, samples.Length);
    }

    static void GenerateMatlabRecData(string filePath, string matFile)
    {
        var data = new List<float>();
        using (var reader = new WaveFileReader(filePath))
        {
            var sampleProvider = reader.ToSampleProvider().ToMono();

            var buffer = new float[reader.WaveFormat.SampleRate];
            while (true)
            {
                var length = sampleProvider.Read(buffer, 0, buffer.Length);
                if (length == 0)
                    break;
                data.AddRange(buffer.AsSpan(0, length));
            }
            // Console.WriteLine(data.Count);
        }
        var matrix = Matrix<float>.Build.DenseOfRowMajor(1, data.Count, [..data]);
        MatlabWriter.Write(matFile, matrix, "audio_rec");
    }

    static void GenerateMatlabSendData(float[] samples, string matFile)
    {
        var matrix = Matrix<float>.Build.DenseOfRowMajor(1, samples.Length, [..samples]);
        MatlabWriter.Write(matFile, matrix, "audio");
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
        var toWavOption = new Option < FileInfo ? > (name: "--to-wav",
                                                     description: "Export audio data to wav file",
                                                     isDefault: true,
                                                     parseArgument: result => ParseSingleFileInfo(result, false));
        command.AddOption(toWavOption);

        command.SetHandler(async (file) => await SendCommandTask(audioManager, file), toWavOption);
        return command;
    }

    static Command BuildReceiveCommand(AudioManager audioManager)
    {
        var command = new Command("receive", "receive data");
        var fromWavOption = new Option < FileInfo ? > (name: "--from-wav",
                                                       description: "Import audio data from wav file",
                                                       isDefault: true,
                                                       parseArgument: result => ParseSingleFileInfo(result, false));
        command.AddOption(fromWavOption);
        command.SetHandler(async (file) => await ReceiveCommandTask(audioManager, file), fromWavOption);
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
    static async Task SendCommandTask(AudioManager audioManager, FileInfo? file)
    {
        using var cts = new CancelKeyPressCancellationTokenSource(new CancellationTokenSource());

        using var transmitter = InitTransmitter<WasapiOut, RSPacket>(audioManager);

        Console.WriteLine("start");
        var exec = transmitter.Execute(cts.Source.Token);

        Console.WriteLine(AudioManager.ListAsioDevice()[0]);
        // var wavePro
        var play = file switch {
            null => audioManager.Play<WasapiOut>(transmitter.Samples.ToWaveProvider(), cts.Source.Token),
            // null => audioManager.Play(transmitter.Samples.ToWaveProvider(), "ASIO4ALL v2", cts.Source.Token, 0),
            FileInfo => Task.Run(
                async () =>
                {
                    await exec;
                    var buffer = new float[1024];
                    using var writer = new WaveFileWriter(file.FullName, transmitter.Samples.WaveFormat);

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

        for (int i = 0; i < 10; i++)
        {

            var data = GenerateData(10);
            // await transmitter.Packets.WriteAsync(RawPacket.Create(data), cts.Source.Token);
            await transmitter.Packets.WriteAsync(RSPacket.Encode(data), cts.Source.Token);
            // await Task.Delay(1000, cts.Source.Token);
        }
        transmitter.Packets.Complete();

        await exec;
        await play;
    }

    static async Task ReceiveCommandTask(AudioManager audioManager, FileInfo? file)
    {
        using var cts = new CancelKeyPressCancellationTokenSource(new CancellationTokenSource());
        // var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
        // var transmitter = new Transmitter<DPSKModulator, EmptyPacket, ChirpPreamble>(waveFormat);
        using var receiver = InitReceiver<WasapiCapture, RSPacket>(audioManager, file, out var pipe);

        // var data = GenerateData(10);
        // await transmitter.DataChannel.Writer.WriteAsync(data);

        // var transmitTask = Task.Run(() => transmitter.Execute(CancellationToken.None));
        // var receiveTask = Task.Run(() => receiver.Execute(CancellationToken.None));
        Console.WriteLine("start");

        var exec = receiver.Execute(cts.Source.Token);

        var rec = file switch {
            null => audioManager.Record<WasapiCapture>(pipe.Writer.AsStream(), cts.Source.Token),
            FileInfo => Task.Run(
                () =>
                {
                    using var reader = new WaveFileReader(file.FullName);
                    reader.CopyTo(pipe.Writer.AsStream());
                    pipe.Writer.Complete();
                }
            ),
        };

        // using (var waveOut = new WaveOutEvent())
        // {
        //     var buffer = new byte[1024];
        //     int bytesRead;
        //     while ((bytesRead = await transmitter.StreamOut.ReadAsync(buffer, 0, buffer.Length)) > 0)
        //     {
        //         waveOut.Init(new RawSourceWaveStream(new MemoryStream(buffer, 0, bytesRead), waveFormat));
        //         waveOut.Play();
        //         while (waveOut.PlaybackState == PlaybackState.Playing)
        //         {
        //             await Task.Delay(100);
        //         }
        //     }
        // }

        await foreach (var packet in receiver.Packets.ReadAllAsync(cts.Source.Token))
        {
            foreach (var d in packet.Bytes)
            {
                Console.WriteLine(Convert.ToString(d, 2).PadLeft(8, '0'));
            }
            Console.WriteLine();
        }

        await exec;
        await rec;
    }
}