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
namespace CS120.Utils.Wave;

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

// public class BlockingCollectionSampleProvider
// (WaveFormat waveFormat, BlockingCollection<float> sampleBuffer) : ISampleProvider
// {
//     public WaveFormat WaveFormat { get; } = waveFormat;
//     public int ReadBlocking(float[] buffer, int offset, int count)
//     {
//         if (sampleBuffer.IsCompleted)
//         {
//             return 0;
//         }

//         // Console.WriteLine("Read: " + sampleBuffer.Count);

//         if (sampleBuffer.IsAddingCompleted)
//         {

//             count = Math.Min(count, sampleBuffer.Count);
//         }

//         sampleBuffer.GetConsumingEnumerable().TakeInto(buffer.AsSpan(offset, count));
//         return count;
//     }
//     public int Read(float[] buffer, int offset, int count)
//     {
//         if (sampleBuffer.IsCompleted)
//         {
//             return 0;
//         }

//         var bufferCount = sampleBuffer.Count;

//         if (bufferCount == 0)
//         {
//             buffer[offset] = 0;
//             return 1;
//         }

//         count = Math.Min(count, bufferCount);

//         sampleBuffer.GetConsumingEnumerable().TakeInto(buffer.AsSpan(offset, count));
//         return count;
//     }
// }

public interface IWaveReader<T>
{
    int Length { get; }
    bool Read(out ReadOnlySpan<T> result, Span<T> buffer);
    bool Advance(int count);
}


public struct SequenceWaveReader
(ReadOnlySequence<byte> seq) : IWaveReader<byte>
{
    // private ReadOnlySequence<byte> seq = seq;
    public readonly ReadOnlySequence<byte> Seq => seq;
    public readonly int Length => (int)seq.Length;
    // public SequenceWaveReader(ReadOnlySequence<byte> seq) {
    //     reader = new SequenceReader<byte>(seq);
    // }
    public bool Read(out ReadOnlySpan<byte> result, Span<byte> buffer)
    {
        var success = seq.TryReadOrCopy(out result, buffer);
        if (success)
            Advance(result.Length);
        return success;
    }

    public bool Advance(int count)
    {
        if (seq.Length < count)
            return false;
        seq = seq.Slice(count);
        return true;
    }
}
// public readonly struct MonoWaveProvider
// (WaveFormat waveFormat, IWaveProvider<byte> waveProvider) : IWaveProvider<byte>
// {
//     private readonly int channelCount = waveFormat.Channels;
//     private readonly int bytesPerSample = waveFormat.BitsPerSample / 8;

//     public ReadOnlySpan<byte> Read(Span<byte> buffer)
//     {
//         var result = waveProvider.Read(buffer);
//         waveProvider.Advance((channelCount - 1) * bytesPerSample);
//         return result;
//     }

//     public void Advance(int count)
//     {
//         waveProvider.Advance(count * channelCount * bytesPerSample);
//     }
//     // {
//     //     r
//     //         // waveProvider.Read(buffer);
//     //         new SequenceReader() for (int i = 0; i < buffer.Length; i += channelCount * bytesPerSample) {
//     //             waveProvider.Read(buffer.Slice(i, channelCount * bytesPerSample));
//     // }
// }


public readonly struct PCM16WaveToSampleProvider<T>(IWaveReader<byte> reader, WaveFormat waveFormat) : IWaveReader<T>
    where T : unmanaged, INumber<T>
{

    // private static readonly

    public readonly WaveFormat WaveFormat { get; } = waveFormat;
    public static readonly T PCM16MaxValue = T.CreateChecked(32768);
    public readonly int Length => reader.Length / 2;
    public readonly bool Read(out ReadOnlySpan<T> result, Span<T> buffer)
    {
        if (Length < buffer.Length)
        {
            result = default;
            return false;
        }

        Span<byte> buf = stackalloc byte[2];

        for (int i = 0; i < buffer.Length; i++)
        {
            reader.Read(out var readed, buf);
            buffer[i] = T.CreateChecked(BitConverter.ToInt16(readed)) / PCM16MaxValue;
        }

        result = buffer;
        return true;
    }

    public readonly bool Advance(int count)
    {
        return reader.Advance(count * 2);
    }
}

