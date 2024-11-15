
using System.Threading.Channels;

namespace CS120.TxRx;

public interface ITransmitter
{
    public ChannelWriter<byte[]> Tx { get; }
    // public PipeReader Samples { get; }
}

public interface IReceiver
{
    // PipeWriter Samples { get; }
    ChannelReader<byte[]> Rx { get; }
}

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
public class DuplexBase
() : IDuplex, IDisposable
{
    // protected readonly Channel<byte[]> channelTx = Channel.CreateBounded<byte[]>(1);
    protected readonly Channel<byte[]> channelTx = Channel.CreateUnbounded<byte[]>();
    protected readonly Channel<byte[]> channelRx = Channel.CreateUnbounded<byte[]>();
    public ChannelWriter<byte[]> Tx => channelTx.Writer;
    public ChannelReader<byte[]> Rx => channelRx.Reader;

    public void Dispose()
    {
        channelRx.Writer.TryComplete();
    }
}