namespace CS120.Packet;

public interface IPacket<T>
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

    static virtual T Create(byte[] bytes)
    {
        throw new NotImplementedException();
    }
}

public readonly struct EmptyPacket
() : IPacket<EmptyPacket>
{
    public byte[] Bytes { get; init; } = [];
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