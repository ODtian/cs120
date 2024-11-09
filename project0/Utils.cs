using System.Collections.Concurrent;
using NAudio.Wave;

namespace CS120.Utils;

public class StreamWaveProvider
(WaveFormat waveFormat, Stream stream) : IWaveProvider
{
    public WaveFormat WaveFormat { get; init; } = waveFormat;
    public Stream Stream { get; init; } = stream;

    public int Read(byte[] buffer, int offset, int count)
    {
        // Stream.L
        return Stream.Read(buffer, offset, count);
    }
}
