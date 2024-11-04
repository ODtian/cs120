using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using NAudio.Wave;
using STH1123.ReedSolomon;

namespace CS120.Extension;

static class IEnumerableExtension
{
    public static void TakeInto<T>(this IEnumerable<T> source, Span<T> buffer)
    {
        var index = 0;
        foreach (var item in source.Take(buffer.Length))
        {
            buffer[index++] = item;
        }
    }
    public static IEnumerable<T> TakeBlocked<T>(this BlockingCollection<T> source, int count)
    {

        while (!source.IsAddingCompleted && source.Count < count)
        {
        }
        return source.Take(count);
    }
}

static class SampleProviderExtension
{
    public static int ReadExact(this ISampleProvider sampleProvider, float[] buffer, int offset, int count)
    {
        var readed = 0;
        while (count > 0)
        {
            var read = sampleProvider.Read(buffer, offset, count);
            if (read == 0)
            {
                break;
            }
            readed += read;

            offset += read;
            count -= read;
        }
        return readed;
    }
}

static class PipeReaderExtension
{
    public static bool IsFinished(this PipeReader pipeReader)
    {
        if (pipeReader.TryRead(out var readResult))
        {
            pipeReader.AdvanceTo(readResult.Buffer.Start);
            return readResult.IsFinished();
        }
        return false;
    }

    public static bool IsFinished(this ReadResult readResult)
    {
        return (readResult.IsCompleted || readResult.IsCanceled) && readResult.Buffer.IsEmpty;
    }
}

static class SamplesExtension
{
    public static ReadOnlySpan<byte> AsBytes(this float[] samples)
    {
        return MemoryMarshal.Cast<float, byte>(samples.AsSpan());
    }

    public static ReadOnlySpan<byte> AsBytes(this ReadOnlySpan<float> samples)
    {
        // Console.WriteLine(samples.Length);
        // Console.WriteLine(MemoryMarshal.Cast<float, byte>(samples).Length);
        return MemoryMarshal.Cast<float, byte>(samples);
    }
}

static class WaveFormatExtension
{
    public static int ConvertLatencyToSampleSize(this WaveFormat waveFormat, float latency)
    {
        return (int)(waveFormat.SampleRate * latency) * waveFormat.Channels;
    }

    public static int ConvertSamplesToByteSize(this WaveFormat waveFormat, int samples)
    {
        return samples * waveFormat.BitsPerSample / 8;
    }
}