using System.Buffers;
using System.IO.Pipelines;
using System.Threading.Channels;
using CommunityToolkit.HighPerformance;
using CS120.Extension;
using CS120.Modulate;
using CS120.Preamble;
using CS120.TxRx;
using CS120.Utils;
using NAudio.Wave;

namespace CS120.Phy;

public class PhyTransmitter
(WaveFormat waveFormat,
 PipeWriter pipeWriter,
 IPreamble preamble,
 IPipeWriter<byte> modulator,
 // IPipeWriter<byte> bufferWriter,
 int quietSamples = 4800)
    : ITransmitter, IDisposable
{
    // private readonly Pipe pipe = new(new PipeOptions(pauseWriterThreshold: 0));
    private readonly Channel<byte[]> channel = Channel.CreateUnbounded<byte[]>();
    // private readonly IPreamble preamble = preamble;
    // private readonly IPipeWriter<byte> modulator = modulator;
    // private readonly PipeWriter pipeWriter = pipeWriter;
    private readonly byte[] quietBuffer = new byte[waveFormat.ConvertSamplesToByteSize(quietSamples)];
    public ChannelWriter<byte[]> Tx => channel.Writer;

    public async Task Execute(CancellationToken ct)
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
                    pipeWriter.Write(quietBuffer);

                    await pipeWriter.FlushAsync(ct).ConfigureAwait(false);
                    // modulator.Modulate(data);
                    // pipe.Writer.Write(quietBuffer);
                    // await pipe.Writer.FlushAsync(ct);
                }
            },
            ct
        );
    }

    public void Dispose()
    {
    }
}

public class PhyReceiver
(IPipeAdvance preambleDetection, IPipeReader<byte> demodulator, DemodulateLength demodulateLength)
    : IReceiver, IDisposable
{
    // private readonly Pipe pipe = new(new PipeOptions(pauseWriterThreshold: 0, resumeWriterThreshold: 0));
    private readonly Channel<byte[]> channel = Channel.CreateUnbounded<byte[]>();
    // private readonly IPipeAdvance preambleDetection;
    // private readonly IPipeReader<byte> demodulator;

    // private readonly DemodulateLength demodulateLength;

    // public PipeWriter Samples { get; }
    public ChannelReader<byte[]> Rx => channel.Reader;
    // public ReceiverPhy
    // {
    //     this.preambleDetection = preambleDetection;
    //     this.demodulator = demodulator;
    //     this.demodulateLength = demodulateLength;

    //     Rx = channel.Reader;
    //     // Samples = pipe.Writer;
    // }

    public async Task Execute(CancellationToken ct)
    {
        try
        {
            await Task.Run(async () => await Work(ct), ct);
        }
        catch (Exception e)
        {
            channel.Writer.TryComplete(e);
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

        channel.Writer.TryComplete();
    }

    public void Dispose()
    {
        channel.Writer.TryComplete();
    }
}