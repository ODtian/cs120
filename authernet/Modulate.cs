using NAudio.Wave;

using CS120.Symbol;

using CS120.Utils;
using System.IO.Pipelines;
using System.Buffers;
using CommunityToolkit.HighPerformance;
using CS120.Utils.Wave;
using CS120.Utils.Helpers;
using CS120.Utils.Extension;

namespace CS120.Modulate;

public interface IModulator
{
    // void Modulate(ReadOnlySpan<byte> dataBuffer, BlockingCollection<float> sampleBuffer);
    // void Modulate(ReadOnlySpan<byte> dataBuffer, Span<float> sampleBuffer);
    // float[] Modulate(ReadOnlySpan<byte> dataBuffer);
    void Init(WaveFormat waveFormat, PipeWriter sampleBuffer);
    void Modulate(ReadOnlySpan<byte> from);
    void Modulate(ReadOnlySpan<byte> from, out byte[] to);
    // void Modulate(ReadOnlySpan<byte> dataBuffer, out byte[] sampleBuffer);
}

public class DPSKModulator
(PipeWriter pipeWriter, DPSKSymbol symbol) : IPipeWriter<byte>
{
    private readonly DPSKSymbol symbol = symbol;
    private readonly int numSamplesPerSymbol = symbol.Option.NumSamplesPerSymbol;
    // private PipeWriter ? sampleBuffer;
    public PipeWriter SourceWriter { get; } = pipeWriter;

    // public int NumSamplesPerSymbol { get; } = symbols.Option.NumSamplesPerSymbol;

    // public static explicit operator DPSKModulator(DPSKSymbol symbols) => new(symbols);

    // public void Init(WaveFormat waveFormat, PipeWriter sampleBuffer)
    // {
    //     if (this.sampleBuffer is not null)
    //     {
    //         throw new InvalidOperationException("sampleBuffer is already initialized");
    //     }

    //     this.sampleBuffer = sampleBuffer;
    // }

    // public DPSKModulator Build(WaveFormat waveFormat, PipeWriter sampleBuffer)
    // {
    //     Init(waveFormat, sampleBuffer);
    //     return this;
    // }
    public int Write(ReadOnlySpan<byte> dataBuffer)
    {
        var count = 0;
        for (int i = 0; i < dataBuffer.Length; i++)
        {
            var data = dataBuffer[i];
            for (int j = 0; j < 8; j++)
            {
                var s = symbol.Samples.Span[data >> j & 1].Span.AsBytes();
                count += s.Length;
                SourceWriter.Write(s);
            }
            // SourceWriter.Write(ModulateHelper.GetModulateSamples(symbol.Samples.Span, dataBuffer, i).AsBytes());
        }
        return count;
    }
    // public void Modulate(ReadOnlySpan<byte> dataBuffer)
    // {
    //     if (sampleBuffer is null)
    //     {
    //         throw new InvalidOperationException("sampleBuffer is not initialized");
    //     }
    //     for (int i = 0; i < dataBuffer.Length * 8; i++)
    //     {
    //         sampleBuffer.Write(GetSamples(dataBuffer, i).AsBytes());
    //     }
    // }

    // public void Modulate(ReadOnlySpan<byte> dataBuffer, Span<float> sampleBuffer)
    // {
    //     var index = 0;
    //     for (int i = 0; i < dataBuffer.Length * 8; i++)
    //     {
    //         var samples = GetSamples(dataBuffer, i);
    //         samples.CopyTo(sampleBuffer.Slice(index));
    //         index += samples.Length;
    //     }
    // }

    // public void Modulate(ReadOnlySpan<byte> dataBuffer, out byte[] sampleBuffer)
    // {
    //     var samples = new List<byte>();
    //     for (int i = 0; i < dataBuffer.Length * 8; i++)
    //     {
    //         samples.AddRange(GetSamples(dataBuffer, i).AsBytes());
    //     }

    //     sampleBuffer = [..samples];
    // }
    // public ReadOnlySpan<float> GetSamples(ReadOnlySpan<byte> dataBuffer, int bitOffset)
    // {
    //     return symbol.Samples.Span[(dataBuffer[bitOffset / 8] >> (bitOffset % 8)) & 1].Span;
    // }

    // public void GetSamples(byte data, Span<float[]> sampleBuffer)
    // {
    //     for (int i = 0; i < 8; i++)
    //     {
    //         sampleBuffer[i] = symbols.Samples[(data >> i) & 1];
    //     }
    // }
}

