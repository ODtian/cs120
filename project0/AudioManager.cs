using System.Diagnostics.CodeAnalysis;
using System.Reflection.Metadata;
using NAudio;
using NAudio.CoreAudioApi;
using NAudio.Utils;
using NAudio.Wave;

namespace CS120
{

class AudioManager
{

    public static string[] ListAsioDevice()
    {
        return AsioOut.GetDriverNames();
    }

    public async Task Play(string audioFile, string deviceName, CancellationToken ct, int inputChannelOffset = 0)
    {
        using var audio = new AudioFileReader(audioFile);

        using var outputDevice = new AsioOut(deviceName) { AutoStop = true, InputChannelOffset = inputChannelOffset };
        ct.Register(() => outputDevice.Stop());

        var tsc = new TaskCompletionSource();
        outputDevice.PlaybackStopped += (s, e) => tsc.SetResult();

        outputDevice.Init(audio);
        outputDevice.Play();

        await tsc.Task;
    }

    public async Task RecordAndPlay(
        string recordFile,
        string deviceName,
        CancellationToken ct,
        int inputChannelOffset = 0,
        int channelCount = 2,
        int sampleRate = 48000,
        string? audioFile = null
        // IWaveProvider? audioIn = null
    )
    {
        Console.WriteLine(Directory.GetCurrentDirectory());

        using var fileWriter = new WaveFileWriter(recordFile, new WaveFormat(sampleRate, channelCount));

        using var asioOut = new AsioOut(deviceName) { InputChannelOffset = inputChannelOffset };
        ct.Register(() => asioOut.Stop());

        // var inputChannels = asioOut.DriverInputChannelCount;
        // Console.WriteLine(inputChannels);

        // for (int i = 0; i < asioOut.DriverInputChannelCount; i++)
        // {
        //     Console.WriteLine(asioOut.AsioInputChannelName(i));
        // }

        var buffer = new float[1024];
        asioOut.AudioAvailable += (s, e) =>
        {
            // BufferHelpers.Ensure(buffer, e.SamplesPerBuffer * channelCount);

            // var x = e.GetAsInterleavedSamples();
            // Console.WriteLine(x.Max());
            var count = e.GetAsInterleavedSamples(buffer);
            fileWriter.WriteSamples(buffer, 0, count);
        };

        var tsc = new TaskCompletionSource();
        asioOut.PlaybackStopped += (s, e) => tsc.SetResult();

        asioOut.InitRecordAndPlayback(
            audioFile != null ? new AudioFileReader(audioFile) : null, channelCount, sampleRate
        );
        asioOut.Play(); // start recording

        await tsc.Task;
    }
    public async Task Record<T>(string recordFile, CancellationToken ct)
        where T : IWaveIn, new()
    {

        using var recorder = new T();
        ct.Register(() => recorder.StopRecording());

        using var fileWriter = new WaveFileWriter(recordFile, recorder.WaveFormat);
        recorder.DataAvailable += (s, e) =>
        { fileWriter.Write(e.Buffer, 0, e.BytesRecorded); };

        var tsc = new TaskCompletionSource();
        recorder.RecordingStopped += (s, e) => tsc.SetResult();

        recorder.StartRecording();

        await tsc.Task;
    }
}
}