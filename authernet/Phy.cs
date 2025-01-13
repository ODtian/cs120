using System.Buffers;
using System.IO.Pipelines;
using System.Numerics;
using System.Threading.Channels;
using CommunityToolkit.HighPerformance;
using CS120.Modulate;
using CS120.Packet;
using CS120.Preamble;
using CS120.TxRx;
using CS120.Utils;
using CS120.Utils.Extension;
using CS120.Utils.IO;
using CS120.Utils.Wave;
using DotNext.Threading;
using MathNet.Numerics.Data.Matlab;
using MathNet.Numerics.LinearAlgebra;
using NAudio.Wave;
using Nerdbank.Streams;

namespace CS120.Phy;

// public class PhyTransmitter
// (WaveFormat waveFormat,
//  PipeWriter pipeWriter,
//  IPreamble preamble,
//  IPipeWriter<byte> modulator,
//  // IPipeWriter<byte> bufferWriter,
//  int quietSamples = 4800)
//     : ITransmitter, IDisposable
// {
//     // private readonly Pipe pipe = new(new PipeOptions(pauseWriterThreshold: 0));
//     private readonly Channel<byte[]> channel = Channel.CreateUnbounded<byte[]>();
//     // private readonly IPreamble preamble = preamble;
//     // private readonly IPipeWriter<byte> modulator = modulator;
//     // private readonly PipeWriter pipeWriter = pipeWriter;
//     private readonly byte[] quietBuffer = new byte[waveFormat.ConvertSamplesToByteSize(quietSamples)];
//     public ChannelWriter<byte[]> Tx => channel.Writer;

//     public async Task Execute(CancellationToken ct)
//     {

//         await Task.Run(
//             async () =>
//             {
//                 await foreach (var data in channel.Reader.ReadAllAsync(ct))
//                 {
//                     Console.WriteLine(data.Length);
//                     // pipe.Writer.Write(preamble.Samples.AsBytes());
//                     pipeWriter.Write(preamble.Samples.Span.AsBytes());
//                     modulator.Write(data);
//                     pipeWriter.Write(quietBuffer);

//                     await pipeWriter.FlushAsync(ct).ConfigureAwait(false);
//                     // modulator.Modulate(data);
//                     // pipe.Writer.Write(quietBuffer);
//                     // await pipe.Writer.FlushAsync(ct);
//                 }
//             },
//             ct
//         );
//     }

//     public void Dispose()
//     {
//     }
// }

// public class PhyReceiver
// (IPipeAdvance preambleDetection, IPipeReader<byte> demodulator, DemodulateLength demodulateLength)
//     : IReceiver, IDisposable
// {
//     // private readonly Pipe pipe = new(new PipeOptions(pauseWriterThreshold: 0, resumeWriterThreshold: 0));
//     private readonly Channel<byte[]> channel = Channel.CreateUnbounded<byte[]>();
//     // private readonly IPipeAdvance preambleDetection;
//     // private readonly IPipeReader<byte> demodulator;

//     // private readonly DemodulateLength demodulateLength;

//     // public PipeWriter Samples { get; }
//     public ChannelReader<byte[]> Rx => channel.Reader;
//     // public ReceiverPhy
//     // {
//     //     this.preambleDetection = preambleDetection;
//     //     this.demodulator = demodulator;
//     //     this.demodulateLength = demodulateLength;

//     //     Rx = channel.Reader;
//     //     // Samples = pipe.Writer;
//     // }

//     public async Task Execute(CancellationToken ct)
//     {
//         try
//         {
//             await Task.Run(async () => await Work(ct), ct);
//         }
//         catch (Exception e)
//         {
//             channel.Writer.TryComplete(e);
//         }
//     }

//     private async Task Work(CancellationToken ct)
//     {
//         if (demodulateLength is DemodulateLength.FixedLength(int length))
//         {
//             while (true)
//             {
//                 while (preambleDetection.TryAdvance())
//                 {
//                     ct.ThrowIfCancellationRequested();
//                 }
//                 var packet = new byte[length];
//                 if (!demodulator.TryReadTo(packet))
//                 {
//                     break;
//                 }
//                 await channel.Writer.WriteAsync(packet, ct);
//             }
//         }
//         else if (demodulateLength is DemodulateLength.VariableLength(int numLengthByte))
//         {
//             var lengthByte = new byte[numLengthByte];
//             while (true)
//             {
//                 while (preambleDetection.TryAdvance())
//                 {
//                     ct.ThrowIfCancellationRequested();
//                 }

