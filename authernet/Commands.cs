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
using CS120.Mac;
using CS120.Utils.Extension;
namespace CS120.Commands;

public static class CommandTask
{
    public static async Task AudioCommandTask(FileInfo? play, FileInfo? record, bool recordPlayBack, int duration)
    {
        // var audioCtses = new CancellationTokenSource[2] { new(duration), new(duration) };
        using var cancelToken = new CancelKeyPressCancellationTokenSource(new CancellationTokenSource(duration));
        using var cancelToken1 = new CancelKeyPressCancellationTokenSource(new CancellationTokenSource(), false);

        var taskPlay = play switch {
            FileInfo => Audio.Play<WasapiOut>(play.FullName, cancelToken.Source.Token),
            null => Task.CompletedTask,
        };

        var taskRecord = (record, recordPlayBack) switch {
            (_, true) => Audio.RecordThenPlay<WasapiCapture, WasapiOut>(new[] { cancelToken, cancelToken1 }.Select(
                cts =>
                {
                    cts.Enable(true);
                    cts.Source.CancelAfter(duration);
                    return cts.Source.Token;
                }
            )),
            (FileInfo, _) => Audio.Record<WasapiCapture>(record.FullName, cancelToken.Source.Token),
            _ => Task.CompletedTask,
        };

        var taskUni = Task.WhenAll(taskPlay, taskRecord);

        await taskUni;
    }

