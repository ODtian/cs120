using System.Buffers;
using System.Buffers.Binary;
using System.IO.Hashing;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Aether.NET.Mac;
using Aether.NET.Utils;
using Aether.NET.Utils.Buffer;
using Aether.NET.Utils.Codec;
using Aether.NET.Utils.Extension;
namespace Aether.NET.Packet;

public static class PacketHelper
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
