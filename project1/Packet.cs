namespace CS120.Packet;

public interface IPacket
{
    byte[] Bytes { get; }

    static virtual IPacket Create(byte[] bytes)
    {
        throw new NotImplementedException();
    }
}

public readonly struct EmptyPacket
() : IPacket
{
    public byte[] Bytes { get; } = [];

    public static IPacket Create(byte[] bytes)
    {
        return new EmptyPacket();
    }
}

public readonly struct RawPacket
(byte[] bytes) : IPacket
{
    public byte[] Bytes { get; init; } = bytes;
    public static IPacket Create(byte[] bytes)
    {
        return new RawPacket(bytes);
    }
}