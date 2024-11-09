using System.Buffers;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using CS120.Extension;
using CS120.Modulate;
using CS120.Packet;
using CS120.Preamble;
using CS120.Utils;
using NAudio.Wave;

namespace CS120.TxRx;

public interface ITransmitter
{
    public ChannelWriter<byte[]> Packets { get; }
    // public PipeReader Samples { get; }
    Task Execute(CancellationToken ct);
}

public class Transmitter : ITransmitter, IDisposable
{
    // private readonly Pipe pipe = new(new PipeOptions(pauseWriterThreshold: 0));
    private readonly Channel<byte[]> channel = Channel.CreateUnbounded<byte[]>();
    private readonly IPreamble preamble;
    private readonly IPipeWriter<byte> modulator;
    private readonly PipeWriter pipeWriter;
    private readonly byte[] quietBuffer;
    public ChannelWriter<byte[]> Packets { get; }
    // public PipeReader Samples { get; }

    // public Transmitter(
    //     WaveFormat waveFormat, IPreamble preamble, IWriterBuilder<IModulator> modulatorBuilder, int quietSamples =
    //     4800
    // )
    public Transmitter(
        WaveFormat waveFormat,
        PipeWriter pipeWriter,
        IPreamble preamble,
        IPipeWriter<byte> modulator,
        // IPipeWriter<byte> bufferWriter,
        int quietSamples = 4800
    )
    {
        this.pipeWriter = pipeWriter;
        this.preamble = preamble;
        this.modulator = modulator;

        quietBuffer = new byte[waveFormat.ConvertSamplesToByteSize(quietSamples)];
        // quietBuffer.AsSpan().Clear();
        // this.quiet = quiet;

        Packets = channel.Writer;
        // Samples = pipe.Reader;
        // Samples = new BlockingCollectionSampleProvider(
        //     WaveFormat.CreateIeeeFloatWaveFormat(waveFormat.SampleRate, 1), sampleBuffer
        // );
    }

    public async Task Execute(CancellationToken ct)
    {
        try
        {
            await Task.Run(
                async () =>
                {
                    await foreach (var data in channel.Reader.ReadAllAsync(ct))
                    {
                        Console.WriteLine(data.Length);
                        // pipe.Writer.Write(preamble.Samples.AsBytes());
                        pipeWriter.Write(preamble.Samples.Span.AsBytes());
                        modulator.Write(data);
                        // pipeWriter.Write(quietBuffer);

                        await pipeWriter.FlushAsync(ct).ConfigureAwait(false);
                        // modulator.Modulate(data);
                        // pipe.Writer.Write(quietBuffer);
                        // await pipe.Writer.FlushAsync(ct);
                    }
                    pipeWriter.Complete();
                },
                ct
            );
        }
        catch (Exception e)
        {
            pipeWriter.Complete(e);
        }
    }

    public void Dispose()
    {
        pipeWriter.Complete();
    }
}

public interface IReceiver
{
    // PipeWriter Samples { get; }
    ChannelReader<byte[]> Packets { get; }
    Task Execute(CancellationToken ct);
}
public class Receiver : IReceiver, IDisposable
{
    // private readonly Pipe pipe = new(new PipeOptions(pauseWriterThreshold: 0, resumeWriterThreshold: 0));
    private readonly Channel<byte[]> channel = Channel.CreateUnbounded<byte[]>();
    private readonly IPipeAdvance preambleDetection;
    private readonly IPipeReader<byte> demodulator;

    private readonly DemodulateLength demodulateLength;

    // public PipeWriter Samples { get; }
    public ChannelReader<byte[]> Packets { get; }
    public Receiver(IPipeAdvance preambleDetection, IPipeReader<byte> demodulator, DemodulateLength demodulateLength)
    {
        this.preambleDetection = preambleDetection;
        this.demodulator = demodulator;
        this.demodulateLength = demodulateLength;

        Packets = channel.Reader;
        // Samples = pipe.Writer;
    }

    public void Dispose()
    {
        if (!channel.Reader.Completion.IsCompleted)
        {
            channel.Writer.Complete();
        }
    }

    public async Task Execute(CancellationToken ct)
    {
        try
        {
            await Task.Run(async () => await Work(ct), ct);
        }
        catch (Exception e)
        {
            channel.Writer.Complete(e);
        }
    }

    private async Task Work(CancellationToken ct)
    {
        if (demodulateLength is DemodulateLength.FixedLength(int length))
        {
            while (true)
            {
                while (preambleDetection.TryAdvance())
                {
                    ct.ThrowIfCancellationRequested();
                }
                var packet = new byte[length];
                if (!demodulator.TryReadTo(packet))
                {
                    break;
                }
                await channel.Writer.WriteAsync(packet, ct);
            }
        }
        else if (demodulateLength is DemodulateLength.VariableLength(int numLengthByte))
        {
            var lengthByte = new byte[numLengthByte];
            while (true)
            {
                while (preambleDetection.TryAdvance())
                {
                    ct.ThrowIfCancellationRequested();
                }

                if (!demodulator.TryReadTo(lengthByte))
                {
                    break;
                }

                var packet = new byte[BitConverter.ToInt32(lengthByte)];
                if (!demodulator.TryReadTo(packet))
                {
                    break;
                }
                await channel.Writer.WriteAsync(packet, ct);
            }
        }
        else
        {
            throw new InvalidOperationException();
        }
        // await channel.Writer.WriteAsync(new byte[12], ct);
        // var success = demodulator.Demodulate(out var data);
        // if (success)
        // {
        //     await channel.Writer.WriteAsync(data, ct);
        // }

        channel.Writer.Complete();
    }
}