#define DUPLEX_ASIO
// #define AIR_ASIO

using System.CommandLine;
using System.IO.Pipelines;
using System.Text;
using Aether.NET.CarrierSense;
using Aether.NET.Modulate;
using Aether.NET.Packet;
using Aether.NET.Phy;
using Aether.NET.Preamble;
using Aether.NET.Utils;
using Aether.NET.Utils.Helpers;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Aether.NET.Symbol;
using Aether.NET.Mac;
using System.Buffers;
using DotNext;
using WinTun;
using System.Threading.Channels;
using PacketDotNet;
using System.Net;
using Aether.NET.Utils.Extension;
using CommunityToolkit.HighPerformance;
using Aether.NET.Utils.IO;
using ARSoft.Tools.Net.Dns;
using Aether.NET.Tcp;
namespace Aether.NET.Commands;

public static class CommandTask
{
    public static async Task SendCommandTaskAsync(FileInfo? file, FileInfo? toWav, bool binaryTxt, int render)
    {
        ArgumentNullException.ThrowIfNull(file);

        using var cts = new CancelKeyPressCancellationTokenSource(new());

        using var player = Audio.GetWASAPIDevice(render, DataFlow.Render);

        using var wasapiOut = new WasapiOut(player, AudioClientShareMode.Shared, true, 50);

        Console.WriteLine(wasapiOut.OutputWaveFormat.SampleRate);

        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(wasapiOut.OutputWaveFormat.SampleRate, 1);

        dynamic GetOutChannel(out Task play)
        {
            if (toWav is null)
            {
                var audioOut = new AudioOutChannel(waveFormat);
                wasapiOut.Init(audioOut.SampleProvider.ToWaveProvider());
                play = Audio.PlayAsync(wasapiOut, cts.Source.Token);
                return audioOut;
            }
            else
            {
                var audioOut = new AudioPipeOutChannel(wasapiOut.OutputWaveFormat);
                async Task CopyToAudioAsync()
                {
                    using var writer = new WaveFileWriter(toWav.FullName, waveFormat);
                    using var stream = audioOut.Reader.AsStream();
                    await stream.CopyToAsync(writer);
                }
                play = CopyToAudioAsync();
                return audioOut;
            }
        }

        await using dynamic outChannel = GetOutChannel(out var play);

        var symbols = new[] {
            new DPSKSymbol<float>(Program.options[0] with { SampleRate = waveFormat.SampleRate }),
            new DPSKSymbol<float>(Program.options[1] with { SampleRate = waveFormat.SampleRate })
        };
        var modulator = new OFDMModulator<ChirpPreamble<float>, DPSKSymbol<float>>(
            new ChirpPreamble<float>(Program.chirpOptionAir with { SampleRate = waveFormat.SampleRate }), symbols
        );

        // var symbols = new LineSymbol<float>(Program.lineOption);
        // var modulator = new Modulator<ChirpPreamble<float>, DPSKSymbol<float>>(
        //     new ChirpPreamble<float>(symbols: Program.chirpOptionAir with { SampleRate = waveFormat.SampleRate }),
        //     symbols
        // );
        // var modulator = new Modulator<ChirpPreamble<float>, LineSymbol<float>>(
        //     new ChirpPreamble<float>(symbols: Program.chirpOption with { SampleRate = waveFormat.SampleRate }),
        //     symbols
        // );

        await using var tx = new TXPhy<float, byte>(outChannel, modulator);

        // var warmup = new WarmupPreamble<DPSKSymbol<float>, float>(symbols, 2000);
        // await outChannel.WriteAsync(new ReadOnlySequence<float>(warmup.Samples), cts.Source.Token);
        // await Task.Delay(500);

        var index = 0;
        await foreach (var data in FileHelper.ReadFileChunkAsync(file, Program.dataNum, binaryTxt, cts.Source.Token))
        {
            await tx.WriteAsync(new ReadOnlySequence<byte>(data).IDEncode<byte>(index++), cts.Source.Token);
            await Task.Delay(50);
        }

        await play;
    }