public class LineModulator
(PipeWriter pipeWriter, LineSymbol symbol) : IPipeWriter<byte>
{
    private readonly LineSymbol symbol = symbol;
    private readonly int numSamplesPerSymbol = symbol.Option.NumSamplesPerSymbol;
    // private PipeWriter ? sampleBuffer;
    public PipeWriter SourceWriter { get; } = pipeWriter;

    // public int NumSamplesPerSymbol { get; } = symbols.Option.NumSamplesPerSymbol;

    // public static explicit operator DPSKModulator(DPSKSymbol symbols) => new(symbols);

    // public void Init(WaveFormat waveFormat, PipeWriter sampleBuffer)
    // {
    //     if (this.sampleBuffer is not null)
    //     {
    //         throw new InvalidOperationException("sampleBuffer is already initialized");
    //     }

    //     this.sampleBuffer = sampleBuffer;
    // }

    // public DPSKModulator Build(WaveFormat waveFormat, PipeWriter sampleBuffer)
    // {
    //     Init(waveFormat, sampleBuffer);
    //     return this;
    // }
    public int Write(ReadOnlySpan<byte> dataBuffer)
    {
        int count = 0;
        for (int i = 0; i < dataBuffer.Length; i++)
        {
            var data = dataBuffer[i];
            for (int j = 0; j < 8; j++)
            {
                var s = symbol.Samples.Span[data >> j & 1].Span.AsBytes();
                count += s.Length;
                SourceWriter.Write(s);
            }
            // SourceWriter.Write(ModulateHelper.GetModulateSamples(symbol.Samples.Span, dataBuffer, i).AsBytes());
        }
        return count;
    }
    // public void Modulate(ReadOnlySpan<byte> dataBuffer)
    // {
    //     if (sampleBuffer is null)
    //     {
    //         throw new InvalidOperationException("sampleBuffer is not initialized");
    //     }
    //     for (int i = 0; i < dataBuffer.Length * 8; i++)
    //     {
    //         sampleBuffer.Write(GetSamples(dataBuffer, i).AsBytes());
    //     }
    // }

    // public void Modulate(ReadOnlySpan<byte> dataBuffer, Span<float> sampleBuffer)
    // {
    //     var index = 0;
    //     for (int i = 0; i < dataBuffer.Length * 8; i++)
    //     {
    //         var samples = GetSamples(dataBuffer, i);
    //         samples.CopyTo(sampleBuffer.Slice(index));
    //         index += samples.Length;
    //     }
    // }

    // public void Modulate(ReadOnlySpan<byte> dataBuffer, out byte[] sampleBuffer)
    // {
    //     var samples = new List<byte>();
    //     for (int i = 0; i < dataBuffer.Length * 8; i++)
    //     {
    //         samples.AddRange(GetSamples(dataBuffer, i).AsBytes());
    //     }

    //     sampleBuffer = [..samples];
    // }
    // public ReadOnlySpan<float> GetSamples(ReadOnlySpan<byte> dataBuffer, int bitOffset)
    // {
    //     return symbol.Samples.Span[(dataBuffer[bitOffset / 8] >> (bitOffset % 8)) & 1].Span;
    // }

    // public void GetSamples(byte data, Span<float[]> sampleBuffer)
    // {
    //     for (int i = 0; i < 8; i++)
    //     {
    //         sampleBuffer[i] = symbols.Samples[(data >> i) & 1];
    //     }
    // }
}

public abstract record DemodulateLength()
{
    public record FixedLength(int Length) : DemodulateLength;
    public record VariableLength(int NumLengthByte) : DemodulateLength;
}

public interface IDemodulator
{
    // static abstract void Demodulate(BlockingCollection<float> sampleBuffer, Span<byte> buffer);
    // static abstract IDemodulator Create(WaveFormat waveFormat);
    // {
    //     throw new NotImplementedException();
    // }
    void Init(WaveFormat waveFormat, PipeReader sampleBuffer);
    bool Demodulate(out byte[] to);
    bool Demodulate(Span<byte> to);
    // bool Demodulate(ReadOnlySpan<float> sampleBuffer, Span<byte> dataBuffer);
    // byte[] Demodulate(BlockingCollection<float> sampleBuffer);
    // void Demodulate(BlockingCollection<float> sampleBuffer, Span<byte> dataBuffer);
    // void Demodulate(ReadOnlySpan<float> sampleBuffer, Span<byte> dataBuffer);
    // T Demodulate<T>(ReadOnlySpan<float> samples, int numBits)
    //     where T : IBinaryInteger<T>, IUnsignedNumber<T>;
}

// public class DPSKDemodulatorBuilder
// (DPSKSymbol symbols) : IReaderBuilder<DPSKDemodulator>
// {

//     private readonly DPSKSymbol symbols = symbols;
//     // private readonly WaveFormat waveFormat;
//     private readonly DemodulateLength demodulateLength = DemodulateLength.Default;

//     public DPSKDemodulatorBuilder(DPSKSymbol symbols, DemodulateLength demodulateLength) : this(symbols)
//     {
//         this.demodulateLength = demodulateLength;
//     }

//     public DPSKDemodulatorBuilder(DPSKSymbolOption option) : this(new DPSKSymbol(option))
//     {
//     }

//     public DPSKDemodulatorBuilder(DPSKSymbolOption option, DemodulateLength demodulateLength)
//         : this(new DPSKSymbol(option), demodulateLength)
//     {
//     }

//     public DPSKDemodulator Build(WaveFormat waveFormat, PipeReader sampleBuffer)
//     {
//         return new(
//             new StreamWaveProvider(waveFormat, sampleBuffer.AsStream()).ToSampleProvider().ToMono(),
//             symbols,
//             demodulateLength
//         );
//     }
// }

