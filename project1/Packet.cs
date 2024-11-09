using System.Numerics;
using CS120.Extension;
using CS120.Utils;
using STH1123.ReedSolomon;
namespace CS120.Packet;

// public interface IPacket<T>
// {
//     byte[] Bytes { get; }

//     static abstract T Create(byte[] bytes);

//     public abstract TNext Convert<TNext>() where TNext : IPacket<TNext>;
// }

// // public abstract class ConvertAblePacket : IPacket<ConvertAblePacket>{

// // }

// public readonly struct EmptyPacket
// () : IPacket<EmptyPacket>
// {
//     public byte[] Bytes { get; } = [];

//     public static EmptyPacket Create(byte[] bytes)
//     {
//         return new EmptyPacket();
//     }

//     public TNext Convert<TNext>()
//         where TNext : IPacket<TNext> => TNext.Create(Bytes);
// }

// public readonly struct RawPacket
// (byte[] bytes) : IPacket<RawPacket>
// {
//     public byte[] Bytes { get; init; } = bytes;
//     public static RawPacket Create(byte[] bytes)
//     {
//         return new RawPacket(bytes);
//     }
//     public TNext Convert<TNext>()
//     where TNext : IPacket<TNext> => TNext.Create(Bytes);
// }

// public readonly struct RSEncodePacket : IPacket<RSEncodePacket>
// {
//     public byte[] Bytes { get; init; }
//     public static RSEncodePacket Create(byte[] bytes)
//     {
//         return new RSEncodePacket() { Bytes = CodecRS.Encode(bytes) };
//     }
//     public TNext Convert<TNext>()
//     where TNext : IPacket<TNext> => TNext.Create(Bytes);
// }

// public readonly struct RSDecodePacket : IPacket<RSDecodePacket>
// {
//     public byte[] Bytes { get; init; }
//     public bool? Valid { get; private init; }
//     public static RSDecodePacket Create(byte[] bytes)
//     {
//         var valid = CodecRS.Decode(bytes, out var data);
//         return new RSDecodePacket() { Bytes = data, Valid = valid };
//     }
//     public TNext Convert<TNext>()
//     where TNext : IPacket<TNext> => TNext.Create(Bytes);
// }
// // public readonly struct RSPacket : IPacket<RSPacket>
// // {

// //     // private const int dataShards = 7;

// //     // private static readonly ReedSolomon rs = new(dataShards, parityShards);
// //     public byte[] Bytes { get; init; }
// //     public bool? Valid { get; private init; }

// //     public static RSPacket Create(byte[] rawBytes)
// //     {
// //         return Decode(rawBytes);
// //     }

// //     public static RSPacket Encode(byte[] bytes)
// //     {
// //         return new RSPacket() { Bytes = RSPacketConfig.encoder.EncodeEx(bytes, RSPacketConfig.eccNums) };
// //     }

// //     public static RSPacket Decode(byte[] bytes)
// //     {
// //         var result = rsd.DecodeEx(bytes, eccNums, out var data);
// //         return new RSPacket() { Bytes = data, Valid = result };
// //     }
// // }

// public readonly struct C4B5BEncodePacket : IPacket<C4B5BEncodePacket>
// {
//     public byte[] Bytes { get; init; }
//     public static C4B5BEncodePacket Create(byte[] bytes)
//     {
//         return new C4B5BEncodePacket() { Bytes = Codec4B5B.Encode(bytes) };
//     }

//     public TNext Convert<TNext>()
//     where TNext : IPacket<TNext> => TNext.Create(Bytes);
// }

// public readonly struct C4B5BDecodePacket : IPacket<C4B5BDecodePacket>
// {
//     public byte[] Bytes { get; init; }
//     public static C4B5BDecodePacket Create(byte[] bytes)
//     {
//         return new C4B5BDecodePacket() { Bytes = Codec4B5B.Decode(data: bytes) };
//     }
//     public TNext Convert<TNext>()
//     where TNext : IPacket<TNext> => TNext.Create(Bytes);
// }

// public readonly struct LengthEncodePacket : IPacket<LengthEncodePacket>
// {
//     public byte[] Bytes { get; init; }

//     public LengthEncodePacket(byte[] bytes, int padding)
//     {
//         // Console.WriteLine($"LengthEncodePacket: {(ushort)bytes.Length}");
//         var length = BitConverter.GetBytes((ushort)bytes.Length);
//         Bytes = [.. length, .. bytes, .. new byte[padding - bytes.Length]];
//     }
//     public static LengthEncodePacket Create(byte[] bytes)
//     {
//         var length = BitConverter.GetBytes((ushort)bytes.Length);
//         return new LengthEncodePacket() { Bytes = [.. length, .. bytes] };
//     }

//     public TNext Convert<TNext>()
//         where TNext : IPacket<TNext> => TNext.Create(Bytes);
// }

// public readonly struct LengthDecodePacket : IPacket<LengthDecodePacket>
// {
//     public byte[] Bytes { get; init; }
//     public bool Valid { get; private init; }
//     public static LengthDecodePacket Create(byte[] bytes)
//     {
//         var length = BitConverter.ToUInt16(bytes.AsSpan(0, 2));
//         var valid = length <= bytes.Length - 2;
//         return new LengthDecodePacket() { Bytes = bytes.AsSpan(2, Math.Min(length, bytes.Length - 2)).ToArray(),
//         Valid = valid };
//     }

//     public TNext Convert<TNext>()
//     where TNext : IPacket<TNext> => TNext.Create(Bytes);
// }

static class BytePacketExtension
{
    public static byte[] RSEncode(this byte[] packet, int eccNums)
    {
        return CodecRS.Encode(packet, eccNums);
    }

    public static byte[] RSDecode(this byte[] packet, int eccNums, out bool valid)
    {
        return CodecRS.Decode(packet, eccNums, out valid);
    }

    public static byte[] C4B5BEncode(this byte[] packet)
    {
        return Codec4B5B.Encode(packet);
    }

    public static byte[] C4B5BDecode(this byte[] packet)
    {
        return Codec4B5B.Decode(packet);
    }

    public static byte[] LengthEncode(this byte[] packet, int padding)
    {
        var length = BitConverter.GetBytes((ushort)packet.Length);
        return [..length, ..packet, ..new byte[padding - packet.Length]];
    }

    public static byte[] LengthDecode(this byte[] packet, out bool valid)
    {
        var length = BitConverter.ToUInt16(packet.AsSpan(0, 2));
        // Console.WriteLine($"LengthDecode: {packet[0]} {packet[1]} {length}");
        valid = length <= packet.Length - 2;
        return packet.AsSpan(2, Math.Min(length, packet.Length - 2)).ToArray();
    }

    public static byte[] IDEncode(this byte[] packet, int id)
    {
        return [..BitConverter.GetBytes((ushort)id), ..packet];
    }

    public static byte[] IDDecode(this byte[] packet, out int id)
    {
        var idd = BitConverter.ToUInt16(packet.AsSpan(0, 2));
        id = (int)idd;
        return packet.AsSpan(2).ToArray();
    }
}