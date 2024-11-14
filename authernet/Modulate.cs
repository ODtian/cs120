using System.Buffers;
using System.Numerics;
using CS120.Symbol;
using CS120.Utils;
using CS120.Utils.Helpers;
using CS120.Utils.Wave;

namespace CS120.Modulate2;

public interface ISequnceReader<TIn, TOut>
{
    // bool TryReadTo(Span<T> dst, bool advandce = true);
    bool TryRead(ref ReadOnlySequence<TIn> inSeq, out ReadOnlySequence<TOut> outSeq);
}

// public abstract class Demodulator<T>(int maxByteLength) : ISequnceReader<T, byte>
//     where T : unmanaged, INumber<T>
// {
//     protected int maxByteLength = maxByteLength;
//     public bool TryRead(ref ReadOnlySequence<T> samples, out ReadOnlySequence<byte> data)
//     {
//         if (!TryGetLength(samples, out var length))
//             goto done;

//         if (samples.Length < length)
//             goto done;

//         var dataBuffer = new byte[length];

//         if (TryRead(ref samples, dataBuffer))
//         {
//             data = new ReadOnlySequence<byte>(dataBuffer);
//             return true;
//         }

//     done:
//         data = default;
//         return false;
//     }
//     protected abstract bool TryRead(ref ReadOnlySequence<T> samples, Span<byte> data);
//     protected virtual bool TryGetLength(ReadOnlySequence<T> samples, out int length)
//     {
//         length = maxByteLength;
//         return true;
//     }
// }

// public class OFDMDemodulator<T>(DPSKSymbol<T>[] symbols, int maxByteLength)
//     : Demodulator<T>(maxByteLength)
//     where T : unmanaged, INumber<T>
// {
//     protected override sealed bool TryRead(ref ReadOnlySequence<T> samples, Span<byte> data)
//     {
//         var buffer = new T[symbols[0].Option.NumSamplesPerSymbol * 8];
//         var length = data.Length;

//         if (samples.Length < length)
//             return false;

//         for (int i = 0; i < length; i += symbols.Length)
//         {
//             samples.TryReadOrCopy(out var buf, buffer);

//             for (int j = 0; j < symbols.Length && i + j < length; j++)
//             {
//                 data[i + j] = ModulateHelper.DotProductDemodulateByte(buf, symbols[j].Samples.Span[0].Span);
//             }
//         }
//         return true;
//     }
// }

// public class OFDMDemodulator<TSample, TSize>(DPSKSymbol<TSample>[] symbols, int maxByteLength)
//     : OFDMDemodulator<TSample>(symbols, maxByteLength)
//     where TSample : unmanaged, INumber<TSample>
//     where TSize : IBinaryInteger<TSize>

// {
//     protected override bool TryGetLength(ReadOnlySequence<TSample> samples, out int length)
//     {
//         Span<byte> buffer = stackalloc byte[BinaryIntegerSizeTrait<int>.Size];
//         if (TryRead(ref samples, buffer))
//         {
//             length = Math.Min(int.CreateChecked(TSize.ReadLittleEndian(buffer, true)), maxByteLength);
//             return true;
//         }
//         length = default;
//         return false;
//     }
// }




public interface IDemodulator<TSample> : ISequnceReader<TSample, byte>
{
    int MaxByteLength { get; }
    override bool ISequnceReader<TSample, byte>.TryRead(ref ReadOnlySequence<TSample> samples, out
    ReadOnlySequence<byte> data)
    {
        if (!TryGetLength(samples, out var length))
            goto done;

        if (samples.Length < length)
            goto done;

        var dataBuffer = new byte[length];

        if (TryRead(ref samples, dataBuffer))
        {
            data = new ReadOnlySequence<byte>(dataBuffer);
            return true;
        }

    done:
        data = default;
        return false;
    }
    bool TryRead(ref ReadOnlySequence<TSample> samples, Span<byte> data);
    bool TryGetLength(ReadOnlySequence<TSample> samples, out int length)
    {
        length = MaxByteLength;
        return true;
    }
}

public interface IDemodulator<TSample, TSize> : IDemodulator<TSample> where TSize : unmanaged, IBinaryInteger<TSize>
{
    bool IDemodulator<TSample>.TryGetLength(ReadOnlySequence<TSample> samples, out int length)
    {
        Span<byte> buffer = stackalloc byte[BinaryIntegerSizeTrait<int>.Size];
        if (TryRead(ref samples, buffer))
        {
            length = Math.Min(int.CreateChecked(TSize.ReadLittleEndian(buffer, true)), MaxByteLength);
            return true;
        }
        length = default;
        return false;
    }
}

public class OFDMDemodulator<TSample>(DPSKSymbol<TSample>[] symbols, int maxByteLength)
    : IDemodulator<TSample>
    where TSample : unmanaged, INumber<TSample>
{
    public int MaxByteLength { get; } = maxByteLength;
    public bool TryRead(ref ReadOnlySequence<TSample> samples, Span<byte> data)
    {
        var buffer = new TSample[symbols[0].Option.NumSamplesPerSymbol * 8];
        var length = data.Length;

        if (samples.Length < length)
            return false;

        for (int i = 0; i < length; i += symbols.Length)
        {
            samples.TryReadOrCopy(out var buf, buffer);

            for (int j = 0; j < symbols.Length && i + j < length; j++)
            {
                data[i + j] = ModulateHelper.DotProductDemodulateByte(buf, symbols[j].Samples.Span[0].Span);
            }
        }
        return true;
    }
}

public class OFDMDemodulator<TSample, TSize>(DPSKSymbol<TSample>[] symbols, int maxByteLength)
    : OFDMDemodulator<TSample>(symbols, maxByteLength), IDemodulator<TSample, TSize>
    where TSample : unmanaged, INumber<TSample>
    where TSize : unmanaged, IBinaryInteger<TSize>
{
}
