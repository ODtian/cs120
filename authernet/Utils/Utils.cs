using System.IO.Pipelines;
using System.Numerics;

namespace CS120.Utils;

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
        Enable(false);
        Source.Dispose();
    }
}

public static class BinaryIntegerTrait<T>
    where T : IBinaryInteger<T>
{
    public static readonly int Size = T.Zero.GetByteCount();
}

public interface IPipeReader<T>
{
    PipeReader SourceReader { get; }

    bool TryReadTo(Span<T> dst, bool advandce = true);
}

public interface IPipeWriter<T>
{
    PipeWriter SourceWriter { get; }

    void Write(ReadOnlySpan<T> src);

    ValueTask<FlushResult> FlushAsync(CancellationToken ct)
    {
        return SourceWriter.FlushAsync(ct);
    }
}

public interface IPipeAdvance
{
    PipeReader SourceReader { get; }
    bool TryAdvance();
}

// public interface IExtendable<T>
// {
//     void Extend(ReadOnlySpan<T> other);
// }

// public interface IPipeReaderBuilder<out T>
// {
//     T Build(WaveFormat waveFormat, PipeReader sampleBuffer);
// }

// public interface IPipeWriterBuilder<out T>
// {
//     T Build(WaveFormat waveFormat, PipeWriter sampleBuffer);
// }

// public class BufferWriter
// (PipeWriter pipeWriter) : IPipeWriter<byte>
// {
//     public PipeWriter SourceWriter { get; } = pipeWriter;

//     public void Write(ReadOnlySpan<byte> dataBuffer)
//     {
//         SourceWriter.Write(dataBuffer);
//     }
// }