    public static async Task ReceiveCommandTaskAsync(FileInfo? fromWav, FileInfo? file, bool binaryTxt, int capture)
    {
        using var cts = new CancelKeyPressCancellationTokenSource(new());

#if AIR_ASIO
        using var asio = new AsioOut();
#else
        // using var wasapiIn = new WasapiLoopbackCapture();
        using var recorder = Audio.GetWASAPIDevice(capture, DataFlow.Capture);
        using var wasapiIn = new WasapiCapture(recorder, true, 10);
#endif

        dynamic GetInStream(out Task rec, out WaveFormat waveFormat)
        {
            if (fromWav is null)
            {
#if AIR_ASIO
                waveFormat = new WaveFormat(48000, 32, 1);
                asio.InitRecordAndPlayback(null, capture, 48000);
#else
                waveFormat = wasapiIn.WaveFormat;
#endif
                var audio = new AudioMonoInStream<float>(waveFormat, 0);
#if AIR_ASIO
                asio.AudioAvailable += audio.DataAvailable;
                rec = Audio.PlayAsync(asio, cts.Source.Token);
#else
                wasapiIn.DataAvailable += audio.DataAvailable;
                rec = Audio.RecordAsync(wasapiIn, cts.Source.Token);
#endif
                return audio;
            }
            else
            {
                static async Task CopyToAudioAsync(WaveFileReader reader, PipeWriter writer)
                {
                    using var stream = writer.AsStream();
                    var buffer = new byte[2048];
                    while (true)
                    {
                        var read = await reader.ReadAsync(buffer.AsMemory());
                        if (read == 0)
                            break;
                        await writer.WriteAsync(buffer.AsMemory(0, read));
                        await Task.Delay(10);
                    }
                    await writer.CompleteAsync();
                }

                var reader = new WaveFileReader(fromWav.FullName);
                waveFormat = reader.WaveFormat;
                var audio = new AudioPipeInStream<float>(waveFormat);
                rec = CopyToAudioAsync(reader, audio.Writer);
                return audio;
            }
        }

        await using dynamic inStream = GetInStream(out var rec, out var waveFormat);

        var preamble = new ChirpPreamble<float>(Program.chirpOptionAir with { SampleRate = waveFormat.SampleRate });

        var demodulator = new OFDMDemodulator<DPSKSymbol<float>, float>([Program.options[0], Program.options[1]]);
        // var demodulator =
        //     new Demodulator<DPSKSymbol<float>, float>(Program.option with { SampleRate = waveFormat.SampleRate },
        //     136);
        // var demodulator = new Demodulator<LineSymbol<float>, float, byte>(Program.lineOption, byte.MaxValue);

        await using var rx = new RXPhy<float, byte>(
            inStream,
            demodulator,
            new PreambleDetection<float>(
                preamble, Program.corrThresholdAir, Program.smoothedEnergyFactor, Program.maxPeakFallingAir
            ),
            Program.dataLengthInByte
        );

        // var index = 0;
        using var stream = file switch { null => Console.OpenStandardOutput(), FileInfo => file.OpenWrite() };
        while (true)
        {
            var packet = await rx.ReadAsync(cts.Source.Token);
            if (packet.IsEmpty)
                break;
            packet = packet.IDDecode<byte>(out var id);
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

#if DUPLEX_ASIO
        var sampleRate = 96000;
        using var asio = new AsioOut() { ChannelOffset = render, InputChannelOffset = capture };
        var recordFormat = new WaveFormat(sampleRate, 32, 1);
        var playbackFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        Console.WriteLine($"ASIO support sampleRate {sampleRate}: {asio.IsSampleRateSupported(sampleRate)}");
#else
        // using var wasapiIn = new WasapiLoopbackCapture();
        using var player = Audio.GetWASAPIDevice(render, DataFlow.Render);
        using var recorder = Audio.GetWASAPIDevice(capture, DataFlow.Capture);
        using var wasapiIn = new WasapiCapture(recorder, true, 20);
        using var wasapiOut = new WasapiOut(player, AudioClientShareMode.Shared, true, 20);
        var recordFormat = wasapiIn.WaveFormat;
        var playbackFormat = WaveFormat.CreateIeeeFloatWaveFormat(wasapiOut.OutputWaveFormat.SampleRate, 1);

        Console.WriteLine($"wasapiIn sampleRate: {wasapiIn.WaveFormat.SampleRate}");
        Console.WriteLine($"wasapiOut sampleRate: {wasapiOut.OutputWaveFormat.SampleRate}");
#endif
        await using var audioIn = new AudioMonoInStream<float>(recordFormat, 0);
        await using var audioOut = new AudioOutChannel(playbackFormat);

        using var wave = Path.Exists($"../matlab/debug{addressSource}.wav")
                             ? new WaveFileWriter($"../matlab/debug{addressSource}.wav", playbackFormat)
                             : null;
#if DUPLEX_ASIO
        asio.InitRecordAndPlayback(audioOut.SampleProvider.ToWaveProvider(), 1, sampleRate);
        asio.AudioAvailable += audioIn.DataAvailable;
        var buf = new float[2048];
        asio.AudioAvailable += (s, e) =>
        {
            var size = e.GetAsInterleavedSamples(buf);
            wave?.Write(buf.AsSpan(0, size).AsBytes());
        };
        var audioTask = Audio.PlayAsync(asio, cts.Source.Token);

#else
        wasapiIn.DataAvailable += audioIn.DataAvailable;
        wasapiIn.DataAvailable += (s, e) => wave?.Write(e.Buffer, 0, e.BytesRecorded);
        wasapiOut.Init(audioOut.SampleProvider.ToWaveProvider());
        var audioTask =
            Task.WhenAll(Audio.PlayAsync(wasapiOut, cts.Source.Token), Audio.RecordAsync(wasapiIn, cts.Source.Token));

#endif

        var modPreamble = new ChirpPreamble<float>(Program.chirpOption with { SampleRate = sampleRate, Amp = 0.3f });
        var demodPreamble = new ChirpPreamble<float>(Program.chirpOption with { SampleRate = sampleRate });

        var modSym = new LineSymbol<float>(Program.lineOption with { Amp = 0.3f });
        var demodSym = new LineSymbol<float>(Program.lineOption);

        var modulator = new Modulator<ChirpPreamble<float>, LineSymbol<float>>(modPreamble, modSym);
        var demodulator = new Demodulator<LineSymbol<float>, float>(demodSym);

        // var modSym = new DPSKSymbol<float>(Program.option);
        // var demodSym = new DPSKSymbol<float>(Program.option);

        await using var phyDuplex = new CSMAPhy<float, ushort>(
            audioIn,
            audioOut,
            demodulator,
            modulator,
            new PreambleDetection<float>(
                demodPreamble, Program.corrThreshold, Program.smoothedEnergyFactor, Program.maxPeakFalling
            ),
            new CarrierQuietSensor<float>(Program.carrierSenseThreshold),
            78
        );

        await using var mac = new MacD(phyDuplex, phyDuplex, addressSource, addressDest, 1, 32);

        // await Task.Delay(5000);
        var warmup = new WarmupPreamble<LineSymbol<float>, float>(demodSym, 512);
        await audioOut.WriteAsync(new ReadOnlySequence<float>(warmup.Samples), cts.Source.Token);
        Console.WriteLine("Ready");
        using var inputReader = new StreamReader(Console.OpenStandardInput(), Console.InputEncoding);
        await inputReader.ReadLineAsync();

        // await Task.Delay(500);

        if (send is not null)
        {
            // { wave.Write(e.Buffer, 0, e.BytesRecorded); };
            await foreach (var packet in FileHelper.ReadFileChunkAsync(send, 64, binaryTxt, cts.Source.Token))
                await mac.WriteAsync(new ReadOnlySequence<byte>(packet), cts.Source.Token);
            // await Task.Delay(200);
        }

        using var stream = receive switch { null => Console.OpenStandardOutput(), FileInfo f => f.OpenWrite() };

        while (true)
        {

            var packet = await mac.ReadAsync(cts.Source.Token);
            if (packet.IsEmpty)
                break;

            if (binaryTxt)
                foreach (var b in packet.GetElements())
                    await stream.WriteAsync(Encoding.UTF8.GetBytes(Convert.ToString(b, 2).PadLeft(8, '0')));
            else
                await stream.WriteAsync(packet.ToArray());
        }
        Console.WriteLine("Done");
        await audioTask;
    }

    static readonly Guid[] guids =
        [new("8D2D7623-926F-BCCF-0DA8-D5AFAF4C1B27"), new("2C83A46F-1AF2-85D9-DB52-DFBF0D26B629")];
    public static async Task DummyAdapterTaskAsync(bool loopBack, string adapterName, int guidIndex)
    {
        using var adapter = Adapter.Create(adapterName, adapterName, guids[guidIndex]);
        using var session = adapter.StartSession(0x40000);

        await Task.Run(
            () =>
            {
                while (true)
                {
                    if (session.ReceivePacket(out var packet))
                    {
                        var data = packet.Span.ToArray();
                        var ipPacket = new IPv4Packet(new(data));
                        Console.WriteLine(ipPacket.ToString(StringOutputType.VerboseColored));
                        Console.WriteLine(Convert.ToHexString(data));
                        Console.WriteLine();
                        if (loopBack)
                        {
                            session.AllocateSendPacket((uint)packet.Span.Length, out var send);
                            packet.Span.CopyTo(send.Span);
                            session.SendPacket(send);
                        }
                        session.ReleaseReceivePacket(packet);
                    }
                    else
                        session.WaitForRead(TimeSpan.MaxValue);
                }
            }
        );
    }

    public static async Task AdapterTaskAsync(
        byte addressSource,
        byte addressDest,
        int render,
        int capture,
        string adapterName,
        int guidIndex,
        uint? seqHijack
    )
    {
        using var adapter = Adapter.Create(adapterName, adapterName, guids[guidIndex]);
        using var session = adapter.StartSession(0x40000);
        var tx = Channel.CreateUnbounded<ReadOnlySequence<byte>>();
        var rx = Channel.CreateUnbounded<ReadOnlySequence<byte>>();
        using var cts = new CancelKeyPressCancellationTokenSource(new());

        var seqHijackProxy = seqHijack.HasValue ? new SeqHijack(seqHijack.Value) : null;

        async Task TunTxAsync()
        {
            while (true)
            {
                cts.Source.Token.ThrowIfCancellationRequested();
                if (session.ReceivePacket(out var packet))
                {
                    var ipPacket = new IPv4Packet(new(packet.Span.ToArray()));

                    bool isIcmp = ipPacket.Protocol is ProtocolType.Icmp;
                    bool isDns = ipPacket.Protocol is ProtocolType.Udp &&
                                 ipPacket.PayloadPacket as UdpPacket is { DestinationPort : 53 } or { SourcePort : 53 };
                    if (isDns)
                    {
                        var message = DnsMessage.Parse(ipPacket.PayloadPacket.PayloadData);
                        isDns = message.Questions[0].Name.Equals(new(["example", "com"])) &&
                                message.Questions[0].RecordType == RecordType.A;
                    }
                    bool isTcp = ipPacket.Protocol is ProtocolType.Tcp &&
                                 (ipPacket.SourceAddress.Equals(IPAddress.Parse("93.184.215.14")) ||
                                  ipPacket.DestinationAddress.Equals(IPAddress.Parse("93.184.215.14")));

                    if (isIcmp || isDns || isTcp)
                    {
                        if (isTcp && ipPacket.PayloadPacket is TcpPacket tcp && seqHijackProxy is not null)
                        {
                            if (tcp.Synchronize)
                                seqHijackProxy.Init(tcp);

                            seqHijackProxy.Send(tcp);
                        }
                        Console.WriteLine(ipPacket.ToString(StringOutputType.VerboseColored));
                        Console.WriteLine();
                        await tx.Writer.WriteAsync(new(ipPacket.Bytes), cts.Source.Token);
                    }
                    session.ReleaseReceivePacket(packet);
                }
                else
                    session.WaitForRead(TimeSpan.MaxValue);
            }
        }

        async Task TunRxAsync()
        {
            await foreach (var data in rx.Reader.ReadAllAsync(cts.Source.Token))
            {
                var ipPacket = new IPv4Packet(new(data.ToArray()));
                if (ipPacket.Protocol is ProtocolType.Tcp && ipPacket.PayloadPacket is TcpPacket tcp)
                    seqHijackProxy?.Receive(tcp);

                Console.WriteLine(ipPacket.ToString(StringOutputType.VerboseColored));
                // foreach (var b in data.GetElements())
                //     Console.Write($"{b:X2} ");
                session.AllocateSendPacket((uint)ipPacket.TotalLength, out var send);
                ipPacket.Bytes.CopyTo(send.Span);
                session.SendPacket(send);
            }
        }

#if DUPLEX_ASIO
        var sampleRate = 96000;
        using var asio = new AsioOut() { ChannelOffset = render, InputChannelOffset = capture };
        var recordFormat = new WaveFormat(sampleRate, 32, 1);
        var playbackFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);
        Console.WriteLine($"ASIO support sampleRate {sampleRate}: {asio.IsSampleRateSupported(sampleRate)}");
#else
        // using var wasapiIn = new WasapiLoopbackCapture();
        using var player = Audio.GetWASAPIDevice(render, DataFlow.Render);
        using var recorder = Audio.GetWASAPIDevice(capture, DataFlow.Capture);
        using var wasapiIn = new WasapiCapture(recorder, true, 20);
        using var wasapiOut = new WasapiOut(player, AudioClientShareMode.Shared, true, 20);
        var recordFormat = wasapiIn.WaveFormat;
        var playbackFormat = WaveFormat.CreateIeeeFloatWaveFormat(wasapiOut.OutputWaveFormat.SampleRate, 1);

        Console.WriteLine($"wasapiIn sampleRate: {wasapiIn.WaveFormat.SampleRate}");
        Console.WriteLine($"wasapiOut sampleRate: {wasapiOut.OutputWaveFormat.SampleRate}");
#endif
        await using var audioIn = new AudioMonoInStream<float>(recordFormat, 0);
        await using var audioOut = new AudioOutChannel(playbackFormat);

        using var wave = Path.Exists($"../matlab/debug{addressSource}.wav")
                             ? new WaveFileWriter($"../matlab/debug{addressSource}.wav", playbackFormat)
                             : null;
#if DUPLEX_ASIO
        asio.InitRecordAndPlayback(audioOut.SampleProvider.ToWaveProvider(), 1, sampleRate);
        asio.AudioAvailable += audioIn.DataAvailable;
        var buf = new float[2048];
        asio.AudioAvailable += (s, e) =>
        {
            
            var size = e.GetAsInterleavedSamples(buf);
            wave?.Write(buf.AsSpan(0, size).AsBytes());
        };
        var audioTask = Audio.PlayAsync(asio, cts.Source.Token);

#else
        wasapiIn.DataAvailable += audioIn.DataAvailable;
        wasapiIn.DataAvailable += (s, e) => wave?.Write(e.Buffer, 0, e.BytesRecorded);
        wasapiOut.Init(audioOut.SampleProvider.ToWaveProvider());
        var audioTask =
            Task.WhenAll(Audio.PlayAsync(wasapiOut, cts.Source.Token), Audio.RecordAsync(wasapiIn, cts.Source.Token));

#endif

        var modPreamble = new ChirpPreamble<float>(Program.chirpOption with { SampleRate = sampleRate, Amp = 0.2f });
        var demodPreamble = new ChirpPreamble<float>(Program.chirpOption with { SampleRate = sampleRate });

        var modulator =
            new Modulator<ChirpPreamble<float>, LineSymbol<float>>(modPreamble, Program.lineOption with { Amp = 0.2f });
        var demodulator = new Demodulator<LineSymbol<float>, float>(Program.lineOption);

        // var modSym = new DPSKSymbol<float>(Program.option);
        // var demodSym = new DPSKSymbol<float>(Program.option);

        await using var phyDuplex = new CSMAPhy<float, ushort>(
            audioIn,
            audioOut,
            demodulator,
            modulator,
            new PreambleDetection<float>(
                demodPreamble, Program.corrThreshold, Program.smoothedEnergyFactor, Program.maxPeakFalling
            ),
            new CarrierQuietSensor<float>(Program.carrierSenseThreshold),
            1600
        );

        await using var mac = new MacD(phyDuplex, phyDuplex, addressSource, addressDest, 1, 32);

        // await Task.Delay(5000);
        // var warmup = new WarmupPreamble<LineSymbol<float>, float>(demodSym, 512);
        // await audioOut.WriteAsync(new ReadOnlySequence<float>(warmup.Samples), cts.Source.Token);
        Console.WriteLine("Ready");

        async Task MacTxAsync()
        {
            await foreach (var packet in tx.Reader.ReadAllAsync(cts.Source.Token))
                await mac.WriteAsync(packet, cts.Source.Token);
        }

        async Task MacRxAsync()
        {
            while (true)
            {
                var packet = await mac.ReadAsync(cts.Source.Token);
                Console.WriteLine(packet.ToArray().Length);
                if (packet.IsEmpty)
                    break;
                await rx.Writer.WriteAsync(packet, cts.Source.Token);
            }
        }

        await foreach (var task in Task.WhenEach(
                           MacTxAsync(), MacRxAsync(), audioTask, Task.Run(TunRxAsync), Task.Run(TunTxAsync)
                       )) await task;
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
}

public static class CommandBuilder
{
    // public static Command BuildAudioCommand()
    // {
    //     var command = new Command("audio", "play with audio stuff");

    //     var playOption = new Option < FileInfo ? > (name: "--play",
    //                                                 description: "The file path to play",
    //                                                 isDefault: true,
    //                                                 parseArgument: result => FileHelper.ParseSingleFileInfo(result));

    //     var recordOption =
    //         new Option < FileInfo ? > (name: "--record",
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

        var seqHijackOption = new Option < uint
            ? > (name: "--seq-hijack", description: "Hijack sequence number", getDefaultValue: () => null);

        command.AddArgument(addressSourceArgument);
        command.AddArgument(addressDestArgument);
        command.AddOption(txOption);
        command.AddOption(rxOption);
        command.AddOption(adapterNameOption);
        command.AddOption(guidIndexOption);
        command.AddOption(seqHijackOption);
        command.SetHandler(
            CommandTask.AdapterTaskAsync,
            addressSourceArgument,
            addressDestArgument,
            txOption,
            rxOption,
            adapterNameOption,
            guidIndexOption,
            seqHijackOption
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