public class DPSKDemodulator
(PipeReader pipeReader, WaveFormat waveFormat, DPSKSymbol symbol) : IPipeReader<byte>
{

    // public readonly DPSKSymbolOption option;
    private readonly DPSKSymbol symbol = symbol;
    private readonly int numSamplesPerSymbol = symbol.Option.NumSamplesPerSymbol;
    // private readonly DemodulateLength demodulateLength = DemodulateLength.Default;
    private readonly ISampleProvider sampleProvider =
        new StreamWaveProvider(waveFormat, pipeReader.AsStream()).ToSampleProvider().ToMono();
    private readonly ISampleProvider viewSampleProvider = new PipeViewProvider(waveFormat, pipeReader);
    public PipeReader SourceReader { get; } = pipeReader;
    // private ISampleProvider ? sampleProvider;

    // public int NumSamplesPerSymbol { get; } = symbols.Option.NumSamplesPerSymbol;

    // public DPSKDemodulator(DPSKSymbolOption option) : this(new DPSKSymbol(option))
    // {
    // }

    // public DPSKDemodulator(DPSKSymbol symbol, DemodulateLength demodulateLength)
    //     : this(symbols) => this.demodulateLength = demodulateLength;

    // public DPSKDemodulator(DPSKSymbolOption option, DemodulateLength demodulateLength)
    //     : this(new DPSKSymbol(option), demodulateLength)
    // {
    // }
    // public DPSKDemodulator(DPSKSymbol symbols, DemodulateLength demodulateLength) : this(symbols)
    // {
    //     DemodulateLength = demodulateLength;
    // }

    // public void Init(WaveFormat waveFormat, PipeReader sampleBuffer)
    // {
    //     if (sampleProvider is not null)
    //     {
    //         throw new InvalidOperationException("sampleProvider is already initialized");
    //     }

    //     sampleProvider = new StreamWaveProvider(waveFormat, sampleBuffer.AsStream()).ToSampleProvider().ToMono();
    // }

    // public DPSKDemodulator Build(WaveFormat waveFormat, PipeReader sampleBuffer)
    // {
    //     Init(waveFormat, sampleBuffer);
    //     return this;
    // }
    public bool TryReadTo(Span<byte> dst, bool advandce = true)
    {
        var buffer = new float[numSamplesPerSymbol * 8];

        var provider = advandce ? sampleProvider : viewSampleProvider;

        for (int i = 0; i < dst.Length; i++)
        {
            if (provider.ReadExact(buffer, 0, buffer.Length) == 0)
            {
                return false;
            }
            dst[i] = ModulateHelper.DotProductDemodulateByte(buffer, symbol.Samples.Span[0].Span);
        }

        return true;
    }
    // public bool Demodulate(Span<byte> dataBuffer)
    // {
    //     // var sampleProvider = new StreamWaveProvider(waveFormat,
    //     sampleBuffer.AsStream()).ToSampleProvider().ToMono(); if (sampleProvider is null)
    //     {
    //         throw new InvalidOperationException("sampleProvider is not initialized");
    //     }

    //     var buffer = new float[NumSamplesPerSymbol * 8];

    //     for (int i = 0; i < dataBuffer.Length; i++)
    //     {
    //         if (sampleProvider.ReadExact(buffer, 0, buffer.Length) == 0)
    //         {
    //             return false;
    //         }
    //         dataBuffer[i] = Demodulate<byte>(buffer, 8);
    //     }

    //     return true;
    // }

    // public bool Demodulate(out byte[] dataBuffer)
    // {
    //     var dataLengthInBit = GetLength();
    //     dataBuffer = new byte[(int)Math.Ceiling(dataLengthInBit / 8f)];
    //     if (dataLengthInBit == 0)
    //     {
    //         return false;
    //     }

    //     return Demodulate(dataBuffer.AsSpan());
    // }

    // public bool Demodulate(ReadOnlySpan<float> samples, Span<byte> dataBuffer)
    // {
    //     if (dataBuffer.Length * 8 * numSamplesPerSymbol > samples.Length)
    //     {
    //         return false;
    //     }
    //     for (int i = 0; i < dataBuffer.Length; i++)
    //     {
    //         dataBuffer[i] = Demodulate<byte>(samples.Slice(i * numSamplesPerSymbol, 8 * numSamplesPerSymbol), 8);
    //     }
    //     return true;
    // }

    //     private static void Smooth(Span<float> samples, int windowSize)
    //     {
    // #if DEBUG
    //         if (windowSize <= 0)
    //         {
    //             throw new ArgumentException("Window size must be greater than 0", nameof(windowSize));
    //         }
    // #endif
    //         int halfWindow = windowSize / 2;
    //         float[] originalSamples = samples.ToArray();

    //         for (int i = 0; i < samples.Length; i++)
    //         {
    //             float sum = 0;
    //             int count = 0;

    //             for (int j = -halfWindow; j <= halfWindow; j++)
    //             {
    //                 int index = i + j;
    //                 if (index >= 0 && index < originalSamples.Length)
    //                 {
    //                     sum += originalSamples[index];
    //                     count++;
    //                 }
    //             }

    //             samples[i] = sum / count;
    //         }
    //     }
    // public byte Demodulate(ReadOnlySpan<float> samples, int numBits)
    // {
    //     // #if false
    //     //         {

    //     //             // Console.WriteLine("Debug模式");
    //     //             var size = Marshal.SizeOf<T>() * 8;

    //     //             if (numBits > size)
    //     //             {
    //     //                 throw new ArgumentException($"numBits must be less than {size}, given {numBits}");
    //     //             }
    //     //         }
    //     // #endif
    //     byte result = 0;

    //     // for (int i = 0; i < numBits; i++)
    //     // {
    //     //     var offset = numSamplesPerSymbol * i;
    //     //     for (int j = 0; j < numSamplesPerSymbol; j++)
    //     //     {
    //     //         samples[j + offset] *= symbols.Samples[0][j];
    //     //     }
    //     // }
    //     // Smooth(samples, 12);

    //     for (int i = 0; i < numBits; i++)
    //     {
    //         var energy = 0f;
    //         var offset = NumSamplesPerSymbol * i;
    //         for (int j = 0; j < NumSamplesPerSymbol; j++)
    //         {
    //             energy += samples[j + offset] * symbols.Samples[0][j];
    //         }
    //         result |= energy < 0f ? T.One << i : T.Zero;
    //     }

    //     return result;
    // }
    // public T Demodulate<T>(ReadOnlySpan<float> samples, int numBits)
    //     where T : IBinaryInteger<
    //               T>,
    //               IUnsignedNumber<T>
    // {
    //     // #if false
    //     //         {

    //     //             // Console.WriteLine("Debug模式");
    //     //             var size = Marshal.SizeOf<T>() * 8;

    //     //             if (numBits > size)
    //     //             {
    //     //                 throw new ArgumentException($"numBits must be less than {size}, given {numBits}");
    //     //             }
    //     //         }
    //     // #endif
    //     var result = T.Zero;

    //     // for (int i = 0; i < numBits; i++)
    //     // {
    //     //     var offset = numSamplesPerSymbol * i;
    //     //     for (int j = 0; j < numSamplesPerSymbol; j++)
    //     //     {
    //     //         samples[j + offset] *= symbols.Samples[0][j];
    //     //     }
    //     // }
    //     // Smooth(samples, 12);

    //     for (int i = 0; i < numBits; i++)
    //     {
    //         var energy = 0f;
    //         var offset = NumSamplesPerSymbol * i;
    //         for (int j = 0; j < NumSamplesPerSymbol; j++)
    //         {
    //             energy += samples[j + offset] * symbols.Samples[0][j];
    //         }
    //         result |= energy < 0f ? T.One << i : T.Zero;
    //     }

    //     return result;
    // }

    // public int GetLength()
    // {
    //     if (demodulateLength is DemodulateLength.FixedLength(int length))
    //     {
    //         return length;
    //     }
    //     else if (demodulateLength is DemodulateLength.VariableLength(int lengthPartNumBits))
    //     {
    //         if (sampleProvider is null)
    //         {
    //             throw new InvalidOperationException("sampleProvider is not initialized");
    //         }
    //         var buffer = new float[NumSamplesPerSymbol * lengthPartNumBits];
    //         if (sampleProvider.ReadExact(buffer, 0, buffer.Length) == 0)
    //         {
    //             return 0;
    //         }
    //         return (int)Demodulate<uint>(buffer, lengthPartNumBits);
    //     }
    //     else
    //     {
    //         return 0;
    //     }
    // }
}

