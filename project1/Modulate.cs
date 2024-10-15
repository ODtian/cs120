using NAudio.Wave;
using System.Collections.Concurrent;
using System.Numerics;
using CS120.Symbol;
using CS120.Extension;
using CS120.Preamble;
using System.Diagnostics;
using CS120.Modulate;

namespace CS120.Modulate;

public interface IModulator
{
    void Modulate(ReadOnlySpan<byte> dataBuffer, BlockingCollection<float> sampleBuffer);
    void Modulate(ReadOnlySpan<byte> dataBuffer, Span<float> sampleBuffer);
    float[] Modulate(ReadOnlySpan<byte> dataBuffer);
}

public class DPSKModulator : IModulator
{
    private readonly DPSKSymbol symbols;

    public DPSKModulator(DPSKSymbolOption option) : this(new DPSKSymbol(option))
    {
    }

    public DPSKModulator(DPSKSymbol symbols)
    {
        this.symbols = symbols;
    }

    public void Modulate(ReadOnlySpan<byte> dataBuffer, BlockingCollection<float> sampleBuffer)
    {
        for (int i = 0; i < dataBuffer.Length * 8; i++)
        {
            foreach (var sample in GetSamples(dataBuffer, i))
            {
                sampleBuffer.Add(sample);
            }
        }
    }
    public void Modulate(ReadOnlySpan<byte> dataBuffer, Span<float> sampleBuffer)
    {
        var index = 0;
        for (int i = 0; i < dataBuffer.Length * 8; i++)
        {
            var samples = GetSamples(dataBuffer, i);
            samples.CopyTo(sampleBuffer.Slice(index));
            index += samples.Length;
        }
    }

    public float[] Modulate(ReadOnlySpan<byte> dataBuffer)
    {
        var samples = new List<float>();
        for (int i = 0; i < dataBuffer.Length * 8; i++)
        {
            samples.AddRange(GetSamples(dataBuffer, i));
        }
        return samples.ToArray();
    }
    private float[] GetSamples(ReadOnlySpan<byte> dataBuffer, int offset)
    {
        return symbols.Samples[(dataBuffer[offset / 8] >> (offset % 8)) & 1];
    }
    // private IEnumerable<float[]> GenerateSamples(ReadOnlySpan<byte> dataBuffer)
    // {
    //     for (int i = 0; i < dataBuffer.Length; i++)
    //     {
    //         var data = dataBuffer[i];
    //         for (int j = 0; j < 8; j++)
    //         {
    //             yield return symbols.Samples[(data >> i) & 1];
    //         }
    //     }
    //     // var symbols = DFSKSymbol.Get(option);

    //     // var samples = new List<float>();

    //     // IPreamble? preamble = ChirpPreamble.Create(WaveFormat.CreateIeeeFloatWaveFormat(option.SampleRate, 1));

    //     // samples.AddRange(Enumerable.Range(0, 48000).Select(
    //     //     _ => 0f
    //     // ));
    //     // samples.AddRange(preamble.PreambleData);

    //     // foreach (var d in data)
    //     // {
    //     //     for (int i = 0; i < 8; i++)
    //     //     {
    //     //         samples.AddRange(symbols[(d >> i) & 1]);
    //     //     }
    //     // }

    //     // samples.AddRange(Enumerable.Range(0, 48000).Select(
    //     //     _ => 0f
    //     // ));

    //     // return samples.ToArray();
    // }
}

public abstract record DemodulateLength()
{
    public record FixedLength(int length) : DemodulateLength;
    public record VariableLength(int numlengthBit) : DemodulateLength;

    public static readonly DemodulateLength Default = new FixedLength(480);
}

public interface IDemodulator
{
    // static abstract void Demodulate(BlockingCollection<float> sampleBuffer, Span<byte> buffer);
    // static abstract IDemodulator Create(WaveFormat waveFormat);
    // {
    //     throw new NotImplementedException();
    // }
    byte[] Demodulate(BlockingCollection<float> sampleBuffer);
    void Demodulate(BlockingCollection<float> sampleBuffer, Span<byte> dataBuffer);
    T Demodulate<T>(ReadOnlySpan<float> samples, int numBits)
        where T : IBinaryInteger<T>, IUnsignedNumber<T>;
}