//                 if (!demodulator.TryReadTo(lengthByte))
//                 {
//                     break;
//                 }

//                 var packet = new byte[BitConverter.ToInt32(lengthByte)];
//                 if (!demodulator.TryReadTo(packet))
//                 {
//                     break;
//                 }
//                 await channel.Writer.WriteAsync(packet, ct);
//             }
//         }
//         else
//         {
//             throw new InvalidOperationException();
//         }
//         // await channel.Writer.WriteAsync(new byte[12], ct);
//         // var success = demodulator.Demodulate(out var data);
//         // if (success)
//         // {
//         //     await channel.Writer.WriteAsync(data, ct);
//         // }

//         channel.Writer.TryComplete();
//     }

//     public void Dispose()
//     {
//         channel.Writer.TryComplete();
//     }
// }

// public class CSMAPhyHalfDuplex
// (PipeWriter pipeWriter,
//  PipeReader pipeReader,
//  WaveFormat waveFormatWrite,
//  WaveFormat waveFormatRead,
//  IPipeAdvance carrierSensor,
//  IPreamble preamble,
//  IPipeWriter<byte> modulator,
//  IPipeAdvance preambleDetection,
//  IPipeReader<byte> demodulator,
//  int maxBytePerPacket,
//  int quietSamples = 4800,
//  float baseBackOffTime = 0.1f)
//     : DuplexBase
// {
//     public enum CSMAState
//     {
//         CarrierSense,
//         FrameDetect,
//         Send,
//         Quit
//     }

//     private Task delay = Task.CompletedTask;

//     // private readonly PipeWriter pipeWriter = pipeWriter;
//     // private readonly PipeReader pipeReader = pipeReader;
//     // private readonly WaveFormat waveFormatWrite = waveFormatWrite;
//     // private readonly WaveFormat waveFormatRead = waveFormatRead;

//     // private readonly IPipeAdvance carrierSense = carrierSense;
//     // private readonly IPreamble preamble = preamble;
//     // private readonly IPipeWriter<byte> modulator = modulator;
//     // private readonly IPipeAdvance preambleDetection = preambleDetection;
//     protected readonly IPipeReader<byte> demodulator = demodulator;
//     protected readonly int maxBytePerPacket = maxBytePerPacket;

//     // private readonly DemodulateLength demodulateLength;
//     private readonly byte[] quietBuffer = new byte[waveFormatWrite.ConvertSamplesToByteSize(quietSamples)];
//     private readonly TimeSpan baseBackOffTime = TimeSpan.FromSeconds(baseBackOffTime);

//     public async Task Execute(CancellationToken ct)
//     {
//         await Task.Run(() => Start(ct), ct);
//     }

//     private async Task Start(CancellationToken ct)
//     {
//         CSMAState state = CSMAState.CarrierSense;
//         try
//         {
//             while (true)
//             {
//                 state = state switch
//                 {
//                     CSMAState.CarrierSense => await CarrierSense(ct),
//                     CSMAState.FrameDetect => await FrameDetect(ct),
//                     CSMAState.Send => await Send(ct),
//                     _ => state
//                 };

//                 if (state is CSMAState.Quit)
//                 {
//                     Console.WriteLine("Quit");
//                     channelRx.Writer.TryComplete();
//                     return;
//                 }

//                 ct.ThrowIfCancellationRequested();
//             }
//         }
//         catch (Exception e)
//         {
//             channelRx.Writer.TryComplete(e);
//         }
//     }
//     private ValueTask<CSMAState> CarrierSense(CancellationToken ct)
//     {
//         while (carrierSensor.TryAdvance())
//         {
//             ct.ThrowIfCancellationRequested();
//             if (channelTx.Reader.Count > 0 && delay.IsCompleted)
//                 return ValueTask.FromResult(CSMAState.Send);
//         }

//         /* Collision! */
//         if (channelTx.Reader.Count > 0)
//             delay = Task.Delay(baseBackOffTime, ct);

//         return ValueTask.FromResult(CSMAState.FrameDetect);
//     }

//     private async ValueTask<CSMAState> FrameDetect(CancellationToken ct)
//     {

//         if (preambleDetection.TryAdvance())
//         {
//             return CSMAState.CarrierSense;
//         }

