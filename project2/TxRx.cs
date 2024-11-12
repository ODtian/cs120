using System.IO.Pipelines;
using System.Threading.Channels;
using CS120.Modulate;
using CS120.Preamble;
using CS120.Utils;
using NAudio.Wave;
using CS120.Extension;
using System.Buffers;
using CS120.Phy;

namespace CS120.TxRx;

public interface IDuplex : ITransmitter,
                           IReceiver
{
}

// public class DuplexBase : IDuplex, IDisposable
// {
//     protected readonly Channel<byte[]> channelTx = Channel.CreateUnbounded<byte[]>();
//     protected readonly Channel<byte[]> channelRx = Channel.CreateUnbounded<byte[]>();
//     protected readonly ChannelWriter<byte[]> writer;
//     protected readonly ChannelReader<byte[]> reader;
//     public ChannelWriter<byte[]> Tx { get; }
//     public ChannelReader<byte[]> Rx { get; }

//     public DuplexBase(ChannelWriter<byte[]> writer, ChannelReader<byte[]> reader)
//     {
//         this.writer = writer;
//         this.reader = reader;

//         Tx = channelTx.Writer;
//         Rx = channelRx.Reader;
//     }

//     public void Dispose()
//     {
//         channelRx.Writer.TryComplete();
//     }
// }
public class DuplexBase : IDuplex, IDisposable
{
    protected readonly Channel<byte[]> channelTx = Channel.CreateUnbounded<byte[]>();
    protected readonly Channel<byte[]> channelRx = Channel.CreateUnbounded<byte[]>();
    public ChannelWriter<byte[]> Tx { get; }
    public ChannelReader<byte[]> Rx { get; }

    public DuplexBase()
    {
        Tx = channelTx.Writer;
        Rx = channelRx.Reader;
    }

    public void Dispose()
    {
        channelRx.Writer.TryComplete();
    }
}