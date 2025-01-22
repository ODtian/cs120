using System.Buffers;
using System.Numerics;
using Aether.NET.Preamble;
using Aether.NET.Symbol;
using Aether.NET.Utils.Extension;
using Aether.NET.Utils.Helpers;

namespace Aether.NET.Modulate;

public interface ISequnceReader<TIn, TOut>
{
    bool TryRead(ref ReadOnlySequence<TIn> inSeq, IBufferWriter<TOut> writer)
    {
        throw new NotImplementedException();
    }
    bool TryRead(ref ReadOnlySequence<TIn> inSeq, Span<TOut> buffer)
    {
        throw new NotImplementedException();
    }
}

public class Modulator<TPreamble, TSymbol>(TPreamble preamble, TSymbol symbol) : ISequnceReader<byte, float>
    where TPreamble : IPreamble<float>
    where TSymbol : ISymbol<float>
{
    public bool TryRead(ref ReadOnlySequence<byte> inSeq, IBufferWriter<float> writer)
    {
        writer.Write(preamble.Samples.Span);

        foreach (var seg in inSeq)
        {
            for (int i = 0; i < seg.Length; i++)
            {
                var data = seg.Span[i];
                for (int j = 0; j < 8; j++)
                    writer.Write(symbol.Samples.Span[data >> j & 1].Span);
            }
        }

        return true;
    }
}

public class OFDMModulator<TPreamble, TSymbol>(TPreamble preamble, TSymbol[] symbols) : ISequnceReader<byte, float>
    where TPreamble : IPreamble<float>
    where TSymbol : ISymbol<float>
{
    private readonly int numSamplesPerSymbol = symbols[0].NumSamplesPerSymbol;
    public bool TryRead(ref ReadOnlySequence<byte> inSeq, IBufferWriter<float> writer)
    {
        writer.Write(preamble.Samples.Span);
        Span<float> buffer = stackalloc float[numSamplesPerSymbol * 8];

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

public class Demodulator<TSymbol, TSample>(TSymbol symbol) : ISequnceReader<TSample, byte>
    where TSymbol : ISymbol<TSample>
    where TSample : unmanaged, INumber<TSample>
{
    private int SamplePerByte { get; } = symbol.NumSamplesPerSymbol * 8;
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

public class OFDMDemodulator<TSymbol, TSample>(TSymbol[] symbols) : ISequnceReader<TSample, byte>
    where TSymbol : ISymbol<TSample>
    where TSample : unmanaged, INumber<TSample>
{
    private int SamplePerByte { get; } = symbols[0].NumSamplesPerSymbol * 8;
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
