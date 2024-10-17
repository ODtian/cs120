using System.Collections.Concurrent;
using System.Diagnostics;
using CS120.Extension;
using CS120.Symbol;
using NAudio.Wave;

namespace CS120.Preamble;
public interface IPreamble
{
    float[] Samples { get; }
    // static abstract IPreamble Create(WaveFormat waveFormat);
}

public readonly struct ChirpPreamble
(ChirpSymbol symbols) : IPreamble
{
    public float[] Samples { get; } = [..symbols.Samples[0], ..symbols.Samples[1]];
}

public readonly struct PreambleDetection
{
    // const float smoothedEnergyFactor = 1f / 64f;
    private readonly float smoothedEnergyFactor;
    private readonly float corrThreshold;
    private readonly int maxPeakFalling;
    // static float corrThreshold = Program.corrThreshold;
    // static int maxPeakFalling = Program.maxPeakFalling;

    private readonly IPreamble preamble;
    public readonly int PreambleLength { get; }

    // private float mmax = 0f;
    // private int inx = 0;

    public PreambleDetection(
        IPreamble preamble, float corrThreshold = 0.05f, float smoothedEnergyFactor = 1f / 64f, int maxPeakFalling = 220
    )
    {
        this.corrThreshold = corrThreshold;
        this.smoothedEnergyFactor = smoothedEnergyFactor;
        this.maxPeakFalling = maxPeakFalling;

        this.preamble = preamble;
        PreambleLength = preamble.Samples.Length;

        Debug.Assert(PreambleLength >= maxPeakFalling);
        Console.WriteLine("PreambleLength: " + PreambleLength);
    }

    // public readonly void DetectPreamble(BlockingCollection<float> sampleBuffer, CancellationToken ct)
    // {
    //     var factor = smoothedEnergyFactor;
    //     var localMaxCorr = 0f;

    //     var smoothedEnergy = sampleBuffer.TakeBlocked(preambleLength - 1)
    //                              .Aggregate(0f, (acc, sample) => factor * sample * sample + (1 - factor) * acc);

    //     var restNum = -1;

    //     ReadOnlySpan<float> preambleSpan = preamble.Samples.AsSpan();

    //     var buffer = new float[preambleLength];
    //     var bufferEnd = 0;
    //     // sampleBuffer.TakeInto(buffer.AsSpan(0, buffer.Length - 1));

    //     while (restNum != 0)
    //     {
    //         ct.ThrowIfCancellationRequested();

    //         var corr = 0f;
    //         var index = 0;

    //         var currentSample = sampleBuffer.TakeBlocked(preambleLength).Skip(preambleLength - 1).First();
    //         // var currentSample = sampleBuffer.AsEnumerable().Take(1).First();
    //         buffer[bufferEnd] = currentSample;
    //         bufferEnd = (bufferEnd + 1) % buffer.Length;

    //         foreach (var sample in buffer.AsSpan(bufferEnd))
    //         {
    //             corr += sample * preambleSpan[index];
    //             index++;
    //         }

    //         foreach (var sample in buffer.AsSpan(0, bufferEnd))
    //         {
    //             corr += sample * preambleSpan[index];
    //             index++;
    //         }

    //         smoothedEnergy =
    //             smoothedEnergyFactor * currentSample * currentSample + (1 - smoothedEnergyFactor) * smoothedEnergy;
    //         // for (int i = 0; i < preambleLength; i++) {
    //         //     var s = x[i];
    //         // }
    //         // foreach (var sample in x)
    //         // foreach (var sample in sampleBuffer.TakeBlocked(preambleLength))
    //         // {
    //         //     corr += sample * preambleSpan[index];
    //         //     // corr += x[index] * preambleSpan[index];
    //         //     if (index == preambleLength - 1)
    //         //     {
    //         //         smoothedEnergy =
    //         //             smoothedEnergyFactor * sample * sample + (1 - smoothedEnergyFactor) * smoothedEnergy;
    //         //     }
    //         //     ++index;
    //         // }
    //         corr /= preambleLength;

    //         sampleBuffer.Take();

    //         // var x = corr * corr / smoothedEnergy;
    //         if (corr > corrThreshold && corr > localMaxCorr)
    //         {
    //             Console.WriteLine("Detected");
    //             Console.WriteLine($"corr {corr}");
    //             localMaxCorr = corr;
    //             restNum = maxPeakFalling;
    //         }
    //         else if (restNum > 0)
    //         {
    //             restNum--;
    //         }
    //     }

    //     for (int i = 0; i < preambleLength - maxPeakFalling; i++)
    //     {
    //         sampleBuffer.Take();
    //     }

    //     Console.WriteLine("End Detect");
    // }
    public readonly void DetectPreamble(BlockingCollection<float> sampleBuffer, CancellationToken ct)
    {
        var factor = smoothedEnergyFactor;
        var localMaxCorr = 0f;

        var smoothedEnergy = sampleBuffer.TakeBlocked(PreambleLength - 1)
                                 .Aggregate(0f, (acc, sample) => factor * sample * sample + (1 - factor) * acc);

        var restNum = -1;

        ReadOnlySpan<float> preambleSpan = preamble.Samples.AsSpan();

        // var readBuffer = new float[preambleLength];
        var buffer = new float[PreambleLength * 2 - 1];
        // var bufferEnd = 0;
        // sampleBuffer.TakeInto(buffer.AsSpan(0, buffer.Length - 1));
        var bufferSpan = buffer.AsSpan();
        while (restNum != 0)
        {
            ct.ThrowIfCancellationRequested();
            // var index = 0;
            sampleBuffer.TakeBlocked(PreambleLength * 2 - 1).TakeInto(bufferSpan);
            // Console.WriteLine("TakeInto");
            // foreach (var r in sampleBuffer.TakeBlocked(preambleLength * 2))
            // {
            //     bufferSpan[index++] = r;
            // }
            // .TakeInto(buffer);
            // readBuffer.CopyTo(buffer.AsSpan(bufferEnd));
            for (int i = 0; i < PreambleLength && restNum != 0; i++)
            {
                var corr = 0f;
                for (int j = 0; j < PreambleLength; j++)
                {
                    corr += bufferSpan[i + j] * preambleSpan[j];
                }
                var sample = bufferSpan[i + PreambleLength - 1];
                smoothedEnergy += factor * sample * sample + (1 - factor) * smoothedEnergy;

                corr /= PreambleLength;

                sampleBuffer.Take();
                // Console.Write($"corr {corr}");
                // var x = corr * corr / smoothedEnergy;
                if (corr > corrThreshold && corr > localMaxCorr)
                {
                    Console.WriteLine("Detected");
                    Console.WriteLine($"corr {corr}");
                    localMaxCorr = corr;
                    restNum = maxPeakFalling;
                }
                else if (restNum > 0)
                {
                    restNum--;
                }
            }
            // var index = 0;

            // var currentSample = sampleBuffer.TakeBlocked(preambleLength).Skip(preambleLength - 1).First();
            // // var currentSample = sampleBuffer.AsEnumerable().Take(1).First();
            // buffer[bufferEnd] = currentSample;
            // bufferEnd = (bufferEnd + 1) % buffer.Length;

            // foreach (var sample in buffer.AsSpan(bufferEnd))
            // {
            //     corr += sample * preambleSpan[index];
            //     index++;
            // }

            // foreach (var sample in buffer.AsSpan(0, bufferEnd))
            // {
            //     corr += sample * preambleSpan[index];
            //     index++;
            // }

            // smoothedEnergy =
            //     smoothedEnergyFactor * currentSample * currentSample + (1 - smoothedEnergyFactor) * smoothedEnergy;
            // for (int i = 0; i < preambleLength; i++) {
            //     var s = x[i];
            // }
            // foreach (var sample in x)
            // foreach (var sample in sampleBuffer.TakeBlocked(preambleLength))
            // {
            //     corr += sample * preambleSpan[index];
            //     // corr += x[index] * preambleSpan[index];
            //     if (index == preambleLength - 1)
            //     {
            //         smoothedEnergy =
            //             smoothedEnergyFactor * sample * sample + (1 - smoothedEnergyFactor) * smoothedEnergy;
            //     }
            //     ++index;
            // }
            // corr /= preambleLength;

            // sampleBuffer.Take();

            // // var x = corr * corr / smoothedEnergy;
            // if (corr > corrThreshold && corr > localMaxCorr)
            // {
            //     Console.WriteLine("Detected");
            //     Console.WriteLine($"corr {corr}");
            //     localMaxCorr = corr;
            //     restNum = maxPeakFalling;
            // }
            // else if (restNum > 0)
            // {
            //     restNum--;
            // }
        }

        for (int i = 0; i < PreambleLength - maxPeakFalling; i++)
        {
            sampleBuffer.Take();
        }

        Console.WriteLine("End Detect");
    }
}

