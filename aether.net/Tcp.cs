using PacketDotNet;

namespace Aether.NET.Tcp;

public class SeqHijack
(uint hijack)
{
    // int seq;
    private uint? initSeq = null;
    public void Init(TcpPacket packet)
    {
        initSeq = packet.SequenceNumber;
    }

    public void Send(TcpPacket packet)
    {

        // {
        //     Seq = packet.SequenceNumber;
        //     inited = true;
        // }
        if (initSeq.HasValue)
        {
            packet.SequenceNumber = packet.SequenceNumber + hijack - initSeq.Value;
            packet.UpdateTcpChecksum();
        }
        // Seq += packet.PayloadData.Length;
    }

    public void Receive(TcpPacket packet)
    {
        if (initSeq.HasValue)
        {
            packet.AcknowledgmentNumber = packet.AcknowledgmentNumber - hijack + initSeq.Value;
            packet.UpdateTcpChecksum();
        }
    }
}