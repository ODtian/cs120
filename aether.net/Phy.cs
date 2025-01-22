using System.Buffers;
using System.Numerics;
using System.Threading.Channels;
using Aether.NET.Modulate;
using Aether.NET.Packet;
using Aether.NET.Preamble;
using Aether.NET.Utils;
using Aether.NET.Utils.Extension;
using Aether.NET.Utils.IO;
using DotNext.Threading;
using Nerdbank.Streams;

namespace Aether.NET.Phy;

public class TXPhy<TSample, TLength>(
    IOutChannel<ReadOnlySequence<TSample>> outChannel, ISequnceReader<byte, TSample> modulator
)
    : IOutChannel<ReadOnlySequence<byte>>, IAsyncDisposable
    where TSample : unmanaged, INumber<TSample>
    where TLength : IBinaryInteger<TLength>
{
    private readonly IOutChannel<ReadOnlySequence<TSample>> samplesOut = outChannel;
    private readonly ISequnceReader<byte, TSample> modulator = modulator;
    private readonly AsyncExclusiveLock sendLock = new();
    private readonly Sequence<TSample> sequence = new();

    public async ValueTask WriteAsync(ReadOnlySequence<byte> data, CancellationToken ct)
    {
        Console.WriteLine("//// Send");
        Console.WriteLine(Convert.ToHexString(data.ToArray()));
        Console.WriteLine("////");
        data = data.RSEncode(Program.eccNums).LengthEncode<TLength>();

        using (await sendLock.AcquireLockAsync(ct))
        {
            modulator.TryRead(ref data, sequence);
            await samplesOut.WriteAsync(sequence.AsReadOnlySequence, ct);
            sequence.Reset();
        }
    }

    public ValueTask DisposeAsync()
    {

        return ValueTask.CompletedTask;
    }
}

public class RXPhy<TSample, TLength> : IInChannel<ReadOnlySequence<byte>>, IAsyncDisposable
    where TSample : unmanaged, INumber<TSample>
    where TLength : IBinaryInteger<TLength>
{
    private readonly IInStream<TSample> samplesIn;
    private readonly ISequnceReader<TSample, byte> demodulator;
    private readonly ISequnceSearcher<TSample> preambleDetection;
    private readonly Channel<ReadOnlySequence<byte>> channelRx = Channel.CreateUnbounded<ReadOnlySequence<byte>>();
    private readonly CancellationTokenSource cts = new();
    private readonly Task processTask;

    private readonly int maxPacketSize;

    private ChannelReader<ReadOnlySequence<byte>> RxReader => channelRx.Reader;
    private ChannelWriter<ReadOnlySequence<byte>> RxWriter => channelRx.Writer;
    // private readonly List<TSample[]> samples = [];

    public RXPhy(
        IInStream<TSample> inStream,
        ISequnceReader<TSample, byte> demodulator,
        ISequnceSearcher<TSample> preambleDetection,
        int maxPacketSize
    )
    {
        samplesIn = inStream;
        this.demodulator = demodulator;
        this.preambleDetection = preambleDetection;
        processTask = Task.Run(RunProcessAsync);
        this.maxPacketSize = maxPacketSize;
    }

    public async ValueTask DisposeAsync()
    {
        await cts.CancelAsync();
        await processTask.WaitAsync(CancellationToken.None);
        cts.Dispose();
        RxWriter.TryComplete();
        // if (samples.Count > 0 && false)
        // {
        //     var length = samples.Select(x => x.Length).Max();
        //     var samplesResize = samples.Select(x => x.Concat(Enumerable.Repeat(default(TSample), length -
        //     x.Length))); var mat = Matrix<TSample>.Build.DenseOfRows(samplesResize);
        //     MatlabWriter.Write("../matlab/receive.mat", mat, $"audio_rec");
        // }
    }

    private async Task RunProcessAsync()
    {
        Exception? exception = null;
        try
        {
            await ProcessAsync();
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
        }
    }

    private async Task ProcessAsync()
    {
        while (true)
        {
            var result = await samplesIn.ReadAsync(cts.Token);
            var seq = result.Buffer;

            if (preambleDetection.TrySearch(ref seq))
            {
                samplesIn.AdvanceTo(seq.Start);
                // var writer = new ArrayBufferWriter<byte>();
                var buffer = new byte[maxPacketSize];

                // var count = samples.Count;
                // samples.Add(seq.ToArray());
                while (!demodulator.TryRead(ref seq, buffer))
                {
                    if (result.IsCompleted)
                        return;
                    result = await samplesIn.ReadAsync(cts.Token);
                    seq = result.Buffer;
                    // samples[count] = seq.ToArray();
                }

                Console.WriteLine("//// Receive");
                Console.WriteLine(Convert.ToHexString(buffer));
                var data = new ReadOnlySequence<byte>(buffer);

                data = data.LengthDecode<TLength>(out var lengthValid).RSDecode(Program.eccNums, out var eccValid);
                Console.WriteLine();
                Console.WriteLine($"lengthValid {lengthValid} eccValid {eccValid}");
                Console.WriteLine("////");

                if (lengthValid && eccValid)
                {
                    RxWriter.TryWrite(data);
                }
            }
            else if (result.IsCompleted)
                return;

            samplesIn.AdvanceTo(seq.Start);
        }
    }
    public ValueTask<ReadOnlySequence<byte>> ReadAsync(CancellationToken ct) => RxReader.TryReadAsync(ct);
}

