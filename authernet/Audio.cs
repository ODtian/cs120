using System.IO.Pipelines;
using CS120.Utils.Wave;
using CS120.Utils.Wave.Provider;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.Asio;

namespace CS120;

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
        // recorder recorder.DataAvailable += (s, e) =>
        // {
        //     // Console.WriteLine(e.BytesRecorded);
        //     audioStream.Write(e.Buffer, 0, e.BytesRecorded);
        // };

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

    // public static async Task PlayAsync(AsioOut player, CancellationToken ct)
    // {
    //     var tsc = new TaskCompletionSource();
    //     player.PlaybackStopped += (s, e) =>
    //     {
    //         if (e.Exception != null)
    //             tsc.SetException(e.Exception);
    //         else
    //             tsc.SetResult();
    //     };

    //     player.Play();

    //     var stopTask = Task.CompletedTask;
    //     using (ct.Register(() => stopTask = Task.Run(player.Stop)))
    //     {
    //         await tsc.Task;
    //         await stopTask;
    //     }
    // }

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

// public class AudioManager
// {

//     public static string[] ListAsioDevice()
//     {
//         return AsioOut.GetDriverNames();
//     }

//     public async Task Play(IWaveProvider audioProvider, string deviceName, CancellationToken ct, int channelOffset =
//     0)
//     {

//         using var outputDevice = new AsioOut(deviceName) { AutoStop = true, ChannelOffset = channelOffset };

//         var tsc = new TaskCompletionSource();
//         outputDevice.PlaybackStopped += (s, e) =>
//         {
//             if (e.Exception != null)
//                 tsc.SetException(e.Exception);
//             else
//                 tsc.SetResult();
//         };

//         outputDevice.Init(audioProvider);
//         outputDevice.Play();

//         using (ct.Register(outputDevice.Stop)) await tsc.Task;
//     }
//     public async Task Play(string audioFile, string deviceName, CancellationToken ct, int channelOffset = 0)
//     {
//         using var audioProvider = new AudioFileReader(audioFile);
//         await Play(audioProvider, deviceName, ct, channelOffset);
//     }

//     public async Task RecordAndPlay(
//         Stream audioStream,
//         string deviceName,
//         CancellationToken ct,
//         int inputChannelOffset = 0,
//         int channelCount = 2,
//         int sampleRate = 48000,
//         IWaveProvider? audioProvider = null
//     )
//     {
//         using var asioOut = new AsioOut(deviceName) { InputChannelOffset = inputChannelOffset };

//         // var inputChannels = asioOut.DriverInputChannelCount;
//         // Console.WriteLine(inputChannels);

//         // for (int i = 0; i < asioOut.DriverInputChannelCount; i++)
//         // {
//         //     Console.WriteLine(asioOut.AsioInputChannelName(i));
//         // }

//         // var x = Channel.CreateUnbounded<byte>();
//         var buffer = new float[1024];
//         var writer = new BinaryWriter(audioStream);
//         asioOut.AudioAvailable += (s, e) =>
//         {
//             // BufferHelpers.Ensure(buffer, e.SamplesPerBuffer * channelCount);

//             // var x = e.GetAsInterleavedSamples();
//             // Console.WriteLine(x.Max());
//             var count = e.GetAsInterleavedSamples(buffer);
//             // e.InputBuffers.AsSpan()
//             // audioStream.Write(Mem)
//             // audioStream.Write()
//             for (int i = 0; i < count; i++)
//             {
//                 writer.Write(buffer[i]);
//             }
//         };

//         var tsc = new TaskCompletionSource();
//         asioOut.PlaybackStopped += (s, e) =>
//         {
//             if (e.Exception != null)
//                 tsc.SetException(e.Exception);
//             else
//                 tsc.SetResult();
//         };

//         asioOut.InitRecordAndPlayback(audioProvider, channelCount, sampleRate);
//         asioOut.Play(); // start recording

//         using (ct.Register(asioOut.Stop)) await tsc.Task;
//     }

//     public async Task RecordAndPlay(
//         string recordName,
//         string deviceName,
//         CancellationToken ct,
//         int inputChannelOffset = 0,
//         int channelCount = 2,
//         int sampleRate = 48000,
//         string? audioName = null
//     )
//     {
//         // using var audioStream = new MemoryStream();

