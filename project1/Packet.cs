namespace CS120.Packet;

public interface IPacket
{
    byte[] Bytes { get; }

    bool CheckValid()
    {
        return true;
    }
    byte[] Extract()
    {
        var result = new byte[Bytes.Length];
        Bytes.CopyTo(result.AsSpan());
        return result;
    }

    static virtual IPacket Create(byte[] bytes)
    {
        throw new NotImplementedException();
    }
}

public readonly struct EmptyPacket
() : IPacket
{
    public byte[] Bytes { get; init; } = [];

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