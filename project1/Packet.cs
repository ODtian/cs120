namespace CS120.Packet;

public interface IPacket<T>
{
    byte[] Bytes { get; }

    static abstract T Create(byte[] bytes);
}

public readonly struct EmptyPacket
() : IPacket<EmptyPacket>
{
    public byte[] Bytes { get; } = [];

    public static EmptyPacket Create(byte[] bytes)
    {
        return new EmptyPacket();
    }
}

public readonly struct RawPacket
(byte[] bytes) : IPacket<RawPacket>
{
    public byte[] Bytes { get; init; } = bytes;
    public static RawPacket Create(byte[] bytes)
    {
        return new RawPacket(bytes);
    }
}