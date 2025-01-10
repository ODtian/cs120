using System.Buffers;
using System.IO.Pipelines;
using System.Numerics;
using CS120.Utils.Extension;
using NAudio.Wave;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace CS120.Utils.Wave.Reader;
public interface IWaveReader<T>
{
    WaveFormat WaveFormat { get; }
    int Length { get; }
    ReadOnlySpan<T> Read(Span<T> buffer);
    // bool Advance(int count);
}

public readonly struct PCM24WaveToSampleReader<TSample>(PipeReader reader, WaveFormat waveFormat) : IWaveReader<TSample>
    where TSample : unmanaged, INumber<TSample>
{
    [StructLayout(LayoutKind.Sequential)]
    readonly struct Int24
    ()
    {
        private readonly byte byte1 = 0;
        private readonly byte byte2 = 0;
        private readonly byte byte3 = 0;

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
                var currentSpan = resultBuffer.ConsumeCast(remain, shortBuffer);
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

public readonly struct PCMWaveToSampleReader<TWave, TSample>(PipeReader reader, WaveFormat waveFormat)
    : IWaveReader<TSample>
    where TWave : unmanaged, IBinaryInteger<TWave>, IMinMaxValue<TWave>
    where TSample : unmanaged, INumber<TSample>
{

    public readonly WaveFormat WaveFormat { get; } = waveFormat;
    public static readonly TSample PCMMaxValue = TSample.CreateChecked(TWave.MaxValue);
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
                var currentSpan = resultBuffer.ConsumeCast(remain, shortBuffer);
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

public readonly struct IEEEWaveToSampleReader<TSample>(PipeReader reader, WaveFormat waveFormat) : IWaveReader<TSample>
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
                var currentSpan = resultBuffer.ConsumeCast(remain, shortBuffer);
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
        var length = Math.Min(buffer.Length, Length);
        var readBuffer = ArrayPool<T>.Shared.Rent(channelCount * length);
        // Span<T> readBuffer = stackalloc T[channelCount];
        buffer.Clear();

        var readed = reader.Read(readBuffer);
        for (int i = 0; i < buffer.Length; i++)
            for (int j = 0; j < channelCount; j++)
                buffer[i] += readed[i * channelCount + j] * coffs[j];

        ArrayPool<T>.Shared.Return(readBuffer);
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
        var readBuffer = ArrayPool<T>.Shared.Rent(channelCount * length);
        // Span<T> readBuffer = stackalloc T[channelCount];
        buffer.Clear();

        var readed = reader.Read(readBuffer);

        for (int i = 0; i < buffer.Length; i++)
            buffer[i] = readed[i * channelCount + index];

        ArrayPool<T>.Shared.Return(readBuffer);

        return buffer;
    }
}

public static class WaveReaderExtension
{
    public static IWaveReader<T> ToMonoMix<T>(this IWaveReader<T> reader)
        where T : unmanaged, INumber<T>
    {
        if (reader.WaveFormat.Channels == 1)
            return reader;
        return new MonoMixSampleReader<T>(
            reader, CreateCustomWaveFormat(reader.WaveFormat.SampleRate, 1, reader.WaveFormat.BitsPerSample)
        );
    }

    public static IWaveReader<T> ToMonoSelect<T>(this IWaveReader<T> reader, int index = 0)
        where T : unmanaged, INumber<T>
    {
        if (reader.WaveFormat.Channels == 1)
            return reader;
        return new MonoSelectSampleReader<T>(
            reader, CreateCustomWaveFormat(reader.WaveFormat.SampleRate, 1, reader.WaveFormat.BitsPerSample), index
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
                8 => new PCMWaveToSampleReader<sbyte, T>(reader, newWaveFormat),
                16 => new PCMWaveToSampleReader<short, T>(reader, newWaveFormat),
                24 => new PCM24WaveToSampleReader<T>(reader, newWaveFormat),
                32 => new PCMWaveToSampleReader<int, T>(reader, newWaveFormat),
                _ => throw new NotSupportedException()
            },
            WaveFormatEncoding.IeeeFloat => new IEEEWaveToSampleReader<T>(reader, newWaveFormat),
            _ => throw new NotSupportedException()
        };
        }
    }