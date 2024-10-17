using System.Collections.Concurrent;
using NAudio.Wave;

namespace CS120.Extension;

static class IEnumerableExtension
{
    public static void TakeInto<T>(this IEnumerable<T> source, Span<T> buffer)
    {
        var index = 0;
        foreach (var item in source.Take(buffer.Length))
        {
            buffer[index++] = item;
        }
    }
    public static IEnumerable<T> TakeBlocked<T>(this BlockingCollection<T> source, int count)
    {

        while (!source.IsAddingCompleted && source.Count < count)
        {
        }
        return source.Take(count);
    }
}