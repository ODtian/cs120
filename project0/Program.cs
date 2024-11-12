using System.CommandLine;
using System.CommandLine.Parsing;

using CS120.Commands;
namespace CS120;

class Program
{
    // static readonly string defaultAudioFileName = "assets/初星学園,ギガP,花海咲季 - Fighting My Way -
    // Instrumental.mp3"; static readonly string defaultRecordFileName = "assets/recorded.wav";

    [STAThread]
    static int Main(string[] args)
    {
        // AudioManager.ListAsioDevice().ToList().ForEach(x => Console.WriteLine(x));
        var rootCommand = new RootCommand("CS120 project CLI program, welcome to use!");

        rootCommand.AddCommand(CommandBuilder.BuildAudioCommand());

        return rootCommand.Invoke(args);
    }
}