//         if (!TryGetLength(out var packetLength))
//             return CSMAState.Quit;

//         var packet = new byte[packetLength];
//         if (!demodulator.TryReadTo(packet))
//             return CSMAState.Quit;

//         await channelRx.Writer.WriteAsync(packet, ct);

//         return CSMAState.CarrierSense;
//     }

//     private async ValueTask<CSMAState> Send(CancellationToken ct)
//     {
//         var packet = await channelTx.Reader.ReadAsync(ct);

//         pipeWriter.Write(preamble.Samples.Span.AsBytes());
//         modulator.Write(packet);
//         pipeWriter.Write(quietBuffer);

//         await pipeWriter.FlushAsync(ct).ConfigureAwait(false);

//         return CSMAState.CarrierSense;
//     }

//     protected virtual bool TryGetLength(out int length)
//     {
//         length = maxBytePerPacket;
//         return true;
//     }
// }

// public class CSMAPhyHalfDuplex<T>(
//     PipeWriter pipeWriter,
//     PipeReader pipeReader,
//     WaveFormat waveFormatWrite,
//     WaveFormat waveFormatRead,
//     IPipeAdvance carrierSensor,
//     IPreamble preamble,
//     IPipeWriter<byte> modulator,
//     IPipeAdvance preambleDetection,
//     IPipeReader<byte> demodulator,
//     // DemodulateLength demodulateLength,
//     int maxBytePerPacket,
//     int quietSamples = 4800,
//     float baseBackOffTime = 0.1f
// )
//     : CSMAPhyHalfDuplex(
//           pipeWriter,
//           pipeReader,
//           waveFormatWrite,
//           waveFormatRead,
//           carrierSensor,
//           preamble,
//           modulator,
//           preambleDetection,
//           demodulator,
//           maxBytePerPacket,
//           quietSamples,
//           baseBackOffTime
//       )
//     where T : IBinaryInteger<T>
// {
//     private readonly byte[] lengthBuffer = new byte[BinaryIntegerTrait<T>.Size];
//     protected override bool TryGetLength(out int length)
//     {
//         var success = demodulator.TryReadTo(lengthBuffer, false);

//         length =
//             Math.Min(success ? int.CreateChecked(T.ReadLittleEndian(lengthBuffer, true)) : default,
//             maxBytePerPacket);
//         return success;
//     }
// }
// class PhyUtilDuplex
// (ChannelWriter<byte[]> writer, ChannelReader<byte[]> reader) : DuplexBase
// {
//     public async Task Execute(CancellationToken ct)
//     {
//         channelRx.Writer.TryComplete((await Task.WhenAny(Send(ct), Receive(ct))).Exception);
//     }

//     private async Task Receive(CancellationToken ct)
//     {

//         await foreach (var packet in reader.ReadAllAsync(ct))
//         {
//             var p = packet.LengthDecode<byte>(out var lengthValid);

//             if (!lengthValid)
//                 continue;

//             p = p.RSDecode(Program.eccNums, out var eccValid);

//             Console.WriteLine($"EccValid: {eccValid}");

//             if (!eccValid)
//                 continue;

//             // .LengthDecode<byte>(out var lengthValid)
//             // .IDDecode<byte>(out var id);
//             await channelRx.Writer.WriteAsync(p, ct);
//         }
//     }

//     private async Task Send(CancellationToken ct)
//     {

