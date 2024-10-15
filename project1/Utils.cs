using System.Collections.Concurrent;
using System.Windows.Forms;
using CS120.Extension;
using NAudio.Wave;

namespace CS120.Utils;

public class BlockingCollectionSampleProvider
(WaveFormat waveFormat, BlockingCollection<float> sampleBuffer) : ISampleProvider
{
    public WaveFormat WaveFormat { get; } = waveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        if (sampleBuffer.IsCompleted)
        {
            return 0;
        }

        if (sampleBuffer.Count == 0)
        {
            buffer.AsSpan(offset, count).Clear();
            return count;
        }

        if (sampleBuffer.IsAddingCompleted)
        {

            count = Math.Min(count, sampleBuffer.Count);
        }

        sampleBuffer.GetConsumingEnumerable().TakeInto(buffer.AsSpan(offset, count));
        return count;
    }
}

public class CancelKeyPressCancellationTokenSource : IDisposable
{
    public CancellationTokenSource Source { get; }
    private readonly ConsoleCancelEventHandler cancelHandler;
    private bool enabled = false;

    public CancelKeyPressCancellationTokenSource(CancellationTokenSource source, bool enabled = true)
    {
        Source = source;
        cancelHandler =
            new((s, e) =>
                {
                    e.Cancel = true;
                    Source.Cancel();
                });

        Enable(enabled);
    }

    public void Enable(bool enable)
    {
        if (!enabled && enable)
        {
            Console.CancelKeyPress += cancelHandler;
        }
        else if (enabled && !enable)
        {
            Console.CancelKeyPress -= cancelHandler;
        }
        enabled = enable;
    }

    public void Dispose()
    {
        Console.CancelKeyPress -= cancelHandler;
        Source.Dispose();
    }
}