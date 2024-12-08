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

public readonly struct PCMWaveToSampleProvider<TWave, TSample>(PipeReader reader, WaveFormat waveFormat)
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

    public record struct PlayTask
    (ReadOnlySequence<float> Data, TaskCompletionSource<bool> Task);

    public class NotifySampleProvider
    (ChannelReader<PlayTask> channelReader, WaveFormat waveFormat) : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = waveFormat;

        private PlayTask currentTask = default;

        public int Read(float[] buffer, int offset, int count)
        {
            try
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
                return readed;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return 0;
            }
        }
    }

    public interface IInChannel<T>
    {
        ValueTask<T?> ReadAsync(CancellationToken token = default);
        // ValueTask CompleteAsync(Exception? e = null);
        bool IsCompleted { get; }
    }

    public interface IOutChannel<T>
    {
        ValueTask WriteAsync(T data, CancellationToken token = default);
        ValueTask CompleteAsync(Exception? exception = null);
    }

    public interface IIOChannel<T> : IInChannel<T>,
                                     IOutChannel<T>
    {
    }

    public class AudioPipeOutChannel
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

        public async ValueTask CompleteAsync(Exception? exception = null)
        {
            await Writer.CompleteAsync(exception);
        }

        public async ValueTask DisposeAsync()
        {
            await CompleteAsync();
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

        public ValueTask CompleteAsync(Exception? exception = null)
        {
            channel.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return CompleteAsync();
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

    public interface IInStream<T> : IInChannel<ReadResult<T>>
    {
        void AdvanceTo(SequencePosition position);
    }

    public readonly struct ReadResult<T>(ReadOnlySequence<T> data, bool isCompleted)
    {
        public readonly ReadOnlySequence<T> Buffer { get; } = data;
        public readonly bool IsCompleted { get; } = isCompleted;
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
        public bool IsCompleted
        {
            get {
                return Reader.IsFinished();
            }
        }
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
        public bool IsCompleted => Reader.IsFinished();
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

            // for (int i = 0; i < args.SamplesPerBuffer; i++)
            // {
            //     Console.Write(z[i]);
            // }
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