using System.IO.Pipelines;
using CS120.Symbol;
using CS120.Utils;
using CS120.Utils.Extension;
using CS120.Utils.Wave;
using NAudio.Wave;

namespace CS120.Preamble;
public interface IPreamble
{
    ReadOnlyMemory<float> Samples { get; }
    // static abstract IPreamble Create(WaveFormat waveFormat);
}

public readonly struct ChirpPreamble
(ChirpSymbol symbols) : IPreamble
{
    public ReadOnlyMemory<float> Samples {
        get;
    } = new([..symbols.Samples.Span[0].Span, ..symbols.Samples.Span[1].Span]);
}

// public readonly struct PreambleDetection
// {
//     private readonly float smoothedEnergyFactor;
//     private readonly float corrThreshold;
//     private readonly int maxPeakFalling;

//     private readonly IPreamble preamble;
//     public readonly int PreambleLength { get; }

//     public PreambleDetection(
//         IPreamble preamble, float corrThreshold = 0.05f, float smoothedEnergyFactor = 1f / 64f, int maxPeakFalling =
//         220
//     )
//     {
//         this.corrThreshold = corrThreshold;
//         this.smoothedEnergyFactor = smoothedEnergyFactor;
//         this.maxPeakFalling = maxPeakFalling;

//         this.preamble = preamble;
//         PreambleLength = preamble.Samples.Length;

//         Debug.Assert(PreambleLength >= maxPeakFalling);
//         Console.WriteLine("PreambleLength: " + PreambleLength);
//     }

//     public readonly void DetectPreamble(BlockingCollection<float> sampleBuffer, CancellationToken ct)
//     {
//         var factor = smoothedEnergyFactor;
//         var localMaxCorr = 0f;

//         var smoothedEnergy = sampleBuffer.TakeBlocked(PreambleLength - 1)
//                                  .Aggregate(0f, (acc, sample) => factor * sample * sample + (1 - factor) * acc);

//         var restNum = -1;

//         ReadOnlySpan<float> preambleSpan = preamble.Samples.AsSpan();

//         // var readBuffer = new float[preambleLength];
//         var buffer = new float[PreambleLength * 2 - 1];
//         // var bufferEnd = 0;
//         // sampleBuffer.TakeInto(buffer.AsSpan(0, buffer.Length - 1));
//         var bufferSpan = buffer.AsSpan();
//         while (restNum != 0)
//         {
//             ct.ThrowIfCancellationRequested();
//             sampleBuffer.TakeBlocked(PreambleLength * 2 - 1).TakeInto(bufferSpan);
//             for (int i = 0; i < PreambleLength && restNum != 0; i++)
//             {
//                 var corr = 0f;
//                 for (int j = 0; j < PreambleLength; j++)
//                 {
//                     corr += bufferSpan[i + j] * preambleSpan[j];
//                 }
//                 var sample = bufferSpan[i + PreambleLength - 1];
//                 smoothedEnergy += factor * sample * sample + (1 - factor) * smoothedEnergy;

//                 corr /= PreambleLength;

//                 sampleBuffer.Take();
//                 // Console.Write($"corr {corr}");
//                 // var x = corr * corr / smoothedEnergy;
//                 if (corr > corrThreshold && corr > localMaxCorr)
//                 {
//                     // Console.WriteLine("Detected");
//                     // Console.WriteLine($"corr {corr}");
//                     localMaxCorr = corr;
//                     restNum = maxPeakFalling;
//                 }
//                 else if (restNum > 0)
//                 {
//                     restNum--;
//                 }
//             }
//         }

//         for (int i = 0; i < PreambleLength - maxPeakFalling; i++)
//         {
//             sampleBuffer.Take();
//         }

//         // Console.WriteLine("End Detect");
//     }
// }
public class PreambleDetection : IPipeAdvance
{
    private readonly float corrThreshold;
    private readonly float smoothedEnergyFactor;
    private readonly int maxPeakFalling;
    private readonly IPreamble preamble;
    private readonly PipeViewProvider sampleBuffer;
    private readonly int preambleLength;
    private readonly float[] buffer;

    private int restNum = -1;
    private float localMaxCorr = 0f;

    public PipeReader SourceReader { get; }
    public PreambleDetection(
        PipeReader pipeReader,
        WaveFormat waveFormat,
        IPreamble preamble,
        float corrThreshold = 0.05f,
        float smoothedEnergyFactor = 1f / 64f,
        int maxPeakFalling = 220
    )
    {
        this.corrThreshold = corrThreshold;
        this.smoothedEnergyFactor = smoothedEnergyFactor;
        this.maxPeakFalling = maxPeakFalling;

        this.preamble = preamble;
        sampleBuffer = new PipeViewProvider(waveFormat, pipeReader);

        SourceReader = pipeReader;

        preambleLength = preamble.Samples.Length;

        buffer = new float[preambleLength * 2 - 1];

        // Debug.Assert(preamble.Samples.Length >= maxPeakFalling);
        if (preambleLength < maxPeakFalling)
        {
            throw new ArgumentException("maxPeakFalling must be less than preamble length");
        }
    }

