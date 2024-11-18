using System.Buffers;
using System.IO.Pipelines;
using System.Numerics;
using System.Threading.Channels;
using CommunityToolkit.HighPerformance;
using CS120.Packet;
using CS120.Preamble;
using CS120.TxRx;
using CS120.Utils;
using CS120.Utils.Extension;
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

public class CSMAPhyHalfDuplex
(PipeWriter pipeWriter,
 PipeReader pipeReader,
 WaveFormat waveFormatWrite,
 WaveFormat waveFormatRead,
 IPipeAdvance carrierSensor,
 IPreamble preamble,
 IPipeWriter<byte> modulator,
 IPipeAdvance preambleDetection,
 IPipeReader<byte> demodulator,
 int maxBytePerPacket,
 int quietSamples = 4800,
 float baseBackOffTime = 0.1f)
    : DuplexBase
{
    public enum CSMAState
    {
        CarrierSense,
        FrameDetect,
        Send,
        Quit
    }

    private Task delay = Task.CompletedTask;

    // private readonly PipeWriter pipeWriter = pipeWriter;
    // private readonly PipeReader pipeReader = pipeReader;
    // private readonly WaveFormat waveFormatWrite = waveFormatWrite;
    // private readonly WaveFormat waveFormatRead = waveFormatRead;

    // private readonly IPipeAdvance carrierSense = carrierSense;
    // private readonly IPreamble preamble = preamble;
    // private readonly IPipeWriter<byte> modulator = modulator;
    // private readonly IPipeAdvance preambleDetection = preambleDetection;
    protected readonly IPipeReader<byte> demodulator = demodulator;
    protected readonly int maxBytePerPacket = maxBytePerPacket;

    // private readonly DemodulateLength demodulateLength;
    private readonly byte[] quietBuffer = new byte[waveFormatWrite.ConvertSamplesToByteSize(quietSamples)];
    private readonly TimeSpan baseBackOffTime = TimeSpan.FromSeconds(baseBackOffTime);

    public async Task Execute(CancellationToken ct)
    {
        await Task.Run(() => Start(ct), ct);
    }

    private async Task Start(CancellationToken ct)
    {
        CSMAState state = CSMAState.CarrierSense;
        try
        {
            while (true)
            {
                state = state switch
                {
                    CSMAState.CarrierSense => await CarrierSense(ct),
                    CSMAState.FrameDetect => await FrameDetect(ct),
                    CSMAState.Send => await Send(ct),
                    _ => state
                };

                if (state is CSMAState.Quit)
                {
                    Console.WriteLine("Quit");
                    channelRx.Writer.TryComplete();
                    return;
                }

                ct.ThrowIfCancellationRequested();
            }
        }
        catch (Exception e)
        {
            channelRx.Writer.TryComplete(e);
        }
    }
    private ValueTask<CSMAState> CarrierSense(CancellationToken ct)
    {
        while (carrierSensor.TryAdvance())
        {
            ct.ThrowIfCancellationRequested();
            if (channelTx.Reader.Count > 0 && delay.IsCompleted)
                return ValueTask.FromResult(CSMAState.Send);
        }

        /* Collision! */
        if (channelTx.Reader.Count > 0)
            delay = Task.Delay(baseBackOffTime, ct);

        return ValueTask.FromResult(CSMAState.FrameDetect);
    }

    private async ValueTask<CSMAState> FrameDetect(CancellationToken ct)
    {

        if (preambleDetection.TryAdvance())
        {
            return CSMAState.CarrierSense;
        }

        if (!TryGetLength(out var packetLength))
            return CSMAState.Quit;

        var packet = new byte[packetLength];
        if (!demodulator.TryReadTo(packet))
            return CSMAState.Quit;

        await channelRx.Writer.WriteAsync(packet, ct);

        return CSMAState.CarrierSense;
    }

    private async ValueTask<CSMAState> Send(CancellationToken ct)
    {
        var packet = await channelTx.Reader.ReadAsync(ct);

        pipeWriter.Write(preamble.Samples.Span.AsBytes());
        modulator.Write(packet);
        pipeWriter.Write(quietBuffer);

        await pipeWriter.FlushAsync(ct).ConfigureAwait(false);

        return CSMAState.CarrierSense;
    }

    protected virtual bool TryGetLength(out int length)
    {
        length = maxBytePerPacket;
        return true;
    }
}

public class CSMAPhyHalfDuplex<T>(
    PipeWriter pipeWriter,
    PipeReader pipeReader,
    WaveFormat waveFormatWrite,
    WaveFormat waveFormatRead,
    IPipeAdvance carrierSensor,
    IPreamble preamble,
    IPipeWriter<byte> modulator,
    IPipeAdvance preambleDetection,
    IPipeReader<byte> demodulator,
    // DemodulateLength demodulateLength,
    int maxBytePerPacket,
    int quietSamples = 4800,
    float baseBackOffTime = 0.1f
)
    : CSMAPhyHalfDuplex(
          pipeWriter,
          pipeReader,
          waveFormatWrite,
          waveFormatRead,
          carrierSensor,
          preamble,
          modulator,
          preambleDetection,
          demodulator,
          maxBytePerPacket,
          quietSamples,
          baseBackOffTime
      )
    where T : IBinaryInteger<T>
{
    private readonly byte[] lengthBuffer = new byte[BinaryIntegerTrait<T>.Size];
    protected override bool TryGetLength(out int length)
    {
        var success = demodulator.TryReadTo(lengthBuffer, false);

        length =
            Math.Min(success ? int.CreateChecked(T.ReadLittleEndian(lengthBuffer, true)) : default, maxBytePerPacket);
        return success;
    }
}
class PhyUtilDuplex
(ChannelWriter<byte[]> writer, ChannelReader<byte[]> reader) : DuplexBase
{
    public async Task Execute(CancellationToken ct)
    {
        channelRx.Writer.TryComplete((await Task.WhenAny(Send(ct), Receive(ct))).Exception);
    }

    private async Task Receive(CancellationToken ct)
    {

        await foreach (var packet in reader.ReadAllAsync(ct))
        {
            var p = packet.LengthDecode<byte>(out var lengthValid);

            if (!lengthValid)
                continue;

            p = p.RSDecode(Program.eccNums, out var eccValid);

            Console.WriteLine($"EccValid: {eccValid}");

            if (!eccValid)
                continue;

            // .LengthDecode<byte>(out var lengthValid)
            // .IDDecode<byte>(out var id);
            await channelRx.Writer.WriteAsync(p, ct);
        }
    }

    private async Task Send(CancellationToken ct)
    {

        await foreach (var packet in channelTx.Reader.ReadAllAsync(ct))
        {
            await writer.WriteAsync(packet.RSEncode(Program.eccNums).LengthEncode<byte>(), ct);
        }
    }
}