public class LineDemodulator
(PipeReader pipeReader, WaveFormat waveFormat, LineSymbol symbol) : IPipeReader<byte>
{

    // public readonly DPSKSymbolOption option;
    private readonly LineSymbol symbol = symbol;
    private readonly int numSamplesPerSymbol = symbol.Option.NumSamplesPerSymbol;
    // private readonly DemodulateLength demodulateLength = DemodulateLength.Default;
    private readonly ISampleProvider sampleProvider =
        new StreamWaveProvider(waveFormat, pipeReader.AsStream()).ToSampleProvider().ToMono();
    private readonly ISampleProvider viewSampleProvider = new PipeViewProvider(waveFormat, pipeReader);
    public PipeReader SourceReader { get; } = pipeReader;
    // private ISampleProvider ? sampleProvider;

    // public int NumSamplesPerSymbol { get; } = symbols.Option.NumSamplesPerSymbol;

    // public DPSKDemodulator(DPSKSymbolOption option) : this(new DPSKSymbol(option))
    // {
    // }

    // public DPSKDemodulator(DPSKSymbol symbol, DemodulateLength demodulateLength)
    //     : this(symbols) => this.demodulateLength = demodulateLength;

    // public DPSKDemodulator(DPSKSymbolOption option, DemodulateLength demodulateLength)
    //     : this(new DPSKSymbol(option), demodulateLength)
    // {
    // }
    // public DPSKDemodulator(DPSKSymbol symbols, DemodulateLength demodulateLength) : this(symbols)
    // {
    //     DemodulateLength = demodulateLength;
    // }

    // public void Init(WaveFormat waveFormat, PipeReader sampleBuffer)
    // {
    //     if (sampleProvider is not null)
    //     {
    //         throw new InvalidOperationException("sampleProvider is already initialized");
    //     }

    //     sampleProvider = new StreamWaveProvider(waveFormat, sampleBuffer.AsStream()).ToSampleProvider().ToMono();
    // }

    // public DPSKDemodulator Build(WaveFormat waveFormat, PipeReader sampleBuffer)
    // {
    //     Init(waveFormat, sampleBuffer);
    //     return this;
    // }
    public bool TryReadTo(Span<byte> dst, bool advandce = true)
    {
        var buffer = new float[numSamplesPerSymbol * 8];

        var provider = advandce ? sampleProvider : viewSampleProvider;

        for (int i = 0; i < dst.Length; i++)
        {
            if (provider.ReadExact(buffer, 0, buffer.Length) == 0)
            {
                return false;
            }
            dst[i] = ModulateHelper.DotProductDemodulateByte(buffer, symbol.Samples.Span[0].Span);
        }

        return true;
    }
    // public bool Demodulate(Span<byte> dataBuffer)
    // {
    //     // var sampleProvider = new StreamWaveProvider(waveFormat,
    //     sampleBuffer.AsStream()).ToSampleProvider().ToMono(); if (sampleProvider is null)
    //     {
    //         throw new InvalidOperationException("sampleProvider is not initialized");
    //     }

    //     var buffer = new float[NumSamplesPerSymbol * 8];

    //     for (int i = 0; i < dataBuffer.Length; i++)
    //     {
    //         if (sampleProvider.ReadExact(buffer, 0, buffer.Length) == 0)
    //         {
    //             return false;
    //         }
    //         dataBuffer[i] = Demodulate<byte>(buffer, 8);
    //     }

    //     return true;
    // }

    // public bool Demodulate(out byte[] dataBuffer)
    // {
    //     var dataLengthInBit = GetLength();
    //     dataBuffer = new byte[(int)Math.Ceiling(dataLengthInBit / 8f)];
    //     if (dataLengthInBit == 0)
    //     {
    //         return false;
    //     }

    //     return Demodulate(dataBuffer.AsSpan());
    // }

    // public bool Demodulate(ReadOnlySpan<float> samples, Span<byte> dataBuffer)
    // {
    //     if (dataBuffer.Length * 8 * numSamplesPerSymbol > samples.Length)
    //     {
    //         return false;
    //     }
    //     for (int i = 0; i < dataBuffer.Length; i++)
    //     {
    //         dataBuffer[i] = Demodulate<byte>(samples.Slice(i * numSamplesPerSymbol, 8 * numSamplesPerSymbol), 8);
    //     }
    //     return true;
    // }

    //     private static void Smooth(Span<float> samples, int windowSize)
    //     {
    // #if DEBUG
    //         if (windowSize <= 0)
    //         {
    //             throw new ArgumentException("Window size must be greater than 0", nameof(windowSize));
    //         }
    // #endif
    //         int halfWindow = windowSize / 2;
    //         float[] originalSamples = samples.ToArray();

    //         for (int i = 0; i < samples.Length; i++)
    //         {
    //             float sum = 0;
    //             int count = 0;

    //             for (int j = -halfWindow; j <= halfWindow; j++)
    //             {
    //                 int index = i + j;
    //                 if (index >= 0 && index < originalSamples.Length)
    //                 {
    //                     sum += originalSamples[index];
    //                     count++;
    //                 }
    //             }

    //             samples[i] = sum / count;
    //         }
    //     }
    // public byte Demodulate(ReadOnlySpan<float> samples, int numBits)
    // {
    //     // #if false
    //     //         {

    //     //             // Console.WriteLine("Debug模式");
    //     //             var size = Marshal.SizeOf<T>() * 8;

    //     //             if (numBits > size)
    //     //             {
    //     //                 throw new ArgumentException($"numBits must be less than {size}, given {numBits}");
    //     //             }
    //     //         }
    //     // #endif
    //     byte result = 0;

    //     // for (int i = 0; i < numBits; i++)
    //     // {
    //     //     var offset = numSamplesPerSymbol * i;
    //     //     for (int j = 0; j < numSamplesPerSymbol; j++)
    //     //     {
    //     //         samples[j + offset] *= symbols.Samples[0][j];
    //     //     }
    //     // }
    //     // Smooth(samples, 12);

    //     for (int i = 0; i < numBits; i++)
    //     {
    //         var energy = 0f;
    //         var offset = NumSamplesPerSymbol * i;
    //         for (int j = 0; j < NumSamplesPerSymbol; j++)
    //         {
    //             energy += samples[j + offset] * symbols.Samples[0][j];
    //         }
    //         result |= energy < 0f ? T.One << i : T.Zero;
    //     }

    //     return result;
    // }
    // public T Demodulate<T>(ReadOnlySpan<float> samples, int numBits)
    //     where T : IBinaryInteger<
    //               T>,
    //               IUnsignedNumber<T>
    // {
    //     // #if false
    //     //         {

    //     //             // Console.WriteLine("Debug模式");
    //     //             var size = Marshal.SizeOf<T>() * 8;

    //     //             if (numBits > size)
    //     //             {
    //     //                 throw new ArgumentException($"numBits must be less than {size}, given {numBits}");
    //     //             }
    //     //         }
    //     // #endif
    //     var result = T.Zero;

    //     // for (int i = 0; i < numBits; i++)
    //     // {
    //     //     var offset = numSamplesPerSymbol * i;
    //     //     for (int j = 0; j < numSamplesPerSymbol; j++)
    //     //     {
    //     //         samples[j + offset] *= symbols.Samples[0][j];
    //     //     }
    //     // }
    //     // Smooth(samples, 12);

    //     for (int i = 0; i < numBits; i++)
    //     {
    //         var energy = 0f;
    //         var offset = NumSamplesPerSymbol * i;
    //         for (int j = 0; j < NumSamplesPerSymbol; j++)
    //         {
    //             energy += samples[j + offset] * symbols.Samples[0][j];
    //         }
    //         result |= energy < 0f ? T.One << i : T.Zero;
    //     }

    //     return result;
    // }

    // public int GetLength()
    // {
    //     if (demodulateLength is DemodulateLength.FixedLength(int length))
    //     {
    //         return length;
    //     }
    //     else if (demodulateLength is DemodulateLength.VariableLength(int lengthPartNumBits))
    //     {
    //         if (sampleProvider is null)
    //         {
    //             throw new InvalidOperationException("sampleProvider is not initialized");
    //         }
    //         var buffer = new float[NumSamplesPerSymbol * lengthPartNumBits];
    //         if (sampleProvider.ReadExact(buffer, 0, buffer.Length) == 0)
    //         {
    //             return 0;
    //         }
    //         return (int)Demodulate<uint>(buffer, lengthPartNumBits);
    //     }
    //     else
    //     {
    //         return 0;
    //     }
    // }
}