public class DPSKDemodulator : IDemodulator
    {

        // public readonly DPSKSymbolOption option;
        private readonly DPSKSymbol symbols;
        private readonly int numSamplesPerSymbol;
        private readonly DemodulateLength demodulateLength = DemodulateLength.Default;

        // private readonly int lengthPartNumBits = 16;
        public DPSKDemodulator(DPSKSymbolOption option) : this(new DPSKSymbol(option))
        {
        }
        public DPSKDemodulator(DPSKSymbol symbols)
        {
            this.symbols = symbols;
            numSamplesPerSymbol = this.symbols.Option.NumSamplesPerSymbol;
        }

        public DPSKDemodulator(DPSKSymbol symbols, DemodulateLength demodulateLength) : this(symbols)
        {
            this.demodulateLength = demodulateLength;
        }

        public void Demodulate(BlockingCollection<float> sampleBuffer, Span<byte> dataBuffer)

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

            // byte[]? data = new byte[(int)Math.Ceiling(dataLengthInBit / 8f)];

            var buffer = new float[numSamplesPerSymbol * 8];

            for (int i = 0; i < dataBuffer.Length; i++)
            {
                consuming.TakeInto(buffer);
                dataBuffer[i] = Demodulate<byte>(buffer, 8);
            }

            var t2 = DateTime.Now;
            Console.WriteLine($"time {t2 - t1}");

            // return data;
        }

        public byte[] Demodulate(BlockingCollection<float> sampleBuffer)

        {
            var dataLengthInBit = demodulateLength switch {
                DemodulateLength.FixedLength(int length) => length,
                DemodulateLength.VariableLength(int lengthPartNumBits) => Demodulate<ushort>(
                    sampleBuffer.GetConsumingEnumerable().Take(numSamplesPerSymbol * lengthPartNumBits).ToArray(),
                    lengthPartNumBits
                ),
                _ => 0
            };

            byte[]? data = new byte[(int)Math.Ceiling(dataLengthInBit / 8f)];

            Demodulate(sampleBuffer, data.AsSpan());

            return data;
            // var consuming = sampleBuffer.GetConsumingEnumerable();
            // // var lengthPart = consuming.Take(numSamplesPerSymbol * lengthPartNumBits).ToArray();

            // // Console.WriteLine($"lengthPart.Length {lengthPart.Length}");
            // var dataLengthInBit =
            //     demodulateLength switch { DemodulateLength.FixedLength(int length) => length,
            //                               DemodulateLength.VariableLength(int lengthPartNumBits) =>
            //                               Demodulate<ushort>(
            //                                   consuming.Take(numSamplesPerSymbol * lengthPartNumBits).ToArray(),
            //                                   lengthPartNumBits
            //                               ),
            //                               _ => 0 };
            // // var dataLengthInBit = (int)Demodulate<ushort>(lengthPart, lengthPartNumBits);

            // // Console.WriteLine($"dataLengthInBit a {Convert.ToString((byte)dataLengthInBit, 2)}");
            // // Console.WriteLine($"dataLengthInBit b {Convert.ToString((byte)(dataLengthInBit >> 8), 2)}");

            // // // dataLengthInBit = Math.Min(2048, dataLengthInBit);

            // // Console.WriteLine($"dataLengthInBit {dataLengthInBit}");
            // // dataLengthInBit = 480;
            // var t1 = DateTime.Now;

            // byte[]? data = new byte[(int)Math.Ceiling(dataLengthInBit / 8f)];

            // var buffer = new float[numSamplesPerSymbol * 8];

            // for (int i = 0; i < data.Length; i++)
            // {
            //     consuming.TakeInto(buffer);
            //     data[i] = Demodulate<byte>(buffer, 8);
            // }

            // var t2 = DateTime.Now;
            // Console.WriteLine($"time {t2 - t1}");
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

        public T Demodulate<T>(ReadOnlySpan<float> samples, int numBits)
            where T : IBinaryInteger<
                      T>,
                      IUnsignedNumber<T>
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

            // for (int i = 0; i < numBits; i++)
            // {
            //     var offset = numSamplesPerSymbol * i;
            //     for (int j = 0; j < numSamplesPerSymbol; j++)
            //     {
            //         samples[j + offset] *= symbols.Samples[0][j];
            //     }
            // }
            // Smooth(samples, 12);

            for (int i = 0; i < numBits; i++)
            {
                var energy = 0f;
                var offset = numSamplesPerSymbol * i;
                for (int j = 0; j < numSamplesPerSymbol; j++)
                {
                    energy += samples[j + offset] * symbols.Samples[0][j];
                }
                result |= energy < 0f ? T.One << i : T.Zero;
            }

            return result;
        }
    }