//         await foreach (var packet in channelTx.Reader.ReadAllAsync(ct))
//         {
//             await writer.WriteAsync(packet.RSEncode(Program.eccNums).LengthEncode<byte>(), ct);
//         }
//     }
// }

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

    private ChannelReader<ReadOnlySequence<byte>> RxReader => channelRx.Reader;
    private ChannelWriter<ReadOnlySequence<byte>> RxWriter => channelRx.Writer;
    private readonly List<TSample[]> samples = [];
    public bool IsCompleted => RxReader.IsFinished();
    public RXPhy(
        IInStream<TSample> inStream,
        ISequnceReader<TSample, byte> demodulator,
        ISequnceSearcher<TSample> preambleDetection
    )
    {
        samplesIn = inStream;
        this.demodulator = demodulator;
        this.preambleDetection = preambleDetection;
        processTask = Task.Run(RunProcessAsync);
    }

    public async ValueTask DisposeAsync()
    {
        await cts.CancelAsync();
        await processTask.WaitAsync(CancellationToken.None);
        cts.Dispose();
        RxWriter.TryComplete();
        if (samples.Count > 0)
        {
            // var length = samples.Select(x => x.Length).Max();
            // var samplesResize = samples.Select(x => x.Concat(Enumerable.Repeat(default(TSample), length -
            // x.Length))); var mat = Matrix<TSample>.Build.DenseOfRows(samplesResize);
            // MatlabWriter.Write("../matlab/receive.mat", mat, $"audio_rec");
        }
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
            // Console.WriteLine(seq.Length);

            if (preambleDetection.TrySearch(ref seq))
            {
                samplesIn.AdvanceTo(seq.Start);
                var writer = new ArrayBufferWriter<byte>();

                var count = samples.Count;

                samples.Add(seq.ToArray());
                while (!demodulator.TryRead(ref seq, writer))
                {
                    if (result.IsCompleted)
                        return;
                    result = await samplesIn.ReadAsync(cts.Token);
                    seq = result.Buffer;
                    samples[count] = seq.ToArray();
                }

                var data = new ReadOnlySequence<byte>(writer.WrittenMemory)
                               .LengthDecode<TLength>(out var lengthValid)
                               .RSDecode(Program.eccNums, out var eccValid);

                Console.WriteLine("//// Receive");
                foreach (var d in data.GetElements())
                {
                    Console.Write($"{d:X2} ");
                }
                Console.WriteLine();
                Console.WriteLine($"lengthValid {lengthValid} eccValid {eccValid}");
                Console.WriteLine("////");

                if (lengthValid && eccValid)
                {
                    await RxWriter.WriteAsync(data);
                }
                // data.MacGet(out var mac);
                // Console.WriteLine($"Receive mac {mac.Source} to {mac.Dest} of {mac.Type} {mac.SequenceNumber}");
            }
            else if (result.IsCompleted)
                return;

            samplesIn.AdvanceTo(seq.Start);
        }
    }
    public async ValueTask<ReadOnlySequence<byte>> ReadAsync(CancellationToken ct) => await RxReader.ReadAsync(ct);
}

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
    // private int i = 0;

    private readonly List<TSample[]> samples = [];
    public async ValueTask WriteAsync(ReadOnlySequence<byte> data, CancellationToken ct)
    {
        Console.WriteLine("//// Send");
        foreach (var d in data.GetElements())
        {
            Console.Write($"{d:X2} ");
        }
        Console.WriteLine();
        Console.WriteLine("////");
        data = data.RSEncode(Program.eccNums).LengthEncode<TLength>();
        using (await sendLock.AcquireLockAsync(ct))
        {

            // var writer = new ArrayBufferWriter<TSample>();
            modulator.TryRead(ref data, sequence);
            samples.Add(sequence.AsReadOnlySequence.ToArray());
            await samplesOut.WriteAsync(sequence.AsReadOnlySequence, ct);
            sequence.Reset();
        }
    }

    public ValueTask DisposeAsync()
    {
        // var length = samples.Select(x => x.Length).Max();
        // var samplesResize = samples.Select(x => x.Concat(Enumerable.Repeat(default(TSample), length - x.Length)));
        // var mat = Matrix<TSample>.Build.DenseOfRows(samplesResize);
        // MatlabWriter.Write("../matlab/send.mat", mat, $"audio");
        // return samplesOut.CompleteAsync();
        return ValueTask.CompletedTask;
    }
}

