#define ASIO

using System.CommandLine;
using System.IO.Pipelines;
using System.Text;
using CS120.CarrierSense;
using CS120.Modulate;
using CS120.Packet;
using CS120.Phy;
using CS120.Preamble;
using CS120.Utils;
using CS120.Utils.Helpers;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using CS120.Utils.Wave;
using CS120.Symbol;
using CS120.Mac;
using System.Buffers;
using DotNext;
using WinTun;
using System.Threading.Channels;
using PacketDotNet;
using System.Net;
using System.CommandLine.Invocation;
namespace CS120.Commands;

public static class CommandTask
{
    // public static async Task AudioCommandTask(FileInfo? play, FileInfo? record, bool recordPlayBack, int duration)
    // {
    //     // var audioCtses = new CancellationTokenSource[2] { new(duration), new(duration) };
    //     using var cancelToken = new CancelKeyPressCancellationTokenSource(new CancellationTokenSource(duration));
    //     using var cancelToken1 = new CancelKeyPressCancellationTokenSource(new CancellationTokenSource(), false);

    //     var taskPlay = play switch {
    //         FileInfo => Audio.Play<WasapiOut>(play.FullName, cancelToken.Source.Token),
    //         null => Task.CompletedTask,
    //     };

    //     var taskRecord = (record, recordPlayBack) switch {
    //         (_, true) => Audio.RecordThenPlay<WasapiCapture, WasapiOut>(new[] { cancelToken, cancelToken1 }.Select(
    //             cts =>
    //             {
    //                 cts.Enable(true);
    //                 cts.Source.CancelAfter(duration);
    //                 return cts.Source.Token;
    //             }
    //         )),
    //         (FileInfo, _) => Audio.Record<WasapiCapture>(record.FullName, cancelToken.Source.Token),
    //         _ => Task.CompletedTask,
    //     };

    //     var taskUni = Task.WhenAll(taskPlay, taskRecord);

    //     await taskUni;
    // }

    // public static async Task SendCommandTask(FileInfo? file, FileInfo? toWav, bool binaryTxt)
    // {
    //     ArgumentNullException.ThrowIfNull(file);

    //     using var cts = new CancelKeyPressCancellationTokenSource(new CancellationTokenSource());

    //     var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));

    //     // var wavePro
    //     // var play = Task.CompletedTask;
    //     var waveFormat = Audio.GetPlayerWaveFormat<WasapiOut>();
    //     var provider = new NonBlockingPipeWaveProvider(
    //         WaveFormat.CreateIeeeFloatWaveFormat(waveFormat.SampleRate, 1), pipe.Reader
    //     );

    //     var play = toWav switch {
    //         null => Audio.Play<WasapiOut>(provider, cts.Source.Token),
    //         FileInfo => Task.Run(
    //             () =>
    //             {
    //                 using var writer = new WaveFileWriter(toWav.FullName, provider.WaveFormat);
    //                 pipe.Reader.AsStream().CopyTo(writer);
    //             }
    //         ),
    //     };

    //     using var transmitter = new PhyTransmitter(
    //         waveFormat,
    //         pipe.Writer,
    //         new ChirpPreamble(Program.chirpOption with { SampleRate = waveFormat.SampleRate }),
    //         new DPSKModulator(pipe.Writer, Program.option with { SampleRate = waveFormat.SampleRate }),
    //         // new OFDMModulator(
    //         //     pipe,
    //         //     [
    //         //         option with { SampleRate = waveFormat.SampleRate },
    //         //         option with {
    //         //             Freq = option.Freq * 2,
    //         //             NumRedundant = option.NumRedundant * 2,
    //         //             SampleRate = waveFormat.SampleRate
    //         //         }
    //         //     ]
    //         // ),
    //         // sendFormat.ConvertLatencyToByteSize
    //         0
    //         // sendFormat.ConvertLatencyToByteSize(1)
    //     );

    //     var exec = transmitter.Execute(cts.Source.Token);

    //     var dataPipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));

    //     var index = 0;

    //     await foreach (var data in FileHelper.ReadFileChunk(file, Program.dataNum, binaryTxt, cts.Source.Token))
    //     {
    //         await transmitter.Tx.WriteAsync(
    //             data.IDEncode<byte>(index++)
    //                 .LengthEncode<byte>(Program.dataNum + Program.idNum)
    //                 .RSEncode(Program.eccNums),
    //             cts.Source.Token
    //         );
    //     }

    //     transmitter.Tx.Complete();

    //     await exec;

    //     pipe.Writer.Complete();

    //     await play;
    // }
    // public static async Task ReceiveCommandTask(FileInfo? fromWav, FileInfo? file, bool binaryTxt)
    // {
    //     using var cts = new CancelKeyPressCancellationTokenSource(new CancellationTokenSource());

    //     var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));

    //     Task rec;
    //     WaveFormat waveFormat;

    //     if (fromWav is null)
    //     {
    //         rec = Audio.Record<WasapiCapture>(pipe.Writer.AsStream(), cts.Source.Token);
    //         waveFormat = Audio.GetRecorderWaveFormat<WasapiCapture>();
    //     }
    //     else
    //     {
    //         rec = Task.CompletedTask;
    //         using var reader = new WaveFileReader(fromWav.FullName);
    //         waveFormat = reader.WaveFormat;
    //         reader.CopyTo(pipe.Writer.AsStream());
    //         pipe.Writer.Complete();
    //     }
    //     // Console.WriteLine(waveFormat.SampleRate);

