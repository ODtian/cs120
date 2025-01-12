using System.Buffers;
using System.Threading.Channels;
using CS120.Packet;
using CS120.TxRx;
using CS120.Utils.Extension;
using CS120.Utils.IO;
using CS120.Utils.Wave;
using DotNext.Threading;

namespace CS120.Mac;

public struct MacFrame
{
    // public enum FrameType
    // {
    //     Data = 0,
    //     Ack = 1,
    //     Nack = 2,
    //     Beacon = 3
    // }
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

    // private byte ackNumber;
    // private byte source;
    // private byte dest;
    // private byte typeAndsequenceNumber;

    // public byte Source
    // {
    //     readonly get => source;
    //     set => source = value;
    // }
    // public byte Dest
    // {
    //     readonly get => dest;
    //     set => dest = value;
    // }

    // public FrameType Type
    // {
    //     readonly get => (FrameType)(typeAndsequenceNumber >> 6);
    //     set
    //     {
    //         typeAndsequenceNumber &= 0b0011_1111;
    //         typeAndsequenceNumber |= (byte)((byte)value << 6);
    //     }
    // }

    // public byte SequenceNumber
    // {
    //     readonly get => (byte)(typeAndsequenceNumber & 0b0011_1111);
    //     set
    //     {
    //         typeAndsequenceNumber &= 0b1100_0000;
    //         typeAndsequenceNumber |= (byte)(value & 0b0011_1111);
    //     }
    // }
}

public class MacD : IIOChannel<ReadOnlySequence<byte>>, IAsyncDisposable
{
    readonly struct Void
    {
    }
    // readonly struct MacTask
    // (bool isSend, bool isAck, int sequenceNumber, ReadOnlySequence<byte> data)
    // {
    //     private readonly byte flags = (byte)(0 | (isSend ? Flags.Send : Flags.None) | (isAck ? Flags.Ack :
    //     Flags.None)); enum Flags
    //     {
    //         None = 0,
    //         Send = 1,
    //         Ack = 2
    //     }

    //     public int SequenceNumber { get; } = sequenceNumber;
    //     public readonly bool IsSend => (flags & (byte)Flags.Send) != 0;
    //     public readonly bool IsAck => (flags & (byte)Flags.Ack) != 0;
    //     public ReadOnlySequence<byte> Data { get; } = data;
    // }

    // private int lastDataSend = -1;

    private readonly byte from;
    private readonly byte to;
    private readonly int windowSize;
    private readonly int sequenceSize;

    private readonly IInChannel<ReadOnlySequence<byte>> inChannel;
    private readonly IOutChannel<ReadOnlySequence<byte>> outChannel;
    // private int lastDataReceived = -1;

    // private int NextSequenceNumber => (lastDataSend + 1) % sequenceSize;

    // private int WindowStart => lastAckReceived + 1;
    // private int WindowEnd => lastAckReceived + windowSize;
    // record struct WindowItem
    // (TaskCompletionSource Tcs, int retry, ReadOnlySequence<byte> Data);

    // private readonly WindowItem[] sendWindow = new WindowItem[sequenceSize];
    // private readonly Dictionary<int, WindowItem> sendWindow = [];
    // private readonly ReadOnlySequence<byte>[] receiveWindow = new ReadOnlySequence<byte>[windowSize];
    // private readonly Channel<MacTask> channelWork = Channel.CreateUnboundedPrioritized<MacTask>(new());

    // private readonly Channel<WindowItem> channelPending = Channel.CreateUnbounded<WindowItem>();
    private readonly Channel<ReadOnlySequence<byte>> channelRx = Channel.CreateUnbounded<ReadOnlySequence<byte>>();

    private ChannelReader<ReadOnlySequence<byte>> RxReader => channelRx.Reader;
    private ChannelWriter<ReadOnlySequence<byte>> RxWriter => channelRx.Writer;
    // private readonly Channel<ReadOnlySequence<byte>> channelTx = Channel.CreateUnbounded<ReadOnlySequence<byte>>();

    // private readonly AsyncExclusiveLock sendLock = new();

    private readonly Channel<int> windowSlotChannel = Channel.CreateUnbounded<int>();

    // private readonly List<int> receivedAckCache = [];

    private readonly bool[] receivedAckCache;
    private readonly ReadOnlySequence<byte>[] receivedDataCache;
    // private readonly Dictionary<int, bool> receivedAckCache = [];
    // private readonly Dictionary<int, ReadOnlySequence<byte>> receivedDataCache = [];
    // private readonly PriorityQueue<int, int> ackSource;
    // private readonly Stack
    private readonly AsyncCorrelationSource<int, object?> ackSource;
    // public async ValueTask WriteAsync(ReadOnlySequence<byte> data, CancellationToken ct)
    // {
    //     var tcs = new TaskCompletionSource();
    //     // var t = new AsyncCorrelationSource();
    //     await channelPending.Writer.WriteAsync(new WindowItem(tcs, data), ct);
    //     await tcs.Task;
    //     // var await Task.WhenAny(sendWindow.Select(w => w.Tcs.Task));
    // }
    private readonly Task receiveTask;
    private readonly CancellationTokenSource cts = new();
    private int LastAckReceived { get; set; }
    private int LastDataReceived { get; set; }
    private int NextDataNumber => ToSequnceNumber(LastDataReceived + 1);
    // private int NextAck(int ack) => (ack + 1) % sequenceSize;

