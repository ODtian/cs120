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
    WaveFormat WaveFormat { get; }
    int Length { get; }
    ReadOnlySpan<T> Read(Span<T> buffer);
    // bool Advance(int count);
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

// public readonly struct PCM8WaveToSampleProvider<T>(PipeReader reader, WaveFormat waveFormat) : IWaveReader<T>
//     where T : unmanaged, INumber<T>
// {
//     public readonly WaveFormat WaveFormat { get; } = waveFormat;
//     public static readonly T PCM8MaxValue = T.CreateChecked(128);
//     public readonly int Length => (int)reader.GetLength();
//     public readonly ReadOnlySpan<T> Read(Span<T> buffer)
//     {
//         if (reader.TryRead(out var result))
//         {
//             var remain = buffer.Length;
//             var resultBuffer = result.Buffer;
//             // never used
//             Span<sbyte> shortBuffer = default;
//             do
//             {
//                 var currentSpan = resultBuffer.GetSpanOrCopyBitCast(remain, shortBuffer);
//                 if (currentSpan.Length == 0)
//                     break;
//                 for (int i = 0; i < currentSpan.Length; i++)
//                     buffer[buffer.Length - remain + i] = T.CreateChecked(currentSpan[i]) / PCM8MaxValue;
//                 remain -= currentSpan.Length;
//             } while (remain > 0);

//             reader.AdvanceTo(resultBuffer.Start);

//             return buffer[..(buffer.Length - remain)];
//         }

//         return default;
//     }
// }

// public readonly struct PCM16WaveToSampleProvider<T>(PipeReader reader, WaveFormat waveFormat) : IWaveReader<T>
//     where T : unmanaged, INumber<T>
// {
//     public readonly WaveFormat WaveFormat { get; } = waveFormat;
//     public static readonly T PCM16MaxValue = T.CreateChecked(32768);
//     public readonly int Length => (int)(reader.GetLength() / 2L);
//     public readonly ReadOnlySpan<T> Read(Span<T> buffer)
//     {
//         if (reader.TryRead(out var result))
//         {
//             var remain = buffer.Length;
//             var resultBuffer = result.Buffer;
//             Span<short> shortBuffer = stackalloc short[1];
//             do
//             {
//                 var currentSpan = resultBuffer.GetSpanOrCopyBitCast(remain, shortBuffer);
//                 if (currentSpan.Length == 0)
//                     break;
//                 for (int i = 0; i < currentSpan.Length; i++)
//                     buffer[buffer.Length - remain + i] = T.CreateChecked(currentSpan[i]) / PCM16MaxValue;
//                 remain -= currentSpan.Length;
//             } while (remain > 0);

//             reader.AdvanceTo(resultBuffer.Start);

//             return buffer[..(buffer.Length - remain)];
//         }

//         return default;
//     }
// }

// public readonly struct PCM32WaveToSampleProvider<T>(PipeReader reader, WaveFormat waveFormat) : IWaveReader<T>
//     where T : unmanaged, INumber<T>
// {

//     public readonly WaveFormat WaveFormat { get; } = waveFormat;
//     public static readonly T PCM32MaxValue = T.CreateChecked(2147483648);
//     public readonly int Length => (int)(reader.GetLength() / 3L);
//     public readonly ReadOnlySpan<T> Read(Span<T> buffer)
//     {
//         if (reader.TryRead(out var result))
//         {
//             // var index = buffer.Length;
//             var remain = buffer.Length;
//             var resultBuffer = result.Buffer;
//             Span<byte> shortBuffer = stackalloc byte[3];
//             do
//             {
//                 var currentSpan = resultBuffer.GetSpanOrCopyBitCast(remain, shortBuffer.Cast<byte, int>());
//                 if (currentSpan.Length == 0)
//                     break;
//                 for (int i = 0; i < currentSpan.Length; i++)
//                     buffer[buffer.Length - remain + i] = T.CreateChecked(currentSpan[i]) / PCM32MaxValue;
//                 remain -= currentSpan.Length;
//             } while (remain > 0);

//             reader.AdvanceTo(resultBuffer.Start);

//             return buffer[..(buffer.Length - remain)];
//         }

//         return default;
//     }
// }

