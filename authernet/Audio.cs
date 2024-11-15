using System.IO.Pipelines;
using CS120.Utils.Wave;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CS120;

public static class Audio
{
    public static string[] ListAsioDevices()
    {
        return AsioOut.GetDriverNames();
    }

    public static void ListWASAPIDevices()
    {
        var enumerator = new MMDeviceEnumerator();

        Console.WriteLine("Render devices (Speakers):");
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            Console.WriteLine($"\tFriendlyName: {device.FriendlyName, -40}, ID: {device.ID, -40}");

        Console.WriteLine();

        Console.WriteLine("Capture devices (Recorder):");
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            Console.WriteLine($"\tFriendlyName: {device.FriendlyName, -40}, ID: {device.ID, -40}");
    }

    public static async Task Play<T>(T player, CancellationToken ct)
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

    public static async Task Play<T>(IWaveProvider audioProvider, CancellationToken ct)
        where T : IWavePlayer, new()
    {
        using var player = new T();
        player.Init(audioProvider);
        await Play(player, ct);
    }

    public static async Task Play<T>(string audioFile, CancellationToken ct)
        where T : IWavePlayer, new()
    {
        using var audio = new AudioFileReader(audioFile);
        await Play<T>(audio, ct);
    }

    public static async Task Record<T>(T recorder, CancellationToken ct)
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

    public static async Task Record<T>(Stream audioStream, CancellationToken ct)
        where T : IWaveIn, new()
    {
        using var recorder = new T();
        recorder.DataAvailable += (s, e) => audioStream.Write(e.Buffer, 0, e.BytesRecorded);
        await Record(recorder, ct);
    }

    public static async Task Record<T>(string recordFile, CancellationToken ct)
        where T : IWaveIn, new()
    {
        using var fileWriter = new WaveFileWriter(recordFile, GetRecorderWaveFormat<T>());
        await Record<T>(fileWriter, ct);
    }

    public static MMDevice GetWASAPIDeviceByName(string deviceName, DataFlow dataFlow)
    {
        return new MMDeviceEnumerator()
            .EnumerateAudioEndPoints(dataFlow, DeviceState.Active)
            .First(device => device.FriendlyName == deviceName);
    }

    public static MMDevice GetWASAPIDeviceByID(string deviceID, DataFlow dataFlow)
    {
        return new MMDeviceEnumerator()
            .EnumerateAudioEndPoints(dataFlow, DeviceState.Active)
            .First(device => device.ID == deviceID);
    }

    public static async Task RecordThenPlay<TWaveIn, TWaveOut>(IEnumerable<CancellationToken> ct)
        where TWaveIn : IWaveIn, new()
        where TWaveOut : IWavePlayer, new()
    {
        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));

        using (var streamIn = pipe.Writer.AsStream())
        {
            await Record<TWaveIn>(streamIn, ct.First());
        }

        using var streamOut = pipe.Reader.AsStream();
        await Play<TWaveOut>(new StreamWaveProvider(GetRecorderWaveFormat<TWaveIn>(), streamOut), ct.Skip(1).First());
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
