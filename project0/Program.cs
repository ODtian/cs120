using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.Asio;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Threading.Channels;
using System.IO.Pipelines;
using System.IO.Pipes;
namespace CS120;

class Program
{
    // static readonly string defaultAudioFileName = "assets/初星学園,ギガP,花海咲季 - Fighting My Way -
    // Instrumental.mp3"; static readonly string defaultRecordFileName = "assets/recorded.wav";

    // static Channel<CancellationTokenSource> crtlCts;

    [STAThread]
    static void Main(string[] args)
    {
        // AudioManager.ListAsioDevice().ToList().ForEach(x => Console.WriteLine(x));

        var audioManager = new AudioManager();

        var rootCommand = new RootCommand("CS120 project CLI program, welcome to use!");

        var audioCommand = new Command("audio", "play with audio stuff");

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

        var taskPlay = play switch { FileInfo => manager.Play<WasapiOut>(play.FullName, audioCtses[0].Token),
                                     null => Task.CompletedTask };

        var taskRecord = (record, recordPlayBack) switch {
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