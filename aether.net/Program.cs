using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.CommandLine;
using System.CommandLine.Parsing;
using Aether.NET.Symbol;
using Aether.NET.Preamble;
using Aether.NET.TxRx;
using Aether.NET.Modulate;
using Aether.NET.Packet;
using System.IO.Pipelines;
using Aether.NET.Utils;
using System.Buffers;
using System.Runtime.InteropServices;
using Aether.NET.Phy;
using System.Text;
using Aether.NET.Mac;
using Aether.NET.CarrierSense;
using System.Threading.Channels;
using System.Runtime.CompilerServices;
using Aether.NET.Commands;
using Nerdbank.Streams;
using Aether.NET.Utils.Extension;
using MathNet.Numerics;

namespace Aether.NET;

class Program
{
    // static readonly string defaultAudioFileName = "assets/初星学園,ギガP,花海咲季 - Fighting My Way -
    // Instrumental.mp3"; static readonly string defaultRecordFileName = "assets/recorded.wav";

    public static int sampleRate = 48_000;

    public static DPSKSymbolOption[] options = [
        new(NumRedundant: 1, SampleRate: 48000, Freq: 4_000),
        new(NumRedundant: 2, SampleRate: 48000, Freq: 8_000),
    ];

    public static ChirpSymbolOption chirpOptionAir =
        new(Duration: 0.005f, SampleRate: 48_000, FreqA: 3_000, FreqB: 10_000);

    public static ChirpSymbolOption chirpOption =
        new(Duration: 0.001f, SampleRate: 48_000, FreqA: 2_000, FreqB: 16_000);

    public static LineSymbolOption lineOption = new(NumSamplesPerSymbol: 2);

    public static TriSymbolOption triOption = new(NumSamplesPerSymbol: 2);

    public static float corrThresholdAir = 0.015f;
    public static float corrThreshold = 0.3f;
    public static float carrierSenseThreshold = 0.25f;
    public static int maxPeakFallingAir = chirpOptionAir.NumSamplesPerSymbol;
    public static int maxPeakFalling = chirpOption.NumSamplesPerSymbol;
    public static float smoothedEnergyFactor = 1f / 64f;

    public static int eccNums = 5;
    public static int dataNum = 32;
    // 456471
    public const int magicNum = 1;
    public static readonly int idNum = BinaryIntegerTrait<byte>.Size;
    public static readonly int lengthNum = BinaryIntegerTrait<byte>.Size;
    public static int dataLengthInByte = dataNum + eccNums + magicNum + lengthNum + idNum;

    [STAThread]
    static int Main(string[] args)
    {

        var rootCommand = new RootCommand("CS120 project CLI program, welcome to use!");

        // rootCommand.AddCommand(CommandBuilder.BuildAudioCommand());
        rootCommand.AddCommand(CommandBuilder.BuildSendCommand());
        rootCommand.AddCommand(CommandBuilder.BuildReceiveCommand());
        rootCommand.AddCommand(CommandBuilder.BuildDuplexCommand());
        rootCommand.AddCommand(CommandBuilder.BuildListWASAPICommand());
        rootCommand.AddCommand(CommandBuilder.DummyAdapterCommand());
        rootCommand.AddCommand(CommandBuilder.AdapterCommand());
        rootCommand.AddCommand(CommandBuilder.HotSpotCommand());

        return rootCommand.Invoke(args);
        // return 0;
    }
}