public readonly struct PCM24WaveToSampleProvider<TSample>(PipeReader reader, WaveFormat waveFormat)
    : IWaveReader<TSample>
    where TSample : unmanaged, INumber<TSample>
{
    readonly struct Int24
    {
        private readonly byte byte1;
        private readonly byte byte2;
        private readonly byte byte3;

        private int Value => byte1 | (byte2 << 8) | (byte3 << 16) | ((byte3 & 0x80) > 0 ? 0xFF : 0x00);

        public static explicit operator int(Int24 value) => value.Value;
    }

    public readonly WaveFormat WaveFormat { get; } = waveFormat;
    public static readonly TSample PCM24MaxValue = TSample.CreateChecked(8388608);
    public readonly int Length => (int)(reader.GetLength() / 3L);
    public readonly ReadOnlySpan<TSample> Read(Span<TSample> buffer)
    {
        if (reader.TryRead(out var result))
        {
            var remain = buffer.Length;
            var resultBuffer = result.Buffer;
            Span<Int24> shortBuffer = stackalloc Int24[1];
            do
            {
                var currentSpan = resultBuffer.GetSpanOrCopyBitCast(remain, shortBuffer);
                if (currentSpan.Length == 0)
                    break;
                for (int i = 0; i < currentSpan.Length; i++)
                    buffer[buffer.Length - remain + i] = TSample.CreateChecked((int)currentSpan[i]) / PCM24MaxValue;
                remain -= currentSpan.Length;
            } while (remain > 0);

            reader.AdvanceTo(resultBuffer.Start);

            return buffer[..(buffer.Length - remain)];
        }

        return default;
    }
}

public readonly struct PCMWaveToSampleProvider<TWave, TSample>(PipeReader reader, WaveFormat waveFormat)
    : IWaveReader<TSample>
    where TWave : unmanaged, IBinaryInteger<TWave>, ISignedNumber<TWave>
    where TSample : unmanaged, INumber<TSample>
{

    public readonly WaveFormat WaveFormat { get; } = waveFormat;
    public static readonly TSample PCMMaxValue = TSample.CreateChecked(BinaryIntegerTrait<TWave>.MaxValue);
    public readonly int Length => (int)(reader.GetLength() / BinaryIntegerTrait<TWave>.Size);
    public readonly ReadOnlySpan<TSample> Read(Span<TSample> buffer)
    {
        if (reader.TryRead(out var result))
        {
            var remain = buffer.Length;
            var resultBuffer = result.Buffer;
            Span<TWave> shortBuffer = stackalloc TWave[1];
            do
            {
                var currentSpan = resultBuffer.GetSpanOrCopyBitCast(remain, shortBuffer);
                if (currentSpan.Length == 0)
                    break;
                for (int i = 0; i < currentSpan.Length; i++)
                    buffer[buffer.Length - remain + i] = TSample.CreateChecked(currentSpan[i]) / PCMMaxValue;
                remain -= currentSpan.Length;
            } while (remain > 0);

            reader.AdvanceTo(resultBuffer.Start);

            return buffer[..(buffer.Length - remain)];
        }

        return default;
    }
}

