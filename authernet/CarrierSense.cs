using System.Buffers;
using System.IO.Pipelines;
using System.Numerics;
using CS120.Preamble;
using CS120.Utils;
using CS120.Utils.Wave;
using DotNext.Buffers;
using NAudio.Wave;

namespace CS120.CarrierSense;

public class CarrierSensor
(PipeReader pipeReader, WaveFormat waveFormat, int senseSamples = 220, float senseThreshold = 0.05f) : IPipeAdvance
{
    private readonly PipeViewProvider sampleBuffer = new(waveFormat, pipeReader);
    private readonly float[] buffer = new float[senseSamples];
    public PipeReader SourceReader { get; } = pipeReader;

    public bool TryAdvance()
    {
        sampleBuffer.Read(buffer, 0, buffer.Length);

        var energy = 0f;

        for (var i = 0; i < buffer.Length; i++)
        {
            energy += buffer[i] * buffer[i];
        }

        energy /= buffer.Length;

        if (energy < senseThreshold)
        {
            sampleBuffer.AdvanceSamples(buffer.Length);
            return true;
        }

        return false;
    }
}

public class CarrierQuietSensor1<TSample>(float threshold = 0.05f) : ISequnceSearcher<TSample>
    where TSample : INumber<TSample>
{
    private readonly TSample threshold = TSample.CreateChecked(threshold);
    public bool TrySearch(ref ReadOnlySequence<TSample> buffer)
    {
        var pos = buffer.Start;
        while (buffer.TryGet(ref pos, out var next))
        {
            for (var i = 0; i < next.Length; i++)
            {
                // Console.WriteLine($"CarrierQuietSensor1: Found carrier{TSample.Abs(next.Span[i])}");
                if (TSample.Abs(next.Span[i]) > threshold)
                {
                    buffer = buffer.Slice(i);
                    return true;
                }
            }
            buffer = buffer.Slice(next.Length);
        }
        return false;
    }
}

public class CarrierCollisionSensor1<TSample>(float threshold = 1.05f) : ISequnceSearcher<TSample>
    where TSample : INumber<TSample>
{
    private readonly TSample threshold = TSample.CreateChecked(threshold);
    public bool TrySearch(ref ReadOnlySequence<TSample> buffer)
    {
        foreach (var segment in buffer)
        {
            for (var i = 0; i < segment.Length; i++)
                if (TSample.Abs(segment.Span[i]) > threshold)
                    return true;
        }
        return false;
    }
}