public class CSMAPhy<TSample, TLength>
    : IOutChannel<ReadOnlySequence<byte>>, IInChannel<ReadOnlySequence<byte>>, IAsyncDisposable
    where TSample : unmanaged, INumber<TSample>
    where TLength : IBinaryInteger<TLength>
{

    private readonly AsyncExclusiveLock sendLock = new();
    private readonly AsyncTrigger quietTrigger = new();

    private readonly IInStream<TSample> samplesIn;
    private readonly IOutChannel<ReadOnlySequence<TSample>> samplesOut;

    private readonly ISequnceReader<TSample, byte> demodulator;
    private readonly ISequnceReader<byte, TSample> modulator;

    private readonly ISequnceSearcher<TSample> preambleDetection;
    private readonly ISequnceSearcher<TSample> carrierSensor;

    private readonly Channel<ReadOnlySequence<byte>> channelRx = Channel.CreateUnbounded<ReadOnlySequence<byte>>();
    private ChannelReader<ReadOnlySequence<byte>> RxReader => channelRx.Reader;
    private ChannelWriter<ReadOnlySequence<byte>> RxWriter => channelRx.Writer;
    private bool quiet = false;

    // private bool quiting = false;

    private readonly Sequence<TSample> sequence = new();
    private readonly CancellationTokenSource cts = new();
    private readonly Task processTask;

    // private int seed;
    private readonly List<TSample[]> samples2 = [];
    private readonly List<TSample[]> samples = [];

    // public bool IsCompleted => RxReader.IsFinished();
    public CSMAPhy(
        IInStream<TSample> inStream,
        IOutChannel<ReadOnlySequence<TSample>> outChannel,
        ISequnceReader<TSample, byte> demodulator,
        ISequnceReader<byte, TSample> modulator,
        ISequnceSearcher<TSample> preambleDetection,
        ISequnceSearcher<TSample> carrierSensor
    )
    {
        samplesIn = inStream;
        samplesOut = outChannel;
        this.demodulator = demodulator;
        this.modulator = modulator;
        this.preambleDetection = preambleDetection;
        this.carrierSensor = carrierSensor;
        // quietBuffer = new byte[quietSamples];
        // quietTrigger = new AsyncTrigger();
        // channelRx = Channel.CreateUnbounded<byte[]>();
        // cts = new CancellationTokenSource();
        // processTask = Task.Run(ProcessAsync);
        processTask = Task.Run(RunProcessAsync);
        // this.seed = seed;
    }
    public async ValueTask<ReadOnlySequence<byte>> ReadAsync(CancellationToken ct) => await RxReader.ReadAsync(ct);

    public async ValueTask WriteAsync(ReadOnlySequence<byte> data, CancellationToken ct)
    {
        // data = data.RSEncode(Program.eccNums)
        data = data.C4B5BEncode().CrcEncode().LengthEncode<TLength>();
        Console.WriteLine("//// Send");
        Console.WriteLine(Convert.ToHexString(data.ToArray()));
        Console.WriteLine("////");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cts.Token);

        using (await sendLock.AcquireLockAsync(linked.Token))
        {
            // while (!quiet)
            // {
            //     await Task.Delay(200 * new Random(seed).Next(1, 4), linked.Token);
            // }
            // if (!quiet)
            // {
            //     var quiet = false;
            //     while (!quiet)
            //     {
            //         var result = await samplesIn.ReadAsync(linked.Token);
            //         var seq = result.Buffer.Slice(Math.Min(0, result.Buffer.Length - 400));
            //         quiet = !carrierSensor.TrySearch(ref seq);
            //         // if (quiet)
            //         //     quietTrigger.Signal();
            //         // samplesIn.AdvanceTo(seq.Start);
            //     }
            // }
            // if (data.Length > 16)
            await quietTrigger.WaitAsync(linked.Token);

            // var writer = new ArrayBufferWriter<TSample>();
            modulator.TryRead(ref data, sequence);
            samples2.Add(sequence.AsReadOnlySequence.ToArray());

            await samplesOut.WriteAsync(sequence.AsReadOnlySequence, linked.Token);
            sequence.Reset();

            // await Task.Delay(40, linked.Token);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await cts.CancelAsync();
        await processTask.WaitAsync(CancellationToken.None);
        cts.Dispose();
        RxWriter.TryComplete();

        if (samples.Count > 0 && false)
        {
            var length = samples.Select(x => x.Length).Max();
            var samplesResize = samples.Select(x => x.Concat(Enumerable.Repeat(default(TSample), length - x.Length)));
            var mat = Matrix<TSample>.Build.DenseOfRows(samplesResize);
            MatlabWriter.Write("../matlab/receive.mat", mat, $"audio_rec");
        }
        if (samples2.Count > 0 && false)
        {
            var length = samples2.Select(x => x.Length).Max();
            var samplesResize = samples2.Select(x => x.Concat(Enumerable.Repeat(default(TSample), length - x.Length)));
            var mat = Matrix<TSample>.Build.DenseOfRows(samplesResize);
            MatlabWriter.Write("../matlab/receive2.mat", mat, $"audio_rec");
        }
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
        // var buf = new TSample[480];
        // buf.AsSpan().Fill(TSample.One * TSample.CreateChecked(1));
        while (true)
        {

            var result = await samplesIn.ReadAsync(cts.Token);
            // var originalLength = seq.Length;
            var seq = result.Buffer;
            // var x = seq.Slice(originalLength);
            // Console.WriteLine(quiet);
            // if (quiet && new Random().NextSingle() < 0.5)
            if (!carrierSensor.TrySearch(ref seq))
                quietTrigger.Signal();
            // if (carrierSensor.TrySearch(ref seq) == quiet)
            // {
            //     quiet = !quiet;
            //     if (quiet)
            //         quietTrigger.Signal();
            // }

            if (preambleDetection.TrySearch(ref seq))
            {
                samplesIn.AdvanceTo(seq.Start);
                var writer = new ArrayBufferWriter<byte>();

                var count = samples.Count;
                samples.Add(seq.ToArray());

                while (!demodulator.TryRead(ref seq, writer))
                {
                    if (result.IsCompleted)
                        return;
                    result = await samplesIn.ReadAsync(cts.Token);
                    // originalLength = seq.Length;
                    // seq = result.Buffer;
                    // x = seq.Slice(originalLength);
                    // quiet = !carrierSensor.TrySearch(ref x);
                    // if (quiet)
                    //     quietTrigger.Signal();
                    seq = result.Buffer;
                    samples[count] = seq.ToArray();
                }
                // Console.WriteLine(DateTime.Now - ts);
                // await samplesOut.WriteAsync(new ReadOnlySequence<TSample>(buf), cts.Token);
                var data = new ReadOnlySequence<byte>(writer.WrittenMemory);
                Console.WriteLine("//// Receive");
                Console.WriteLine(Convert.ToHexString(data.ToArray()));
                // Console.WriteLine($"lengthValid {lengthValid} eccValid {eccValid}");
                data = data.LengthDecode<TLength>(out var lengthValid);

                var eccValid = false;
                if (lengthValid)
                    data = data.CrcDecode(out eccValid);

                if (eccValid)
                    data = data.C4B5BDecode();
                // .RSDecode(Program.eccNums, out var eccValid);
                data.MacGet(out var mac);
                Console.WriteLine($"lengthValid {lengthValid} eccValid {eccValid}");
                Console.WriteLine(
                    $"Receive mac {mac.Source} to {mac.Dest} of Seq {mac.SequenceNumber} Ack {mac.AckNumber}"
                );
                Console.WriteLine("////");
                Console.WriteLine();

                if (lengthValid && eccValid)
                {
                    await RxWriter.WriteAsync(data);
                }
            }
            else if (result.IsCompleted)
            {
                return;
            }

            // if (carrierSensor.TrySearch(ref seq))
            // {
            //     quiet = false;
            //     if (preambleDetection.TrySearch(ref seq))
            //     {
            //         samplesIn.AdvanceTo(seq.Start);
            //         var writer = new ArrayBufferWriter<byte>();
            //         while (!demodulator.TryRead(ref seq, writer))
            //         {
            //             if (result.IsCompleted)
            //                 return;
            //             result = await samplesIn.ReadAsync(cts.Token);
            //             seq = result.Buffer;
            //         }

            //         var data = new ReadOnlySequence<byte>(writer.WrittenMemory)
            //                        .LengthDecode<byte>(out var lengthValid)
            //                        .RSDecode(Program.eccNums, out var eccValid);
            //         // Console.WriteLine("//// Receive");
            //         // foreach (var d in data.GetElements())
            //         // {
            //         //     Console.Write($"{d:X2} ");
            //         // }
            //         // Console.WriteLine();
            //         // Console.WriteLine($"lengthValid {lengthValid} eccValid {eccValid}");
            //         // Console.WriteLine("////");
            //         // Console.WriteLine($"lengthValid {lengthValid} eccValid {eccValid}");

            //         if (lengthValid && eccValid)
            //         {
            //             await RxWriter.WriteAsync(data);
            //             data.MacGet(out var mac);
            //             Console.WriteLine($"Receive mac {mac.Source} to {mac.Dest} of {mac.Type}
            //             {mac.SequenceNumber}");
            //         }
            //     }
            //     else if (samplesIn.IsCompleted)
            //         return;
            // }
            // else if (result.IsCompleted)
            //     return;
            // else
            // {
            //     quiet = true;
            //     quietTrigger.Signal();
            // }

            samplesIn.AdvanceTo(seq.Start);
        }
    }
}