// public struct _PreambleDetection<TPreamble>
//     where TPreamble : IPreamble<TPreamble>
// {
//     const float smoothedEnergyFactor = 1 / 64;
//     const float corrThreshold = 0.1f;

//     private readonly TPreamble preamble;
//     private readonly int preambleLength;
//     private readonly float[] window;
//     private readonly int windowLength;

//     private int startIndex;
//     private float smoothedEnergy = 0f;
//     private float localMaxCorr = 0f;
//     private int localMaxCorrIndex = 0;

//     public PreambleDetection(TPreamble preamble)
//     {
//         this.preamble = preamble;
//         preambleLength = preamble.PreambleData.Length;

//         window = new float[preambleLength * 2];
//         windowLength = window.Length;

//         startIndex = preambleLength - 1;
//     }

//     public bool DetectPreamble(float nextSample)
//     {
//         var windowSpan = window.AsSpan();
//         var preambleSpan = preamble.PreambleData.AsSpan();

//         int sampleIndex = (startIndex + preambleLength) % windowLength;
//         windowSpan[sampleIndex] = nextSample;

//         startIndex = (startIndex + 1) % windowLength;

//         var energy = nextSample * nextSample;
//         smoothedEnergy = smoothedEnergyFactor * energy + (1 - smoothedEnergyFactor) * smoothedEnergy;