    public static async Task SendCommandTask(FileInfo? file, FileInfo? toWav, bool binaryTxt)
    {
        ArgumentNullException.ThrowIfNull(file);

        using var cts = new CancelKeyPressCancellationTokenSource(new CancellationTokenSource());

        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));

        // var wavePro
        // var play = Task.CompletedTask;
        var waveFormat = Audio.GetPlayerWaveFormat<WasapiOut>();
        var provider = new NonBlockingPipeWaveProvider(
            WaveFormat.CreateIeeeFloatWaveFormat(waveFormat.SampleRate, 1), pipe.Reader
        );

        var play = toWav switch {
            null => Audio.Play<WasapiOut>(provider, cts.Source.Token),
            FileInfo => Task.Run(
                () =>
                {
                    using var writer = new WaveFileWriter(toWav.FullName, provider.WaveFormat);
                    pipe.Reader.AsStream().CopyTo(writer);
                }
            ),
        };

        using var transmitter = new PhyTransmitter(
            waveFormat,
            pipe.Writer,
            new ChirpPreamble(Program.chirpOption with { SampleRate = waveFormat.SampleRate }),
            new DPSKModulator(pipe.Writer, Program.option with { SampleRate = waveFormat.SampleRate }),
            // new OFDMModulator(
            //     pipe,
            //     [
            //         option with { SampleRate = waveFormat.SampleRate },
            //         option with {
            //             Freq = option.Freq * 2,
            //             NumRedundant = option.NumRedundant * 2,
            //             SampleRate = waveFormat.SampleRate
            //         }
            //     ]
            // ),
            // sendFormat.ConvertLatencyToByteSize
            0
            // sendFormat.ConvertLatencyToByteSize(1)
        );

        var exec = transmitter.Execute(cts.Source.Token);

        var dataPipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));

        var index = 0;

        await foreach (var data in FileHelper.ReadFileChunk(file, Program.dataNum, binaryTxt, cts.Source.Token))
        {
            await transmitter.Tx.WriteAsync(
                data.IDEncode<byte>(index++)
                    .LengthEncode<byte>(Program.dataNum + Program.idNum)
                    .RSEncode(Program.eccNums),
                cts.Source.Token
            );
        }

        transmitter.Tx.Complete();

        await exec;

        pipe.Writer.Complete();

        await play;
    }
    public static async Task ReceiveCommandTask(FileInfo? fromWav, FileInfo? file, bool binaryTxt)
    {
        using var cts = new CancelKeyPressCancellationTokenSource(new CancellationTokenSource());

        var pipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));

        Task rec;
        WaveFormat waveFormat;

        if (fromWav is null)
        {
            rec = Audio.Record<WasapiCapture>(pipe.Writer.AsStream(), cts.Source.Token);
            waveFormat = Audio.GetRecorderWaveFormat<WasapiCapture>();
        }
        else
        {
            rec = Task.CompletedTask;
            using var reader = new WaveFileReader(fromWav.FullName);
            waveFormat = reader.WaveFormat;
            reader.CopyTo(pipe.Writer.AsStream());
            pipe.Writer.Complete();
        }
        // Console.WriteLine(waveFormat.SampleRate);

        using var receiver = new PhyReceiver(
            new PreambleDetection(
                pipe.Reader,
                waveFormat,
                new ChirpPreamble(Program.chirpOption with { SampleRate = waveFormat.SampleRate }),
                Program.corrThreshold,
                Program.smoothedEnergyFactor,
                Program.maxPeakFalling
            ),
            new DPSKDemodulator(pipe.Reader, waveFormat, Program.option with { SampleRate = waveFormat.SampleRate }),
            // new OFDMDemodulator(
            //     pipe,
            //     waveFormat,
            //     [
            //         option with { SampleRate = waveFormat.SampleRate },
            //         option with {
            //             Freq = option.Freq * 2,
            //             NumRedundant = option.NumRedundant * 2,
            //             SampleRate = waveFormat.SampleRate
            //         }
            //     ]
            // ),
            new DemodulateLength.FixedLength(Program.dataLengthInByte)
        );
        var exec = receiver.Execute(cts.Source.Token);

        // var index = 0;
        using var stream = file switch { null => Console.OpenStandardOutput(), FileInfo => file.OpenWrite() };
        await foreach (var packet in receiver.Rx.ReadAllAsync(cts.Source.Token))
        {
            var p = packet.RSDecode(Program.eccNums, out var eccValid)
                        .LengthDecode<byte>(out var lengthValid)
                        .IDDecode<byte>(out var id);

            Console.WriteLine(
                $"Receive a packet: Length {p.Length}, eccValid: {eccValid}, lengthValid: {lengthValid}, {id}"
            );

            if (binaryTxt)
                foreach (var b in p)
                    stream.Write(Encoding.ASCII.GetBytes(Convert.ToString(b, 2).PadLeft(8, '0')));
            else
                stream.Write(p);
        }

        await exec;

        pipe.Reader.Complete();

        await rec;
    }

    public static async Task DuplexCommandTask(
        byte addressSource,
        byte addressDest,
        FileInfo? send,
        FileInfo? receive,
        bool binaryTxt,
        float sleep,
        string? renderID,
        string? captureID
    )
    {
        using var cts = new CancelKeyPressCancellationTokenSource(new());

        var player =
            renderID switch { null =>
                                  new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia),
                              _ => Audio.GetWASAPIDeviceByID(renderID, DataFlow.Render) };

        var recorder =
            captureID switch { null =>
                                   new MMDeviceEnumerator().GetDefaultAudioEndpoint(DataFlow.Capture, Role.Multimedia),
                               _ => Audio.GetWASAPIDeviceByID(captureID, DataFlow.Capture) };

        var wasapiIn = new WasapiCapture(recorder, true, 2);
        var pipeRec = new Pipe(new PipeOptions(pauseWriterThreshold: 0));

        var inStream = pipeRec.Writer.AsStream();
        wasapiIn.DataAvailable += (s, e) => inStream.Write(e.Buffer, 0, e.BytesRecorded);

        var wasapiOut = new WasapiOut(player, AudioClientShareMode.Exclusive, true, 10);
        var format = WaveFormat.CreateIeeeFloatWaveFormat(wasapiOut.OutputWaveFormat.SampleRate, 1);
        var pipePlay = new Pipe(new PipeOptions(pauseWriterThreshold: 0, resumeWriterThreshold: -1));

        wasapiOut.Init(new NonBlockingPipeWaveProvider(format, pipePlay.Reader));

        var rec = Audio.Record(wasapiIn, cts.Source.Token);
        var play = Audio.Play(wasapiOut, cts.Source.Token);
        // var waveFormatPlay = audioManager.GetPlayerWaveFormat<WasapiOut>();
        // var waveFormatReceive = audioManager.GetRecorderWaveFormat<WasapiCapture>();
        Console.WriteLine(wasapiIn.WaveFormat.SampleRate);
        Console.WriteLine(wasapiOut.OutputWaveFormat.SampleRate);
        // foreach (var device in new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Render,
        // DeviceState.Active))
        // {
        //     Console.WriteLine(device.);
        // }
        // var rec = audioManager.Record<WasapiCapture>(pipeRec.Writer.AsStream(), cts.Source.Token);
        // var play = audioManager.Play<WasapiOut>(
        //     new NonBlockingPipeWaveProvider(
        //         WaveFormat.CreateIeeeFloatWaveFormat(waveFormatPlay.SampleRate, 1), pipePlay.Reader
        //     ),
        //     cts.Source.Token
        // );
        var preamble = new ChirpPreamble(Program.chirpOption with { SampleRate = wasapiIn.WaveFormat.SampleRate });

        using var csma = new CSMAPhyHalfDuplex<byte>(
            pipePlay.Writer,
            pipeRec.Reader,
            wasapiOut.OutputWaveFormat,
            wasapiIn.WaveFormat,
            new CarrierSensor(pipeRec.Reader, wasapiIn.WaveFormat, 220, 0.005f),
            preamble,
            // new DPSKModulator(
            //     pipePlay.Writer, Program.option with { SampleRate = wasapiOut.OutputWaveFormat.SampleRate }
            // ),
            new LineModulator(pipePlay.Writer, Program.lineOption),
            new PreambleDetection(
                pipeRec.Reader,
                wasapiIn.WaveFormat,
                preamble,
                Program.corrThreshold,
                Program.smoothedEnergyFactor,
                Program.maxPeakFalling
            ),
            // new DPSKDemodulator(
            //     pipeRec.Reader, wasapiIn.WaveFormat, Program.option with { SampleRate =
            //     wasapiIn.WaveFormat.SampleRate }
            // ),
            new LineDemodulator(pipeRec.Reader, wasapiIn.WaveFormat, Program.lineOption),
            256,
            wasapiOut.OutputWaveFormat.ConvertLatencyToSampleSize(0.1f),
            0.01f
        );

        using var util = new PhyUtilDuplex(csma.Tx, csma.Rx);

        // using var mac = new MacDuplex2(util.Tx, util.Rx, addressSource);

        var daemon = Task.WhenAny(csma.Execute(cts.Source.Token), util.Execute(cts.Source.Token));
        // mac.Execute(cts.Source.Token),
        await Task.Delay(TimeSpan.FromSeconds(sleep), cts.Source.Token);

        if (send is not null)
        {
            int index = 0;

            await foreach (var packet in FileHelper.ReadFileChunk(send, 128, binaryTxt, cts.Source.Token))
            {
                await util.Tx.WriteAsync(packet.IDEncode<ushort>((addressDest << 8) + index++), cts.Source.Token);
                // await mac.Tx.WriteAsync(
                //     packet.IDEncode<ushort>((addressDest << 8) + index++)
                //         .MacEncode(new() { Dest = addressDest, Type = MacFrame.FrameType.Data }),
                //     cts.Source.Token
                // );
            }
            Console.WriteLine(index);
        }
        // util.Tx.TryComplete();

        await foreach (var packet in util.Rx.ReadAllAsync(cts.Source.Token))
        {
            // packet.MacDecode(out var x).IDGet<ushort>(out var id);
            packet.IDGet<ushort>(out var id);
            // Console.WriteLine($"Receive a packet: {x.Dest} {x.Type}");
            if (id >> 8 == addressSource)
            {
                Console.WriteLine(id & 0x00ff);
            }
        }
        // Console.WriteLine("Done");
        await daemon;

        // pipe 大小调整为和系统buffer差不多
        // var daemon =
        //     Task.WhenAll(csma.Execute(cts.Source.Token), util.Execute(cts.Source.Token),
        //     mac.Execute(cts.Source.Token));
    }
}

