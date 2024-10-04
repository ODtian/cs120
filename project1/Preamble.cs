using System.Collections.Concurrent;
using System.Diagnostics;
using CS120.Extension;
using CS120.Symbol;
using NAudio.Wave;

namespace CS120.Preamble;
public interface IPreamble
{
    float[] PreambleData { get; }
    static abstract IPreamble Create(WaveFormat waveFormat);
}

public readonly struct ChirpPreamble : IPreamble
{
    public float[] PreambleData { get; init; }

    public static IPreamble Create(WaveFormat waveFormat)
    {
        var option = new ChirpSymbolOption {
            NumSymbols = 2,
            NumSamplesPerSymbol = 220, // Read config or something
            SampleRate = waveFormat.SampleRate,
            FreqA = 3_000, // Read config or something
            FreqB = 6_000  // Read config or something
        };

        var symbols = ChirpSymbol.Get(option);

        return new ChirpPreamble { PreambleData = [..symbols[0], ..symbols[1]] };
    }
}

public struct PreambleDetection
{
    const float smoothedEnergyFactor = 1f / 64f;
    const float corrThreshold = 0.05f;
    const int maxPeakFalling = 220;

    private readonly IPreamble preamble;
    private readonly int preambleLength;
    // private float mmax = 0f;
    // private int inx = 0;

    public PreambleDetection(IPreamble preamble)
    {
        this.preamble = preamble;
        preambleLength = preamble.PreambleData.Length;

        Debug.Assert(preambleLength >= maxPeakFalling);
    }
    public void DetectPreamble(BlockingCollection<float> sampleBuffer)
    {
        var localMaxCorr = 0f;
        var smoothedEnergy =
            sampleBuffer.TakeBlocked(preambleLength - 1)
                .Aggregate(
                    0f, (acc, sample) => smoothedEnergyFactor * sample * sample + (1 - smoothedEnergyFactor) * acc
                );

        var restNum = -1;

        ReadOnlySpan<float> preambleSpan = preamble.PreambleData.AsSpan();

        while (restNum != 0)
        {
            var corr = 0f;
            var index = 0;

            foreach (var sample in sampleBuffer.TakeBlocked(preambleLength))
            {
                corr += sample * preambleSpan[index];
                if (index == preambleLength - 1)
                {
                    smoothedEnergy =
                        smoothedEnergyFactor * sample * sample + (1 - smoothedEnergyFactor) * smoothedEnergy;
                }
                ++index;
            }
            corr /= preambleLength;

            sampleBuffer.Take();

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

        for (int i = 0; i < preambleLength - maxPeakFalling; i++)
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