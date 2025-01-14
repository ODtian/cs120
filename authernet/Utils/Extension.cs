using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using CommunityToolkit.HighPerformance;
using NAudio.Wave;
using STH1123.ReedSolomon;

namespace CS120.Utils.Extension;

public static class IEnumerableExtension
{
    public static IEnumerable<T> GetElements<T>(this ReadOnlySequence<T> source)
    {
        foreach (var item in source)
            for (var i = 0; i < item.Length; i++)
                yield return item.Span[i];
    }
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
                break;

            readed += read;

            offset += read;
            count -= read;
        }
        return readed;
    }
}

public static class ReaderExtension
{
    public static bool IsFinished<T>(this ChannelReader<T> reader)
    {
        return reader.Completion.IsCompleted && !reader.TryPeek(out _);
    }

    public static async ValueTask<T> TryReadAsync<T>(this ChannelReader<T> reader, CancellationToken ct = default)
        where T : struct
    {
        if (await reader.WaitToReadAsync(ct))
            if (reader.TryRead(out var data))
                return data;

        return default;
    }

    // public static bool IsFinished(this PipeReader pipeReader)
    // {
    //     if (pipeReader.TryRead(out var readResult))
    //     {
    //         pipeReader.AdvanceTo(readResult.Buffer.Start);
    //         return readResult.IsFinished();
    //     }
    //     return false;
    // }

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

public static class ReadOnlySequnceExtension
{
    public static long GetLength(this PipeReader reader)
    {
        if (reader.TryRead(out var result))
        {
            var length = (int)result.Buffer.Length;
            reader.AdvanceTo(result.Buffer.Start);
            return length;
        }
        return 0;
    }
    public static ReadOnlySpan<U> ConsumeCast<T, U>(
        this scoped ref ReadOnlySequence<T> seq, int maxLength, Span<U> buffer
    )
        where T : unmanaged
        where U : unmanaged
    {
        var result = seq.GetSpanCast(maxLength, buffer);
        if (result.Length == 0)
            return result;

        seq = seq.Slice(result.Cast<U, T>().Length);

        return result;
    }
    public static ReadOnlySpan<T> ConsumeExact<T>(this scoped ref ReadOnlySequence<T> seq, Span<T> buffer)
    {
        var result = seq.GetSpanExact(buffer);

        if (result.Length == 0)
            return result;

        seq = seq.Slice(result.Length);

        return result;
    }
    // Read as much struct in first span from byte seq as possible, if first span is not enough, copy to buffer
    public static ReadOnlySpan<U> GetSpanCast<T, U>(this ReadOnlySequence<T> seq, int maxLength, Span<U> buffer)
        where T : unmanaged
        where U : unmanaged
    {
        if (seq.Length * Unsafe.SizeOf<T>() < Unsafe.SizeOf<U>())
            return default;

        var result = seq.FirstSpan.Cast<T, U>();
        result = result[..Math.Min(result.Length, maxLength)];

        if (result.Length == 0)
        {
            seq.CopyTo(buffer.Cast<U, T>());
            result = buffer;
        }

        return result;
    }

    public static ReadOnlySpan<T> GetSpanExact<T>(this ReadOnlySequence<T> seq, Span<T> buffer)
    {
        if (seq.Length < buffer.Length)
            return default;

        ReadOnlySpan<T> result = buffer;

        if (seq.FirstSpan.Length >= buffer.Length)
            result = seq.FirstSpan[..buffer.Length];
        else
            seq.Slice(0, buffer.Length).CopyTo(buffer);

        return result;
    }
}