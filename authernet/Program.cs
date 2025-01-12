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
using Nerdbank.Streams;
using CS120.Utils.Extension;
using MathNet.Numerics;

namespace CS120;

class Program
{
    // static readonly string defaultAudioFileName = "assets/初星学園,ギガP,花海咲季 - Fighting My Way -
    // Instrumental.mp3"; static readonly string defaultRecordFileName = "assets/recorded.wav";

    public static DPSKSymbolOption option =
        new() { NumSymbols = 2, NumRedundant = 1, SampleRate = 48000, Freq = 6_000 };

    public static ChirpSymbolOption chirpOption = new() {
        NumSymbols = 2,
        Duration = 0.001f, // Read config or something
        SampleRate = 48_000,
        FreqA = 2_000, // Read config or something
        FreqB = 16_000 // Read config or something
    };

    public static LineSymbolOption lineOption = new() {
        NumSymbols = 2,
        NumSamplesPerSymbol = 2,
    };

    public static TriSymbolOption triOption = new() {
        NumSymbols = 2,
        NumSamplesPerSymbol = 2,
    };

    public static float corrThreshold = 0.4f;
    public static float carrierSenseThreshold = 0.25f;
    public static int maxPeakFalling = chirpOption.NumSamplesPerSymbol;
    // public static int maxPeakFalling = 8;
    public static float smoothedEnergyFactor = 1f / 64f;

    public static int eccNums = 5;
    public static int dataNum = 32;
    // 456471
    public const int magicNum = 1;
    public static readonly int idNum = BinaryIntegerTrait<byte>.Size;
    public static readonly int lengthNum = BinaryIntegerTrait<byte>.Size;
    public static int dataLengthInByte = dataNum + eccNums + magicNum + lengthNum + idNum;
    // public static int dataLengthInBit = (2 + 60 + 40) * 8;

    [STAThread]
    static int Main(string[] args)
    {
        // AudioManager.ListAsioDevice().ToList().ForEach(x => Console.WriteLine(x));

        // var audioManager = new AudioManager();

        var rootCommand = new RootCommand("CS120 project CLI program, welcome to use!");

        // rootCommand.AddCommand(CommandBuilder.BuildAudioCommand());
        rootCommand.AddCommand(CommandBuilder.BuildSendCommand());
        rootCommand.AddCommand(CommandBuilder.BuildReceiveCommand());
        rootCommand.AddCommand(CommandBuilder.BuildDuplexCommand());
        rootCommand.AddCommand(CommandBuilder.BuildListWASAPICommand());
        rootCommand.AddCommand(CommandBuilder.DummyAdapterCommand());
        rootCommand.AddCommand(CommandBuilder.AdapterCommand());
        rootCommand.AddCommand(CommandBuilder.HotSpotCommand());

        // var x =
        // Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager.CreateFromConnectionProfile(profile);

        // x.
        // var x = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };

        // var b = new ReadOnlySequence<byte>(x).RSEncode(3).LengthEncode<byte>();
        // Console.WriteLine("//// Receive");
        // foreach (var segment in b)
        // {
        //     var span = segment.Span;
        //     for (var i = 0; i < span.Length; i++)
        //     {
        //         Console.Write($"{span[i]:X2} ");
        //     }
        // }
        // Console.WriteLine();
        // var d = b.LengthDecode<byte>(out _).RSDecode(3, out _);
        // Console.WriteLine("//// Receive");
        // foreach (var segment in d)
        // {
        //     var span = segment.Span;
        //     for (var i = 0; i < span.Length; i++)
        //     {
        //         Console.Write($"{span[i]:X2} ");
        //     }
        // }
        // Console.WriteLine();
        // var seq = new Sequence<byte>();
        // // seq.Write(new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 });
        // var data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };

        // data.CopyTo(seq.GetSpan(data.Length));
        // seq.Advance(data.Length);
        // foreach (var d in seq.AsReadOnlySequence.GetElements())
        // {
        //     Console.Write($"{d:X2} ");
        // }
        // Console.WriteLine(seq.AsReadOnlySequence.Start.GetInteger());
        // data.CopyTo(seq.GetSpan(data.Length));
        // seq.Advance(data.Length);
        // foreach (var d in seq.AsReadOnlySequence.GetElements())
        // {
        //     Console.Write($"{d:X2} ");
        // }
        // Console.WriteLine(seq.AsReadOnlySequence.Start.GetInteger());

        return rootCommand.Invoke(args);
        // return 0;
    }
}
