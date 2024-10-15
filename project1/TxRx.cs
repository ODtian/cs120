using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Threading.Channels;
using CS120.Modulate;
using CS120.Packet;
using CS120.Preamble;
using CS120.Utils;
using NAudio.Wave;

namespace CS120.TxRx;

public interface ITransmitter<T>
    where T : IPacket<T> {
    public ChannelWriter<T> Packets { get; }
    public ISampleProvider Samples { get; }
    Task Execute(CancellationToken ct);
}

public class Transmitter<TPacket> : ITransmitter<TPacket>, IDisposable
    where TPacket : IPacket<TPacket>
{
    private readonly Channel<TPacket> channel = Channel.CreateUnbounded<TPacket>();
    private readonly BlockingCollection<float> sampleBuffer = [];
    private readonly IPreamble preamble;
    private readonly IModulator modulator;

    public ChannelWriter<TPacket> Packets { get; }
    public ISampleProvider Samples { get; }

    public Transmitter(WaveFormat waveFormat, IPreamble preamble, IModulator modulator)
    {
        this.preamble = preamble;
        this.modulator = modulator;

        Packets = channel.Writer;
        Samples = new BlockingCollectionSampleProvider(
            WaveFormat.CreateIeeeFloatWaveFormat(waveFormat.SampleRate, 1), sampleBuffer
        );
    }

    public async Task Execute(CancellationToken ct)
    {
        try
        {
            await foreach (var data in channel.Reader.ReadAllAsync(ct))
            {
                foreach (var p in preamble.Samples)
                {
                    sampleBuffer.Add(p, ct);
                }

                modulator.Modulate(data.Bytes, sampleBuffer);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Canceled by user");
        }
        finally
        {
            sampleBuffer.CompleteAdding();
        }
    }

    public void Dispose()
    {
        sampleBuffer.Dispose();
    }
}

public interface IReceiver<T>
    where T : IPacket<T> {
    // Stream StreamIn { get; }
    ISampleProvider Samples { init; }
    ChannelReader<T> Packets { get; }
    Task Execute(CancellationToken ct);
}

public class Receiver<TPacket> : IReceiver<TPacket>, IDisposable
// where TDemodulator : IDemodulator
    where TPacket : IPacket<TPacket>
// where TPreamble : IPreamble
{

    // private readonly Pipe pipe = new(new PipeOptions(pauseWriterThreshold: 0));
    private readonly Channel<TPacket> channel = Channel.CreateUnbounded<TPacket>();
    private readonly BlockingCollection<float> sampleBuffer = [];
    private readonly PreambleDetection preambleDetection;
    private readonly IDemodulator demodulator;

    // public Stream StreamIn { get; }

    public ISampleProvider Samples { private get; init; }
    public ChannelReader<TPacket> Packets { get; }
    // public Channel<TPacket> PacketChannel { get; } = Channel.CreateUnbounded<TPacket>();
    public Receiver(ISampleProvider sampleProvider, PreambleDetection preambleDetection, IDemodulator demodulator)
    {

        this.preambleDetection = preambleDetection;
        this.demodulator = demodulator;

        Packets = channel.Reader;
        Samples = sampleProvider;
    }

    public void Dispose()
    {
        sampleBuffer.Dispose();
    }
    private void FillSample()
    {
        try
        {
            var buffer = new float[1024];
            while (true)
            {
                var numSample = Samples.Read(buffer, 0, buffer.Length);
                // Console.WriteLine(sampleBuffer.Count);
                if (numSample == 0)
                {
                    Console.WriteLine("stream close");
                    break;
                }

                foreach (var sample in buffer.AsSpan(0, numSample))
                {
                    sampleBuffer.Add(sample);
                }
            }
        }
        finally
        {
            Console.WriteLine("End fill job");
            sampleBuffer.CompleteAdding();
        }
    }
    public async Task Execute(CancellationToken ct)
    {
        var fillTask = Task.Run(FillSample, ct);
        // var preambleDetect = new PreambleDetection(preamble);
        // var demodulator = TDemodulator.Create(sampleProvider.WaveFormat);

        try
        {
            while (!sampleBuffer.IsCompleted)
            {
                ct.ThrowIfCancellationRequested();

                preambleDetection.DetectPreamble(sampleBuffer);
                var data = demodulator.Demodulate(sampleBuffer);
                // foreach (var d in data)
                // {
                //     Console.WriteLine($"d {Convert.ToString(d, 2)}");
                // }
                Console.WriteLine();

                await channel.Writer.WriteAsync(TPacket.Create(data), ct);
            }
            await fillTask;
        }
        catch (InvalidOperationException)
        {
            Console.WriteLine("End of stream");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("Canceled by user");
        }
        finally
        {
            channel.Writer.Complete();
        }
    }
}