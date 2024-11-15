using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.CommandLine;
using System.CommandLine.Parsing;
using CS120.Symbol;
using CS120.Preamble;
using CS120.TxRx;
using CS120.Modulate;
using CS120.Packet;
using System.IO.Pipelines;
using CS120.Utils;
using System.Buffers;
using System.Runtime.InteropServices;
using CS120.Phy;
using System.Text;
using CS120.Mac;
using CS120.CarrierSense;
using System.Threading.Channels;
using System.Runtime.CompilerServices;
using CS120.Commands;

namespace CS120;

class Program
{
    // static readonly string defaultAudioFileName = "assets/初星学園,ギガP,花海咲季 - Fighting My Way -
    // Instrumental.mp3"; static readonly string defaultRecordFileName = "assets/recorded.wav";

    public static DPSKSymbolOption option =
        new() { NumSymbols = 2, NumRedundant = 1, SampleRate = 48000, Freq = 6_000 };

    public static ChirpSymbolOption chirpOption = new()
    {
        NumSymbols = 2,
        Duration = 0.001f, // Read config or something
        SampleRate = 48_000,
        FreqA = 3_000, // Read config or something
        FreqB = 10_000 // Read config or something
    };

    public static LineSymbolOption lineOption = new() { NumSymbols = 2, NumSamplesPerSymbol = 4 };

    public static float corrThreshold = 0.4f;
    public static int maxPeakFalling = chirpOption.NumSamplesPerSymbol;
    public static float smoothedEnergyFactor = 1f / 64f;

    public static int eccNums = 2;
    public static int dataNum = 32;

    public const int magicNum = 1;
    public static readonly int idNum = BinaryIntegerSizeTrait<byte>.Size;
    public static readonly int lengthNum = BinaryIntegerSizeTrait<byte>.Size;
    public static int dataLengthInByte = dataNum + eccNums + magicNum + lengthNum + idNum;
    // public static int dataLengthInBit = (2 + 60 + 40) * 8;

    [STAThread]
    static int Main(string[] args)
    {
        // AudioManager.ListAsioDevice().ToList().ForEach(x => Console.WriteLine(x));

        // var audioManager = new AudioManager();

        var rootCommand = new RootCommand("CS120 project CLI program, welcome to use!");

        rootCommand.AddCommand(CommandBuilder.BuildAudioCommand());
        rootCommand.AddCommand(CommandBuilder.BuildSendCommand());
        rootCommand.AddCommand(CommandBuilder.BuildReceiveCommand());
        rootCommand.AddCommand(CommandBuilder.BuildDuplexCommand());
        rootCommand.AddCommand(CommandBuilder.BuildListWASAPICommand());

        return rootCommand.Invoke(args);
    }
}