//         using var waveWriter = new WaveFileWriter(recordName, new WaveFormat(sampleRate, channelCount));
//         using var audioProvider = audioName == null ? null : new AudioFileReader(audioName);

//         await RecordAndPlay(waveWriter, deviceName, ct, inputChannelOffset, channelCount, sampleRate, audioProvider);
//     }

//     public async Task Play<T>(IWaveProvider audioProvider, CancellationToken ct)
//         where T : IWavePlayer, new()
//     {

//         using var outputDevice = new T();

//         var tsc = new TaskCompletionSource();
//         outputDevice.PlaybackStopped += (s, e) =>
//         {
//             if (e.Exception != null)
//                 tsc.SetException(e.Exception);
//             else
//                 tsc.SetResult();
//         };
//         // outputDevice.PlaybackStopped += (s, e) => tsc.SetResult();

//         outputDevice.Init(audioProvider);
//         outputDevice.Play();

//         Task? stopTask = null;
//         using (ct.Register(() => stopTask = Task.Run(outputDevice.Stop)))
//         {
//             await tsc.Task;
//             if (stopTask != null)
//                 await stopTask;
//         }
//     }

//     public async Task Play<T>(string audioFile, CancellationToken ct)
//         where T : IWavePlayer, new()
//     {
//         using var audioProvider = new AudioFileReader(audioFile);
//         await Play<T>(audioProvider, ct);
//     }

//     // public async Task Record(Stream audioStream, IWaveIn recorder, CancellationToken ct)
//     // {
//     //     recorder.DataAvailable += (s, e) => audioStream.Write(e.Buffer, 0, e.BytesRecorded);

//     //     var tsc = new TaskCompletionSource();
//     //     recorder.RecordingStopped += (s, e) =>
//     //     {
//     //         if (e.Exception != null)
//     //             tsc.SetException(e.Exception);
//     //         else
//     //             tsc.SetResult();
//     //     };

//     //     recorder.StartRecording();

//     //     ct.Register(() => recorder.StopRecording());

//     //     await tsc.Task;
//     // }

//     public async Task Record<T>(Stream audioStream, CancellationToken ct)
//         where T : IWaveIn, new()
//     {
//         using var recorder = new T();

//         recorder.DataAvailable += (s, e) =>
//         {
//             // Console.WriteLine(e.BytesRecorded);
//             audioStream.Write(e.Buffer, 0, e.BytesRecorded);
//         };

//         var tsc = new TaskCompletionSource();
//         recorder.RecordingStopped += (s, e) =>
//         {
//             if (e.Exception != null)
//                 tsc.SetException(e.Exception);
//             else
//                 tsc.SetResult();
//         };

//         recorder.StartRecording();

//         Task? stopTask = null;
//         using (ct.Register(() => stopTask = Task.Run(recorder.StopRecording)))
//         {
//             await tsc.Task;
//             if (stopTask != null)
//                 await stopTask;
//         }
//     }

//     public async Task Record<T>(string recordFile, CancellationToken ct)
//         where T : IWaveIn, new()
//     {
//         using var fileWriter = new WaveFileWriter(recordFile, GetRecorderWaveFormat<T>());
//         await Record<T>(fileWriter, ct);
//     }

//     public async Task RecordThenPlay<TWaveIn, TWaveOut>(IEnumerable<CancellationToken> ct)
//         where TWaveIn : IWaveIn, new()
//         where TWaveOut : IWavePlayer, new()
//     {
//         var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));

//         using (var streamIn = pipe.Writer.AsStream())
//         {
//             await Record<TWaveIn>(streamIn, ct.First());
//         }

//         using var streamOut = pipe.Reader.AsStream();
//         await Play<TWaveOut>(new StreamWaveProvider(GetRecorderWaveFormat<TWaveIn>(), streamOut),
//         ct.Skip(1).First());
//     }

//     public WaveFormat GetRecorderWaveFormat<TWaveIn>()
//         where TWaveIn : IWaveIn, new()
//     {
//         using var recorder = new TWaveIn();
//         return recorder.WaveFormat;
//     }

//     public WaveFormat GetPlayerWaveFormat<TWaveOut>()
//         where TWaveOut : IWavePlayer, new()
//     {
//         using var player = new TWaveOut();
//         return player.OutputWaveFormat;
//     }
// }
