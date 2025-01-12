using System.Buffers;
using System.IO.Pipelines;
using System.Numerics;
using CS120.Utils.Extension;
using NAudio.Wave;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CS120.Mac;
using CS120.Utils;
using CS120.Utils.Codec;
using STH1123.ReedSolomon;
using System.Text.Json;
using CommunityToolkit.HighPerformance;
using System.Threading.Channels;
using Nerdbank.Streams;
using NAudio.Wave.Asio;
using DotNext;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Data.Matlab;
using CS120.Utils.Wave.Provider;
using static CS120.Utils.Wave.Provider.NotifySampleProvider;
using CS120.Utils.Wave.Reader;

namespace CS120.Utils.IO;
public interface IInChannel<T>
{
    ValueTask<T?> ReadAsync(CancellationToken token = default);
}

public interface IOutChannel<T>
{
    ValueTask WriteAsync(T data, CancellationToken token = default);
}

public interface IIOChannel<T> : IInChannel<T>,
                                 IOutChannel<T>
{
}

public interface IInStream<T> : IInChannel<ReadResult<T>>
{
    void AdvanceTo(SequencePosition position);
}

public readonly record struct ReadResult<T>(ReadOnlySequence<T> Buffer, bool IsCompleted)
{
}

public partial class AudioPipeOutChannel
(WaveFormat waveFormat, int quietSamples = 220) : IOutChannel<ReadOnlySequence<float>>, IAsyncDisposable
{
    // private readonly Channel<PlayTask> channel = Channel.CreateUnbounded<PlayTask>();
    private readonly Pipe pipe = new(new(pauseWriterThreshold: 0));
    private readonly float[] quietBuffer = new float[quietSamples];
    private PipeWriter Writer => pipe.Writer;
    public WaveFormat WaveFormat { get; } = waveFormat;
    public PipeReader Reader => pipe.Reader;
    public async ValueTask WriteAsync(ReadOnlySequence<float> data, CancellationToken ct = default)
    {
        foreach (var seg in data)
        {
            Writer.Write(seg.Span);
        }
        ReadOnlySpan<float> quietSpan = quietBuffer;
        Writer.Write(quietSpan);
        await Writer.FlushAsync(ct);
    }

    // public async ValueTask CompleteAsync(Exception? exception = null)
    // {
    //     await Writer.CompleteAsync(exception);
    // }

    public async ValueTask DisposeAsync()
    {
        await Writer.CompleteAsync();
    }
    // public ValueTask Flush(bool cancel = true)
    // {
    //     while (channel.Reader.TryRead(out var task))
    //     {
    //         if (cancel)
    //             task.Task.TrySetCanceled();
    //         else
    //             task.Task.TrySetResult(true);
    //     }
    //     return default;
    // }
}
public class AudioOutChannel : IOutChannel<ReadOnlySequence<float>>, IAsyncDisposable
{
    private readonly Channel<PlayTask> channel = Channel.CreateUnbounded<PlayTask>();
    public WaveFormat WaveFormat { get; }
    public ISampleProvider SampleProvider { get; }
    public AudioOutChannel(WaveFormat waveFormat)
    {
        WaveFormat = waveFormat;
        SampleProvider = new NotifySampleProvider(channel.Reader, waveFormat);
    }
    public async ValueTask WriteAsync(ReadOnlySequence<float> data, CancellationToken ct = default)
    {
        var task = new TaskCompletionSource<bool>();
        using (ct.Register(() => task.TrySetCanceled()))
        {
            await channel.Writer.WriteAsync(new PlayTask(data, task), ct);
            await task.Task;
        }
    }

    // public ValueTask CompleteAsync(Exception? exception = null)
    // {
    //     channel.Writer.TryComplete();
    //     return ValueTask.CompletedTask;
    // }

    public ValueTask DisposeAsync()
    {
        channel.Writer.TryComplete();
        return ValueTask.CompletedTask;
        // return CompleteAsync();
    }
    // public ValueTask Flush(bool cancel = true)
    // {
    //     while (channel.Reader.TryRead(out var task))
    //     {
    //         if (cancel)
    //             task.Task.TrySetCanceled();
    //         else
    //             task.Task.TrySetResult(true);
    //     }
    //     return default;
    // }
}

public class AudioPipeInStream<TSample> : IInStream<TSample>, IAsyncDisposable
    where TSample : unmanaged, INumber<TSample>
{
    private readonly Pipe pipe = new(new(pauseWriterThreshold: 0));
    private PipeReader Reader => pipe.Reader;
    private readonly IWaveReader<TSample> sampleReader;
    private readonly Sequence<TSample> seq = new();

    public WaveFormat WaveFormat { get; }
    public PipeWriter Writer => pipe.Writer;

    // private TaskCompletionSource dataNotify = new();

    // private ReadOnlySequence<TSample> samples = default;
    public AudioPipeInStream(WaveFormat waveFormat)
    {
        WaveFormat = waveFormat;

        sampleReader = Reader.ToSamples<TSample>(waveFormat);
        Console.WriteLine(WaveFormat.Channels);
        Console.WriteLine(WaveFormat.SampleRate);
        Console.WriteLine(WaveFormat.Encoding);
    }

    public async ValueTask DisposeAsync()
    {
        await pipe.Reader.CompleteAsync();
    }

    public async ValueTask<ReadResult<TSample>> ReadAsync(CancellationToken token = default)
    {
        var result = await Reader.ReadAsync(token);
        Reader.AdvanceTo(result.Buffer.Start);
        if (!result.IsFinished())
        {
            var length = sampleReader.Length;

            // Console.WriteLine($"l1 {seq.Length} {seq.AsReadOnlySequence.Start.}");
            var readed = sampleReader.Read(seq.GetSpan(length)[..length]);
            // Console.WriteLine($"l2 {seq.Length} {readed.Length} {length}");
            seq.Advance(readed.Length);
        }

        // Console.WriteLine($"l3 {seq.Length} {readed.Length} {length}");
        return new(seq, result.IsCompleted);
    }

    public void AdvanceTo(SequencePosition position)
    {
        // samples = samples.Slice(position);
        seq.AdvanceTo(position);
    }
}

