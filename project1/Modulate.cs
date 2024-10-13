using NAudio.Wave;
using System.Collections.Concurrent;
using System.Numerics;
using CS120.Symbol;
using CS120.Extension;
<<<<<<< HEAD
using CS120.Preamble;
=======
using System.Diagnostics;
>>>>>>> 897d10191a9594cf317069a890ae91a30ae084fa

namespace CS120.Modulate;

public interface IModulator
{
<<<<<<< HEAD
    static abstract IModulator Create(WaveFormat waveFormat);
=======
>>>>>>> 897d10191a9594cf317069a890ae91a30ae084fa
    float[] Modulate(BlockingCollection<byte> dataBuffer);
}

public struct DPSKModulator : IModulator
{
    private readonly DPSKSymbolOption option;
    private readonly WaveFormat waveFormat;

    private DPSKModulator(WaveFormat waveFormat)
    {
        option = Program.option with { SampleRate = waveFormat.SampleRate };
        this.waveFormat = waveFormat;
    }

    public static IModulator Create(WaveFormat waveFormat)
    {
        return new DPSKModulator(waveFormat);
    }

    public float[] Modulate(BlockingCollection<byte> dataBuffer)
    {
        var data = dataBuffer.ToArray();
        return GenerateSamples(data);
    }

    private float[] GenerateSamples(byte[] data)
    {
        var symbols = DFSKSymbol.Get(option);

        var samples = new List<float>();

        IPreamble? preamble = ChirpPreamble.Create(WaveFormat.CreateIeeeFloatWaveFormat(option.SampleRate, 1));

        samples.AddRange(Enumerable.Range(0, 48000).Select(
            _ => 0f
        ));
        samples.AddRange(preamble.PreambleData);

        foreach (var d in data)
        {
            for (int i = 0; i < 8; i++)
            {
                samples.AddRange(symbols[(d >> i) & 1]);
            }
        }

        samples.AddRange(Enumerable.Range(0, 48000).Select(
            _ => 0f
        ));

        return samples.ToArray();
    }
}

public interface IDemodulator
{
    // static abstract void Demodulate(BlockingCollection<float> sampleBuffer, Span<byte> buffer);
    // static abstract IDemodulator Create(WaveFormat waveFormat);
    // {
    //     throw new NotImplementedException();
    // }
    byte[] Demodulate(BlockingCollection<float> sampleBuffer);
}

public readonly struct DPSKDemodulator : IDemodulator
{

    // public readonly DPSKSymbolOption option;
    private readonly DPSKSymbol symbols;
    private readonly int numSamplesPerSymbol;

    private readonly int dataLengthInBit = 16;
    // private readonly int lengthPartNumBits = 16;

    public DPSKDemodulator(DPSKSymbol symbols, int dataLengthInBit = 440)
    {
        // option = Program.option with { SampleRate = waveFormat.SampleRate };
        // option = new() { NumSymbols = 2, NumSamplesPerSymbol = 24, SampleRate = waveFormat.SampleRate, Freq = 4_000
        // };
        this.symbols = symbols;
        this.dataLengthInBit = dataLengthInBit;

        var length = symbols.Samples[0].Length;
#if DEBUG
        Debug.Assert(symbols.Samples.All(s => s.Length == length));
#endif
        numSamplesPerSymbol = length;
    }

    public readonly byte[] Demodulate(BlockingCollection<float> sampleBuffer)

    {
        var consuming = sampleBuffer.GetConsumingEnumerable();
        // var lengthPart = consuming.Take(numSamplesPerSymbol * lengthPartNumBits).ToArray();

        // Console.WriteLine($"lengthPart.Length {lengthPart.Length}");

        // var dataLengthInBit = (int)Demodulate<ushort>(lengthPart, lengthPartNumBits);

        // Console.WriteLine($"dataLengthInBit a {Convert.ToString((byte)dataLengthInBit, 2)}");
        // Console.WriteLine($"dataLengthInBit b {Convert.ToString((byte)(dataLengthInBit >> 8), 2)}");

        // // dataLengthInBit = Math.Min(2048, dataLengthInBit);

        // Console.WriteLine($"dataLengthInBit {dataLengthInBit}");
        // dataLengthInBit = 480;
        var t1 = DateTime.Now;

        byte[]? data = new byte[(int)Math.Ceiling(dataLengthInBit / 8f)];

        var buffer = new float[numSamplesPerSymbol * 8];

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

    // public static IDemodulator Create(WaveFormat waveFormat)
    // {
    //     return new DPSKDemodulator(waveFormat);
    // }

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
            var offset = numSamplesPerSymbol * i;
            for (int j = 0; j < numSamplesPerSymbol; j++)
            {
                samples[j + offset] *= symbols.Samples[0][j];
            }
        }
        // Smooth(samples, 12);
        for (int i = 0; i < numBits; i++)
        {
            var energy = 0f;
            var offset = numSamplesPerSymbol * i;
            for (int j = 0; j < numSamplesPerSymbol; j++)
            {
                energy += samples[j + offset];
            }
            result |= energy < 0f ? T.One << i : T.Zero;
        }

        return result;
    }
}