    //     using var receiver = new PhyReceiver(
    //         new PreambleDetection(
    //             pipe.Reader,
    //             waveFormat,
    //             new ChirpPreamble(Program.chirpOption with { SampleRate = waveFormat.SampleRate }),
    //             Program.corrThreshold,
    //             Program.smoothedEnergyFactor,
    //             Program.maxPeakFalling
    //         ),
    //         new DPSKDemodulator(pipe.Reader, waveFormat, Program.option with { SampleRate = waveFormat.SampleRate }),
    //         // new OFDMDemodulator(
    //         //     pipe,
    //         //     waveFormat,
    //         //     [
    //         //         option with { SampleRate = waveFormat.SampleRate },
    //         //         option with {
    //         //             Freq = option.Freq * 2,
    //         //             NumRedundant = option.NumRedundant * 2,
    //         //             SampleRate = waveFormat.SampleRate
    //         //         }
    //         //     ]
    //         // ),
    //         new DemodulateLength.FixedLength(Program.dataLengthInByte)
    //     );
    //     var exec = receiver.Execute(cts.Source.Token);

    //     // var index = 0;
    //     using var stream = file switch { null => Console.OpenStandardOutput(), FileInfo => file.OpenWrite() };
    //     await foreach (var packet in receiver.Rx.ReadAllAsync(cts.Source.Token))
    //     {
    //         var p = packet.RSDecode(Program.eccNums, out var eccValid)
    //                     .LengthDecode<byte>(out var lengthValid)
    //                     .IDDecode<byte>(out var id);

    //         Console.WriteLine(
    //             $"Receive a packet: Length {p.Length}, eccValid: {eccValid}, lengthValid: {lengthValid}, {id}"
    //         );

    //         if (binaryTxt)
    //             foreach (var b in p)
    //                 stream.Write(Encoding.ASCII.GetBytes(Convert.ToString(b, 2).PadLeft(8, '0')));
    //         else
    //             stream.Write(p);
    //     }

    //     await exec;

    //     pipe.Reader.Complete();

    //     await rec;
    // }

    // public static async Task DuplexCommandTask(
    //     byte addressSource,
    //     byte addressDest,
    //     FileInfo? send,
    //     FileInfo? receive,
    //     bool binaryTxt,
    //     float sleep,
    //     string? render,
    //     string? capture
    // )
    // {
    //     using var cts = new CancelKeyPressCancellationTokenSource(new());
    //     var pipeRec = new Pipe(new PipeOptions(pauseWriterThreshold: 0));
    //     var pipePlay = new Pipe(new PipeOptions(pauseWriterThreshold: 0));

    //     var player =
    //         render switch { null => new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render,
    //         Role.Multimedia),
    //                         _ => Audio.GetWASAPIDevice(render, DataFlow.Render) };

    //     var recorder =
    //         capture switch { null =>
    //                              new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia),
    //                          _ => Audio.GetWASAPIDevice(capture, DataFlow.Capture) };

    //     var wasapiIn = new WasapiCapture(recorder, true, 20);

    //     var inStream = pipeRec.Writer.AsStream();
    //     wasapiIn.DataAvailable += (s, e) => inStream.Write(e.Buffer, 0, e.BytesRecorded);

    //     var wasapiOut = new WasapiOut(player, AudioClientShareMode.Shared, true, 20);

    //     wasapiOut.Init(new NonBlockingPipeWaveProvider(
    //         WaveFormat.CreateIeeeFloatWaveFormat(wasapiOut.OutputWaveFormat.SampleRate, 1), pipePlay.Reader
    //     ));

    //     var rec = Audio.Record(wasapiIn, cts.Source.Token);
    //     var play = Audio.Play(wasapiOut, cts.Source.Token);
    //     // var waveFormatPlay = audioManager.GetPlayerWaveFormat<WasapiOut>();
    //     // var waveFormatReceive = audioManager.GetRecorderWaveFormat<WasapiCapture>();

    //     // foreach (var device in new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Render,
    //     // DeviceState.Active))
    //     // {
    //     //     Console.WriteLine(device.);
    //     // }
    //     // var rec = audioManager.Record<WasapiCapture>(pipeRec.Writer.AsStream(), cts.Source.Token);
    //     // var play = audioManager.Play<WasapiOut>(
    //     //     new NonBlockingPipeWaveProvider(
    //     //         WaveFormat.CreateIeeeFloatWaveFormat(waveFormatPlay.SampleRate, 1), pipePlay.Reader
    //     //     ),
    //     //     cts.Source.Token
    //     // );
    //     var preamble = new ChirpPreamble(Program.chirpOption with { SampleRate = wasapiIn.WaveFormat.SampleRate });