    private int NextAckNumber => ToSequnceNumber(LastAckReceived + 1);
    private int LastDataSend => ToSequnceNumber(LastAckReceived + windowSize);

    private int ToSequnceNumber(int index) => index % sequenceSize;
    // private Random random;

    // private float factor;
    public bool IsCompleted => RxReader.IsFinished();
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

        // random = new(from);
        // factor = random.NextSingle() * 0.5f + 0.5f;
        // Console.WriteLine($"Factor: {factor}");

        ackSource = new(windowSize);
        receivedAckCache = Enumerable.Repeat(false, sequenceSize).ToArray();
        receivedDataCache = Enumerable.Repeat(ReadOnlySequence<byte>.Empty, sequenceSize).ToArray();

        receiveTask = RunProcessReceiveAsync();

        foreach (var seq in Enumerable.Range(0, windowSize))
            windowSlotChannel.Writer.TryWrite(seq);

        LastAckReceived = LastDataReceived = this.sequenceSize - 1;
    }

    public async ValueTask<ReadOnlySequence<byte>> ReadAsync(CancellationToken ct) => await RxReader.ReadAsync(ct);

    public async ValueTask WriteAsync(ReadOnlySequence<byte> data, CancellationToken ct)
    {
        // var tcs = new TaskCompletionSource();
        var slot = await windowSlotChannel.Reader.ReadAsync(ct);

        var task = ackSource.WaitAsync(slot, ct).AsTask();
        // var tries = random.NextSingle() * 0.5f + 0.5f;
        // var retry = 0;
        var random = new Random();
        while (true)
        {
            Console.WriteLine($"Send {slot}");
            await outChannel.WriteAsync(
                data.MacEncode(new(
                ) { Source = from, Dest = to, SequenceNumber = (byte)slot, AckNumber = (byte)LastDataReceived }),
                ct
            );
            // await outChannel.WriteAsync(
            //     data.MacEncode(new(
            //     ) { Source = from, Dest = to, Type = MacFrame.FrameType.Data, SequenceNumber = (byte)slot }),
            //     ct
            // );
            try
            {
                await task.WaitAsync(TimeSpan.FromMilliseconds(500) * (random.NextSingle() * 0.5f + 0.5f), ct);
                return;
            }
            catch (TimeoutException)
            {
            }
        }
        // await channelRxRaw.Writer.WriteAsync(
        //     new ReadOnlySequence<byte>([]).MacEncode(new(
        //     ) { Source = to, Dest = from, Type = MacFrame.FrameType.Ack, SequenceNumber = (byte)slot }),
        //     ct
        // );
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
                        // Console.WriteLine($"Ack {frame.SequenceNumber} {(DateTime.Now - ts).TotalMilliseconds}");
                        receivedDataCache[header.SequenceNumber] = payload;

                    if (header.SequenceNumber == NextDataNumber)
                    {
                        while (!receivedDataCache[NextDataNumber].IsEmpty)
                        {
                            await RxWriter.WriteAsync(receivedDataCache[NextDataNumber]);
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
                // Console.WriteLine($"Ack {frame.SequenceNumber} {(DateTime.Now - ts).TotalMilliseconds}");
                // receivedAckCache[header.SequenceNumber] = true;
                // if (header.Type is MacFrame.FrameType.Data)
                // {
                //     Console.WriteLine($"Receive Data {header.SequenceNumber}");
                //     var ts = DateTime.Now;
                //     _ = outChannel
                //             .WriteAsync(
                //                 new ReadOnlySequence<byte>([]).MacEncode(new(
                //                 ) { Source = header.Dest,
                //                     Dest = header.Source,
                //                     Type = MacFrame.FrameType.Ack,
                //                     SequenceNumber = header.SequenceNumber }),
                //                 cts.Token
                //             )
                //             .AsTask();

                //     if (ValueInWindowRange(NextDataNumber, header.SequenceNumber))
                //         // Console.WriteLine($"Ack {frame.SequenceNumber} {(DateTime.Now - ts).TotalMilliseconds}");
                //         receivedDataCache[header.SequenceNumber] = payload;

                //     if (header.SequenceNumber == NextDataNumber)
                //     {
                //         while (!receivedDataCache[NextDataNumber].IsEmpty)
                //         {
                //             await RxWriter.WriteAsync(receivedDataCache[NextDataNumber]);
                //             receivedDataCache[NextDataNumber] = ReadOnlySequence<byte>.Empty;
                //             LastDataReceived = NextDataNumber;
                //         }
                //     }
                // }
                // else if (header.Type is MacFrame.FrameType.Ack)
                // {
                //     Console.WriteLine($"Receive Ack {header.SequenceNumber}");
                //     ackSource.Pulse(header.SequenceNumber, null);
                //     if (ValueInWindowRange(NextAckNumber, header.SequenceNumber))
                //         receivedAckCache[header.SequenceNumber] = true;

                //     if (header.SequenceNumber == NextAckNumber)
                //     {
                //         while (receivedAckCache[NextAckNumber])
                //         {
                //             receivedAckCache[NextAckNumber] = false;
                //             LastAckReceived = NextAckNumber;
                //             windowSlotChannel.Writer.TryWrite(LastDataSend);
                //         }
                //     }
                // }
            }
        }
    }
}