using System.Buffers;
using System.Buffers.Binary;
using System.IO.Hashing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CS120.Mac;
using CS120.Utils;
using CS120.Utils.Buffer;
using CS120.Utils.Codec;
using CS120.Utils.Extension;
using Nerdbank.Streams;
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
public static class PackHelper
{
    public static bool TryReadBinaryLittleEndian<T>(this ReadOnlySequence<byte> packet, out T val, bool isUnsigned)
        where T : IBinaryInteger<T>
    {
        if (packet.Length < BinaryIntegerTrait<T>.Size)
        {
            val = T.Zero;
            return false;
        }
        val = packet.ReadBinaryLittleEndian<T>(isUnsigned);
        return true;
    }

    public static T ReadBinaryLittleEndian<T>(this ReadOnlySequence<byte> packet, bool isUnsigned)
        where T : IBinaryInteger<T>
    {
        T val;
        if (packet.First.Length < BinaryIntegerTrait<T>.Size)
        {
            Span<byte> buffer = stackalloc byte[BinaryIntegerTrait<T>.Size];
            packet.Slice(0, BinaryIntegerTrait<T>.Size).CopyTo(buffer);
            val = T.ReadLittleEndian(buffer, isUnsigned);
        }
        else
            val = T.ReadLittleEndian(packet.FirstSpan[..BinaryIntegerTrait<T>.Size], isUnsigned);

        return val;
    }
}
public static class PacketExtension
{
    public static ReadOnlySequence<byte> RSEncode(this ReadOnlySequence<byte> packet, int eccNums)
    {

        return new(CodecRS.Encode(packet.ToArray(), eccNums));
    }

    public static ReadOnlySequence<byte> RSDecode(this ReadOnlySequence<byte> packet, int eccNums, out bool valid)
    {
        return new(CodecRS.Decode(packet.ToArray(), eccNums, out valid));
    }

    public static ReadOnlySequence<byte> C4B5BEncode(this ReadOnlySequence<byte> packet)
    {
        return new(Codec4B5B.Encode(packet.ToArray()));
    }

    public static ReadOnlySequence<byte> C4B5BDecode(this ReadOnlySequence<byte> packet)
    {
        return new(Codec4B5B.Decode(packet.ToArray()));
    }

    public static ReadOnlySequence<byte> ScramblerEncode(this ReadOnlySequence<byte> packet)
    {
        var result = packet.ToArray();
        CodecScrambler.Scramble(result);
        return new(result);
    }

    public static ReadOnlySequence<byte> ScramblerDecode(this ReadOnlySequence<byte> packet) => ScramblerEncode(packet);

    public static ReadOnlySequence<byte> LengthEncode<T>(this ReadOnlySequence<byte> packet, int? padding = null)
        where T : IBinaryInteger<T>
    {
        var length = T.CreateChecked(packet.Length + BinaryIntegerTrait<T>.Size);
        var header = new byte[BinaryIntegerTrait<T>.Size];

        length.WriteLittleEndian(header);
        var result = new ChunkedSequence<byte>();
        result.Append(header);
        result.Append(packet);

        if (padding is not null)
            result.Append(new byte[(int)(padding - packet.Length)]);

        return result;
    }

    public static ReadOnlySequence<byte> LengthDecode<T>(this ReadOnlySequence<byte> packet, out bool valid)
        where T : IBinaryInteger<T>
    {
        packet.LengthGet<T>(out valid, out var length);
        if (valid)
            return packet.Slice(BinaryIntegerTrait<T>.Size, length -= BinaryIntegerTrait<T>.Size);
        return packet;
    }

    public static ReadOnlySequence<byte> LengthGet<T>(
        this ReadOnlySequence<byte> packet, out bool valid, out int length
    )
        where T : IBinaryInteger<T>
    {
        valid = packet.TryReadBinaryLittleEndian(out T val, true);
        length = int.CreateChecked(val);
        valid = valid && length <= packet.Length && length >= BinaryIntegerTrait<T>.Size;

        return packet;
    }

    public static ReadOnlySequence<byte> IDEncode<T>(this ReadOnlySequence<byte> packet, int id)
        where T : IBinaryInteger<T>
    {
        var header = new byte[BinaryIntegerTrait<T>.Size];
        T.CreateChecked(id).WriteLittleEndian(header);
        var result = new ChunkedSequence<byte>();
        result.Append(header);
        result.Append(packet);

        return result;
    }

    public static ReadOnlySequence<byte> IDDecode<T>(this ReadOnlySequence<byte> packet, out int id)
        where T : IBinaryInteger<T>
    {
        packet.IDGet<T>(out id);
        return packet.Slice(BinaryIntegerTrait<T>.Size);
    }

    public static ReadOnlySequence<byte> IDGet<T>(this ReadOnlySequence<byte> packet, out int id)
        where T : IBinaryInteger<T>
    {
        id = int.CreateChecked(packet.ReadBinaryLittleEndian<T>(true));
        return packet;
    }

    public static ReadOnlySequence<byte> MacEncode(this ReadOnlySequence<byte> packet, in MacFrame mac)
    {
        var header = mac.AsBytes().ToArray();
        // var result = new byte[packet.Length + macSpan.Length];
        var result = new ChunkedSequence<byte>();
        result.Append(header);
        result.Append(packet);
        return result;
    }

    public static ReadOnlySequence<byte> MacDecode(this ReadOnlySequence<byte> packet, out MacFrame mac)
    {
        packet.MacGet(out mac);
        return packet.Slice(Unsafe.SizeOf<MacFrame>());
    }