public class AudioMonoInStream<TSample> : IInStream<TSample>, IAsyncDisposable
    where TSample : unmanaged, INumber<TSample>
{
    private readonly Pipe pipe = new(new(pauseWriterThreshold: 0));
    private readonly IWaveReader<TSample> sampleReader;
    private readonly Sequence<TSample> seq = new();
    private readonly int channel;
    private readonly int bytesPerSample;
    private PipeWriter Writer => pipe.Writer;
    private PipeReader Reader => pipe.Reader;

    public WaveFormat WaveFormat { get; }
    // public bool IsCompleted => Reader.IsFinished();
    private List<TSample> samples = [];
    // private TaskCompletionSource dataNotify = new();

    // private ReadOnlySequence<TSample> samples = default;
    public AudioMonoInStream(WaveFormat waveFormat, int channel = 0)
    {
        WaveFormat = waveFormat;

        sampleReader = Reader.ToSamples<TSample>(waveFormat);

        bytesPerSample = waveFormat.BitsPerSample / 8;
        this.channel = channel;
        Console.WriteLine(WaveFormat.Channels);
        Console.WriteLine(WaveFormat.SampleRate);
        Console.WriteLine(WaveFormat.Encoding);
        // .ToMonoSelect();
    }
    public void DataAvailable(object? sender, WaveInEventArgs args)
    {
        var span = args.Buffer.AsSpan(0, args.BytesRecorded);

        if (WaveFormat.Channels == 1)
            Writer.Write(span);
        else
            for (int i = 0; i < span.Length; i += bytesPerSample * WaveFormat.Channels)
                Writer.Write(span.Slice(i + channel * bytesPerSample, bytesPerSample));
        Writer.FlushAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public unsafe void DataAvailable(object? sender, AsioAudioAvailableEventArgs args)
    {
        var length = args.SamplesPerBuffer * args.AsioSampleType switch { AsioSampleType.Int16LSB => 2,
                                                                          AsioSampleType.Int24LSB => 3,
                                                                          AsioSampleType.Int32LSB => 4,
                                                                          AsioSampleType.Float32LSB => 4,
                                                                          _ => throw new NotSupportedException() };
        // Console.WriteLine(length);

        // // for (int i = 0; i < args.SamplesPerBuffer; i++)
        // // {
        // //     Console.Write(z[i]);
        // // }
        Writer.Write(new Span<byte>((byte *)args.InputBuffers[channel], length));
        Writer.FlushAsync().AsTask().ConfigureAwait(false).GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {

        // var mat = Matrix<TSample>.Build.DenseOfColumnMajor(1, samples.Count, [..samples]);
        // MatlabWriter.Write("../matlab/debugwav.mat", mat, $"audio");
        await Writer.CompleteAsync();
    }
    // private async Task DecodeSampleAsync()
    // {
    //     var seq = new Sequence<TSample>();
    //     var sampleReader = reader.ToSamples<TSample>(WaveFormat).ToMonoSelect();
    //     while (true)
    //     {
    //         var result = await reader.ReadAsync();
    //         reader.AdvanceTo(result.Buffer.Start);

    //         seq.AdvanceTo(samples.Start);
    //         var length = sampleReader.Length;

    //         if (length == 0)
    //             continue;

    //         var readed = sampleReader.Read(seq.GetSpan(length));
    //         seq.Advance(readed.Length);
    //         samples = seq.AsReadOnlySequence;

    //         dataNotify.TrySetResult();

    //     }

    // }

    public async ValueTask<ReadResult<TSample>> ReadAsync(CancellationToken token = default)
    {
        var result = await Reader.ReadAsync(token);
        Reader.AdvanceTo(result.Buffer.Start);
        if (!result.IsFinished())
        {
            var length = sampleReader.Length;

            // Console.WriteLine($"l1 {seq.Length} {seq.AsReadOnlySequence.Start.}");
            var readed = sampleReader.Read(seq.GetSpan(length)[..length]);
            samples.AddRange(readed);
            // Console.WriteLine($"l2 {seq.Length} {readed.Length} {length}");
            seq.Advance(readed.Length);
        }

        // Console.WriteLine($"l3 {seq.Length} {readed.Length} {length}");
        return new(seq, result.IsCompleted);
    }

    public void AdvanceTo(SequencePosition position)
    {
        // samples = samples.Slice(position);
        seq.AdvanceTo(position);
    }
}