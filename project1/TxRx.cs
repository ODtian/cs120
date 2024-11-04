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
    public PipeReader Samples { get; }
    Task Execute(CancellationToken ct);
}

public class Transmitter : ITransmitter, IDisposable
{
    private readonly Pipe pipe = new(new PipeOptions(pauseWriterThreshold: 0));
    private readonly Channel<byte[]> channel = Channel.CreateUnbounded<byte[]>();
    private readonly IPreamble preamble;
    private readonly IModulator modulator;
    private readonly byte[] quietBuffer;
    public ChannelWriter<byte[]> Packets { get; }
    public PipeReader Samples { get; }

    public Transmitter(
        WaveFormat waveFormat, IPreamble preamble, IWriterBuilder<IModulator> modulatorBuilder, int quietSamples = 4800
    )
    {
        this.preamble = preamble;
        modulator = modulatorBuilder.Build(waveFormat, pipe.Writer);
        quietBuffer = new byte[waveFormat.ConvertSamplesToByteSize(quietSamples)];
        // quietBuffer.AsSpan().Clear();
        // this.quiet = quiet;

        Packets = channel.Writer;
        Samples = pipe.Reader;
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
                        pipe.Writer.Write(preamble.Samples.AsBytes());

                        modulator.Modulate(data);

                        pipe.Writer.Write(quietBuffer);
                        await pipe.Writer.FlushAsync(ct);
                    }
                    pipe.Writer.Complete();
                },
                ct
            );
        }
        catch (Exception e)
        {
            pipe.Writer.Complete(e);
        }
    }

    public void Dispose()
    {
        pipe.Writer.Complete();
    }
}

public interface IReceiver
{
    PipeWriter Samples { get; }
    ChannelReader<byte[]> Packets { get; }
    Task Execute(CancellationToken ct);
}
public class Receiver : IReceiver, IDisposable
{
    private readonly Pipe pipe = new(new PipeOptions(pauseWriterThreshold: 0, resumeWriterThreshold: 0));
    private readonly Channel<byte[]> channel = Channel.CreateUnbounded<byte[]>();
    private readonly PreambleDetection preambleDetection;
    private readonly IDemodulator demodulator;

    public PipeWriter Samples { get; }
    public ChannelReader<byte[]> Packets { get; }
    public Receiver(
        WaveFormat waveFormat,
        IReaderBuilder<PreambleDetection> preambleDetectionBuilder,
        IReaderBuilder<IDemodulator> demodulatorBuilder
    )
    {
        preambleDetection = preambleDetectionBuilder.Build(waveFormat, pipe.Reader);
        demodulator = demodulatorBuilder.Build(waveFormat, pipe.Reader);

        Packets = channel.Reader;
        Samples = pipe.Writer;
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

            await Task.Run(
                async () =>
                {
                    while (!pipe.Reader.IsFinished())
                    {
                        ct.ThrowIfCancellationRequested();

                        preambleDetection.DetectPreamble(ct);
                        // await channel.Writer.WriteAsync(new byte[12], ct);
                        var success = demodulator.Demodulate(out var data);
                        if (success)
                        {
                            await channel.Writer.WriteAsync(data, ct);
                        }
                    }
                    channel.Writer.Complete();
                },
                ct
            );
        }
        catch (Exception e)
        {
            channel.Writer.Complete(e);
        }
    }
}