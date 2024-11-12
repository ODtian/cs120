using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CS120.Extension;

public static class MemoryExtension
{
    public static Span<byte> AsBytes<T>(this T value) where T : unmanaged
    {
        return MemoryMarshal.CreateSpan(ref Unsafe.As<T, byte>(ref value), Unsafe.SizeOf<T>());
    }
}