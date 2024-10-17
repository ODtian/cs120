using ReedSolomonCodes;
namespace CS120.Packet;

public interface IPacket<T>
{
    byte[] Bytes { get; }

    static abstract T Create(byte[] rawBytes);
}

public readonly struct EmptyPacket
() : IPacket<EmptyPacket>
{
    public byte[] Bytes { get; } = [];

    public static EmptyPacket Create(byte[] rawBytes)
    {
        return new EmptyPacket();
    }
}

public readonly struct RawPacket
(byte[] bytes) : IPacket<RawPacket>
{
    public byte[] Bytes { get; init; } = bytes;
    public static RawPacket Create(byte[] rawBytes)
    {
        return new RawPacket(rawBytes);
    }
}

public readonly struct RSPacket : IPacket<RSPacket>
{

    private const int dataShards = 7;
    private const int parityShards = 1;
    // private static readonly ReedSolomon rs = new(dataShards, parityShards);
    // private static readonly GenericGF rs = new(8, 7, 0);
    // private static readonly ReedSolomonEncoder rse = new(rs);
    // private static readonly ReedSolomonDecoder rsd = new(rs);
    public byte[] Bytes { get; init; }

    public static RSPacket Create(byte[] rawBytes)
    {
        return Decode(rawBytes);
    }

    public static RSPacket Encode(byte[] bytes)
    {
        // var s = rs.ManagedEncode(bytes, dataShards, parityShards);
        // var s = rse.Encode(bytes.AsEnumerable().ToArray(), 9);
        // for (int i = 0; i < bytes.Length; i++) {
        //     Console.WriteLine(Convert.ToString(bytes[i], 2).PadLeft(8, '0'));
        // }
        var s = ReedSolomon255X239.Encode(bytes);
        // if (s == null)
        // {
        //     Console.WriteLine("s is null");
        // }
        // Console.WriteLine(s?.Length);

        // foreach (var r in s)
        // {
        //     foreach (var x in r)
        //     {
        //         Console.WriteLine(Convert.ToString(x, 2).PadLeft(8, '0'));
        //     }
        //     Console.WriteLine(r.Length);
        // }
        // Console.WriteLine(s.Select(r => r.Length).ToArray());
        return new RSPacket() { Bytes = s ?? [] };
    }

    public static RSPacket Decode(byte[] bytes)
    {
        // var s = bytes.Chunk(15).ToArray();
        // foreach (var r in s)
        // {
        //     foreach (var x in r)
        //     {
        //         Console.WriteLine(Convert.ToString(x, 2).PadLeft(8, '0'));
        //     }
        //     Console.WriteLine(r.Length);
        // }
        var s = ReedSolomon255X239.Decode(bytes);
        return new RSPacket() { Bytes = s ?? [] };
    }
    // public TTo Convert<TFrom, TTo>(this TFrom from)
    //     where TFrom : IPacket<TFrom>
    //     where TTo : IPacket<TTo>
    // {
    //     return TTo.Create(from.Bytes);
    // }
}