public class OFDMModulator : IPipeWriter<byte>
{
    // private readonly DPSKModulator[] modulators;
    private readonly DPSKSymbol[] symbols;
    // private PipeWriter? sampleBuffer;
    private readonly int numSamplesPerSymbol;
    // public int NumSamplesPerSymbol { get; }
    public PipeWriter SourceWriter { get; }

    // readonly struct ExtendAbleList
    // (List<byte> buffer) : IExtendable<float>
    // {
    //     readonly List<byte> buffer = buffer;
    //     public readonly void Extend(ReadOnlySpan<float> samples)
    //     {
    //         buffer.AddRange(samples.AsBytes());
    //     }
    // }

    // readonly struct ExtendAblePipe
    // (PipeWriter buffer) : IExtendable<float>
    // {
    //     readonly PipeWriter buffer = buffer;
    //     public readonly void Extend(ReadOnlySpan<float> samples)
    //     {
    //         buffer.Write(samples.AsBytes());
    //     }
    // }
    public OFDMModulator(PipeWriter pipeWriter, DPSKSymbol[] symbols)
    {
        // modulators = symbols.Select(s => new DPSKModulator(s)).ToArray();
        this.symbols = symbols;
        // NumSamplesPerSymbol = modulators[0].NumSamplesPerSymbol;
        numSamplesPerSymbol = symbols[0].Option.NumSamplesPerSymbol;

        SourceWriter = pipeWriter;

        if (symbols.Any(s => s.Option.NumSamplesPerSymbol != numSamplesPerSymbol))
        {
            throw new ArgumentException("All symbols must have the same number of samples per symbol");
        }
    }

