using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CS120.Mac;
using CS120.Utils;
using CS120.Utils.Codec;
using CS120.Utils.Extension;
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

public static class BytePacketExtension
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

    public static byte[] LengthEncode<T>(this byte[] packet, int? padding = null)
        where T : IBinaryInteger<T>
    {
        var length = T.CreateChecked(packet.Length + BinaryIntegerSizeTrait<T>.Size);
        var result = new byte[BinaryIntegerSizeTrait<T>.Size + (padding ?? packet.Length)];
        length.WriteLittleEndian(result.AsSpan(0, BinaryIntegerSizeTrait<T>.Size));
        packet.CopyTo(result.AsSpan(BinaryIntegerSizeTrait<T>.Size));

        return result;
    }

    public static byte[] LengthDecode<T>(this byte[] packet, out bool valid)
        where T : IBinaryInteger<T>
    {
        packet.LengthGet<T>(out valid, out var length);
        // length = Math.Min(length, packet.Length - BinaryIntegerSizeTrait<T>.Size);
        byte[]? result = packet[BinaryIntegerSizeTrait<T>.Size..Math.Min(length, packet.Length)];
        // Console.WriteLine($"LengthDecode: {length} {result.Length} 11111111111");
        return result;
    }

    public static byte[] LengthGet<T>(this byte[] packet, out bool valid, out int length)
        where T : IBinaryInteger<T>
    {
        length = int.CreateChecked(T.ReadLittleEndian(packet.AsSpan(0, BinaryIntegerSizeTrait<T>.Size), true));
        valid = length <= packet.Length;
        return packet;
    }

    public static byte[] IDEncode<T>(this byte[] packet, int id)
        where T : IBinaryInteger<T>
    {
        var result = new byte[BinaryIntegerSizeTrait<T>.Size + packet.Length];
        T.CreateChecked(id).WriteLittleEndian(result.AsSpan(0, BinaryIntegerSizeTrait<T>.Size));
        // Console.WriteLine($"abc {id} {result[0]}");

        packet.CopyTo(result.AsSpan(BinaryIntegerSizeTrait<T>.Size));
        return result;
    }

    public static byte[] IDDecode<T>(this byte[] packet, out int id)
        where T : IBinaryInteger<T>
    {
        packet.IDGet<T>(out id);
        return packet.AsSpan(BinaryIntegerSizeTrait<T>.Size).ToArray();
    }

    public static byte[] IDGet<T>(this byte[] packet, out int id)
        where T : IBinaryInteger<T>
    {
        id = int.CreateChecked(T.ReadLittleEndian(packet.AsSpan(0, BinaryIntegerSizeTrait<T>.Size), true));
        return packet;
    }

    public static byte[] MacEncode(this byte[] packet, in MacFrame mac)
    {
        var macSpan = mac.AsBytes();
        var result = new byte[packet.Length + macSpan.Length];
        macSpan.CopyTo(result);
        packet.CopyTo(result.AsSpan(macSpan.Length));
        return result;
    }

    public static byte[] MacDecode(this byte[] packet, out MacFrame mac)
    {
        packet.MacGet(out mac);
        return packet[Unsafe.SizeOf<MacFrame>()..];
    }

    public static byte[] MacGet(this byte[] packet, out MacFrame mac)
    {
        mac = MemoryMarshal.Read<MacFrame>(packet);
        return packet;
    }
}