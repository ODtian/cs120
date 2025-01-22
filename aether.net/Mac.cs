using System.Buffers;
using System.Threading.Channels;
using Aether.NET.Packet;
using Aether.NET.Utils.Extension;
using Aether.NET.Utils.IO;
using DotNext.Threading;

namespace Aether.NET.Mac;

public struct MacFrame
()
{

    private byte source;
    private byte dest;
    private byte sequenceAndAckNumber;

    public byte Source
    {
        readonly get => source;
        set => source = value;
    }
    public byte Dest
    {
        readonly get => dest;
        set => dest = value;
    }
    public byte SequenceNumber
    {
        readonly get => (byte)(sequenceAndAckNumber & 0b0000_1111);
        set
        {
            sequenceAndAckNumber &= 0b1111_0000;
            sequenceAndAckNumber |= (byte)(value & 0b0000_1111);
        }
    }
    public byte AckNumber
    {
        readonly get => (byte)((sequenceAndAckNumber & 0b1111_0000) >> 4);
        set
        {
            sequenceAndAckNumber &= 0b0000_1111;
            sequenceAndAckNumber |= (byte)((value & 0b0000_1111) << 4);
        }
    }
}

public class MacD : IIOChannel<ReadOnlySequence<byte>>, IAsyncDisposable
{
    readonly struct Void
    {
    }

    private readonly byte from;
    private readonly byte to;
    private readonly int windowSize;
    private readonly int sequenceSize;

    private readonly IInChannel<ReadOnlySequence<byte>> inChannel;
    private readonly IOutChannel<ReadOnlySequence<byte>> outChannel;

    private readonly Channel<ReadOnlySequence<byte>> channelRx = Channel.CreateUnbounded<ReadOnlySequence<byte>>();
    private ChannelReader<ReadOnlySequence<byte>> RxReader => channelRx.Reader;
    private ChannelWriter<ReadOnlySequence<byte>> RxWriter => channelRx.Writer;

    private readonly Channel<int> windowSlotChannel = Channel.CreateUnbounded<int>();
    private readonly ReadOnlySequence<byte>[] receivedDataCache;
    private readonly AsyncCorrelationSource<int, object?> ackSource;

    private readonly Task receiveTask;
    private readonly CancellationTokenSource cts = new();
    private double rttEstimate = 50;
    private static readonly double rttAlpha = 0.125;

    private int LastAckReceived { get; set; }
    private int LastDataReceived { get; set; }
    private int NextDataNumber => ToSequnceNumber(LastDataReceived + 1);

    private int NextAckNumber => ToSequnceNumber(LastAckReceived + 1);
    private int LastDataSend => ToSequnceNumber(LastAckReceived + windowSize);

    private int ToSequnceNumber(int index) => index % sequenceSize;

    public MacD(
        IInChannel<ReadOnlySequence<byte>> inChannel,
        IOutChannel<ReadOnlySequence<byte>> outChannel,
        byte from,
        byte to,
        int windowSize,
        int sequenceSize
    )
    {
        this.from = from;
        this.to = to;
        this.windowSize = windowSize;
        this.sequenceSize = 16;
        this.inChannel = inChannel;
        this.outChannel = outChannel;

        ackSource = new(windowSize);
        receivedDataCache = Enumerable.Repeat(ReadOnlySequence<byte>.Empty, sequenceSize).ToArray();

        receiveTask = RunProcessReceiveAsync();

        foreach (var seq in Enumerable.Range(0, windowSize))
            windowSlotChannel.Writer.TryWrite(seq);

        LastAckReceived = LastDataReceived = this.sequenceSize - 1;
    }

    public ValueTask<ReadOnlySequence<byte>> ReadAsync(CancellationToken ct) => RxReader.TryReadAsync(ct);

    public async ValueTask WriteAsync(ReadOnlySequence<byte> data, CancellationToken ct)
    {
        var slot = await windowSlotChannel.Reader.ReadAsync(ct);

        var task = ackSource.WaitAsync(slot, ct).AsTask();

        var random = new Random();
        var mac = new MacFrame(
        ) { Source = from, Dest = to, SequenceNumber = (byte)slot, AckNumber = (byte)LastDataReceived };

        data = data.MacEncode(mac);

        while (true)
        {
            Console.WriteLine($"Send {slot}");
            await outChannel.WriteAsync(data, ct);

            var rttStart = DateTime.Now;

            try
            {
                await task.WaitAsync(TimeSpan.FromMilliseconds(rttEstimate) * (random.NextSingle() * 0.5f + 0.5f), ct);
                rttEstimate = rttEstimate * (1 - rttAlpha) + (DateTime.Now - rttStart).TotalMilliseconds * rttAlpha;
                Console.WriteLine($"Send mac {mac.Source} to {mac.Dest} of Seq {mac.SequenceNumber} Ack {mac.AckNumber}"
                );

                return;
            }
            catch (TimeoutException)
            {
                rttEstimate += 10;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await cts.CancelAsync();
        await receiveTask.WaitAsync(CancellationToken.None);
        cts.Dispose();
        RxWriter.TryComplete();
    }

    private async Task RunProcessReceiveAsync()
    {
        Exception? exception = null;
        try
        {
            await ProcessReceiveAsync();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception e)
        {
            exception = e;
        }
        finally
        {
            RxWriter.TryComplete(exception);
            ackSource.PulseAll(exception ?? new Exception("Disposed while still waiting for ack"));
        }
    }

    private async Task ProcessReceiveAsync()
    {
        bool ValueInWindowRange(int windowStart, int val)
        {
            return Enumerable.Range(windowStart, windowSize).Any(v => ToSequnceNumber(v) == val);
        }

        while (true)
        {
            var packet = await inChannel.ReadAsync(cts.Token);
            if (packet.IsEmpty)
                break;
            var payload = packet.MacDecode(out var header);

            Console.WriteLine(
                $"Receive mac {header.Source} to {header.Dest} of Seq {header.SequenceNumber} Ack {header.AckNumber}"
            );
            if (header.Dest == from)
            {
                if (ValueInWindowRange(NextAckNumber, header.AckNumber))
                {
                    Console.WriteLine($"Receive Ack {header.AckNumber}");
                    while (LastAckReceived != header.AckNumber)
                    {

                        ackSource.Pulse(NextAckNumber, null);
                        LastAckReceived = NextAckNumber;
                        windowSlotChannel.Writer.TryWrite(LastDataSend);
                    }
                }

                if (!payload.IsEmpty)
                {
                    Console.WriteLine($"Receive Data {header.SequenceNumber}");

                    if (ValueInWindowRange(NextDataNumber, header.SequenceNumber))
                        receivedDataCache[header.SequenceNumber] = payload;

                    if (header.SequenceNumber == NextDataNumber)
                    {
                        while (!receivedDataCache[NextDataNumber].IsEmpty)
                        {
                            Console.WriteLine($"Write {NextDataNumber} to queue");
                            RxWriter.TryWrite(receivedDataCache[NextDataNumber]);
                            receivedDataCache[NextDataNumber] = ReadOnlySequence<byte>.Empty;
                            LastDataReceived = NextDataNumber;
                        }
                    }
                    _ = outChannel
                            .WriteAsync(
                                ReadOnlySequence<byte>.Empty.MacEncode(new(
                                ) { Source = header.Dest, Dest = header.Source, AckNumber = (byte)LastDataReceived }),
                                cts.Token
                            )
                            .AsTask();
                }
            }
        }
    }
}