public static class CommandBuilder
{
    public static Command BuildAudioCommand()
    {
        var command = new Command("audio", "play with audio stuff");

        var playOption = new Option < FileInfo ? > (name: "--play",
                                                    description: "The file path to play",
                                                    isDefault: true,
                                                    parseArgument: result => FileHelper.ParseSingleFileInfo(result));

        var recordOption =
            new Option < FileInfo ? > (name: "--record",
                                       description: "The file path to record",
                                       isDefault: true,
                                       parseArgument: result => FileHelper.ParseSingleFileInfo(result, false));

        var recordPlayBackOption = new Option<bool>(
            name: "--playback-record",
            description: "Record and then playback, not saving to file",
            getDefaultValue: () => false
        );

        var durationOption = new Option<int>(
            name: "--duration",
            description: "Time duration of audio operation, in seconds, -1 means infinite until canceled by user",
            isDefault: true,
            parseArgument: result =>
                result.Tokens.Count == 0 ? -1 : (int)(float.Parse(result.Tokens.Single().Value) * 1000)
        );

        command.AddOption(playOption);
        command.AddOption(recordOption);
        command.AddOption(recordPlayBackOption);
        command.AddOption(durationOption);
        command.SetHandler(
            CommandTask.AudioCommandTask, playOption, recordOption, recordPlayBackOption, durationOption
        );
        return command;
    }

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

        command.AddArgument(fileArgument);
        command.AddOption(toWavOption);
        command.AddOption(binaryTxtOption);

        command.SetHandler(CommandTask.SendCommandTask, fileArgument, toWavOption, binaryTxtOption);
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

        command.AddOption(fromWavOption);
        command.AddOption(fileOption);
        command.AddOption(binaryTxtOption);

        command.SetHandler(CommandTask.ReceiveCommandTask, fromWavOption, fileOption, binaryTxtOption);
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

        var txOption = new Option < string ? > (name: "--render", description: "Name of rendering device to use");

        var rxOption = new Option < string ? > (name: "--capture", description: "Name of capturing device to use");

        command.AddArgument(addressSourceArgument);
        command.AddArgument(addressDestArgument);
        command.AddOption(fileSendOption);
        command.AddOption(fileReceiveOption);
        command.AddOption(binaryTxtOption);
        command.AddOption(sleepOption);
        command.AddOption(txOption);
        command.AddOption(rxOption);

        command.SetHandler(
            CommandTask.DuplexCommandTask,
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
        var command = new Command("list-wasapi", "list all WASAPI devices");
        command.SetHandler(Audio.ListWASAPIDevices);

        return command;
    }
}