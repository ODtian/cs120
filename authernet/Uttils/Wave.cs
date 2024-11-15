using System.Buffers;
using System.IO.Pipelines;
using CS120.Utils.Extension;
using NAudio.Wave;

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
                result.Buffer.GetPosition(Math.Min(result.Buffer.Length, numSamples * WaveFormat.BitsPerSample / 8 * WaveFormat.Channels))
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