public class CSMAPhy<TSample, TLength>
    : IOutChannel<ReadOnlySequence<byte>>, IInChannel<ReadOnlySequence<byte>>, IAsyncDisposable
    where TSample : unmanaged, INumber<TSample>
    where TLength : IBinaryInteger<TLength>
{

    private readonly AsyncExclusiveLock sendLock = new();
    private readonly AsyncTrigger quietTrigger = new();
    private bool quiet = false;

    private readonly IInStream<TSample> samplesIn;
    private readonly IOutChannel<ReadOnlySequence<float>> samplesOut;

    private readonly ISequnceReader<TSample, byte> demodulator;
    private readonly ISequnceReader<byte, float> modulator;

    private readonly ISequnceSearcher<TSample> preambleDetection;
    private readonly ISequnceSearcher<TSample> carrierSensor;

    private readonly Channel<ReadOnlySequence<byte>> channelRx = Channel.CreateUnbounded<ReadOnlySequence<byte>>();
    private ChannelReader<ReadOnlySequence<byte>> RxReader => channelRx.Reader;
    private ChannelWriter<ReadOnlySequence<byte>> RxWriter => channelRx.Writer;

    private readonly Sequence<float> sequence = new();
    private readonly CancellationTokenSource cts = new();
    private readonly Task processTask;
    private readonly int maxPacketSize;

    // private readonly List<TSample[]> samples = [];

    public CSMAPhy(
        IInStream<TSample> inStream,
        IOutChannel<ReadOnlySequence<float>> outChannel,
        ISequnceReader<TSample, byte> demodulator,
        ISequnceReader<byte, float> modulator,
        ISequnceSearcher<TSample> preambleDetection,
        ISequnceSearcher<TSample> carrierSensor,
        int maxPacketSize
    )
    {
        samplesIn = inStream;
        samplesOut = outChannel;
        this.demodulator = demodulator;
        this.modulator = modulator;
        this.preambleDetection = preambleDetection;
        this.carrierSensor = carrierSensor;

        processTask = Task.Run(RunProcessAsync);
        this.maxPacketSize = maxPacketSize;
    }
    public ValueTask<ReadOnlySequence<byte>> ReadAsync(CancellationToken ct) => RxReader.TryReadAsync(ct);

    public async ValueTask WriteAsync(ReadOnlySequence<byte> data, CancellationToken ct)
    {
        // data.MacGet(out var mac);
        data = data.ScramblerEncode().CrcEncode().LengthEncode<TLength>();
        // Console.WriteLine("//// Send");
        // Console.WriteLine(Convert.ToHexString(data.ToArray()));
        // Console.WriteLine("////");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);

        using (await sendLock.AcquireLockAsync(linked.Token))
        {

            modulator.TryRead(ref data, sequence);

            while (!quiet)
                await quietTrigger.WaitAsync(linked.Token);
            await samplesOut.WriteAsync(sequence.AsReadOnlySequence, linked.Token);
            sequence.Reset();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await cts.CancelAsync();
        await processTask.WaitAsync(CancellationToken.None);
        cts.Dispose();
        RxWriter.TryComplete();

        // if (samples.Count > 0 && false)
        // {
        //     var length = samples.Select(x => x.Length).Max();
        //     var samplesResize = samples.Select(x => x.Concat(Enumerable.Repeat(default(TSample), length -
        //     x.Length))); var mat = Matrix<TSample>.Build.DenseOfRows(samplesResize);
        //     MatlabWriter.Write("../matlab/receive.mat", mat, $"audio_rec");
        // }
    }

    private async Task RunProcessAsync()
    {
        Exception? exception = null;
        try
        {
            await ProcessAsync();
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
        }
    }
    private async Task ProcessAsync()
    {
        async ValueTask<ReadResult<TSample>> ReadAsync()
        {
            var result = await samplesIn.ReadAsync(cts.Token);
            var seq = result.Buffer;

            if (quiet = !carrierSensor.TrySearch(ref seq))
                quietTrigger.Signal();
            return result;
        }

        bool TryGetLength(ReadOnlySequence<TSample> samples, out int length)
        {
            Span<byte> buffer = stackalloc byte[BinaryIntegerTrait<TLength>.Size];
            if (demodulator.TryRead(ref samples, buffer))
            {
                length = Math.Clamp(
                    int.CreateChecked(TLength.ReadLittleEndian(buffer, true)),
                    BinaryIntegerTrait<TLength>.Size,
                    maxPacketSize
                );
                return true;
            }
            length = default;
            return false;
        }

        while (true)
        {

            var result = await ReadAsync();
            var seq = result.Buffer;

            if (preambleDetection.TrySearch(ref seq))
            {
                samplesIn.AdvanceTo(seq.Start);
                var writer = new ArrayBufferWriter<byte>();

                var length = 0;
                while (!TryGetLength(seq, out length))
                {
                    if (result.IsCompleted)
                        return;

                    result = await ReadAsync();
                    seq = result.Buffer;
                }

                var buffer = new byte[length];

                // var count = samples.Count;
                // samples.Add(seq.ToArray());
                while (!demodulator.TryRead(ref seq, buffer))
                {
                    if (result.IsCompleted)
                        return;

                    result = await ReadAsync();
                    seq = result.Buffer;

                    // samples[count] = seq.ToArray();
                }

                var data = new ReadOnlySequence<byte>(writer.WrittenMemory);
                // Console.WriteLine("//// Receive");
                // Console.WriteLine(Convert.ToHexString(data.ToArray()));
                // Console.WriteLine("////");
                // Console.WriteLine();

                data = data.LengthDecode<TLength>(out var valid);
                if (valid)
                    data = data.CrcDecode(out valid);

                if (valid)
                    RxWriter.TryWrite(data.ScramblerDecode());
                else
                    Console.WriteLine("Invalid packet received");
            }
            else if (result.IsCompleted)
                return;

            samplesIn.AdvanceTo(seq.Start);
        }
    }
}