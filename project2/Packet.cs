
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CS120.Extension;
using CS120.Mac;

namespace CS120.Packet;

public static class BytePacketExtension
{
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