public readonly struct PCM24WaveToSampleProvider<T>(IWaveReader<byte> reader, WaveFormat waveFormat) : IWaveReader<T>
    where T : unmanaged, INumber<T>
{
    // private static readonly
    public static readonly T PCM24MaxValue = T.CreateChecked(8388608);
    public readonly WaveFormat WaveFormat { get; } = waveFormat;
    public readonly int Length => reader.Length / 3;
    public readonly bool Read(out ReadOnlySpan<T> result, Span<T> buffer)
    {
        if (Length < buffer.Length)
        {
            result = default;
            return false;
        }

        Span<byte> buf = stackalloc byte[3];
        // var buf3 = buf[1..3];

        for (int i = 0; i < buffer.Length; i++)
        {
            reader.Read(out var readed, buf);
            buffer[i] = T.CreateChecked(buf[0] << 16 | buf[1] << 8 | buf[2]) / PCM24MaxValue;
        }

        result = buffer;
        return true;
    }

    public readonly bool Advance(int count)
    {
        return reader.Advance(count * 2);
    }
}

public readonly struct PCM8WaveToSampleReader<T>(IWaveReader<byte> reader, WaveFormat waveFormat) : IWaveReader<T>
    where T : unmanaged, INumber<T>
{
    public static readonly T PCM8MaxValue = T.CreateChecked(128);
    public readonly WaveFormat WaveFormat { get; } = waveFormat;
    public readonly int Length => reader.Length;
    public bool Read(out ReadOnlySpan<T> result, Span<T> buffer)
    {
        if (Length < buffer.Length)
        {
            result = default;
            return false;
        }

        Span<byte> buf = stackalloc byte[1];
        for (int i = 0; i < buffer.Length; i++)
        {
            reader.Read(out var readed, buf);
            buffer[i] = T.CreateChecked(readed[0]) / PCM8MaxValue - T.One;
        }
        result = buffer;
        return true;
    }

    public bool Advance(int count)
    {
        return reader.Advance(count);
    }
}

public readonly struct MonoMixSampleReader<T>(IWaveReader<T> reader, WaveFormat waveFormat) : IWaveReader<T>
    where T : unmanaged, INumber<T>
{
    private readonly int channelCount = waveFormat.Channels;
    private readonly int bytesPerSample = waveFormat.BitsPerSample / 8;

    private readonly T[] coffs = [T.One / T.CreateChecked(waveFormat.Channels)];

    public readonly WaveFormat WaveFormat { get; } = waveFormat;
    public readonly int Length => reader.Length / channelCount;

    public bool Read(out ReadOnlySpan<T> result, Span<T> buffer)
    {
        if (Length < buffer.Length)
        {
            result = default;
            return false;
        }

        Span<T> readBuffer = stackalloc T[channelCount];
        buffer.Clear();

        for (int i = 0; i < buffer.Length; i++)
        {
            reader.Read(out var readed, readBuffer);
            for (int j = 0; j < channelCount; j++)
                buffer[i] += readed[i] * coffs[j];
        }

        result = buffer;
        return true;
    }

    public bool Advance(int count)
    {
        return reader.Advance(count * channelCount);
    }
}

public readonly struct MonoSelectSampleReader<T>(IWaveReader<T> reader, WaveFormat waveFormat, int index)
    : IWaveReader<T>
    where T : unmanaged, INumber<T>
{

    private readonly int channelCount = waveFormat.Channels;
    private readonly int bytesPerSample = waveFormat.BitsPerSample / 8;
    public readonly WaveFormat WaveFormat { get; } = waveFormat;
    public readonly int Length => reader.Length / channelCount;
    // private readonly T[] coffs = [T.One / T.CreateChecked(waveFormat.Channels)];
    public bool Read(out ReadOnlySpan<T> result, Span<T> buffer)
    {
        if (Length < buffer.Length)
        {
            result = default;
            return false;
        }

        Span<T> readBuffer = stackalloc T[1];
        buffer.Clear();

        for (int i = 0; i < buffer.Length; i++)
        {
            reader.Advance(index);
            reader.Read(out var readed, readBuffer);
            buffer[i] = readed[0];
            reader.Advance(channelCount - index - 1);
        }
        result = buffer;
        return true;
    }

    public bool Advance(int count)
    {
        return reader.Advance(count * channelCount);
    }
}