public readonly struct IEEEWaveToSampleProvider<TSample>(PipeReader reader, WaveFormat waveFormat)
    : IWaveReader<TSample>
    where TSample : unmanaged, INumber<TSample>
{

    public readonly WaveFormat WaveFormat { get; } = waveFormat;
    public readonly int Length => (int)(reader.GetLength() / 4L);
    public readonly ReadOnlySpan<TSample> Read(Span<TSample> buffer)
    {
        if (reader.TryRead(out var result))
        {
            var remain = buffer.Length;
            var resultBuffer = result.Buffer;
            Span<float> shortBuffer = stackalloc float[1];
            do
            {
                var currentSpan = resultBuffer.GetSpanOrCopyBitCast(remain, shortBuffer);
                if (currentSpan.Length == 0)
                    break;
                for (int i = 0; i < currentSpan.Length; i++)
                    buffer[buffer.Length - remain + i] = TSample.CreateChecked(currentSpan[i]);
                remain -= currentSpan.Length;
            } while (remain > 0);

            reader.AdvanceTo(resultBuffer.Start);

            return buffer[..(buffer.Length - remain)];
        }

        return default;
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

    public ReadOnlySpan<T> Read(Span<T> buffer)
    {
        Span<T> readBuffer = stackalloc T[channelCount];
        buffer.Clear();

        for (int i = 0; i < buffer.Length; i++)
        {
            var readed = reader.Read(readBuffer);

            for (int j = 0; j < channelCount; j++)
                buffer[i] += readed[i] * coffs[j];
        }

        return buffer;
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
    public ReadOnlySpan<T> Read(Span<T> buffer)
    {
        var length = Math.Min(buffer.Length, Length);
        Span<T> readBuffer = stackalloc T[channelCount];
        buffer.Clear();

        for (int i = 0; i < buffer.Length; i++)
        {
            var readed = reader.Read(readBuffer);
            buffer[i] = readed[index];
        }

        return buffer;
    }
}

public static class WaveReaderExtension
{
    public static IWaveReader<T> ToMonoMix<T>(this IWaveReader<T> reader, WaveFormat waveFormat)
        where T : unmanaged, INumber<T>
    {
        if (waveFormat.Channels == 1)
            return reader;
        return new MonoMixSampleReader<T>(
            reader, CreateCustomWaveFormat(waveFormat.SampleRate, 1, waveFormat.BitsPerSample)
        );
    }

    public static IWaveReader<T> ToMonoSelect<T>(this IWaveReader<T> reader, WaveFormat waveFormat, int index = 0)
        where T : unmanaged, INumber<T>
    {
        if (waveFormat.Channels == 1)
            return reader;
        return new MonoSelectSampleReader<T>(
            reader, CreateCustomWaveFormat(waveFormat.SampleRate, 1, waveFormat.BitsPerSample), index
        );
    }

    public static WaveFormat CreateCustomWaveFormat(int sampleRate, int channels, int bitsPerSample)
    {
        var blockAlign = bitsPerSample * channels / 8;
        return WaveFormat.CreateCustomFormat(
            WaveFormatEncoding.Unknown, sampleRate, channels, sampleRate * blockAlign, blockAlign, bitsPerSample
        );
    }
    public static IWaveReader<T> ToSamples<T>(this PipeReader reader, WaveFormat waveFormat)
        where T : unmanaged, INumber<T>
    {
        var newWaveFormat = CreateCustomWaveFormat(waveFormat.SampleRate, waveFormat.Channels, Unsafe.SizeOf<T>() * 8);
        return waveFormat.Encoding switch
        {
            WaveFormatEncoding.Pcm => waveFormat.BitsPerSample switch
            {
                8 => new PCMWaveToSampleProvider<sbyte, T>(reader, newWaveFormat),
                16 => new PCMWaveToSampleProvider<short, T>(reader, newWaveFormat),
                24 => new PCM24WaveToSampleProvider<T>(reader, newWaveFormat),
                32 => new PCMWaveToSampleProvider<int, T>(reader, newWaveFormat),
                _ => throw new NotSupportedException()
            },
            WaveFormatEncoding.IeeeFloat => new IEEEWaveToSampleProvider<T>(reader, newWaveFormat),
            _ => throw new NotSupportedException()
        };
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
    // Read as much struct in first span from byte seq as possible, if first span is not enough, copy to buffer
    public static ReadOnlySpan<T> GetSpanOrCopyBitCast<T>(
        this scoped ref ReadOnlySequence<byte> seq, int maxLength, Span<T> buffer
    )
        where T : unmanaged
    {

        var result = seq.FirstSpan.Cast<byte, T>()[..maxLength];

        if (result.Length == 0)
        {
            seq.CopyTo(buffer.Cast<T, byte>());
            result = buffer;
        }

        seq = seq.Slice(result.Length * Unsafe.SizeOf<T>());

        return result;
    }
    public static ReadOnlySpan<T> GetSpanOrCopy<T>(this scoped ref ReadOnlySequence<T> seq, Span<T> buffer)
    {
        var length = Math.Min(buffer.Length, (int)seq.Length);

        ReadOnlySpan<T> result = buffer;

        if (seq.FirstSpan.Length >= length)
            result = seq.FirstSpan[..length];
        else
            seq.CopyTo(buffer);

        seq = seq.Slice(length);

        return result;
    }
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