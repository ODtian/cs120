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

public class MacDuplex
(ChannelWriter<byte[]> writer, ChannelReader<byte[]> reader, byte address) : DuplexBase
{
    private const int SWS = 4;
    private const int RWS = 4;
    private const int AckTimeout = 500;
    private int LAR = 0;
    private int LFS = 0;
    private int LFR = 0;
    private int LAF = 0;

    private readonly Dictionary<int, byte[]?> received = [];
    private readonly Dictionary<int, (byte[] packet, CancellationTokenSource cts)> sent = new();

    public async Task Execute(CancellationToken ct)
    {
        await Task.WhenAll(Receive(ct), Send(ct));
        channelRx.Writer.TryComplete();
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
                    if ((mac.SequenceNumber >= LFR && mac.SequenceNumber < LFR + RWS) || (LFR + RWS > 63 && mac.SequenceNumber < (LFR + RWS) % 64))
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

                        await writer.WriteAsync(
                            Array.Empty<byte>().MacEncode(new()
                            {
                                Source = address,
                                Dest = mac.Source,
                                Type = MacFrame.FrameType.Ack,
                                SequenceNumber = mac.SequenceNumber
                            }),
                            ct
                        );
                    }
                }
                else if (mac.Type is MacFrame.FrameType.Ack)
                {
                    if (sent.ContainsKey(mac.SequenceNumber))
                    {
                        sent[mac.SequenceNumber].cts.Cancel();
                        sent.Remove(mac.SequenceNumber);

                        while (sent.ContainsKey(LAR))
                        {
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
            while ((LFS + SWS - LAR + 64) % 64 >= SWS)
            {
                await Task.Delay(100, ct);
            }

            var cts = new CancellationTokenSource();
            sent[LFS] = (packet, cts);
            await writer.WriteAsync(packet, ct);

            _ = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(AckTimeout, cts.Token);
                        if (!cts.Token.IsCancellationRequested)
                        {
                            await writer.WriteAsync(packet, ct);
                        }
                    }
                    catch (TaskCanceledException) { break; }
                }
            }, cts.Token);

            LFS = (LFS + 1) % 64;
        }
    }
}