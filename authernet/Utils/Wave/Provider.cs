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

namespace CS120.Utils.Wave.Provider;

public class StreamWaveProvider
(WaveFormat waveFormat, Stream stream) : IWaveProvider
{
    public WaveFormat WaveFormat { get; init; } = waveFormat;
    public Stream Stream { get; init; } = stream;

    public int Read(byte[] buffer, int offset, int count)
    {
        // Stream.L
        return Stream.Read(buffer, offset, count);
    }
}

public class PipeViewProvider : IWaveProvider, ISampleProvider
{
    public PipeReader? Reader { get; }
    public ISampleProvider SampleProvider { get; }
    public WaveFormat WaveFormat { get; }

    public PipeViewProvider(WaveFormat waveFormat, PipeReader reader)
    {
        WaveFormat = waveFormat;
        Reader = reader;
        SampleProvider = this.ToSampleProvider().ToMono();
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        if (Reader == null)
        {
            return 0;
        }

        var result = Reader.ReadAtLeastAsync(count).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();

        if (result.IsFinished())
        {
            Reader.AdvanceTo(result.Buffer.Start);
            return 0;
        }

        var resultBuffer = result.Buffer;
        var readed = Math.Min(count, resultBuffer.Length);

        var seq = resultBuffer.Slice(0, readed);
        seq.CopyTo(buffer.AsSpan(offset, (int)readed));
        Reader.AdvanceTo(seq.Start);
        return (int)readed;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        return SampleProvider.Read(buffer, offset, count);
    }

    public void AdvanceSamples(int numSamples)
    {
        if (Reader == null)
        {
            return;
        }
        if (Reader.TryRead(out var result))
        {
            // Console.WriteLine(result.Buffer.Length);
            Reader.AdvanceTo(
                result.Buffer.GetPosition(Math.Min(result.Buffer.Length, numSamples * WaveFormat.BitsPerSample / 8))
            );
        }
    }
}

public class NonBlockingPipeWaveProvider
(WaveFormat waveFormat, PipeReader pipeReader) : IWaveProvider
{
    private readonly PipeReader pipeReader = pipeReader;
    public WaveFormat WaveFormat { get; } = waveFormat;

    public int Read(byte[] buffer, int offset, int count)
    {
        if (pipeReader.TryRead(out var result) && !result.Buffer.IsEmpty)
        {
            var length = Math.Min(count, (int)result.Buffer.Length);

            var resultBuffer = result.Buffer.Slice(0, length);

            resultBuffer.CopyTo(buffer.AsSpan(offset, length));
            pipeReader.AdvanceTo(resultBuffer.End);
            return length;
        }
        else if (result.IsFinished())
        {
            pipeReader.AdvanceTo(result.Buffer.Start);
            return 0;
        }
        else
        {
            buffer.AsSpan(offset, count).Clear();
            return count;
        }
    }
}

public class NotifySampleProvider
(ChannelReader<NotifySampleProvider.PlayTask> channelReader, WaveFormat waveFormat) : ISampleProvider
{
    public record struct PlayTask
    (ReadOnlySequence<float> Data, TaskCompletionSource<bool> Task);
    public WaveFormat WaveFormat { get; } = waveFormat;

    private PlayTask currentTask = default;

    public int Read(float[] buffer, int offset, int count)
    {
        // Console.WriteLine(count);
        if (currentTask.Data.IsEmpty)
        {
            // Console.WriteLine(currentTask.Task is null);
            currentTask.Task?.TrySetResult(true);
            while (true)
            {
                if (!channelReader.TryPeek(out currentTask))
                {
                    if (channelReader.Completion.IsCompleted)
                        return 0;
                    // if (readed == 0)
                    {
                        buffer.AsSpan(offset, count).Clear();
                        return count;
                    }
                }

                if (currentTask.Task.Task.IsCompleted || currentTask.Data.IsEmpty)
                    channelReader.TryRead(out _);
                else
                    break;
            }
        }

        var readed = (int)Math.Min(count, currentTask.Data.Length);
        // if (readed != count)
        buffer.AsSpan(offset + readed, count - readed).Clear();
        currentTask.Data.Slice(0, readed).CopyTo(buffer.AsSpan(offset, readed));
        currentTask.Data = currentTask.Data.Slice(readed);

        // if (currentTask.Data.IsEmpty)
        // {
        //     // Console.WriteLine(currentTask.Task is null);
        //     currentTask.Task?.TrySetResult(true);
        // }
        // Console.WriteLine(readed);
        // for (int i = 0; i < readed; i++)
        // {
        //     Console.WriteLine(buffer[i + offset]);
        // }
        // return readed;
        return count;
    }
}