    // public OFDMModulator(DPSKSymbolOption[] options) : this(options.Select(o => new DPSKSymbol(o)).ToArray())
    // {
    // }

    // public void Init(WaveFormat waveFormat, PipeWriter sampleBuffer)
    // {
    //     if (this.sampleBuffer is not null)
    //     {
    //         throw new InvalidOperationException("sampleBuffer is already initialized");
    //     }

    //     this.sampleBuffer = sampleBuffer;

    //     foreach (var m in modulators)
    //     {
    //         m.Init(waveFormat, sampleBuffer);
    //     }
    // }

    // public OFDMModulator Build(WaveFormat waveFormat, PipeWriter sampleBuffer)
    // {
    //     Init(waveFormat, sampleBuffer);
    //     return this;
    // }

    // public void Modulate(ReadOnlySpan<byte> dataBuffer)
    // {
    //     if (sampleBuffer is null)
    //     {
    //         throw new InvalidOperationException("sampleBuffer is not initialized");
    //     }

    //     Modulate(dataBuffer, new ExtendAblePipe(sampleBuffer));
    // }

    // public void Modulate(ReadOnlySpan<byte> dataBuffer, Span<float> sampleBuffer)
    // {
    //     var symbols = new float [8][];
    //     for (int i = 0; i < dataBuffer.Length; i += modulators.Length)
    //     {
    //         // buffer.AsSpan().Clear();
    //         for (int j = 0; j < modulators.Length && i + j < dataBuffer.Length; j++)
    //         {
    //             symbols.AsSpan().Clear();
    //             modulators[j].GetSamples(dataBuffer[i + j], symbols);

    //             for (int k = 0; k < 8; k++)
    //             {
    //                 for (int l = 0; l < numSamplesPerSymbol; l++)
    //                 {
    //                     sampleBuffer[((i + j) * 8 + k) * numSamplesPerSymbol + l] += symbols[k][l];
    //                 }
    //             }
    //         }
    //     }
    // }

    // public void Modulate(ReadOnlySpan<byte> dataBuffer, out byte[] sampleBuffer)
    // {
    //     var samples = new List<byte>();
    //     Modulate(dataBuffer, new ExtendAbleList(samples));
    //     sampleBuffer = [..samples];
    // }
    // private void Modulate<T>(ReadOnlySpan<byte> dataBuffer, T sampleBuffer)
    //     where T : IExtendable<float>
    // {
    //     var buffer = new float[NumSamplesPerSymbol * 8];
    //     // Console.WriteLine(dataBuffer.Length);
    //     var symbols = new float [8][];
    //     for (int i = 0; i < dataBuffer.Length; i += modulators.Length)
    //     {
    //         buffer.AsSpan().Clear();
    //         for (int j = 0; j < modulators.Length && i + j < dataBuffer.Length; j++)
    //         {
    //             symbols.AsSpan().Clear();
    //             modulators[j].GetSamples(dataBuffer[i + j], symbols);

