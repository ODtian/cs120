using NAudio.CoreAudioApi;
using NAudio.Wave;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace CS120;

class Program
{
    static readonly string defaultAudioFileName = "assets/初星学園,ギガP,花海咲季 - Fighting My Way - Instrumental.mp3";
    static readonly string defaultRecordFileName = "assets/recorded.wav";

    [STAThread]
    static void Main(string[] args)
    {
        AudioManager.ListAsioDevice().ToList().ForEach(x => Console.WriteLine(x));

        using var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        var audioManager = new AudioManager();

        var rootCommand = new RootCommand("CS120 project CLI program, welcome to use!");

        var audioCommand = new Command("audio", "play with audio stuff");

        var playOption =
            new Option<FileInfo?>(name: "--play",
                                       description: "The file path to play",
                                       isDefault: true,
                                       parseArgument: result => ParseSingleFileInfo(result, defaultAudioFileName));

        var recordOption = new Option<FileInfo
            ?>(name: "--record",
                 description: "The file path to record",
                 isDefault: true,
                 parseArgument: result => ParseSingleFileInfo(result, defaultRecordFileName, false));

        var durationOption = new Option<int>(
            name: "--duration",
            description: "time duration of audio operation, in seconds, -1 means infinite until canceled by user",
            isDefault: true,
            parseArgument: result =>
                result.Tokens.Count == 0 ? -1 : (int)(float.Parse(result.Tokens.Single().Value) * 1000)

        );
        // var asioPlayOption =

        audioCommand.AddOption(playOption);
        audioCommand.AddOption(recordOption);
        audioCommand.AddOption(durationOption);

        audioCommand.SetHandler(
            async (FileInfo? play, FileInfo? record, int duration) =>
            {
                await AudioCommandTask(play, record, duration, audioManager, cts.Token);
            },
         playOption, recordOption, durationOption
        );

        rootCommand.AddCommand(audioCommand);

        rootCommand.Invoke(args);
        // AsyncTask(cts.Token).GetAwaiter().GetResult();
    }
    static FileInfo? ParseSingleFileInfo(ArgumentResult result, string defaultFileName, bool checkExist = true)
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
    AudioCommandTask(FileInfo? play, FileInfo? record, int duration, AudioManager manager, CancellationToken ct)
    {
        // await manager.Play(audioFile, "ASIO4ALL v2");
        using var audio_cts = new CancellationTokenSource();
        Task task =
            (play, record) switch
            {
                // (FileInfo, FileInfo) => Task.WhenAll(
                //                         manager.Play(play.FullName, "ASIO4ALL v2", audio_cts.Token),
                //                         manager.Record<WasapiCapture>(record.FullName, audio_cts.Token)
                //                     ),
                (FileInfo, FileInfo) => Task.WhenAll(
                                        manager.Play<WasapiOut>(play.FullName, audio_cts.Token),
                                        manager.Record<WasapiCapture>(record.FullName, audio_cts.Token)
                                    ),
                // (FileInfo, null) => manager.Play(play.FullName, "ASIO4ALL v2", audio_cts.Token),
                (FileInfo, null) => manager.Play<WasapiOut>(play.FullName, audio_cts.Token),
                (null, FileInfo) => manager.Record<WasapiCapture>(record.FullName, audio_cts.Token),
                _ => throw new ArgumentException("must play or record")
            };
        var delay_task = Task.Delay(duration, ct);

        if (await Task.WhenAny(task, delay_task) == delay_task)
        {
            audio_cts.Cancel();
        }
        await task;
    }
}