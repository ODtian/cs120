using System.Buffers;
using System.IO.Pipelines;
using System.Numerics;
using CS120.Modulate;
using CS120.Symbol;
using CS120.Utils;
using CS120.Utils.Extension;
using CS120.Utils.Wave;
using DotNext;
using NAudio.Wave;

namespace CS120.Preamble;
public interface IPreamble<TSample>
    where TSample : INumber<TSample> {
    ReadOnlyMemory<TSample> Samples { get; }
    // static abstract IPreamble Create(WaveFormat waveFormat);
}

public class ChirpPreamble<TSample>(ChirpSymbol2<TSample> symbols) : IPreamble<TSample>
    where TSample : INumber<TSample>

{
    public ReadOnlyMemory<TSample> Samples { get; } = new([
            ..symbols.Samples.Span[0]
            .Span,
            ..symbols.Samples.Span[1]
            .Span,
            // TSample.One,
            // TSample.One,
            // TSample.One,
            // TSample.One,
            // TSample.One,
            // TSample.One,
            // TSample.One,
            // TSample.One
        // TSample.CreateChecked(0.9),
        // TSample.CreateChecked(0.8),
        // TSample.CreateChecked(0.7),
        // TSample.CreateChecked(0.6),
        // TSample.CreateChecked(0.5),
        // TSample.CreateChecked(0.4),
        // TSample.CreateChecked(0.3),
        // TSample.CreateChecked(0.2),
        // TSample.CreateChecked(0.1)
    ]);
}

