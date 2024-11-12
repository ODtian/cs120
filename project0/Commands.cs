using System.CommandLine;
using CS120.Utils;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CS120.Commands;
public static class CommandTask
{

    public static void ListWASAPIDevices()
    {
        var enumerator = new MMDeviceEnumerator();
        Console.WriteLine("Render devices (Speakers):");
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            Console.WriteLine(device.FriendlyName);

        Console.WriteLine("Capture devices (Recorder):");
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            Console.WriteLine(device.FriendlyName);
    }

    public static async Task AudioCommandTask(FileInfo? play, FileInfo? record, bool recordPlayBack, int duration)
    {
        using var cancelToken = new CancelKeyPressCancellationTokenSource(new CancellationTokenSource(duration));
        using var cancelToken1 = new CancelKeyPressCancellationTokenSource(new CancellationTokenSource(), false);

        var taskPlay = play switch {
            FileInfo => Audio.Play<WasapiOut>(play.FullName, cancelToken.Source.Token),
            null => Task.CompletedTask,
        };

        var taskRecord = (record, recordPlayBack) switch {
            (_, true) => Audio.RecordThenPlay<WasapiCapture, WasapiOut>(new[] { cancelToken, cancelToken1 }.Select(
                cts =>
                {
                    cts.Enable(true);
                    cts.Source.CancelAfter(duration);
                    return cts.Source.Token;
                }
            )),
            (FileInfo, _) => Audio.Record<WasapiCapture>(record.FullName, cancelToken.Source.Token),
            _ => Task.CompletedTask,
        };

        var taskUni = Task.WhenAll(taskPlay, taskRecord);

        await taskUni;
    }
}

public static class CommandBuilder
{
    public static Command BuildAudioCommand()
    {
        var command = new Command("audio", "play with audio stuff");

        var playOption = new Option < FileInfo ? > (name: "--play",
                                                    description: "The file path to play",
                                                    isDefault: true,
                                                    parseArgument: result => FileHelper.ParseSingleFileInfo(result));

        var recordOption =
            new Option < FileInfo ? > (name: "--record",
                                       description: "The file path to record",
                                       isDefault: true,
                                       parseArgument: result => FileHelper.ParseSingleFileInfo(result, false));

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
            CommandTask.AudioCommandTask, playOption, recordOption, recordPlayBackOption, durationOption
        );
        return command;
    }
}