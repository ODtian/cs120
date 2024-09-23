using NAudio.Wave;

namespace CS120;

class AudioManager
{

    public static string[] ListAsioDevice()
    {
        return AsioOut.GetDriverNames();
    }
    public async Task
    Play(IWaveProvider audioProvider, string deviceName, CancellationToken ct, int inputChannelOffset = 0)
    {

        using var outputDevice = new AsioOut(deviceName) { AutoStop = true, InputChannelOffset = inputChannelOffset };
        ct.Register(() => outputDevice.Stop());

        var tsc = new TaskCompletionSource();
        outputDevice.PlaybackStopped += (s, e) =>
        {
            Console.WriteLine(outputDevice);
            tsc.SetResult();
        };

        outputDevice.Init(audioProvider);
        outputDevice.Play();

        await tsc.Task;
    }
    public async Task Play(string audioFile, string deviceName, CancellationToken ct, int inputChannelOffset = 0)
    {
        using var audioProvider = new AudioFileReader(audioFile);
        await Play(audioProvider, deviceName, ct, inputChannelOffset);
    }

    public async Task RecordAndPlay(
        Stream audioStream,
        string deviceName,
        CancellationToken ct,
        int inputChannelOffset = 0,
        int channelCount = 2,
        int sampleRate = 48000,
        IWaveProvider? audioProvider = null
    )
    {
        Console.WriteLine(Directory.GetCurrentDirectory());

        using var asioOut = new AsioOut(deviceName) { InputChannelOffset = inputChannelOffset };
        ct.Register(() => asioOut.Stop());

        // var inputChannels = asioOut.DriverInputChannelCount;
        // Console.WriteLine(inputChannels);

        // for (int i = 0; i < asioOut.DriverInputChannelCount; i++)
        // {
        //     Console.WriteLine(asioOut.AsioInputChannelName(i));
        // }

        // var x = Channel.CreateUnbounded<byte>();
        var buffer = new float[1024];
        var writer = new BinaryWriter(audioStream);
        asioOut.AudioAvailable += (s, e) =>
        {
            // BufferHelpers.Ensure(buffer, e.SamplesPerBuffer * channelCount);

            // var x = e.GetAsInterleavedSamples();
            // Console.WriteLine(x.Max());
            var count = e.GetAsInterleavedSamples(buffer);
            // audioStream.Write()
            for (int i = 0; i < count; i++)
            {
                writer.Write(buffer[i]);
            }
            // x.Writer.var count = e.GetAsInterleavedSamples(buffer);
            // fileWriter.WriteSamples(buffer, 0, count);
        };

        var tsc = new TaskCompletionSource();
        asioOut.PlaybackStopped += (s, e) => tsc.SetResult();

        asioOut.InitRecordAndPlayback(audioProvider, channelCount, sampleRate);
        asioOut.Play(); // start recording

        await tsc.Task;
    }

    public async Task RecordAndPlay(
        string recordName,
        string deviceName,
        CancellationToken ct,
        int inputChannelOffset = 0,
        int channelCount = 2,
        int sampleRate = 48000,
        string? audioName = null
    )
    {
        // using var audioStream = new MemoryStream();

        using var waveWriter = new WaveFileWriter(recordName, new WaveFormat(sampleRate, channelCount));
        using var audioProvider = audioName == null ? null : new AudioFileReader(audioName);

        await RecordAndPlay(waveWriter, deviceName, ct, inputChannelOffset, channelCount, sampleRate, audioProvider);
    }

    public async Task Play<T>(IWaveProvider audioProvider, CancellationToken ct)
        where T : IWavePlayer, new()
    {

        using var outputDevice = new T();

        ct.Register(() => outputDevice.Stop());

        var tsc = new TaskCompletionSource();
        outputDevice.PlaybackStopped += (s, e) =>
        {
            tsc.SetResult();
        };

        outputDevice.Init(audioProvider);
        outputDevice.Play();

        await tsc.Task;
    }

    public async Task Play<T>(string audioFile, CancellationToken ct)
        where T : IWavePlayer, new()
    {
        using var audioProvider = new AudioFileReader(audioFile);
        await Play<T>(audioProvider, ct);
    }

    public async Task Record(Stream audioStream, IWaveIn recorder, CancellationToken ct)
    {

        ct.Register(() => recorder.StopRecording());

        recorder.DataAvailable += (s, e) =>
        { audioStream.Write(e.Buffer, 0, e.BytesRecorded); };

        var tsc = new TaskCompletionSource();
        recorder.RecordingStopped += (s, e) => tsc.SetResult();

        recorder.StartRecording();

        await tsc.Task;
    }

    public async Task Record<T>(string recordFile, CancellationToken ct)
        where T : IWaveIn, new()
    {
        using var recorder = new T();
        using var fileWriter = new WaveFileWriter(recordFile, recorder.WaveFormat);

        await Record(fileWriter, recorder, ct);
    }
}