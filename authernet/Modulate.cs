using System.Buffers;
using System.Numerics;
using CS120.Preamble;
using CS120.Symbol;
using CS120.Utils;
using CS120.Utils.Buffer;
using CS120.Utils.Extension;
using CS120.Utils.Helpers;
using CS120.Utils.Wave;

namespace CS120.Modulate;

public interface ISequnceReader<TIn, TOut>
{
    // bool TryReadTo(Span<T> dst, bool advandce = true);
    bool TryRead<T>(ref ReadOnlySequence<TIn> inSeq, T writer)
        where T : IBufferWriter<TOut>;
    // bool TryRead(ref ReadOnlySequence<TIn> inSeq, Span<TOut> buffer);

}

public class Modulator<TPreamble, TSymbol>(TPreamble preamble, TSymbol symbol) : ISequnceReader<byte, float>
        where TPreamble : IPreamble<float>
        where TSymbol : ISymbol<float>
    {
        public bool TryRead<T>(ref ReadOnlySequence<byte> inSeq, T writer)
            where T : IBufferWriter<float>
        {
            writer.Write(preamble.Samples.Span);
            // var result = new ChunkedSequence<float>();

            // result.Append(preamble.Samples);

            foreach (var seg in inSeq)
            {
                for (int i = 0; i < seg.Length; i++)
                {
                    var data = seg.Span[i];
                    for (int j = 0; j < 8; j++)
                    {
                        // result.Append(symbol.Samples.Span[data >> j & 1]);
                        writer.Write(symbol.Samples.Span[data >> j & 1].Span);
                    }
                }
            }
            // outSeq = result;

            return true;
        }
    }

    public class OFDMModulator<TPreamble, TSymbol>(TPreamble preamble, TSymbol[] symbols) : ISequnceReader<byte, float>
        where TPreamble : IPreamble<float>
        where TSymbol : ISymbol<float>
    {
        private readonly int numSamplesPerSymbol = symbols[0].NumSamplesPerSymbol;
        public bool TryRead<T>(ref ReadOnlySequence<byte> inSeq, T writer)
            where T : IBufferWriter<float>
        {
            writer.Write(preamble.Samples.Span);
            Span<float> buffer = stackalloc float[numSamplesPerSymbol * 8];
            // Console.WriteLine(dataBuffer.Length);
            // var symbolsBuffer = new ReadOnlyMemory<float>[8];

            Span<byte> dataBuffer = stackalloc byte[symbols.Length];

            while (inSeq.Length != 0)
            {
                var readed = inSeq.ConsumeExact(dataBuffer);
                for (int i = 0; i < readed.Length; i++)
                {
                    var data = readed[i];
                    for (int j = 0; j < 8; j++)
                    {
                        var symbolSpan = symbols[i].Samples.Span[data >> j & 1].Span;
                        for (int k = 0; k < numSamplesPerSymbol; k++)
                            buffer[j * numSamplesPerSymbol + k] += symbolSpan[k];
                    }
                }
                writer.Write(buffer);
                buffer.Clear();
            }
            return true;
        }
    }

    public interface IDemodulator<TSample> : ISequnceReader<TSample, byte>
    {
        int MaxByteLength { get; }
        int SamplePerByte { get; }
        bool ISequnceReader<TSample, byte>.TryRead<T>(ref ReadOnlySequence<TSample> samples, T writer)
        {
            if (!TryGetLength(samples, out var length))
                return false;

            if (samples.Length < length * SamplePerByte)
                return false;

            var buffer = writer.GetSpan(length)[..length];

            if (TryRead(ref samples, buffer))
            {
                writer.Advance(length);
                return true;
            }

            return false;
        }
        bool TryRead(ref ReadOnlySequence<TSample> samples, Span<byte> data);
        bool TryGetLength(ReadOnlySequence<TSample> samples, out int length)
        {
            length = MaxByteLength;
            return true;
        }
    }

    public interface IDemodulator<TSample, TSize> : IDemodulator<TSample>
        where TSize : unmanaged, IBinaryInteger<TSize> {
        bool IDemodulator<TSample>.TryGetLength(ReadOnlySequence<TSample> samples, out int length)
        {
            if (samples.Length < BinaryIntegerTrait<TSize>.Size * SamplePerByte)
                goto done;

            Span<byte> buffer = stackalloc byte[BinaryIntegerTrait<TSize>.Size];
            if (TryRead(ref samples, buffer))
            {
                length = Math.Clamp(
                    int.CreateChecked(TSize.ReadLittleEndian(buffer, true)),
                    BinaryIntegerTrait<TSize>.Size,
                    MaxByteLength
                );
                return true;
            }
        done:
            length = default;
            return false;
        }
    }

    public class Demodulator<TSymbol, TSample>(TSymbol symbol, int maxByteLength) : IDemodulator<TSample>
        where TSymbol : ISymbol<TSample>
        where TSample : unmanaged, INumber<TSample>
    {
        public int MaxByteLength { get; } = maxByteLength;
        public int SamplePerByte { get; } = symbol.NumSamplesPerSymbol * 8;
        public bool TryRead(ref ReadOnlySequence<TSample> samples, Span<byte> data)
        {
            var length = data.Length;

            var buffer = ArrayPool<TSample>.Shared.Rent(SamplePerByte);
            var span = buffer.AsSpan(0, SamplePerByte);

            for (int i = 0; i < length; i++)
            {
                var readed = samples.ConsumeExact(span);

                data[i] = ModulateHelper.DotProductDemodulateByte(readed, symbol.Samples.Span[0].Span);
            }

            ArrayPool<TSample>.Shared.Return(buffer);

            return true;
        }
    }

    public class Demodulator<TSymbol, TSample, TSize>(TSymbol symbol, int maxByteLength)
        : Demodulator<TSymbol, TSample>(symbol, maxByteLength), IDemodulator<TSample, TSize>
        where TSymbol : ISymbol<TSample>
        where TSample : unmanaged, INumber<TSample>
        where TSize : unmanaged, IBinaryInteger<TSize>
    {
    }

    public class OFDMDemodulator<TSymbol, TSample>(TSymbol[] symbols, int maxByteLength) : IDemodulator<TSample>
        where TSymbol : ISymbol<TSample>
        where TSample : unmanaged, INumber<TSample>
    {
        public int MaxByteLength { get; } = maxByteLength;
        public int SamplePerByte { get; } = symbols[0].NumSamplesPerSymbol * 8;
        public bool TryRead(ref ReadOnlySequence<TSample> samples, Span<byte> data)
        {
            var length = data.Length;

            var buffer = ArrayPool<TSample>.Shared.Rent(SamplePerByte);
            var span = buffer.AsSpan(0, SamplePerByte);

            for (int i = 0; i < length; i += symbols.Length)
            {
                var readed = samples.ConsumeExact(span);

                for (int j = 0; j < symbols.Length && i + j < length; j++)
                {
                    data[i + j] = ModulateHelper.DotProductDemodulateByte(readed, symbols[j].Samples.Span[0].Span);
                }
            }

            ArrayPool<TSample>.Shared.Return(buffer);
            return true;
        }
    }

    public class OFDMDemodulator<TSymbol, TSample, TSize>(TSymbol[] symbols, int maxByteLength)
        : OFDMDemodulator<TSymbol, TSample>(symbols, maxByteLength), IDemodulator<TSample, TSize>
        where TSymbol : ISymbol<TSample>
        where TSample : unmanaged, INumber<TSample>
        where TSize : unmanaged, IBinaryInteger<TSize>
    {
    }