    public static ReadOnlySequence<byte> MacGet(this ReadOnlySequence<byte> packet, out MacFrame mac)
    {
        // ReadOnlySpan<byte> buffer;
        if (packet.First.Length < Unsafe.SizeOf<MacFrame>())
        {
            Span<byte> buffer = stackalloc byte[Unsafe.SizeOf<MacFrame>()];
            packet.CopyTo(buffer);
            mac = MemoryMarshal.Read<MacFrame>(buffer);
        }
        else
            mac = MemoryMarshal.Read<MacFrame>(packet.FirstSpan);
        return packet;
    }

    public static ReadOnlySequence<byte> CrcEncode(this ReadOnlySequence<byte> packet)
    {
        var crc = new Crc32();
        foreach (var seg in packet)
            crc.Append(seg.Span);

        var result = new ChunkedSequence<byte>();
        result.Append(packet);
        result.Append(crc.GetCurrentHash());
        return result;
    }

    public static ReadOnlySequence<byte> CrcDecode(this ReadOnlySequence<byte> packet, out bool valid)
    {
        if (packet.Length < 4)
        {
            valid = false;
            return packet;
        }
        var crc = new Crc32();
        var result = packet.Slice(0, packet.Length - 4);
        foreach (var seg in result)
            crc.Append(seg.Span);
        Span<byte> hash = stackalloc byte[4];
        packet.Slice(packet.Length - 4).CopyTo(hash);
        valid = crc.GetCurrentHashAsUInt32() == BinaryPrimitives.ReadUInt32LittleEndian(hash);
        return result;
    }
}

// public static class BytePacketExtension
// {
//     public static byte[] RSEncode(this byte[] packet, int eccNums)
//     {
//         return CodecRS.Encode(packet, eccNums);
//     }

//     public static byte[] RSDecode(this byte[] packet, int eccNums, out bool valid)
//     {
//         return CodecRS.Decode(packet, eccNums, out valid);
//     }

//     public static byte[] C4B5BEncode(this byte[] packet)
//     {
//         return Codec4B5B.Encode(packet);
//     }

//     public static byte[] C4B5BDecode(this byte[] packet)
//     {
//         return Codec4B5B.Decode(packet);
//     }

//     public static byte[] LengthEncode<T>(this byte[] packet, int? padding = null)
//         where T : IBinaryInteger<T>
//     {
//         var length = T.CreateChecked(packet.Length + BinaryIntegerTrait<T>.Size);
//         var result = new byte[BinaryIntegerTrait<T>.Size + (padding ?? packet.Length)];
//         length.WriteLittleEndian(result.AsSpan(0, BinaryIntegerTrait<T>.Size));
//         packet.CopyTo(result.AsSpan(BinaryIntegerTrait<T>.Size));

//         return result;
//     }

//     public static byte[] LengthDecode<T>(this byte[] packet, out bool valid)
//         where T : IBinaryInteger<T>
//     {
//         packet.LengthGet<T>(out valid, out var length);
//         // length = Math.Min(length, packet.Length - BinaryIntegerSizeTrait<T>.Size);
//         byte[]? result = packet[BinaryIntegerTrait<T>.Size..Math.Min(length, packet.Length)];
//         // Console.WriteLine($"LengthDecode: {length} {result.Length} 11111111111");
//         return result;
//     }

//     public static byte[] LengthGet<T>(this byte[] packet, out bool valid, out int length)
//         where T : IBinaryInteger<T>
//     {
//         length = int.CreateChecked(T.ReadLittleEndian(packet.AsSpan(0, BinaryIntegerTrait<T>.Size), true));
//         valid = length <= packet.Length;
//         return packet;
//     }

//     public static byte[] IDEncode<T>(this byte[] packet, int id)
//         where T : IBinaryInteger<T>
//     {
//         var result = new byte[BinaryIntegerTrait<T>.Size + packet.Length];
//         T.CreateChecked(id).WriteLittleEndian(result.AsSpan(0, BinaryIntegerTrait<T>.Size));
//         // Console.WriteLine($"abc {id} {result[0]}");

//         packet.CopyTo(result.AsSpan(BinaryIntegerTrait<T>.Size));
//         return result;
//     }

//     public static byte[] IDDecode<T>(this byte[] packet, out int id)
//         where T : IBinaryInteger<T>
//     {
//         packet.IDGet<T>(out id);
//         return packet.AsSpan(BinaryIntegerTrait<T>.Size).ToArray();
//     }

//     public static byte[] IDGet<T>(this byte[] packet, out int id)
//         where T : IBinaryInteger<T>
//     {
//         id = int.CreateChecked(T.ReadLittleEndian(packet.AsSpan(0, BinaryIntegerTrait<T>.Size), true));
//         return packet;
//     }

//     public static byte[] MacEncode(this byte[] packet, in MacFrame mac)
//     {
//         var macSpan = mac.AsBytes();
//         var result = new byte[packet.Length + macSpan.Length];
//         macSpan.CopyTo(result);
//         packet.CopyTo(result.AsSpan(macSpan.Length));
//         return result;
//     }

//     public static byte[] MacDecode(this byte[] packet, out MacFrame mac)
//     {
//         packet.MacGet(out mac);
//         return packet[Unsafe.SizeOf<MacFrame>()..];
//     }

//     public static byte[] MacGet(this byte[] packet, out MacFrame mac)
//     {
//         mac = MemoryMarshal.Read<MacFrame>(packet);
//         return packet;
//     }
// }