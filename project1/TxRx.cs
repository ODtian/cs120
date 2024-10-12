using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Threading.Channels;
using CS120.Modulate;
using CS120.Packet;
using CS120.Preamble;
using CS120.Utils;
using NAudio.Wave;

namespace CS120.TxRx;

public interface IReceiver
{
    Stream StreamIn { get; }
    Channel<IPacket> PacketChannel { get; }
    Task Execute(CancellationToken ct);
}

public class Receiver<TDemodulator, TPacket, TPreamble> : IReceiver, IDisposable
    where TDemodulator : IDemodulator
    where TPacket : IPacket
    where TPreamble : IPreamble
{

    private readonly Pipe pipe = new(new PipeOptions(pauseWriterThreshold: 0));
    private readonly BlockingCollection<float> sampleBuffer = [];
    private readonly IPreamble preamble;
    private readonly ISampleProvider sampleProvider;

    public Stream StreamIn { get; init; }
    public Channel<IPacket> PacketChannel { get; init; } = Channel.CreateUnbounded<IPacket>();

    public Receiver(WaveFormat waveFormat)
    {
        // Console.WriteLine(waveFormat);
        StreamIn = pipe.Writer.AsStream();

        preamble = TPreamble.Create(waveFormat);
        sampleProvider = new StreamWaveProvider(waveFormat, pipe.Reader.AsStream()).ToSampleProvider().ToMono();
    }

    public void Dispose()
    {
        sampleBuffer.Dispose();
        PacketChannel.Writer.Complete();
        pipe.Reader.Complete();
    }
    private void FillSample()
    {
        try
        {
            var buffer = new float[10240];
            while (true)
            {
                var numSample = sampleProvider.Read(buffer, 0, buffer.Length);
                Console.WriteLine(sampleBuffer.Count);
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
        var preambleDetect = new PreambleDetection(preamble);
        var demodulator = TDemodulator.Create(sampleProvider.WaveFormat);

        try
        {
            while (!sampleBuffer.IsCompleted)
            {
                ct.ThrowIfCancellationRequested();

                preambleDetect.DetectPreamble(sampleBuffer);
                var data = demodulator.Demodulate(sampleBuffer);
                // foreach (var d in data)
                // {
                //     Console.WriteLine($"d {Convert.ToString(d, 2)}");
                // }
                Console.WriteLine();

                await PacketChannel.Writer.WriteAsync(TPacket.Create(data), ct);
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
    }
}