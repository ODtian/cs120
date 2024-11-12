using System.Buffers;
using System.IO.Pipelines;
using System.Threading.Channels;
using CS120.Extension;
using CS120.Modulate;
using CS120.Preamble;
using CS120.Utils;
using NAudio.Wave;

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