    //     using var csma = new CSMAPhyHalfDuplex<byte>(
    //         pipePlay.Writer,
    //         pipeRec.Reader,
    //         wasapiOut.OutputWaveFormat,
    //         wasapiIn.WaveFormat,
    //         new CarrierSensor(pipeRec.Reader, wasapiIn.WaveFormat),
    //         preamble,
    //         new DPSKModulator(
    //             pipePlay.Writer, Program.option with { SampleRate = wasapiOut.OutputWaveFormat.SampleRate }
    //         ),
    //         new PreambleDetection(
    //             pipeRec.Reader,
    //             wasapiIn.WaveFormat,
    //             preamble,
    //             Program.corrThreshold,
    //             Program.smoothedEnergyFactor,
    //             Program.maxPeakFalling
    //         ),
    //         new DPSKDemodulator(
    //             pipeRec.Reader, wasapiIn.WaveFormat, Program.option with { SampleRate =
    //             wasapiIn.WaveFormat.SampleRate }
    //         ),
    //         256,
    //         128,
    //         0.5f
    //     );

    //     using var util = new PhyUtilDuplex(csma.Tx, csma.Rx);
    //     // // using var mac = new MacDuplex(util.Tx, util.Rx, address);

    //     var daemon = Task.WhenAny(csma.Execute(cts.Source.Token), util.Execute(cts.Source.Token));

    //     int index = 0;
    //     try
    //     {
    //         await foreach (var packet in FileHelper.ReadFileChunk(send, 128, binaryTxt, cts.Source.Token))
    //         {
    //             await util.Tx.WriteAsync(packet.IDEncode<byte>(index++), cts.Source.Token);
    //         }
    //         Console.WriteLine(index);
    //     }
    //     catch (Exception e)
    //     {
    //         Console.WriteLine(e);
    //     }

    //     await foreach (var packet in util.Rx.ReadAllAsync(cts.Source.Token))
    //     {

    //         packet.IDGet<byte>(out var id);
    //         Console.WriteLine(id);
    //     }
    //     // Console.WriteLine("Done");
    //     await daemon;

    //     // pipe 大小调整为和系统buffer差不多
    //     // var daemon =
    //     //     Task.WhenAll(csma.Execute(cts.Source.Token), util.Execute(cts.Source.Token),
    //     //     mac.Execute(cts.Source.Token));
    // }
    public static async Task SendCommandTaskAsync(FileInfo? file, FileInfo? toWav, bool binaryTxt, int render)
    {
        ArgumentNullException.ThrowIfNull(file);

        using var cts = new CancelKeyPressCancellationTokenSource(new());

        using var player = Audio.GetWASAPIDevice(render, DataFlow.Render);

        using var wasapiOut = new WasapiOut(player, AudioClientShareMode.Shared, false, 50);

        Console.WriteLine(wasapiOut.OutputWaveFormat.SampleRate);

        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(wasapiOut.OutputWaveFormat.SampleRate, 1);
        await using var audioOut = new AudioOutChannel(waveFormat);

        await using var audioOutPipe = new AudioPipeOutChannel(wasapiOut.OutputWaveFormat);

        // var recordTask = Audio.RecordAsync(wasapiIn, cts.Source.Token);
        // var playTask = Audio.PlayAsync(wasapiOut, cts.Source.Token);

        // var wavePro
        // var play = Task.CompletedTask;
        // var provider = new NonBlockingPipeWaveProvider(
        //     WaveFormat.CreateIeeeFloatWaveFormat(waveFormat.SampleRate, 1), pipe.Reader
        // );
        Task play;
        IOutChannel<ReadOnlySequence<float>> outChannel;

        if (toWav is null)
        {
            wasapiOut.Init(audioOut.SampleProvider.ToWaveProvider());
            play = Audio.PlayAsync(wasapiOut, cts.Source.Token);
            outChannel = audioOut;
        }
        else
        {
            async Task CopyToAudioAsync()
            {
                using var writer = new WaveFileWriter(toWav.FullName, waveFormat);
                using var stream = audioOutPipe.Reader.AsStream();
                await stream.CopyToAsync(writer);
            }
            play = CopyToAudioAsync();

            outChannel = audioOutPipe;
        }

        var symbols = new DPSKSymbol<float>(Program.option with { SampleRate = waveFormat.SampleRate });
        var modulator = new Modulator<ChirpPreamble<float>, DPSKSymbol<float>>(
            new ChirpPreamble<float>(symbols: Program.chirpOption with { SampleRate = waveFormat.SampleRate }), symbols
        );
        await using var tx = new TXPhy<float>(outChannel, modulator);

        var index = 0;
        var warmup = new WarmupPreamble<DPSKSymbol<float>, float>(symbols, 2000);

        await outChannel.WriteAsync(new ReadOnlySequence<float>(warmup.Samples), cts.Source.Token);
        await Task.Delay(500);

        await foreach (var data in FileHelper.ReadFileChunkAsync(file, 128, binaryTxt, cts.Source.Token))
        {
            await tx.WriteAsync(new ReadOnlySequence<byte>(data).IDEncode<byte>(index++), cts.Source.Token);
            // await tx.WriteAsync(new ReadOnlySequence<byte>(new byte[128]).IDEncode<byte>(index++), cts.Source.Token);
            await Task.Delay(200);
        }
        await audioOutPipe.CompleteAsync();

        await play;
    }

