using System.Buffers;
using System.Numerics;
using Aether.NET.Preamble;

namespace Aether.NET.CarrierSense;

public class CarrierQuietSensor<TSample>(float threshold = 0.05f) : ISequnceSearcher<TSample>
    where TSample : INumber<TSample>
{
    private readonly TSample threshold = TSample.CreateChecked(threshold);
    public bool TrySearch(ref ReadOnlySequence<TSample> buffer)
    {
        if (buffer.IsEmpty)
            return false;

        var pos = buffer.Start;
        while (buffer.TryGet(ref pos, out var next))
        {
            if (pos.Equals(default))
            {
                for (var i = next.Length - Math.Min(next.Length, 64); i < next.Length; i++)
                {
                    if (TSample.Abs(next.Span[i]) > threshold)
                    {
                        return true;
                    }
                }
            }
        }
        // while (buffer.TryGet(ref pos, out var next))
        // {
        //     for (var i = 0; i < next.Length; i++)
        //     {
        //         // Console.WriteLine($"CarrierQuietSensor1: Found carrier{TSample.Abs(next.Span[i])}");
        //         if (TSample.Abs(next.Span[i]) > threshold)
        //         {
        //             // Console.WriteLine($"CarrierQuietSensor1: Found carrier {TSample.Abs(next.Span[i])}");
        //             buffer = buffer.Slice(i);
        //             return true;
        //         }
        //     }
        //     buffer = buffer.Slice(next.Length);
        // }
        return false;
    }
}