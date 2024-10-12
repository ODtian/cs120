using NAudio.Wave;
using System.Collections.Concurrent;
using System.Numerics;
using CS120.Symbol;
using CS120.Extension;

namespace CS120.Modulate;

public interface IModulator
{
    static abstract IDemodulator Create(WaveFormat waveFormat);
    float[] Modulate(BlockingCollection<byte> dataBuffer);
}

public interface IDemodulator
{
    // static abstract void Demodulate(BlockingCollection<float> sampleBuffer, Span<byte> buffer);
    static abstract IDemodulator Create(WaveFormat waveFormat);
    // {
    //     throw new NotImplementedException();
    // }
    byte[] Demodulate(BlockingCollection<float> sampleBuffer);
}

public struct DPSKDemodulator : IDemodulator
{

    public readonly DPSKSymbolOption option;
    private readonly float[][] symbols;

    private const int lengthPartNumBits = 16;

    DPSKDemodulator(WaveFormat waveFormat)
    {
        option = Program.option with { SampleRate = waveFormat.SampleRate };
        // option = new() { NumSymbols = 2, NumSamplesPerSymbol = 24, SampleRate = waveFormat.SampleRate, Freq = 4_000
        // };
        symbols = DFSKSymbol.Get(option);
    }
    public byte[] Demodulate(BlockingCollection<float> sampleBuffer)

    {
        var consuming = sampleBuffer.GetConsumingEnumerable();
        var lengthPart = consuming.Take(option.NumSamplesPerSymbol * lengthPartNumBits).ToArray();

        Console.WriteLine($"lengthPart.Length {lengthPart.Length}");

        var dataLengthInBit = (int)Demodulate<ushort>(lengthPart, lengthPartNumBits);

        Console.WriteLine($"dataLengthInBit a {Convert.ToString((byte)dataLengthInBit, 2)}");
        Console.WriteLine($"dataLengthInBit b {Convert.ToString((byte)(dataLengthInBit >> 8), 2)}");

        // dataLengthInBit = Math.Min(2048, dataLengthInBit);

        Console.WriteLine($"dataLengthInBit {dataLengthInBit}");
        dataLengthInBit = 480;
        var t1 = DateTime.Now;

        byte[]? data = new byte[(int)Math.Ceiling(dataLengthInBit / 8f)];

        var buffer = new float[option.NumSamplesPerSymbol * 8];

        for (int i = 0; i < data.Length; i++)
        {
            consuming.TakeInto(buffer);
            data[i] = Demodulate<byte>(buffer, 8);
        }

        var t2 = DateTime.Now;
        Console.WriteLine($"time {t2 - t1}");

        return data;
    }

    private static void Smooth(Span<float> samples, int windowSize)
    {
#if DEBUG
        if (windowSize <= 0)
        {
            throw new ArgumentException("Window size must be greater than 0", nameof(windowSize));
        }
#endif
        int halfWindow = windowSize / 2;
        float[] originalSamples = samples.ToArray();

        for (int i = 0; i < samples.Length; i++)
        {
            float sum = 0;
            int count = 0;

            for (int j = -halfWindow; j <= halfWindow; j++)
            {
                int index = i + j;
                if (index >= 0 && index < originalSamples.Length)
                {
                    sum += originalSamples[index];
                    count++;
                }
            }

            samples[i] = sum / count;
        }
    }

    public static IDemodulator Create(WaveFormat waveFormat)
    {
        return new DPSKDemodulator(waveFormat);
    }

    // symbols ahead are LSB
    private T Demodulate<T>(Span<float> samples, int numBits)
        where T : IBinaryInteger<T>, IUnsignedNumber<T>
    {
#if false
        {

            // Console.WriteLine("Debug模式");
            var size = Marshal.SizeOf<T>() * 8;

            if (numBits > size)
            {
                throw new ArgumentException($"numBits must be less than {size}, given {numBits}");
            }
        }
#endif
        var result = T.Zero;

        for (int i = 0; i < numBits; i++)
        {
            var offset = option.NumSamplesPerSymbol * i;
            for (int j = 0; j < option.NumSamplesPerSymbol; j++)
            {
                samples[j + offset] *= symbols[0][j];
            }
        }
        // Smooth(samples, 12);
        for (int i = 0; i < numBits; i++)
        {
            var energy = 0f;
            var offset = option.NumSamplesPerSymbol * i;
            for (int j = 0; j < option.NumSamplesPerSymbol; j++)
            {
                energy += samples[j + offset];
            }
            result |= energy < 0f ? T.One << i : T.Zero;
        }

        return result;
    }
}