    public static async Task ReceiveCommandTaskAsync(FileInfo? fromWav, FileInfo? file, bool binaryTxt, int capture)
    {
        using var cts = new CancelKeyPressCancellationTokenSource(new());

        var recorder = Audio.GetWASAPIDevice(capture, DataFlow.Capture);

        // using AudioMonoInStream<float>? audioIn =
        //     new AudioMonoInStream<float>(Audio.GetRecorderWaveFormat<WasapiCapture>(), 0);

        // var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));

        Task rec;
        WaveFormat waveFormat;
        IInStream<float> audioIn;

        // using var asio = new AsioOut();

        if (fromWav is null)
        {
            var wasapiIn = new WasapiLoopbackCapture();
            // var wasapiIn = new WasapiCapture(recorder, true, 10);
            waveFormat = wasapiIn.WaveFormat;
            // waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 0);
            // waveFormat = new WaveFormat(48000, 32, 1);

            var audio = new AudioMonoInStream<float>(waveFormat, 0);

            // asio.InitRecordAndPlayback(null, capture, 48000);
            // asio.ShowControlPanel();
            // Audio.ListAsioDevices();
            // Console.ReadLine();

            wasapiIn.DataAvailable += audio.DataAvailable;
            // asio.AudioAvailable += audio.DataAvailable;

            audioIn = audio;

            rec = Audio.RecordAsync(wasapiIn, cts.Source.Token);
            // rec = Task.CompletedTask;
            // rec = Audio.PlayAsync(asio, cts.Source.Token);
        }
        else
        {
            static async Task CopyToAudioAsync(WaveFileReader reader, PipeWriter writer)
            {
                using var stream = writer.AsStream();
                await reader.CopyToAsync(stream);
                // var buffer = new byte[1900];
                // while (true)
                // {
                //     var read = await reader.ReadAsync(buffer.AsMemory());
                //     if (read == 0)
                //         break;
                //     await writer.WriteAsync(buffer.AsMemory(0, read));
                //     await Task.Delay(10);
                // }
                await writer.CompleteAsync();
            }

            var reader = new WaveFileReader(fromWav.FullName);
            waveFormat = reader.WaveFormat;

            var audio = new AudioPipeInStream<float>(waveFormat);
            audioIn = audio;

            rec = CopyToAudioAsync(reader, audio.Writer);
            // await rec;
        }
        // using (disposable)
        // await using (disposableAsync)
        var preamble = new ChirpPreamble<float>(Program.chirpOption with { SampleRate = waveFormat.SampleRate });

        var demodulator = new Demodulator<LineSymbol<float>, float, byte>(Program.lineOption, 255);
        // var demodulator = new Demodulator<LineSymbol<float>, float, byte>(
        //     Program.lineOption with { SampleRate = waveFormat.SampleRate }, 255
        // );

        // var warmup = new WarmupPreamble<DPSKSymbol<float>, float>(symbols, 2000);

        // await outChannel.WriteAsync(new ReadOnlySequence<float>(warmup.Samples), cts.Source.Token);
        // await Task.Delay(500);

        await using var rx = new RXPhy<float>(
            audioIn,
            demodulator,
            new PreambleDetection<float>(
                preamble, Program.corrThreshold, Program.smoothedEnergyFactor, Program.maxPeakFalling
            )
        );

        // var index = 0;
        using var stream = file switch { null => Console.OpenStandardOutput(), FileInfo => file.OpenWrite() };
        while (true)
        {
            var packet = await rx.ReadAsync(cts.Source.Token);
            if (packet.IsEmpty)
                break;

            packet.IDGet<byte>(out var id);
            // Console.WriteLine(id);
            if (binaryTxt)
                foreach (var b in packet.ToArray())
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(Convert.ToString(b, 2).PadLeft(8, '0')));
            else
                await stream.WriteAsync(packet.ToArray());
        }

