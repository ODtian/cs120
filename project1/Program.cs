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

namespace CS120;

class Program
{
    // static readonly string defaultAudioFileName = "assets/初星学園,ギガP,花海咲季 - Fighting My Way -
    // Instrumental.mp3"; static readonly string defaultRecordFileName = "assets/recorded.wav";

    // static Channel<CancellationTokenSource> crtlCts;
    public static DPSKSymbolOption option =
        new() { NumSymbols = 2, NumSamplesPerSymbol = 20, SampleRate = 48000, Freq = 4_800 };

    public static ChirpSymbolOption chirpOption = new ChirpSymbolOption
    {
        NumSymbols = 2,
        NumSamplesPerSymbol = 220, // Read config or something
        SampleRate = 48_000,
        FreqA = 3_000, // Read config or something
<<<<<<< HEAD
        FreqB = 10_000  // Read config or something
=======
        FreqB = 10_000 // Read config or something
>>>>>>> 897d10191a9594cf317069a890ae91a30ae084fa
    };

    public static float corrThreshold = 0.1f;
    public static int maxPeakFalling = 220;
    public static float smoothedEnergyFactor = 1f / 64f;
    public static int dataLengthInBit = 480;

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

        Console.WriteLine(Convert.ToString(data[0], 2));
        Console.WriteLine(Convert.ToString(data[1], 2));
        for (int i = 0; i < 40; i++)
        {
            data.Add(0b00000000);
        }
        return data.ToArray();
    }

    static float[] GenerateSamples(byte[] data)
    {
        // var symbols = DFSKSymbol.Get(option);
        // var symbols = DPSKSymbolOption.Get(option);
        var symbols = new DPSKSymbol(option).Samples;

        var samples = new List<float>();

        // IPreamble? preamble = ChirpPreamble.Create(WaveFormat.CreateIeeeFloatWaveFormat(option.SampleRate, 1));
        var preamble = new ChirpPreamble(new ChirpSymbol(chirpOption));

        samples.AddRange(Enumerable.Range(0, 48000).Select(
            _ => 0f
        ));
        samples.AddRange(preamble.Samples);

        foreach (var d in data)
        {
            for (int i = 0; i < 8; i++)
            {
                samples.AddRange(symbols[(d >> i) & 1]);
            }
        }

        samples.AddRange(Enumerable.Range(0, 48000).Select(
            _ => 0f
        ));

        return samples.ToArray();
    }

    static IReceiver GetFileReciver(string filePath)
    {
        using var fileReader = new WaveFileReader(filePath);
        var receiver = new Receiver(
            fileReader.WaveFormat,
            new PreambleDetection(
                new ChirpPreamble(new ChirpSymbol(chirpOption with { SampleRate = fileReader.WaveFormat.SampleRate })),
                corrThreshold,
                smoothedEnergyFactor,
                maxPeakFalling
            ),
            new DPSKDemodulator(new DPSKSymbol(option with { SampleRate = fileReader.WaveFormat.SampleRate }))
        );

        using var writer = receiver.StreamIn;

        fileReader.CopyTo(writer);
        return receiver;
    }

    static IReceiver GetRecorderReciver()
    {
        WaveFormat receivedFormat;
        using (var capture = new WasapiCapture())
        {
            receivedFormat = capture.WaveFormat;
            // Console.WriteLine(receivedFormat.SampleRate);
        }
        var receiver = new Receiver(
            receivedFormat,
            new PreambleDetection(
                new ChirpPreamble(new ChirpSymbol(chirpOption with { SampleRate = receivedFormat.SampleRate })),
                corrThreshold,
                smoothedEnergyFactor,
                maxPeakFalling
            ),
            new DPSKDemodulator(new DPSKSymbol(option with { SampleRate = receivedFormat.SampleRate }))
        );
        return receiver;
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
        var matrix = Matrix<float>.Build.DenseOfRowMajor(1, data.Count, [.. data]);
        MatlabWriter.Write(matFile, matrix, "audio_rec");
    }

    static void GenerateMatlabSendData(float[] samples, string matFile)
    {
        var matrix = Matrix<float>.Build.DenseOfRowMajor(1, samples.Length, [.. samples]);
        MatlabWriter.Write(matFile, matrix, "audio");
    }

    [STAThread]
    static void Main(string[] args)
    {
        // AudioManager.ListAsioDevice().ToList().ForEach(x => Console.WriteLine(x));

        var audioManager = new AudioManager();

        var rootCommand = new RootCommand("CS120 project CLI program, welcome to use!");

        var audioCommand = new Command("audio", "play with audio stuff");

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

        audioCommand.AddOption(playOption);
        audioCommand.AddOption(recordOption);
        audioCommand.AddOption(recordPlayBackOption);
        audioCommand.AddOption(durationOption);

        audioCommand.SetHandler(
            async (FileInfo? play, FileInfo? record, bool recordPlayBack, int duration) =>
            {
                await AudioCommandTask(play, record, recordPlayBack, duration, audioManager);
            },
         playOption, recordOption, recordPlayBackOption, durationOption
        );

        rootCommand.AddCommand(audioCommand);

        rootCommand.SetHandler(
            async () =>
            {
                var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
                var transmitter = new Transmitter<DPSKModulator, EmptyPacket, ChirpPreamble>(waveFormat);
                var receiver = GetRecorderReciver();

                var data = GenerateData(10);
                await transmitter.DataChannel.Writer.WriteAsync(data);

                var transmitTask = Task.Run(() => transmitter.Execute(CancellationToken.None));
                var receiveTask = Task.Run(() => receiver.Execute(CancellationToken.None));

                Console.WriteLine("start");

                var rec = audioManager.Record<WasapiCapture>(receiver.StreamIn, CancellationToken.None);

                using (var waveOut = new WaveOutEvent())
                {
                    var buffer = new byte[1024];
                    int bytesRead;
                    while ((bytesRead = await transmitter.StreamOut.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        waveOut.Init(new RawSourceWaveStream(new MemoryStream(buffer, 0, bytesRead), waveFormat));
                        waveOut.Play();
                        while (waveOut.PlaybackState == PlaybackState.Playing)
                        {
                            await Task.Delay(100);
                        }
                    }
                }

                Console.WriteLine("end");

                await foreach (var packet in receiver.PacketChannel.Reader.ReadAllAsync())
                {
                    foreach (var d in packet.Bytes)
                    {
                        Console.WriteLine(Convert.ToString(d, 2));
                    }
                }
            }
        );
        rootCommand.Invoke(args);
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
        var audioCtses = new CancellationTokenSource[2] { new(), new() };

        var taskPlay = play switch
        {
            FileInfo => manager.Play<WasapiOut>(play.FullName, audioCtses[0].Token),
            null => Task.CompletedTask
        };

        var taskRecord = (record, recordPlayBack) switch
        {
            (_, true) => manager.RecordThenPlay<WasapiCapture, WasapiOut>(audioCtses.Select(x => x.Token).ToArray()),
            (FileInfo, _) => manager.Record<WasapiCapture>(record.FullName, audioCtses[0].Token),
            _ => Task.CompletedTask
        };

        var taskUni = Task.WhenAll(taskPlay, taskRecord);

        foreach (var audioCts in audioCtses)
        {
            using var cts = new CancellationTokenSource();
            var delayTask = Task.Delay(duration, cts.Token);

            ConsoleCancelEventHandler cancelHandler =
                new((s, e) =>
                    {
                        e.Cancel = true;
                        cts.Cancel();
                    });

            Console.CancelKeyPress += cancelHandler;

            if (await Task.WhenAny(taskUni, delayTask) == delayTask)
            {
                audioCts.Cancel();
            }

            Console.CancelKeyPress -= cancelHandler;
        }
        await taskUni;
    }
}