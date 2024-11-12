using System.CommandLine;
using System.IO.Pipelines;
using System.Text;
using CS120.Modulate;
using CS120.Packet;
using CS120.Phy;
using CS120.Preamble;
using CS120.Utils;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CS120.Commands;

public static class CommandTask
{
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
}

public static class CommandBuilder
{
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
}