        await rec;
    }

    public static async Task DuplexCommandTaskAsync(
        byte addressSource,
        byte addressDest,
        FileInfo? send,
        FileInfo? receive,
        bool binaryTxt,
        float sleep,
        int render,
        int capture
    )
    {
        using var cts = new CancelKeyPressCancellationTokenSource(new());

        using var player = Audio.GetWASAPIDevice(render, DataFlow.Render);
        using var recorder = Audio.GetWASAPIDevice(capture, DataFlow.Capture);

        // using var wasapiIn = new WasapiLoopbackCapture();
        // using var wasapiIn = new WasapiCapture(recorder, true, 0);
        // using var wasapiOut = new WasapiOut(player, AudioClientShareMode.Shared, true, 0);

        // Console.WriteLine(wasapiIn.WaveFormat.SampleRate);
        // Console.WriteLine(wasapiOut.OutputWaveFormat.SampleRate);

        using var asio = new AsioOut() { ChannelOffset = render, InputChannelOffset = capture };
        var recordFormat = new WaveFormat(48000, 32, 1);
        var playbackFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);
        await using var audioIn = new AudioMonoInStream<float>(recordFormat, 0);
        await using var audioOut = new AudioOutChannel(playbackFormat);
        // await using var audioIn = new AudioMonoInStream<float>(wasapiIn.WaveFormat, 0);
        // await using var audioOut =
        //     new AudioOutChannel(WaveFormat.CreateIeeeFloatWaveFormat(wasapiOut.OutputWaveFormat.SampleRate, 1));

        asio.InitRecordAndPlayback(audioOut.SampleProvider.ToWaveProvider(), 1, 48000);
        asio.AudioAvailable += audioIn.DataAvailable;
        // wasapiIn.DataAvailable += audioIn.DataAvailable;
        // wasapiOut.Init(audioOut.SampleProvider.ToWaveProvider());

        // var recordTask = Audio.RecordAsync(wasapiIn, cts.Source.Token);
        // var playTask = Audio.PlayAsync(wasapiOut, cts.Source.Token);
        var playTask = Audio.PlayAsync(asio, cts.Source.Token);

        var preamble = new ChirpPreamble<float>(Program.chirpOption with { SampleRate = 48000 });
        // var preamble =
        //     new ChirpPreamble<float>(Program.chirpOption with { SampleRate = wasapiIn.WaveFormat.SampleRate });

        var symbol = new LineSymbol<float>(Program.lineOption);
        var demodulator = new Demodulator<LineSymbol<float>, float, byte>(symbol, 255);

        var modulator = new Modulator<ChirpPreamble<float>, LineSymbol<float>>(preamble, symbol);

        await using var phyDuplex = new CSMAPhy<float>(
            audioIn,
            audioOut,
            demodulator,
            modulator,
            new PreambleDetection<float>(
                preamble, Program.corrThreshold, Program.smoothedEnergyFactor, Program.maxPeakFalling
            ),
            new CarrierQuietSensor<float>(0.5f),
            addressSource
        );

        await using var mac = new MacD(phyDuplex, phyDuplex, addressSource, addressDest, 1, 32);

        // var warmup = new WarmupPreamble<DPSKSymbol<float>, float>(symbol, 2000);
        // await audioOut.WriteAsync(new ReadOnlySequence<float>(warmup.Samples), cts.Source.Token);
        // await Task.Delay(500);
        // using var wave = new WaveFileWriter($"../matlab/debug{addressSource}.wav", wasapiIn.WaveFormat);
        // using var wave = new WaveFileWriter($"../matlab/debug{addressSource}.wav", wasapiIn.WaveFormat);
        if (send is not null)
        {
            // wasapiIn.DataAvailable += (s, e) =>
            // {
            //     wave.Write(e.Buffer, 0, e.BytesRecorded);
            // };
            var index = 0;
            await foreach (var packet in FileHelper.ReadFileChunkAsync(send, 128, binaryTxt, cts.Source.Token))
            {
                await mac.WriteAsync(new ReadOnlySequence<byte>(packet).IDEncode<byte>(index++), cts.Source.Token);
                // await Task.Delay(200);
            }
        }
        // await foreach(var x in mac.)
        while (true)
        {

            var packet = await mac.ReadAsync(cts.Source.Token);
            if (packet.IsEmpty)
                break;
            Console.WriteLine(packet);
        }
        Console.WriteLine("Done");
        await playTask;
        // await recordTask;
        // var inStream = pipeRec.Writer.AsStream();
        // wasapiIn.DataAvailable += (s, e) => inStream.Write(e.Buffer, 0, e.BytesRecorded);
    }
    static readonly Guid[] guids =
        [new("8D2D7623-926F-BCCF-0DA8-D5AFAF4C1B27"), new("2C83A46F-1AF2-85D9-DB52-DFBF0D26B629")];
    public static async Task DummyAdapterTaskAsync(bool loopBack, string adapterName, int guidIndex)
    {
        using var adapter = Adapter.Create(adapterName, adapterName, guids[guidIndex]);
        using var session = adapter.StartSession(0x40000);
        var rx = Channel.CreateUnbounded<byte[]>();
        var tx = Channel.CreateUnbounded<byte[]>();

        Logger.SetCallback((level, ts, message) => Console.WriteLine($"{level} {ts}: {message}"));

        void Run()
        {
            WinTun.Packet packet = default;
            while (true)
            {
                // session.WaitForRead
                if (session.ReceivePacket(out packet))
                {
                    // for (var i = 0; i < packet.Span.Length; i++)
                    // {
                    //     Console.Write($"{packet.Span[i]:X2} ");
                    // }
                    // Console.WriteLine();
                    Console.WriteLine();
                    var ipPacket = new IPv4Packet(new(packet.Span.ToArray()));
                    Console.WriteLine(ipPacket.ToString(StringOutputType.VerboseColored));
                    if (loopBack)
                    {
                        session.AllocateSendPacket((uint)packet.Span.Length, out var send);
                        packet.Span.CopyTo(send.Span);
                        session.SendPacket(send);
                    }
                    session.ReleaseReceivePacket(packet);
                }
                else
                {
                    session.WaitForRead(TimeSpan.MaxValue);
                }
            }
            // session.GetReadWaitEvent()
        }
        await Task.Run(Run);
    }

    public static async Task
    AdapterTaskAsync(byte addressSource, byte addressDest, int render, int capture, string adapterName, int guidIndex)
    {
        using var adapter = Adapter.Create(adapterName, adapterName, guids[guidIndex]);
        using var session = adapter.StartSession(0x40000);
        var rx = Channel.CreateUnbounded<ReadOnlySequence<byte>>();
        var tx = Channel.CreateUnbounded<ReadOnlySequence<byte>>();
        using var cts = new CancelKeyPressCancellationTokenSource(new());

        async Task TunRxDaemonAsync()
        {
            while (true)
            {
                cts.Source.Token.ThrowIfCancellationRequested();
                if (session.ReceivePacket(out var packet))
                {
                    // for (var i = 0; i < packet.Span.Length; i++)
                    // {
                    //     Console.Write($"{packet.Span[i]:X2} ");
                    // }
                    // Console.WriteLine();
                    var packetArr = packet.Span.ToArray();
                    var ipPacket = new IPv4Packet(new(packetArr));
                    if (ipPacket.Protocol is ProtocolType.Icmp)
                    {
                        Console.WriteLine(ipPacket.ToString(StringOutputType.VerboseColored));
                        Console.WriteLine();
                        await rx.Writer.WriteAsync(new(packetArr), cts.Source.Token);
                    }
                    // rx.TryWrite(packetArr);
                    session.ReleaseReceivePacket(packet);
                }
                else
                    session.WaitForRead(TimeSpan.FromMilliseconds(20));
            }
        }

        async Task TunTxDaemonAsync()
        {
            await foreach (var data in tx.Reader.ReadAllAsync(cts.Source.Token))
            {
                session.AllocateSendPacket((uint)data.Length, out var send);
                data.CopyTo(send.Span);
                session.SendPacket(send);
            }
        }
        // Console.WriteLine(wasapiIn.WaveFormat.SampleRate);
        // Console.WriteLine(wasapiOut.OutputWaveFormat.SampleRate);
#if ASIO
        using var asio = new AsioOut() { ChannelOffset = render, InputChannelOffset = capture };
        var recordFormat = new WaveFormat(48000, 32, 1);
        var playbackFormat = WaveFormat.CreateIeeeFloatWaveFormat(48000, 1);

        await using var audioIn = new AudioMonoInStream<float>(recordFormat, 0);
        await using var audioOut = new AudioOutChannel(playbackFormat);

        asio.InitRecordAndPlayback(audioOut.SampleProvider.ToWaveProvider(), 1, 48000);
        asio.AudioAvailable += audioIn.DataAvailable;
        var audioTask = Audio.PlayAsync(asio, cts.Source.Token);
#else
        using var player = Audio.GetWASAPIDevice(render, DataFlow.Render);
        using var recorder = Audio.GetWASAPIDevice(capture, DataFlow.Capture);

        // using var wasapiIn = new WasapiLoopbackCapture();
        using var wasapiIn = new WasapiCapture(recorder, true, 0);
        using var wasapiOut = new WasapiOut(player, AudioClientShareMode.Shared, true, 0);

        var recordFormat = wasapiIn.WaveFormat;
        var playbackFormat = WaveFormat.CreateIeeeFloatWaveFormat(wasapiOut.OutputWaveFormat.SampleRate, 1);
        await using var audioIn = new AudioMonoInStream<float>(recordFormat, 0);
        await using var audioOut = new AudioOutChannel(playbackFormat);

        wasapiIn.DataAvailable += audioIn.DataAvailable;
        wasapiOut.Init(audioOut.SampleProvider.ToWaveProvider());

        var audioTask =
            Task.WhenAll(Audio.RecordAsync(wasapiIn, cts.Source.Token), Audio.PlayAsync(wasapiOut, cts.Source.Token));
#endif

        var preamble = new ChirpPreamble<float>(Program.chirpOption with { SampleRate = recordFormat.SampleRate });
        // var preamble =
        //     new ChirpPreamble<float>(Program.chirpOption with { SampleRate = wasapiIn.WaveFormat.SampleRate });

        var symbol = new LineSymbol<float>(Program.lineOption);
        var demodulator = new Demodulator<LineSymbol<float>, float, byte>(symbol, 255);
        var modulator = new Modulator<ChirpPreamble<float>, LineSymbol<float>>(preamble, symbol);

        await using var phyDuplex = new CSMAPhy<float>(
            audioIn,
            audioOut,
            demodulator,
            modulator,
            new PreambleDetection<float>(
                preamble, Program.corrThreshold, Program.smoothedEnergyFactor, Program.maxPeakFalling
            ),
            new CarrierQuietSensor<float>(0.5f),
            addressSource
        );

        await using var mac = new MacD(phyDuplex, phyDuplex, addressSource, addressDest, 1, 32);

        async Task FillTxAsync()
        {
            await foreach (var packet in tx.Reader.ReadAllAsync(cts.Source.Token))
            {
                await mac.WriteAsync(packet, cts.Source.Token);
            }
        }

        async Task FillRxAsync()
        {
            while (true)
            {
                var packet = await mac.ReadAsync(cts.Source.Token);
                if (packet.IsEmpty)
                    break;
                await rx.Writer.WriteAsync(packet, cts.Source.Token);
            }
        }

        await Task.WhenAll(
            FillTxAsync(), FillRxAsync(), audioTask, Task.Run(TunRxDaemonAsync), Task.Run(TunTxDaemonAsync)
        );
    }

    public static async Task HotSpotTaskAsync(string? profileName)
    {
        if (profileName is null)
        {

            foreach (var x in Windows.Networking.Connectivity.NetworkInformation.GetConnectionProfiles())
            {
                Console.WriteLine(x.ProfileName);
            }
            return;
        }
        var profile = Windows.Networking.Connectivity.NetworkInformation.GetConnectionProfiles().First(
            x => x.ProfileName == profileName
        );
        await Windows.Networking.NetworkOperators.NetworkOperatorTetheringManager.CreateFromConnectionProfile(profile)
            .StartTetheringAsync();
    }

    // public static async Task AdapterTask2Async()
    // {

    //     using var adapter = Adapter.Create("Athernet", "Athernet", new("8D2D7623-926F-BCCF-0DA8-D5AFAF7C1B27"));
    //     // var adapter = Adapter.Open("Athernet");
    //     using var session = adapter.StartSession(0x40000);
    //     var rx = Channel.CreateUnbounded<byte[]>();
    //     var tx = Channel.CreateUnbounded<byte[]>();

    //     Logger.SetCallback((level, ts, message) => Console.WriteLine($"{level} {ts}: {message}"));

    //     void Run()
    //     {
    //         WinTun.Packet packet = default;
    //         while (true)
    //         {
    //             // session.WaitForRead
    //             if (session.ReceivePacket(out packet))
    //             {
    //                 // for (var i = 0; i < packet.Span.Length; i++)
    //                 // {
    //                 //     Console.Write($"{packet.Span[i]:X2} ");
    //                 // }
    //                 // Console.WriteLine();
    //                 Console.WriteLine();
    //                 var ipPacket = new IPv4Packet(new(packet.Span.ToArray()));
    //                 Console.WriteLine(ipPacket.ToString(StringOutputType.VerboseColored));
    //                 session.SendPacket(packet);
    //                 session.ReleaseReceivePacket(packet);

    //             }
    //             else
    //             {
    //                 session.WaitForRead(TimeSpan.MaxValue);
    //             }

    //         }
    //         // session.GetReadWaitEvent()
    //     }
    //     await Task.Run(Run);
    // }
}