    //             for (int k = 0; k < 8; k++)
    //             {
    //                 for (int l = 0; l < NumSamplesPerSymbol; l++)
    //                 {
    //                     buffer[k * NumSamplesPerSymbol + l] += symbols[k][l];
    //                 }
    //             }
    //         }
    //         sampleBuffer.Extend(buffer);
    //     }
    // }

    public int Write(ReadOnlySpan<byte> dataBuffer)
    {
        var count = 0;
        var buffer = new float[numSamplesPerSymbol * 8];
        // Console.WriteLine(dataBuffer.Length);
        // var symbolsBuffer = new ReadOnlyMemory<float>[8];
        for (int i = 0; i < dataBuffer.Length; i += symbols.Length)
        {
            buffer.AsSpan().Clear();
            for (int j = 0; j < symbols.Length && i + j < dataBuffer.Length; j++)
            {
                // symbols.AsSpan().Clear();
                // modulators[j].GetSamples(dataBuffer[i + j], symbols);
                // for (int k = 0; k < 8; k++)
                // {
                //     symbols[k] = ModulateHelper.GetModulateSamples(symbols[j].Samples, dataBuffer, i + j);
                // }
                var data = dataBuffer[i + j];
                for (int k = 0; k < 8; k++)
                {
                    var symbolSpan = symbols[j].Samples.Span[data >> k & 1].Span;
                    for (int l = 0; l < numSamplesPerSymbol; l++)
                    {
                        buffer[k * numSamplesPerSymbol + l] += symbolSpan[l];
                    }
                }
            }
            // sampleBuffer.Extend(buffer);
            var s = buffer.AsSpan().AsBytes();
            count += s.Length;
            SourceWriter.Write(s);
        }
        return count;
    }
    // private void Modulate<T>(ReadOnlySpan<byte> dataBuffer, T sampleBuffer)
    //     where T : IExtendable<float>
    // {
    //     var buffer = new float[NumSamplesPerSymbol * 8];
    //     // Console.WriteLine(dataBuffer.Length);
    //     var symbols = new float [8][];
    //     for (int i = 0; i < dataBuffer.Length; i += modulators.Length)
    //     {
    //         buffer.AsSpan().Clear();
    //         for (int j = 0; j < modulators.Length && i + j < dataBuffer.Length; j++)
    //         {
    //             symbols.AsSpan().Clear();
    //             modulators[j].GetSamples(dataBuffer[i + j], symbols);

    //             for (int k = 0; k < 8; k++)
    //             {
    //                 for (int l = 0; l < NumSamplesPerSymbol; l++)
    //                 {
    //                     buffer[k * NumSamplesPerSymbol + l] += symbols[k][l];
    //                 }
    //             }
    //         }
    //         sampleBuffer.Extend(buffer);
    //     }
    // }
}

// public class OFDMDemodulatorBuilder
// (params DPSKSymbol[] symbols) : IReaderBuilder<OFDMDemodulator>
// {
//     // private readonly DPSKDemodulator[] demodulators = demodulators;
//     private readonly DemodulateLength demodulateLength = DemodulateLength.Default;
//     private readonly DPSKSymbol[] symbols = symbols;

//     public OFDMDemodulatorBuilder(DPSKSymbol[] symbols, DemodulateLength demodulateLength) : this(symbols)
//     {
//         this.demodulateLength = demodulateLength;
//     }

//     // public OFDMDemodulatorBuilder(DPSKSymbolOption[] options) : this(options.Select(o => new
//     // DPSKSymbol(o)).ToArray())
//     // {
//     // }
//     // public OFDMDemodulatorBuilder(DPSKSymbolOption[] options, DemodulateLength demodulateLength)
//     //     : this(options.Select(o => new DPSKSymbol(o)).ToArray(), demodulateLength)
//     // {
//     // }

//     public OFDMDemodulator Build(WaveFormat waveFormat, PipeReader sampleBuffer)
//     {
//         var sampleProvider = new StreamWaveProvider(waveFormat, sampleBuffer.AsStream()).ToSampleProvider().ToMono();
//         var demodulatorBuilder =
//             symbols.Select(s => new DPSKDemodulator(sampleProvider, s, demodulateLength)).ToArray();
//         return new OFDMDemodulator(sampleProvider, demodulatorBuilder);
//     }
// }

public class OFDMDemodulator : IPipeReader<byte>
{
    private readonly DPSKSymbol[] symbols;
    // private readonly DPSKDemodulator[] demodulators;
    private readonly ISampleProvider sampleProvider;
    private readonly ISampleProvider viewSampleProvider;
    private readonly int numSamplesPerSymbol;

    public PipeReader SourceReader { get; }

    //     public OFDMDemodulator(WaveFormat waveFormat, params DPSKSymbolOption[] options)
    //         : this(waveFormat, options.Select(o => new DPSKSymbol(o)).ToArray())
    //     {
    //     }