    // public PreambleDetection Build(WaveFormat waveFormat, PipeReader reader)
    // {
    //     if (sampleBuffer is not null)
    //     {
    //         throw new InvalidOperationException("Already built");
    //     }

    //     sampleBuffer = new PipeViewProvider(waveFormat, reader);
    //     return this;
    // }
    private void Reset()
    {
        restNum = -1;
        localMaxCorr = 0f;
    }

    public bool TryAdvance()
    {
        var bufferSpan = buffer.AsSpan();
        ReadOnlySpan<float> preambleSpan = preamble.Samples.Span;

        if (sampleBuffer.ReadExact(buffer, 0, buffer.Length) == 0)
        {
            Reset();
            return false;
        }

        var numEvaluated = 0;
        while (numEvaluated < preambleLength && restNum != 0)
        {
            var corr = 0f;
            for (int j = 0; j < preambleLength; j++)
            {
                corr += bufferSpan[numEvaluated + j] * preambleSpan[j];
            }
            // var sample = bufferSpan[i + PreambleLength - 1];
            // smoothedEnergy += factor * sample * sample + (1 - factor) * smoothedEnergy;

            corr /= preambleLength;

            // Console.WriteLine($"bufferSpan[numEvaluated + j] {corr}");
            // sampleBuffer.Take();
            // var x = corr * corr / smoothedEnergy;
            if (corr > corrThreshold && corr > localMaxCorr)
            {
                // Debug.Assert(maxPeakFalling == 0);
                // Console.WriteLine($"corr {corr}");
                localMaxCorr = corr;
                restNum = maxPeakFalling;
            }
            else if (restNum > 0)
            {
                restNum--;
            }
            numEvaluated++;
        }

        sampleBuffer.AdvanceSamples(numEvaluated - 1);

        if (restNum == 0)
        {
            // if (SourceReader.TryRead(out var readResult)) {
            //     // Console.WriteLine(SourceReader.);
            //     SourceReader.AdvanceTo(readResult.Buffer.Start);
            // }
            sampleBuffer.AdvanceSamples(preambleLength - maxPeakFalling);
            Reset();
            return false;
        }

        return true;
    }
    // public void DetectPreamble(CancellationToken ct)
    // {
    //     if (sampleBuffer is null)
    //     {
    //         throw new InvalidOperationException("Not built yet");
    //     }

    //     // var factor = smoothedEnergyFactor;
    //     var localMaxCorr = 0f;

    //     var restNum = -1;

    //     var buffer = new float[preambleLength * 2 - 1];
    //     // var sampleView = new PipeViewProvider(waveFormat, sampleBuffer);
    //     // sampleView.ReadExact(buffer, 0, PreambleLength);

    //     // var smoothedEnergy = buffer[..(PreambleLength - 1)].Aggregate(
    //     //     0f, (acc, sample) => factor * sample * sample + (1 - factor) * acc
    //     // );

    //     var bufferSpan = buffer.AsSpan();
    //     ReadOnlySpan<float> preambleSpan = preamble.Samples.AsSpan();

    //     while (restNum != 0)
    //     {
    //         ct.ThrowIfCancellationRequested();

    //         if (sampleBuffer.ReadExact(buffer, 0, buffer.Length) == 0)
    //         {
    //             return;
    //         }

    //         var numEvaluated = 0;
    //         while (numEvaluated < preambleLength && restNum != 0)
    //         {
    //             var corr = 0f;
    //             for (int j = 0; j < preambleLength; j++)
    //             {
    //                 corr += bufferSpan[numEvaluated + j] * preambleSpan[j];
    //             }
    //             // var sample = bufferSpan[i + PreambleLength - 1];
    //             // smoothedEnergy += factor * sample * sample + (1 - factor) * smoothedEnergy;

    //             corr /= preambleLength;

    //             // Console.WriteLine($"bufferSpan[numEvaluated + j] {corr}");
    //             // sampleBuffer.Take();
    //             // var x = corr * corr / smoothedEnergy;
    //             if (corr > corrThreshold && corr > localMaxCorr)
    //             {
    //                 // Console.WriteLine($"corr {corr}");
    //                 // Debug.Assert(maxPeakFalling == 0);
    //                 localMaxCorr = corr;
    //                 restNum = maxPeakFalling;
    //             }
    //             else if (restNum > 0)
    //             {
    //                 restNum--;
    //             }
    //             numEvaluated++;
    //         }
    //         sampleBuffer.AdvanceSamples(numEvaluated - 1);
    //     }
    //     sampleBuffer.AdvanceSamples(preambleLength - maxPeakFalling);
    // }
}