using System.Text;
using System.Threading.Channels;
using CS120.Packet;
using CS120.TxRx;

namespace CS120.Mac;

public struct MacFrame
{
    public enum FrameType
    {
        Data = 0,
        Ack = 1,
        DataAndAck = 2,
        Beacon = 3
    }

    private byte source;
    private byte dest;
    private byte type;
    private byte seq1;
    private byte seq2;

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
        readonly get => (FrameType)type;
        set => type = (byte)value;
    }
    public byte SequenceNumber
    {
        readonly get => seq1;
        set => seq1 = value;
    }
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

    public byte Seq2
    {
        readonly get => seq2;
        set => seq2 = value;
    }
}

public class MacDuplex
(ChannelWriter<byte[]> writer, ChannelReader<byte[]> reader, byte address) : DuplexBase
{
    private const int SWS = 1;
    private const int RWS = 1;
    private const int AckTimeout = 1000;
    private int LAR = 0;
    private int LFS = 0;
    private int LFR = 0;
    private int LAF = 0;

    private readonly Dictionary < int, byte[] ? > received = [];
    private readonly Dictionary<int, (byte[] packet, CancellationTokenSource cts)> sent = new();

    public async Task Execute(CancellationToken ct)
    {
        var t = await Task.WhenAny(Receive(ct), Send(ct));
        channelRx.Writer.TryComplete(t.Exception);
    }

    private async Task Receive(CancellationToken ct)
    {
        await foreach (var packet in reader.ReadAllAsync(ct))
        {
            packet.MacGet(out var mac);
            if (mac.Dest == address)
            {
                if (mac.Type is MacFrame.FrameType.Data)
                {
                    Console.WriteLine($"Receive Data: {mac.SequenceNumber}");
                    // if (received[mac.SequenceNumber] is null)
                    // received[mac.SequenceNumber] = packet;

                    /* Find continuous received cached packet */
                    // int seq = mac.SequenceNumber;

                    // while (received[seq] is not null && seq >= 0)
                    //     seq--;

                    // for (int i = seq + 1; i <= mac.SequenceNumber; i++)
                    //     await channelRx.Writer.WriteAsync(received[i]!, ct);
                    // await channelRx.Writer.WriteAsync(packet, ct);
                    // await writer.WriteAsync(
                    //     Array.Empty<byte>().MacEncode(new(
                    //     ) { Source = mac.Dest, Dest = mac.Source, Type = MacFrame.FrameType.Ack }),
                    //     ct
                    // );

                    Console.WriteLine($"Send Ack {mac.SequenceNumber}");
                    await writer.WriteAsync(
                        Array.Empty<byte>().MacEncode(new(
                        ) { Source = address,
                            Dest = mac.Source,
                            Type = MacFrame.FrameType.Ack,
                            SequenceNumber = mac.SequenceNumber }),
                        ct
                    );
                    if ((mac.SequenceNumber >= LFR && mac.SequenceNumber < LFR + RWS) ||
                        (LFR + RWS > 63 && mac.SequenceNumber < (LFR + RWS) % 64))
                    {
                        if (!received.ContainsKey(mac.SequenceNumber))
                        {
                            received[mac.SequenceNumber] = packet;
                            while (received.ContainsKey(LFR))
                            {
                                await channelRx.Writer.WriteAsync(received[LFR]!, ct);
                                received.Remove(LFR);
                                LFR = (LFR + 1) % 64;
                            }
                        }
                    }
                }
                else if (mac.Type is MacFrame.FrameType.Ack)
                {

                    if (sent.ContainsKey(mac.SequenceNumber))
                    {
                        sent[mac.SequenceNumber].cts.Cancel();
                        while (sent.ContainsKey(LAR) && sent[LAR].cts.IsCancellationRequested)
                        {
                            sent.Remove(LAR);
                            LAR = (LAR + 1) % 64;
                        }
                    }

                    // if (received[mac.SequenceNumber] == null)
                    // {
                    //     received[mac.SequenceNumber] = packet;

                    //     await writer.WriteAsync(packet, ct);
                    // }
                }
                // var packet = Packet.Parse(data);
                // if(packet != null)
                // {
                //     received[packet.SequenceNumber] = packet.Data;
                // }
            }
        }
    }
    private async Task Send(CancellationToken ct)
    {

        // await foreach (var packet in channelTx.Reader.ReadAllAsync(ct)) await writer.WriteAsync(packet, ct);
        await foreach (var packet in channelTx.Reader.ReadAllAsync(ct))
        {
            while ((LFS - LAR + 64) % 64 >= SWS)
            {
                // Console.WriteLine($"Wait: {LFS} {LAR}");
                await Task.Delay(100, ct);
            }

            var payload = packet.MacDecode(out var mac);

            var macPacket = payload.MacEncode(mac with { Source = address, SequenceNumber = (byte)LFS });
            var cts = new CancellationTokenSource();

            sent[LFS] = (macPacket, cts);
            Console.WriteLine($"Send: {LFS}");
            await writer.WriteAsync(macPacket, ct);

            _ = Task.Run(
                async () =>
                {
                    int seq = LFS;
                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(AckTimeout, cts.Token);
                            if (!cts.Token.IsCancellationRequested)
                            {
                                Console.WriteLine($"Send: {seq}");
                                await writer.WriteAsync(macPacket, ct);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                },
                cts.Token
            );

            LFS = (LFS + 1) % 64;
        }
    }
}

public class MacDuplex2
(ChannelWriter<byte[]> writer, ChannelReader<byte[]> reader, byte address) : DuplexBase
{
    class MacComparer : IComparer<byte[]>
    {
        public int Compare(byte[]? a, byte[]? b)
        {
            if (a is null && b is null)
            {
                return 0;
            }
            else if (a is null)
            {
                return 1;
            }
            else if (b is null)
            {
                return -1;
            }

            a.MacGet(out var macA);
            b.MacGet(out var macB);
            if (macA.Type == MacFrame.FrameType.Ack && macB.Type == MacFrame.FrameType.Ack)
            {
                return macA.SequenceNumber - macB.SequenceNumber;
            }
            else if (macA.Type == MacFrame.FrameType.Ack)
            {
                return -1;
            }
            else
            {
                return 1;
            }
        }
    }
    private readonly Channel<byte[]> channel = Channel.CreateUnbounded<byte[]>();
    private readonly Channel<byte[]> channelOut =
        Channel.CreateUnboundedPrioritized<byte[]>(new() { Comparer = new MacComparer() });
    private int lastDataReceived = 0;
    private int lastDataSend = -1;
    private int lastAckReceived = -1;
    public async Task Execute(CancellationToken ct)
    {
        try
        {
            var rec = Receive(ct);
            var send = Send(ct);
            await foreach (var packet in channelTx.Reader.ReadAllAsync(ct))
            {
                var p = packet.MacDecode(out var mac);

                lastDataSend = (lastDataSend + 1) % 64;

                // p = p.MacEncode(mac with {
                //     Source = address,
                //     SequenceNumber = (byte)lastDataSend,
                //     Type = MacFrame.FrameType.DataAndAck,
                //     Seq2 = (byte)lastDataReceived
                // });
                // await writer.WriteAsync(p, ct);
                while (lastDataSend > lastAckReceived)
                {
                    var macPacket = p.MacEncode(mac with {
                        Source = address, SequenceNumber = (byte)lastDataSend, Type = MacFrame.FrameType.Data,
                        // Seq2 = (byte)lastDataReceived
                    });
                    var cts = new CancellationTokenSource();
                    cts.CancelAfter(TimeSpan.FromMilliseconds(1000));
                    try
                    {
                        // await writer.WriteAsync(macPacket, cts.Token);
                        await channelOut.Writer.WriteAsync(macPacket, cts.Token);
                        Console.WriteLine($"Send {lastDataSend} to {mac.Dest}");
                        var read = await channel.Reader.ReadAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine($"Timeout");
                    }
                }
            }
            await rec;
            await send;
        }
        catch (Exception e)
        {
            writer.TryComplete(e);
        }
    }

    private async Task Receive(CancellationToken ct)
    {
        try
        {
            await foreach (var packet in reader.ReadAllAsync(ct))
            {
                packet.MacGet(out var mac);
                if (mac.Dest == address)
                {
                    if (mac.Type == MacFrame.FrameType.Data)
                    {
                        Console.WriteLine($"Receive data {mac.SequenceNumber} from {mac.Source}");

                        await channelOut.Writer.WriteAsync(
                            Array.Empty<byte>().MacEncode(new MacFrame {
                                Source = address,
                                Dest = mac.Source,
                                Type = MacFrame.FrameType.Ack,
                                SequenceNumber = mac.SequenceNumber
                            }),
                            ct
                        );
                        if (mac.SequenceNumber > lastDataReceived)
                        {
                            await channelRx.Writer.WriteAsync(packet, ct);
                            lastDataReceived = mac.SequenceNumber;
                        }
                    }
                    else if (mac.Type == MacFrame.FrameType.Ack)
                    {
                        Console.WriteLine($"Receive ack {mac.SequenceNumber} from {mac.Source}");
                        if (mac.SequenceNumber == lastDataSend)
                        {
                            lastAckReceived = mac.SequenceNumber;
                            await channel.Writer.WriteAsync(packet, ct);
                        }
                    }
                    else if (mac.Type == MacFrame.FrameType.DataAndAck)
                    {
                        Console.WriteLine($"Receive data ack {mac.SequenceNumber} {mac.Seq2} from {mac.Source}");
                        if (mac.SequenceNumber > lastDataReceived)
                        {
                            await channelRx.Writer.WriteAsync(packet, ct);
                            lastDataReceived = mac.SequenceNumber;
                        }
                        if (mac.Seq2 == lastDataSend)
                        {
                            lastAckReceived = mac.Seq2;
                            await channel.Writer.WriteAsync(packet, ct);
                        }
                    }
                }
            }
            channel.Writer.TryComplete();
            channelRx.Writer.TryComplete();
        }
        catch (Exception e)
        {
            channel.Writer.TryComplete(e);
            channelRx.Writer.TryComplete(e);
        }
    }

    private async Task Send(CancellationToken ct)
    {
        try
        {
            await foreach (var packet in channelOut.Reader.ReadAllAsync(ct))
            {
                packet.MacDecode(out var mac);
                if (mac.Type == MacFrame.FrameType.Data && mac.SequenceNumber < lastDataSend)
                {
                    continue;
                }
                await writer.WriteAsync(packet, ct);
            }
        }
        catch (Exception e)
        {
            writer.TryComplete(e);
        }
    }
}

// class SlidingWindow<T>(int windowSize, int sequenceLength)
// {
//     struct WindowSlot
//     {
//         public T frame;
//         public Task task;
//         public CancellationTokenSource cts;
//     }
//     int lastAckRecevie = 0;
//     int lastFrameSend = 0;

//     int lastFrameReceive = 0;
//     int lastAckSend = 0;

//     WindowSlot[] window = new WindowSlot[windowSize];

//     public bool TryAddToWindow(T frame)
//     {
//         if (lastFrameSend - lastAckRecevie < windowSize)
//         {
//             sequenceNumber = lastFrameSend;
//             lastFrameSend++;
//             // send frame
//             return true;
//         }
//         else
//         {
//             return false;
//         }
//     }
// }