public class WarmupPreamble<TSymbol, TSample> : IPreamble<TSample>
    where TSymbol : ISymbol<TSample>
    where TSample : INumber<TSample>
{
    public ReadOnlyMemory<TSample> Samples { get; }
    public WarmupPreamble(TSymbol symbols, int repeat)
    {
        var symbolLength = symbols.NumSamplesPerSymbol;
        var combinedSamples = new TSample[2 * symbolLength * repeat];

        for (int i = 0; i < repeat; i++)
        {
            symbols.Samples.Span[0].Span.CopyTo(combinedSamples.AsSpan(2 * i * symbolLength));
            symbols.Samples.Span[1].Span.CopyTo(combinedSamples.AsSpan((2 * i + 1) * symbolLength));
        }

        // 将结果存储在 Samples 属性中
        Samples = new(combinedSamples);
    }
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
public interface ISequnceSearcher<TIn>
{
    bool TrySearch(ref ReadOnlySequence<TIn> inSeq);
}
public class PreambleDetection<TSample>(
    IPreamble<TSample> preamble,
    float corrThreshold = 0.05f,
    float smoothedEnergyFactor = 1f / 64f,
    int maxPeakFalling = 220
)
    : ISequnceSearcher<TSample>
    where TSample : INumber<TSample>
{
    // private int restNum = -1;
    // private int
    private readonly int preambleLength = preamble.Samples.Length;
    private readonly TSample preambleLengthT = TSample.CreateChecked(preamble.Samples.Length);
    private readonly TSample corrThresholdT = TSample.CreateChecked(corrThreshold);
    // private readonly TSample smoothedEnergyFactorT = TSample.CreateChecked(smoothedEnergyFactor);
    public bool TrySearch(ref ReadOnlySequence<TSample> inSeq)
    {
        // if (restNum == -1 && inSeq.Length < maxPeakFalling + preambleLength)
        //     return false;
        if (inSeq.Length < preambleLength * 2 - 1)
            return false;

        var buffer = ArrayPool<TSample>.Shared.Rent((int)inSeq.Length);
        var bufferSpan = buffer.AsSpan(0, (int)inSeq.Length);
        inSeq.CopyTo(bufferSpan);
        var preambleSpan = preamble.Samples.Span;

        var restNum = -1;
        var localMaxCorr = TSample.Zero;
        var found = false;
        var examed = 0;
        // while (examed < bufferSpan.Length - preambleLength + 1 && restNum != 0)
        while (true)
        {
            var corr = TSample.Zero;
            for (int j = 0; j < preambleLength; j++)
            {
                corr += bufferSpan[examed + j] * preambleSpan[j];
            }
            corr /= preambleLengthT;

            if (corr > corrThresholdT && corr > localMaxCorr && corr < TSample.CreateChecked(1.0))
            {
                // Console.WriteLine("Detected");
                // Console.WriteLine($"corr {corr}");
                localMaxCorr = corr;
                restNum = maxPeakFalling;

                if (examed + maxPeakFalling + preambleLength > bufferSpan.Length)
                {
                    break;

                    // inSeq = inSeq.Slice(i);
                    // ArrayPool<TSample>.Shared.Return(buffer);
                    // return false;
                    // break;
                }
            }
            else if (restNum > 0)
            {
                restNum--;
            }

            if (!(restNum != 0 && (examed + 1) < bufferSpan.Length - preambleLength + 1))
                break;

            examed++;
        }

        if (restNum == 0)
            inSeq = inSeq.Slice(examed + preambleLength - maxPeakFalling);
        // found = true;
        // ArrayPool<TSample>.Shared.Return(buffer);
        // return true;
        else if (restNum > 0)

            // if (!found)
            // inSeq = inSeq.Slice(inSeq.End);
            inSeq = inSeq.Slice(examed);
        else
            inSeq = inSeq.Slice(bufferSpan.Length - preambleLength + 1);

        ArrayPool<TSample>.Shared.Return(buffer);

        return restNum == 0;
    }
    public bool TrySearch1(ref ReadOnlySequence<TSample> inSeq)
    {
        if (inSeq.Length < preambleLength * 2 - 1)
            return false;

        var buffer = ArrayPool<TSample>.Shared.Rent(preambleLength * 2 - 1);
        ReadOnlySpan<TSample> preambleSpan = preamble.Samples.Span;

        var restNum = -1;
        var localMaxCorr = TSample.Zero;
        var found = false;

        while (!found && inSeq.Length >= buffer.Length)
        {

            var bufferSpan = inSeq.GetSpanExact(buffer);
            var numEvaluated = 0;
            while (numEvaluated < preambleLength && restNum != 0)
            {
                var corr = TSample.Zero;
                for (int j = 0; j < preambleLength; j++)
                {
                    corr += bufferSpan[numEvaluated + j] * preambleSpan[j];
                }
                // var sample = bufferSpan[i + preambleLength - 1];
                // smoothedEnergy += factor * sample * sample + (1 - factor) * smoothedEnergy;

                corr /= preambleLengthT;

                if (corr > corrThresholdT && corr > localMaxCorr)
                {
                    // Console.WriteLine("Detected");
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
            // Console.WriteLine($"numEvaluated {numEvaluated - 1} {inSeq.Length} {buffer.Length}");
            inSeq = inSeq.Slice(numEvaluated);

            found = restNum == 0;
        }

        ArrayPool<TSample>.Shared.Return(buffer);

        if (found)
            inSeq = inSeq.Slice(preambleLength - maxPeakFalling);

        return found;
    }
}
// public class PreambleDetection : IPipeAdvance
// {
//     private readonly float corrThreshold;
//     private readonly float smoothedEnergyFactor;
//     private readonly int maxPeakFalling;
//     private readonly IPreamble preamble;
//     private readonly PipeViewProvider sampleBuffer;
//     private readonly int preambleLength;
//     private readonly float[] buffer;

//     private int restNum = -1;
//     private float localMaxCorr = 0f;

//     public PipeReader SourceReader { get; }
//     public PreambleDetection(
//         PipeReader pipeReader,
//         WaveFormat waveFormat,
//         IPreamble preamble,
//         float corrThreshold = 0.05f,
//         float smoothedEnergyFactor = 1f / 64f,
//         int maxPeakFalling = 220
//     )
//     {
//         this.corrThreshold = corrThreshold;
//         this.smoothedEnergyFactor = smoothedEnergyFactor;
//         this.maxPeakFalling = maxPeakFalling;

//         this.preamble = preamble;
//         sampleBuffer = new PipeViewProvider(waveFormat, pipeReader);

//         SourceReader = pipeReader;

//         preambleLength = preamble.Samples.Length;

//         buffer = new float[preambleLength * 2 - 1];

//         // Debug.Assert(preamble.Samples.Length >= maxPeakFalling);
//         if (preambleLength < maxPeakFalling)
//         {
//             throw new ArgumentException("maxPeakFalling must be less than preamble length");
//         }
//     }

//     // public PreambleDetection Build(WaveFormat waveFormat, PipeReader reader)
//     // {
//     //     if (sampleBuffer is not null)
//     //     {
//     //         throw new InvalidOperationException("Already built");
//     //     }

//     //     sampleBuffer = new PipeViewProvider(waveFormat, reader);
//     //     return this;
//     // }
//     private void Reset()
//     {
//         restNum = -1;
//         localMaxCorr = 0f;
//     }

//     public bool TryAdvance()
//     {
//         var bufferSpan = buffer.AsSpan();
//         ReadOnlySpan<float> preambleSpan = preamble.Samples.Span;

//         if (sampleBuffer.ReadExact(buffer, 0, buffer.Length) == 0)
//         {
//             Reset();
//             return false;
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
//                 // Debug.Assert(maxPeakFalling == 0);
//                 Console.WriteLine($"corr {corr}");
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

//         if (restNum == 0)
//         {
//             // if (SourceReader.TryRead(out var readResult)) {
//             //     // Console.WriteLine(SourceReader.);
//             //     SourceReader.AdvanceTo(readResult.Buffer.Start);
//             // }
//             sampleBuffer.AdvanceSamples(preambleLength - maxPeakFalling);
//             Reset();
//             return false;
//         }

//         return true;
//     }
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
// }