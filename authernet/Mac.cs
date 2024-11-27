using System.Buffers;
using System.Threading.Channels;
using CS120.Packet;
using CS120.TxRx;
using CS120.Utils.Extension;
using CS120.Utils.Wave;
using DotNext.Threading;

namespace CS120.Mac;

public struct MacFrame
{
    public enum FrameType
    {
        Data = 0,
        Ack = 1,
        Nack = 2,
        Beacon = 3
    }

    private byte source;
    private byte dest;
    private byte typeAndsequenceNumber;

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

    public FrameType Type
    {
        readonly get => (FrameType)(typeAndsequenceNumber >> 6);
        set
        {
            typeAndsequenceNumber &= 0b0011_1111;
            typeAndsequenceNumber |= (byte)((byte)value << 6);
        }
    }

    public byte SequenceNumber
    {
        readonly get => (byte)(typeAndsequenceNumber & 0b0011_1111);
        set
        {
            typeAndsequenceNumber &= 0b1100_0000;
            typeAndsequenceNumber |= (byte)(value & 0b0011_1111);
        }
    }
}

// public class MacDuplex
// (ChannelWriter<byte[]> writer, ChannelReader<byte[]> reader, byte address) : DuplexBase
// {

//     private readonly Dictionary < int, byte[] ? > received = [];

//     public async Task Execute(CancellationToken ct)
//     {
//         await Task.WhenAll(Receive(ct), Send(ct));
//         channelRx.Writer.TryComplete();
//     }

//     private async Task Receive(CancellationToken ct)
//     {
//         await foreach (var packet in reader.ReadAllAsync(ct))
//         {
//             packet.MacGet(out var mac);
//             if (mac.Dest == address)
//             {
//                 if (mac.Type is MacFrame.FrameType.Data)
//                 {
//                     // if (received[mac.SequenceNumber] is null)
//                     // received[mac.SequenceNumber] = packet;

//                     /* Find continuous received cached packet */
//                     // int seq = mac.SequenceNumber;

//                     // while (received[seq] is not null && seq >= 0)
//                     //     seq--;

//                     // for (int i = seq + 1; i <= mac.SequenceNumber; i++)
//                     //     await channelRx.Writer.WriteAsync(received[i]!, ct);
//                     await channelRx.Writer.WriteAsync(packet, ct);
//                     await writer.WriteAsync(
//                         Array.Empty<byte>().MacEncode(new(
//                         ) { Source = mac.Dest, Dest = mac.Source, Type = MacFrame.FrameType.Ack }),
//                         ct
//                     );
//                 }
//                 else if (mac.Type is MacFrame.FrameType.Ack)
//                 {
//                 }

//                 // if (received[mac.SequenceNumber] == null)
//                 // {
//                 //     received[mac.SequenceNumber] = packet;

//                 //     await writer.WriteAsync(packet, ct);
//                 // }
//             }
//             // var packet = Packet.Parse(data);
//             // if(packet != null)
//             // {
//             //     received[packet.SequenceNumber] = packet.Data;
//             // }
//         }
//     }
//     private async Task Send(CancellationToken ct)
//     {
//         await foreach (var packet in channelTx.Reader.ReadAllAsync(ct)) await writer.WriteAsync(packet, ct);
//     }
// }

public class MacD : IIOChannel<ReadOnlySequence<byte>>, IAsyncDisposable
{
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
    // private readonly Channel<ReadOnlySequence<byte>> channelTx = Channel.CreateUnbounded<ReadOnlySequence<byte>>();

    // private readonly AsyncExclusiveLock sendLock = new();

    private readonly Channel<int> windowSlotChannel = Channel.CreateUnbounded<int>();