    //     public OFDMDemodulator(WaveFormat waveFormat, params DPSKSymbol[] symbols)
    //     {
    //         this.waveFormat = waveFormat;
    //         numSamplesPerSymbol = symbols[0].Option.NumSamplesPerSymbol;

    // #if DEBUG
    //         if (symbols.Any(s => s.Option.NumSamplesPerSymbol != numSamplesPerSymbol))
    //         {
    //             throw new ArgumentException("All symbols must have the same number of samples per symbol");
    //         }
    // #endif
    //         demodulators = symbols.Select(s => new DPSKDemodulator(waveFormat, s)).ToArray();
    //         // Console.WriteLine();
    //     }
    public OFDMDemodulator(PipeReader pipeReader, WaveFormat waveFormat, DPSKSymbol[] symbols)
    {
        this.symbols = symbols;
        numSamplesPerSymbol = symbols[0].Option.NumSamplesPerSymbol;
        sampleProvider = new StreamWaveProvider(waveFormat, pipeReader.AsStream()).ToSampleProvider().ToMono();
        viewSampleProvider = new PipeViewProvider(waveFormat, pipeReader);

        SourceReader = pipeReader;

        if (symbols.Any(s => s.Option.NumSamplesPerSymbol != numSamplesPerSymbol))
        {
            throw new ArgumentException("All symbols must have the same number of samples per symbol");
        }
    }
    // public OFDMDemodulator(DPSKSymbol[] symbols)
    // {
    //     demodulators = symbols.Select(s => new DPSKDemodulator(s)).ToArray();
    //     NumSamplesPerSymbol = demodulators[0].NumSamplesPerSymbol;
    // }

    // public OFDMDemodulator(DPSKSymbol[] symbols, DemodulateLength demodulateLength)
    // {
    //     demodulators = symbols.Select(s => new DPSKDemodulator(s, demodulateLength)).ToArray();
    //     NumSamplesPerSymbol = demodulators[0].NumSamplesPerSymbol;
    // }

    // public void Init(WaveFormat waveFormat, PipeReader sampleBuffer)
    // {
    //     if (sampleProvider is not null)
    //     {
    //         throw new InvalidOperationException("sampleProvider is already initialized");
    //     }

    //     sampleProvider = new StreamWaveProvider(waveFormat, sampleBuffer.AsStream()).ToSampleProvider().ToMono();

    //     foreach (var dm in demodulators)
    //     {
    //         dm.Init(waveFormat, sampleBuffer);
    //     }
    // }

    // public OFDMDemodulator Build(WaveFormat waveFormat, PipeReader sampleBuffer)
    // {
    //     Init(waveFormat, sampleBuffer);
    //     return this;
    // }

    public bool TryReadTo(Span<byte> dst, bool advandce = true)
    {
        var buffer = new float[numSamplesPerSymbol * 8];
        var provider = advandce ? sampleProvider : viewSampleProvider;

        for (int i = 0; i < dst.Length; i += symbols.Length)
        {
            if (provider.ReadExact(buffer, 0, buffer.Length) == 0)
            {
                return false;
            }

            for (int j = 0; j < symbols.Length && i + j < dst.Length; j++)
            {
                dst[i + j] = ModulateHelper.DotProductDemodulateByte(buffer, symbols[j].Samples.Span[0].Span);
            }
        }
        return true;
    }

    // public bool Demodulate(Span<byte> dataBuffer)
    // {
    //     if (sampleProvider is null)
    //     {
    //         throw new InvalidOperationException("sampleProvider is not initialized");
    //     }

    //     var buffer = new float[NumSamplesPerSymbol * 8];

    //     for (int i = 0; i < dataBuffer.Length; i += demodulators.Length)
    //     {
    //         if (sampleProvider.ReadExact(buffer, 0, buffer.Length) == 0)
    //         {
    //             return false;
    //         }

    //         for (int j = 0; j < demodulators.Length && i + j < dataBuffer.Length; j++)
    //         {
    //             dataBuffer[i + j] = demodulators[j].Demodulate<byte>(buffer, 8);
    //         }
    //     }
    //     return true;
    // }

    // public bool Demodulate(out byte[] dataBuffer)
    // {
    //     var dataLengthInBit = demodulators[0].GetLength();
    //     dataBuffer = new byte[(int)Math.Ceiling(dataLengthInBit / 8f)];

    //     if (dataLengthInBit == 0)
    //     {
    //         return false;
    //     }

    //     return Demodulate(dataBuffer.AsSpan());
    // }

    // public bool Demodulate(ReadOnlySpan<float> samples, Span<byte> dataBuffer)
    // {
    //     if (dataBuffer.Length * 8 * numSamplesPerSymbol > samples.Length * demodulators.Length)
    //     {
    //         return false;
    //     }

    //     for (int i = 0, j = 0; i < dataBuffer.Length; i += demodulators.Length, j++)
    //     {
    //         var sampleSlice = samples.Slice(j * numSamplesPerSymbol * 8, numSamplesPerSymbol * 8);
    //         for (int k = 0; k < demodulators.Length && i + k < dataBuffer.Length; k++)
    //         {
    //             dataBuffer[i + k] = demodulators[k].Demodulate<byte>(sampleSlice, 8);
    //         }
    //     }

    //     return true;
    // }
}
