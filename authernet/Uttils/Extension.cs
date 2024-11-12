using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using NAudio.Wave;
using STH1123.ReedSolomon;

namespace CS120.Utils.Extension;

public static class IEnumerableExtension
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

public static class SampleProviderExtension
{
    public static int ReadExact<T>(this T sampleProvider, float[] buffer, int offset, int count)
        where T : ISampleProvider
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

public static class PipeReaderExtension
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFinished(this ReadResult readResult)
    {
        return (readResult.IsCompleted || readResult.IsCanceled) && readResult.Buffer.IsEmpty;
    }
}

public static class WaveFormatExtension
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ConvertLatencyToSampleSize(this WaveFormat waveFormat, float latency)
    {
        return (int)(waveFormat.SampleRate * latency) * waveFormat.Channels;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ConvertSamplesToByteSize(this WaveFormat waveFormat, int samples)
    {
        return samples * waveFormat.BitsPerSample / 8;
    }
}

public static class MemoryExtension
{
    public static Span<byte> AsBytes<T>(this T value)
        where T : unmanaged
    {
        return MemoryMarshal.CreateSpan(ref Unsafe.As<T, byte>(ref value), Unsafe.SizeOf<T>());
    }
}