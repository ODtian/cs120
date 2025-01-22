using System.IO.Pipelines;
using Aether.NET.Utils.Wave;
using Aether.NET.Utils.Wave.Provider;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.Asio;

namespace Aether.NET;

public static class Audio
{
    public static void ListAsioDevices()
    {
        using var asio = new AsioOut();

        Console.WriteLine("Render devices (Speakers):");
        for (int i = 0; i < asio.DriverOutputChannelCount; i++)
            Console.WriteLine($"\t{i} {asio.AsioInputChannelName(i)}");

        Console.WriteLine("Capture devices (Recorder):");
        for (int i = 0; i < asio.DriverInputChannelCount; i++)
            Console.WriteLine($"\t{i} {asio.AsioInputChannelName(i)}");
    }

    public static void ListWASAPIDevices()
    {
        using var enumerator = new MMDeviceEnumerator();

        Console.WriteLine("Render devices (Speakers):");
        foreach (var (index, device) in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).Index())
            Console.WriteLine($"\t{index} ID: {device.ID} \tFriendlyName: {device.FriendlyName}");

        Console.WriteLine("Capture devices (Recorder):");
        foreach (var (index, device) in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                     .Index())
            Console.WriteLine($"\t{index} ID: {device.ID} \tFriendlyName: {device.FriendlyName}");
    }

    public static async Task PlayAsync<T>(T player, CancellationToken ct)
        where T : IWavePlayer
    {
        var tsc = new TaskCompletionSource();
        player.PlaybackStopped += (s, e) =>
        {
            if (e.Exception != null)
                tsc.SetException(e.Exception);
            else
                tsc.SetResult();
        };

        player.Play();

        var stopTask = Task.CompletedTask;
        using (ct.Register(() => stopTask = Task.Run(player.Stop)))
        {
            await tsc.Task;
            await stopTask;
        }
    }

    public static async Task PlayAsync<T>(IWaveProvider audioProvider, CancellationToken ct)
        where T : IWavePlayer, new()
    {
        using var player = new T();
        player.Init(audioProvider);
        await PlayAsync(player, ct);
    }

    public static async Task PlayAsync<T>(string audioFile, CancellationToken ct)
        where T : IWavePlayer, new()
    {
        using var audio = new AudioFileReader(audioFile);
        await PlayAsync<T>(audio, ct);
    }

    public static async Task RecordAsync<T>(T recorder, CancellationToken ct)
        where T : IWaveIn
    {

        var tsc = new TaskCompletionSource();
        recorder.RecordingStopped += (s, e) =>
        {
            if (e.Exception != null)
                tsc.SetException(e.Exception);
            else
                tsc.SetResult();
        };

        recorder.StartRecording();

        var stopTask = Task.CompletedTask;
        using (ct.Register(() => stopTask = Task.Run(recorder.StopRecording)))
        {
            await tsc.Task;
            await stopTask;
        }
    }

    public static async Task RecordAsync<T>(Stream audioStream, CancellationToken ct)
        where T : IWaveIn, new()
    {
        using var recorder = new T();
        recorder.DataAvailable += (s, e) => audioStream.Write(e.Buffer, 0, e.BytesRecorded);
        await RecordAsync(recorder, ct);
    }

    public static async Task RecordAsync<T>(string recordFile, CancellationToken ct)
        where T : IWaveIn, new()
    {
        using var fileWriter = new WaveFileWriter(recordFile, GetRecorderWaveFormat<T>());
        await RecordAsync<T>(fileWriter, ct);
    }

    public static async Task RecordThenPlayAsync<TWaveIn, TWaveOut>(IEnumerable<CancellationToken> ct)
        where TWaveIn : IWaveIn, new()
        where TWaveOut : IWavePlayer, new()
    {
        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));

        using (var streamIn = pipe.Writer.AsStream())
        {
            await RecordAsync<TWaveIn>(streamIn, ct.First());
        }

        using var streamOut = pipe.Reader.AsStream();
        await PlayAsync<TWaveOut>(
            new StreamWaveProvider(GetRecorderWaveFormat<TWaveIn>(), streamOut), ct.Skip(1).First()
        );
    }

    public static async Task ASIOPlayAsync(IWaveProvider waveProvider, CancellationToken ct)
    {
        using var player = new AsioOut() {};
        player.Init(waveProvider);
        await PlayAsync(player, ct);
    }

    public static async Task ASIOPlayAsync(string audioFile, CancellationToken ct)
    {
        using var audio = new AudioFileReader(audioFile);
        await ASIOPlayAsync(audio, ct);
    }

    public static MMDevice GetWASAPIDevice(int index, DataFlow dataFlow)
    {
        using var enumerator = new MMDeviceEnumerator();
        return enumerator.EnumerateAudioEndPoints(dataFlow, DeviceState.Active).ElementAt(index);
    }
    public static WaveFormat GetRecorderWaveFormat<TWaveIn>()
        where TWaveIn : IWaveIn, new()
    {
        using var recorder = new TWaveIn();
        return recorder.WaveFormat;
    }

    public static WaveFormat GetPlayerWaveFormat<TWaveOut>()
        where TWaveOut : IWavePlayer, new()
    {
        using var player = new TWaveOut();
        return player.OutputWaveFormat;
    }
}