public static class CommandBuilder
{
    // public static Command BuildAudioCommand()
    // {
    //     var command = new Command("audio", "play with audio stuff");

    //     var playOption = new Option<FileInfo?>(name: "--play",
    //                                                 description: "The file path to play",
    //                                                 isDefault: true,
    //                                                 parseArgument: result => FileHelper.ParseSingleFileInfo(result));

    //     var recordOption =
    //         new Option<FileInfo?>(name: "--record",
    //                                    description: "The file path to record",
    //                                    isDefault: true,
    //                                    parseArgument: result => FileHelper.ParseSingleFileInfo(result, false));

    //     var recordPlayBackOption = new Option<bool>(
    //         name: "--playback-record",
    //         description: "Record and then playback, not saving to file",
    //         getDefaultValue: () => false
    //     );

    //     var durationOption = new Option<int>(
    //         name: "--duration",
    //         description: "Time duration of audio operation, in seconds, -1 means infinite until canceled by user",
    //         isDefault: true,
    //         parseArgument: result =>
    //             result.Tokens.Count == 0 ? -1 : (int)(float.Parse(result.Tokens.Single().Value) * 1000)
    //     );

    //     command.AddOption(playOption);
    //     command.AddOption(recordOption);
    //     command.AddOption(recordPlayBackOption);
    //     command.AddOption(durationOption);
    //     command.SetHandler(
    //         CommandTask.AudioCommandTask, playOption, recordOption, recordPlayBackOption, durationOption
    //     );
    //     return command;
    // }

