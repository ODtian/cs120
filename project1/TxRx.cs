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
    private readonly int quiet;
    public ChannelWriter<TPacket> Packets { get; }
    public ISampleProvider Samples { get; }

    public Transmitter(WaveFormat waveFormat, IPreamble preamble, IModulator modulator, float quiet = 1f)
    {
        this.preamble = preamble;
        this.modulator = modulator;
        this.quiet = (int)(waveFormat.SampleRate * quiet);

        Packets = channel.Writer;
        Samples = new BlockingCollectionSampleProvider(
            WaveFormat.CreateIeeeFloatWaveFormat(waveFormat.SampleRate, 1), sampleBuffer
        );
    }

    public async Task Execute(CancellationToken ct)
    {
        // try
        // {
        await Task.Run(
            async () =>
            {
                await foreach (var data in channel.Reader.ReadAllAsync(ct))
                {
                    foreach (var p in preamble.Samples)
                    {
                        sampleBuffer.Add(p);
                    }

                    modulator.Modulate(data.Bytes, sampleBuffer);
                    // quiet.AsSpan().CopyTo(sampleBuffer);

                    for (int i = 0; i < quiet; i++)
                    {
                        sampleBuffer.Add(0);
                    }
                }
                sampleBuffer.CompleteAdding();
            },
            ct
        );
        // }
        // catch (OperationCanceledException)
        // {
        //     Console.WriteLine("Canceled by user");
        // }
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
    private readonly bool lowLatency;

    // public Stream StreamIn { get; }

    public ISampleProvider Samples { private get; init; }
    public ChannelReader<TPacket> Packets { get; }
    // public Channel<TPacket> PacketChannel { get; } = Channel.CreateUnbounded<TPacket>();
    public Receiver(
        ISampleProvider sampleProvider,
        PreambleDetection preambleDetection,
        IDemodulator demodulator,
        bool lowLatency = false
    )
    {

        this.preambleDetection = preambleDetection;
        this.demodulator = demodulator;
        this.lowLatency = lowLatency;

        Packets = channel.Reader;
        Samples = sampleProvider;
    }

    public void Dispose()
    {
        sampleBuffer.Dispose();
    }
    private void FillSample(CancellationToken ct)
    {
        try
        {
            var buffer = new float[lowLatency ? preambleDetection.PreambleLength * 2 : 1920];
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                var numSample = Samples.Read(buffer, 0, buffer.Length);
                // Console.WriteLine(numSample);
                // Console.WriteLine(sampleBuffer.Count);
                if (numSample == 0)
                {
                    sampleBuffer.CompleteAdding();
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
        }
    }
    public async Task Execute(CancellationToken ct)
    {
        var fillTask = Task.Run(() => FillSample(ct), ct);
        // var preambleDetect = new PreambleDetection(preamble);
        // var demodulator = TDemodulator.Create(sampleProvider.WaveFormat);

        try
        {
            await Task.Run(
                async () =>
                {
                    while (true)
                    {
                        ct.ThrowIfCancellationRequested();

                        preambleDetection.DetectPreamble(sampleBuffer, ct);
                        var data = demodulator.Demodulate(sampleBuffer);
                        // foreach (var d in data)
                        // {
                        //     Console.WriteLine($"d {Convert.ToString(d, 2)}");
                        // }

                        await channel.Writer.WriteAsync(TPacket.Create(data), ct);
                    }
                },
                ct
            );
            await fillTask;
        }
        catch (InvalidOperationException)
        {
            Console.WriteLine("End of stream");
            channel.Writer.Complete();
        }
        // catch (OperationCanceledException)
        // {
        //     Console.WriteLine("Canceled by user");
        // }
    }
}