    // private readonly List<int> receivedAckCache = [];
    private readonly Dictionary<int, Void> receivedAckCache = [];
    private readonly Dictionary<int, ReadOnlySequence<byte>> receivedDataCache = [];
    // private readonly PriorityQueue<int, int> ackSource;
    // private readonly Stack
    struct Void
    {
    }
    private readonly AsyncCorrelationSource<int, Exception?> ackSource;
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
    private int LastAckReceived { get; set; } = -1;
    private int LastDataReceived { get; set; } = -1;
    private int NextDataNumber => (LastDataReceived + 1) % sequenceSize;
    // private int NextAck(int ack) => (ack + 1) % sequenceSize;

    private int NextAckNumber => (LastAckReceived + 1) % sequenceSize;
    private int LastDataSend => (LastAckReceived + windowSize) % sequenceSize;
    private ChannelReader<ReadOnlySequence<byte>> RxReader => channelRx.Reader;
    private ChannelWriter<ReadOnlySequence<byte>> RxWriter => channelRx.Writer;
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
        this.sequenceSize = sequenceSize;
        this.inChannel = inChannel;
        this.outChannel = outChannel;

        ackSource = new(windowSize);
        receiveTask = RunHandleReceiveAsync();

        foreach (var seq in Enumerable.Range(0, windowSize))
            windowSlotChannel.Writer.TryWrite(seq);
    }

    public async ValueTask<ReadOnlySequence<byte>> ReadAsync(CancellationToken ct)
    {
        if (await RxReader.WaitToReadAsync(ct))
            if (RxReader.TryRead(out var data))
                return data;

        return ReadOnlySequence<byte>.Empty;
        // return RxReader.ReadAsync(ct);
    }

    public async ValueTask WriteAsync(ReadOnlySequence<byte> data, CancellationToken ct)
    {
        // var tcs = new TaskCompletionSource();
        var slot = await windowSlotChannel.Reader.ReadAsync(ct);

        var task = ackSource.WaitAsync(slot, ct).AsTask();
        do
        {
            await outChannel.WriteAsync(
                data.MacEncode(new(
                )
                { Source = from, Dest = to, Type = MacFrame.FrameType.Data, SequenceNumber = (byte)slot }),
                ct
            );
            Console.WriteLine($"Send {slot}");
            try
            {
                var exception = await task.WaitAsync(TimeSpan.FromMilliseconds(10000), ct);
                if (exception is not null)
                    throw exception;
                break;
            }
            catch (TimeoutException)
            {
            }
        } while (true);
    }

    public ValueTask CompleteAsync(Exception? exception) => outChannel.CompleteAsync(exception);

    public async ValueTask DisposeAsync()
    {
        await cts.CancelAsync();
        await receiveTask.WaitAsync(CancellationToken.None);
        cts.Dispose();
    }

    private async Task RunHandleReceiveAsync()
    {
        try
        {
            await HandleReceiveAsync();
            RxWriter.TryComplete();
        }
        catch (Exception e)
        {
            if (e is OperationCanceledException)
                RxWriter.TryComplete();
            else
            {
                ackSource.PulseAll(e);
                RxWriter.TryComplete(e);
            }
        }
    }
    private async Task HandleReceiveAsync()
    {
        while (true)
        {
            var packet = await inChannel.ReadAsync(cts.Token);

            if (packet.Length == 0)
                break;

            packet.MacDecode(out var mac);
            if (mac.Dest == from)
            {
                if (mac.Type is MacFrame.FrameType.Data)
                {
                    Console.WriteLine($"Receive Data {mac.SequenceNumber}");
                    _ = outChannel
                            .WriteAsync(
                                new ReadOnlySequence<byte>().MacEncode(new(
                                )
                                {
                                    Source = mac.Dest,
                                    Dest = mac.Source,
                                    Type = MacFrame.FrameType.Ack,
                                    SequenceNumber = mac.SequenceNumber
                                }),
                                cts.Token
                            )
                            .AsTask();

                    receivedDataCache[mac.SequenceNumber] = packet;

                    if (mac.SequenceNumber == NextDataNumber)
                    {
                        while (receivedDataCache.ContainsKey(NextDataNumber))
                        {
                            receivedDataCache.Remove(NextDataNumber);
                            LastDataReceived = NextDataNumber;
                            channelRx.Writer.TryWrite(packet);
                        }
                    }
                }
                else if (mac.Type is MacFrame.FrameType.Ack)
                {
                    Console.WriteLine($"Receive Ack {mac.SequenceNumber}");
                    // ackSource.Complete(mac.SequenceNumber);
                    ackSource.Pulse(mac.SequenceNumber, null);
                    // receivedAckCache.Add(mac.SequenceNumber);
                    receivedAckCache[mac.SequenceNumber] = new();

                    if (mac.SequenceNumber == NextAckNumber)
                    {
                        while (receivedAckCache.ContainsKey(NextAckNumber))
                        {
                            receivedAckCache.Remove(NextAckNumber);
                            LastAckReceived = NextAckNumber;
                            windowSlotChannel.Writer.TryWrite(LastDataSend);
                        }
                    }
                }
            }
        }
    }

    // private async Task PendingHandleAsync()
    // {
    //     while (true)
    //     {
    //         // var emptySlot = await await Task.WhenAny(GetWindowSequenceNumbers().Select(w =>
    //         sendWindow[w].Tcs.Task)); await sendWindow[NextAckNumber].Tcs.Task;
    //         // ref var item = ref sendWindow[emptySlot];
    //         // ref var tiem = ref sendWindow.G

    //         var newTask = sendWindow[NextSequenceNumber] = await channelPending.Reader.ReadAsync();
    //         await channelWork.Writer.WriteAsync(new MacTask(true, false, NextSequenceNumber, newTask.Data));
    //         lastDataSend = NextSequenceNumber;
    //         lastAckReceived = NextAckNumber;
    //         // if (emptySlot == lastAckReceived + 1)
    //         // {
    //         // }
    //     }
    //     // while (true)
    //     // {
    //     //     var item = await channelPending.Reader.ReadAsync();
    //     //     var seq = NextSequenceNumber;
    //     //     sendWindow[seq] = item;
    //     //     await channelWork.Writer.WriteAsync(new MacTask(true, false, item.Data));
    //     //     lastDataSend = seq;
    //     // }
    // }

    // private IEnumerable<int> GetWindowSequenceNumbers()
    // {
    //     for (int i = 0; i < windowSize; i++)
    //         yield return (i + lastAckReceived + 1) % sequenceSize;
    // }

    // private async Task WorkHandleAsync()
    // {
    //     // while (channel.Reader.TryPeek(out _) {

    //     // }
    //     while (true)
    //     {
    //         var task = await channelWork.Reader.ReadAsync();
    //         if (task.IsSend)
    //         {
    //             // await WirteAsync(task.Data);
    //         }
    //         if (!task.IsAck)
    //         {
    //             _ = AddWithTimeout(task);
    //         }
    //     }
    //     // private int NextSequenceNumber(int current) => (current + 1) % sequenceSize;
    // }

    // private async ValueTask AddWithTimeout(MacTask task)
    // {

    //     if (sendWindow[task.SequenceNumber].retry < 0)
    //     {
    //         Task.Factory.StartNew
    //     }
    //     // {
    //     // var tcs = new TaskCompletionSource<int>();
    //     // var delay = Task.Delay(1000);
    //     // if (delay == await Task.WhenAny(sendWindow[task.SequenceNumber].Tcs.Task, delay))
    //     // await channelWork.Writer.WriteAsync(task);
    //     // }
    //     // var tcs = new TaskCompletionSource<int>();
    //     var delay = Task.Delay(1000);
    //     if (delay == await Task.WhenAny(sendWindow[task.SequenceNumber].Tcs.Task, delay))
    //         await channelWork.Writer.WriteAsync(task);
    // }
}