public static class WaveReaderExtension
{
    public static IWaveReader<T> ToMonoMix<T>(this IWaveReader<T> reader, WaveFormat waveFormat)
        where T : unmanaged, INumber<T>
    {
        if (waveFormat.Channels == 1)
            return reader;
        return new MonoMixSampleReader<T>(reader, waveFormat);
    }

    public static IWaveReader<T> ToMonoSelect<T>(this IWaveReader<T> reader, WaveFormat waveFormat, int index = 0)
        where T : unmanaged, INumber<T>
    {
        if (waveFormat.Channels == 1)
            return reader;
        return new MonoSelectSampleReader<T>(reader, waveFormat, index);
    }

    public static IWaveReader<T> ToSamples<T>(this IWaveReader<byte> reader, WaveFormat waveFormat)
        where T : unmanaged, INumber<T>
    {
        return waveFormat.Encoding switch
        {
            WaveFormatEncoding.Pcm => waveFormat.BitsPerSample switch
            {
                8 => new PCM8WaveToSampleReader<T>(reader, waveFormat),
                16 => new PCM16WaveToSampleProvider<T>(reader, waveFormat),
                24 => new PCM24WaveToSampleProvider<T>(reader, waveFormat),
                _ => throw new NotSupportedException()
            },
            WaveFormatEncoding.IeeeFloat => throw new NotSupportedException(),
            _ => throw new NotSupportedException()
        };
    }
}

public static class ReadOnlySequnceExtension
{
    public static bool TryReadOrCopy<T>(
        this scoped ref ReadOnlySequence<T> seq, out ReadOnlySpan<T> result, Span<T> buffer
    )
    {
        var length = buffer.Length;
        if (seq.Length < length)
        {
            result = default;
            return false;
        }
        else
        {
            if (seq.FirstSpan.Length >= length)
                result = seq.FirstSpan[..length];
            else
            {
                seq.CopyTo(buffer);
                result = buffer;
            }
            seq = seq.Slice(length);
            return true;
        }
    }
    // public static bool TryPeakOrCopy<T>(this SequenceReader<T> reader, out ReadOnlySpan<T> buffer, Span<T>
    // copyBuffer)
    //     where T : unmanaged, IEquatable<T>
    // {
    //     int length = copyBuffer.Length;
    //     if (reader.UnreadSpan.Length <= length)
    //     {
    //         buffer = reader.UnreadSpan[..length];
    //         reader.Advance(length);
    //         return false;
    //     }
    //     else
    //     {
    //         var success = reader.TryCopyTo(copyBuffer);
    //         buffer = success ? copyBuffer : default;
    //         return success;
    //     }
    // }
}
// public readonly struct SampleWaveProvider<> : IWaveProvider

// public interface IWaveReader
// {
//     void Read(Span<byte> data);
// }

// public interface ISampleReader
// {
//     void Read(Span<float> data);
// }

// public readonly struct PCM8WaveConverter
// (IWaveReader waveReader)
// {
//     // private readonly WaveFormat waveFormat;
//     private readonly byte[] buffer = new byte[256];
//     public readonly void Read<T>(Span<T> data)
//         where T : unmanaged,
//                   IBinaryInteger<T>
//     {
//         for (int i = 0; i < data.Length; i += 256)
//         {
//             var length = Math.Max(256, data.Length - i);
//             waveReader.Read(buffer.AsSpan(0, length));
//             for (int j = 0; j < length; j++)
//             {
//                 data[i + j] = T.CreateChecked(buffer[j] / 32768f);
//             }
//         }
//     }
// }

// public readonly struct PCM16WaveConverter
// (IWaveReader waveReader)
// {
//     // private readonly WaveFormat waveFormat;
//     private readonly byte[] buffer = new byte[512];
//     public readonly void Read<T>(Span<T> data)
//         where T : unmanaged,
//                   IBinaryInteger<T>
//     {
//         for (int i = 0; i < data.Length; i += 256)
//         {
//             var length = Math.Max(256, data.Length - i);
//             waveReader.Read(buffer.AsSpan(0, length * 2));
//             for (int j = 0; j < length; j++)
//             {
//                 data[i + j] = T.CreateChecked(BitConverter.ToInt16(buffer.AsSpan(j * 2, 2)) / 32768f);
//             }
//         }
//     }
// }