    public static Command BuildSendCommand()
    {
        var command = new Command("send", "send data");

        var fileArgument = new Argument < FileInfo ? > (name: "input",
                                                        description: "The file path to save",
                                                        parse: result => FileHelper.ParseSingleFileInfo(result));

        var toWavOption =
            new Option < FileInfo ? > (name: "--to-wav",
                                       description: "Export audio data to wav file",
                                       isDefault: true,
                                       parseArgument: result => FileHelper.ParseSingleFileInfo(result, false));

        var binaryTxtOption = new Option<bool>(
            name: "--binary-txt", description: "original txt are binary", getDefaultValue: () => false
        );

        var txOption =
            new Option<int>(name: "--render", description: "Name of rendering device to use", getDefaultValue: () => 0);

        command.AddArgument(fileArgument);
        command.AddOption(toWavOption);
        command.AddOption(binaryTxtOption);
        command.AddOption(txOption);

        command.SetHandler(CommandTask.SendCommandTaskAsync, fileArgument, toWavOption, binaryTxtOption, txOption);
        return command;
    }

    public static Command BuildReceiveCommand()
    {
        var command = new Command("receive", "receive data");

        var fileOption =
            new Option < FileInfo ? > (name: "--file",
                                       description: "The file path to save, otherwise stdout",
                                       isDefault: true,
                                       parseArgument: result => FileHelper.ParseSingleFileInfo(result, false));

        var fromWavOption = new Option < FileInfo ? > (name: "--from-wav",
                                                       description: "Import audio data from wav file",
                                                       isDefault: true,
                                                       parseArgument: result => FileHelper.ParseSingleFileInfo(result));

        var binaryTxtOption = new Option<bool>(
            name: "--binary-txt", description: "original txt are binary", getDefaultValue: () => false
        );

        var rxOption = new Option<int>(
            name: "--capture", description: "Name of capturing device to use", getDefaultValue: () => 0
        );

        command.AddOption(fromWavOption);
        command.AddOption(fileOption);
        command.AddOption(binaryTxtOption);
        command.AddOption(rxOption);

        command.SetHandler(CommandTask.ReceiveCommandTaskAsync, fromWavOption, fileOption, binaryTxtOption, rxOption);
        return command;
    }