//         var corr = 0f;
//         for (int i = 0; i < preambleLength; i++)
//         {
//             corr += windowSpan[(i + startIndex) % windowLength] * preambleSpan[i];
//         }
//         corr /= preambleLength;

//         // int restLength = (sampleIndex - localMaxCorrIndex + windowLength) % windowLength;

//         if (corr > localMaxCorr && corr > smoothedEnergy * 2 && corr > corrThreshold)
//         {
//             localMaxCorr = corr;
//             localMaxCorrIndex = sampleIndex;
//         }
//         else if (startIndex == localMaxCorrIndex && smoothedEnergy != 0f)
//         {
//             // var index = localMaxCorrIndex;
//             // localMaxCorr = 0;
//             // localMaxCorrIndex = 0;
//             // smoothedEnergy = 0f;
//             return true;
//         }

//         var b = new BlockingCollection<float>();

//         foreach (var sample in b.AsEnumerable().)
//         {
//             b.Enqueue(sample);
//         }

//         return false;
//     }
//     public readonly void EnqueueRest(Queue<float> buffer)
//     {
//         if (localMaxCorrIndex + preambleLength < windowLength)
//         {
//             foreach (var sample in window.AsSpan(localMaxCorrIndex, preambleLength))
//             {
//                 buffer.Enqueue(sample);
//             }
//             // foreach (var sample in window[localMaxCorrIndex..(localMaxCorrIndex + preambleLength)])
//             // {
//             //     // buffer.Enqueue(sample);
//             //     yield return sample;
//             // }
//         }
//         else
//         {
//             // foreach (var sample in window[localMaxCorrIndex..])
//             // {
//             //     yield return sample;
//             // }
//             // foreach (var sample in window[..(preambleLength - (windowLength - localMaxCorrIndex))])
//             // {
//             //     yield return sample;
//             // }
//             foreach (var sample in window.AsSpan(localMaxCorrIndex))
//             {
//                 buffer.Enqueue(sample);
//             }
//             foreach (var sample in window.AsSpan(0, preambleLength - (windowLength - localMaxCorrIndex)))
//             {
//                 buffer.Enqueue(sample);
//             }
//         }
//     }
// }