    public static Command BuildDuplexCommand()
    {
        var command = new Command("duplex", "duplex data");

        var addressSourceArgument =
            new Argument<byte>(name: "source", description: "The mac address", getDefaultValue: () => 0);

        var addressDestArgument =
            new Argument<byte>(name: "dest", description: "The mac address to send", getDefaultValue: () => 1);

        var fileSendOption =
            new Option < FileInfo ? > (name: "--file-send",
                                       description: "The file path to send data",
                                       isDefault: true,
                                       parseArgument: result => FileHelper.ParseSingleFileInfo(result, true));

        var fileReceiveOption =
            new Option < FileInfo ? > (name: "--file-receive",
                                       description: "The file path to save received data, otherwise stdout",
                                       isDefault: true,
                                       parseArgument: result => FileHelper.ParseSingleFileInfo(result, false));

        var binaryTxtOption = new Option<bool>(
            name: "--binary-txt", description: "original txt are binary", getDefaultValue: () => false
        );

        var sleepOption =
            new Option<float>(name: "--sleep", description: "time to sleep to send data", getDefaultValue: () => 1f);

        var txOption =
            new Option<int>(name: "--render", description: "Name of rendering device to use", getDefaultValue: () => 0);

        var rxOption = new Option<int>(
            name: "--capture", description: "Name of capturing device to use", getDefaultValue: () => 0
        );

        command.AddArgument(addressSourceArgument);
        command.AddArgument(addressDestArgument);
        command.AddOption(fileSendOption);
        command.AddOption(fileReceiveOption);
        command.AddOption(binaryTxtOption);
        command.AddOption(sleepOption);
        command.AddOption(txOption);
        command.AddOption(rxOption);

        command.SetHandler(
            CommandTask.DuplexCommandTaskAsync,
            addressSourceArgument,
            addressDestArgument,
            fileSendOption,
            fileReceiveOption,
            binaryTxtOption,
            sleepOption,
            txOption,
            rxOption
        );
        return command;
    }

    public static Command BuildListWASAPICommand()
    {
        var command = new Command("list-devices", "list all audio devices");

        command.SetHandler(
            () =>
            {
                Console.WriteLine("ASIO Devices:");
                Audio.ListAsioDevices();

                Console.WriteLine();
                Console.WriteLine("WASAPI Devices:");
                Audio.ListWASAPIDevices();
            }
        );

        return command;
    }

    public static Command DummyAdapterCommand()
    {
        var command = new Command("dummy-adapter", "Run an dummy adapter");

        var loopBackOption =
            new Option<bool>(name: "--loopback", description: "Use loopback adapter", getDefaultValue: () => false);

        var adapterNameOption = new Option<string>(
            name: "--name", description: "The adapter name to use", getDefaultValue: () => "Athernet"
        );

        var guidIndexOption =
            new Option<int>(name: "--guid", description: "The guid index to use", getDefaultValue: () => 0);

        command.AddOption(loopBackOption);
        command.AddOption(adapterNameOption);
        command.AddOption(guidIndexOption);
        command.SetHandler(CommandTask.DummyAdapterTaskAsync, loopBackOption, adapterNameOption, guidIndexOption);

        return command;
    }
    public static Command AdapterCommand()
    {
        var command = new Command("adapter", "Run an dummy adapter");

        var addressSourceArgument =
            new Argument<byte>(name: "source", description: "The mac address", getDefaultValue: () => 0);

        var addressDestArgument =
            new Argument<byte>(name: "dest", description: "The mac address to send", getDefaultValue: () => 1);

        var txOption =
            new Option<int>(name: "--render", description: "Name of rendering device to use", getDefaultValue: () => 0);

        var rxOption = new Option<int>(
            name: "--capture", description: "Name of capturing device to use", getDefaultValue: () => 0
        );

        var adapterNameOption = new Option<string>(
            name: "--name", description: "The adapter name to use", getDefaultValue: () => "Athernet"
        );

        var guidIndexOption =
            new Option<int>(name: "--guid", description: "The guid index to use", getDefaultValue: () => 0);

        command.AddArgument(addressSourceArgument);
        command.AddArgument(addressDestArgument);
        command.AddOption(txOption);
        command.AddOption(rxOption);
        command.AddOption(adapterNameOption);
        command.AddOption(guidIndexOption);
        command.SetHandler(
            CommandTask.AdapterTaskAsync,
            addressSourceArgument,
            addressDestArgument,
            txOption,
            rxOption,
            adapterNameOption,
            guidIndexOption
        );

        return command;
    }

    public static Command HotSpotCommand()
    {
        var command = new Command("hotspot", "Run an adapter");

        var profileOption = new Option < string
            ? > (name: "--profile", description: "The profile name to start hotspot", getDefaultValue: () => null);

        command.AddOption(profileOption);
        command.SetHandler(CommandTask.HotSpotTaskAsync, profileOption);

        return command;
    }
}