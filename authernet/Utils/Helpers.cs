
using System.Buffers;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text;
using MathNet.Numerics.Data.Matlab;
using MathNet.Numerics.LinearAlgebra;
using NAudio.Wave;
using System.CommandLine.Parsing;
using System.Numerics;

namespace CS120.Utils.Helpers;
public static partial class FileHelper
{
    public static FileInfo? ParseSingleFileInfo(ArgumentResult result, bool checkExist = true)
    {
        if (result.Tokens.Count == 0)
            return null;
        string? filePath = result.Tokens.Single().Value;
        if (checkExist && !File.Exists(filePath))
        {
            result.ErrorMessage = "File does not exist";
            return null;
        }
        else
            return new FileInfo(filePath);
    }

    public static async IAsyncEnumerable<byte[]> ReadFileChunkAsync(FileInfo file, int chunkSize, bool binaryTxt = false, [
        EnumeratorCancellation
    ] CancellationToken ct = default)
    {
        var dataPipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));
        var task = Task.Run(
            () =>
            {
                try
                {
                    if (binaryTxt)
                    {
                        using var fileData = file.OpenText();
                        var txt = fileData.ReadToEnd().ToArray();
                        // foreach (var b in txt)
                        // {
                        //     Console.WriteLine(b);
                        //     // Console.WriteLine(Convert.ToString(b, 2).PadLeft(8, '0'));
                        // }
                        // foreach (var b in w)
                        // {
                        //     Console.WriteLine(Convert.ToString(b, 2).PadLeft(8, '0'));
                        // }
                        dataPipe.Writer.Write(DataHelper.Convert01ToBytes(txt));
                    }
                    else
                        file.OpenRead().CopyTo(dataPipe.Writer.AsStream());
                    dataPipe.Writer.Complete();
                }
                catch (Exception e)
                {
                    dataPipe.Writer.Complete(e);
                }
            },
            ct
        );

        while (true)
        {
            var read = await dataPipe.Reader.ReadAsync(ct);
            var buffer = read.Buffer;

            while (DataHelper.TryChunkData(chunkSize, read.IsCompleted, ref buffer, out var chunk))
                yield return chunk.ToArray();

            dataPipe.Reader.AdvanceTo(buffer.Start, buffer.End);

            if (read.IsCompleted)
                break;
        }
        await task;
    }
}

public static class ModulateHelper
{
    public static byte DotProductDemodulateByte<T>(ReadOnlySpan<T> samples, ReadOnlySpan<T> symbol) where T : INumber<T>
    {
        byte result = 0;
        for (int i = 0; i < 8; i++)
        {
            var energy = T.Zero;
            for (int j = 0; j < symbol.Length; j++)
            {
                energy += samples[j + i * symbol.Length] * symbol[j];
            }

            result |= energy < T.Zero ? (byte)(1 << i) : (byte)0;
        }

        return result;
    }

    // public static void DotProductDemodulateByte(ReadOnlySpan<float> samples, ReadOnlySpan<float> symbol, Span<byte> dst)
    // {
    //     dst.Clear();
    //     for (int i = 0; i < dst.Length; i++)
    //     {
    //         for (int j = 0; j < 8; j++)
    //         {

    //             var energy = 0f;
    //             for (int k = 0; k < symbol.Length; k++)
    //             {
    //                 energy += samples[k + j * symbol.Length] * symbol[k];
    //             }

    //             dst[i] |= energy < 0f ? (byte)(1 << j) : (byte)0;
    //         }
    //     }
    // }

    public static ReadOnlyMemory<float> GetModulateSamples(
        ReadOnlyMemory<ReadOnlyMemory<float>> symbol, byte data, int bitOffset
    )
    {
        return symbol.Span[(data >> bitOffset) & 1];
    }
}

public static class DataHelper
{
    public static byte[] GenerateData(int length)
    {
        var random = new Random();
        var data = new List<byte> { };

        while (data.Count * 8 < length)
        {
            data.Add((byte)random.Next(0, 256));
        }

        int bitsToKeep = length - (data.Count - 1) * 8;
        data[^1] &= (byte)((1 << bitsToKeep) - 1);

        return [.. data];
    }

    public static byte[] GenerateDataByte(int length)

    {
        return GenerateData(length * 8);
    }
    public static void WriteSamplesToFile(string filePath, float[] samples)
    {
        using var writer = new WaveFileWriter(filePath, WaveFormat.CreateIeeeFloatWaveFormat(48000, 1));
        writer.WriteSamples(samples, 0, samples.Length);
    }

    public static void GenerateMatlabRecData(string filePath, string matFile)
    {
        var data = new List<float>();
        using (var reader = new WaveFileReader(filePath))
        {
            var sampleProvider = reader.ToSampleProvider().ToMono();

            var buffer = new float[reader.WaveFormat.SampleRate];
            while (true)
            {
                var length = sampleProvider.Read(buffer, 0, buffer.Length);
                if (length == 0)
                    break;
                data.AddRange(buffer.AsSpan(0, length));
            }
            // Console.WriteLine(data.Count);
        }
        var matrix = Matrix<float>.Build.DenseOfRowMajor(1, data.Count, [.. data]);
        MatlabWriter.Write(matFile, matrix, "audio_rec");
    }

    public static void GenerateMatlabSendData(float[] samples, string matFile)
    {
        var matrix = Matrix<float>.Build.DenseOfRowMajor(1, samples.Length, [.. samples]);
        MatlabWriter.Write(matFile, matrix, "audio");
    }

    public static byte[] Convert01ToBytes(char[] data)
    {
        var result = new byte[(int)Math.Ceiling((float)data.Length / 8)];
        for (int i = 0; i < data.Length; i++)
        {
            result[i / 8] |= (byte)((data[i] == '1' ? 1 : 0) << (7 - (i % 8)));
        }
        return result;
    }

    static string ReadData(string filePath)
    {
        if (!File.Exists(filePath))
        {
            var random = new Random();
            var data = new StringBuilder();

            for (int i = 0; i < 10000; i++)
            {
                data.Append(random.Next(0, 2));
            }

            File.WriteAllText(filePath, data.ToString());
            return data.ToString();
        }
        else
        {
            return File.ReadAllText(filePath);
        }
    }

    static (int length, List<byte> byteList) ProcessData(string data)
    {
        var length = data.Length;
        var byteList = new List<byte>();
        for (int i = 0; i < data.Length; i += 8)
        {
            string byteString = data.Substring(i, Math.Min(8, data.Length - i));
            if (byteString.Length < 8)
            {
                byteString = byteString.PadRight(8, '0');
            }
            byte byteValue = Convert.ToByte(byteString, 2);
            byteList.Add(byteValue);
        }
        return (length, byteList);
    }

    static void WriteData(string filePath, byte[] data, int length)
    {
        var byteString = new StringBuilder();
        foreach (var d in data)
        {
            byteString.Append(Convert.ToString(d, 2).PadLeft(8, '0'));
        }
        string dataString = byteString.ToString().Substring(0, length);

        using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
        {
            using (var writer = new StreamWriter(fileStream))
            {
                writer.Write(dataString);
            }
        }
    }
    public static bool TryChunkData(
        int chunkSize, bool complete, ref ReadOnlySequence<byte> seq, out ReadOnlySequence<byte> chunk
    )
    {
        if (seq.IsEmpty)
        {
            chunk = seq;
            return false;
        }
        else if (seq.Length < chunkSize)
        {
            if (complete)
            {
                chunk = seq;
                seq = seq.Slice(seq.Length);
            }
            else
                chunk = default;
            return complete;
        }
        else
        {
            chunk = seq.Slice(0, chunkSize);
            seq = seq.Slice(chunkSize);
            return true;
        }
    }

    // static byte[] GenerateData(int length)
    // {
    //     var fragment = new byte[] {
    //         0b10101010,
    //         0b01010101,
    //         0b10101010,
    //         0b01010101,
    //         0b10101010,
    //         0b00000000,
    //     };

    //     var data = new List<byte> {
    //         0b00000000,
    //         0b00000000,
    //     };

    //     for (int i = 0; i < length; i++)
    //     {
    //         data.AddRange(fragment);
    //     }

    //     var totalLength = (data.Count - 2) * 8;
    //     Console.WriteLine(totalLength);

    //     data[0] = (byte)totalLength;
    //     data[1] = (byte)(totalLength >> 8);

    //     Console.WriteLine(Convert.ToString(data[0], 2).PadLeft(8, '0'));
    //     Console.WriteLine(Convert.ToString(data[1], 2).PadLeft(8, '0'));
    //     for (int i = 0; i < 40; i++)
    //     {
    //         data.Add(0b00000000);
    //     }
    //     return data.ToArray();
    // }

    // static float[] GenerateSamples(byte[] data)
    // {
    //     // var symbols = DFSKSymbol.Get(option);
    //     // var symbols = DPSKSymbolOption.Get(option);
    //     var symbols = new DPSKSymbol(option).Samples;

    //     var samples = new List<float>();

    //     // IPreamble? preamble = ChirpPreamble.Create(WaveFormat.CreateIeeeFloatWaveFormat(option.SampleRate, 1));
    //     var preamble = new ChirpPreamble(new ChirpSymbol(chirpOption));

    //     samples.AddRange(Enumerable.Range(0, 48000).Select(
    //         _ => 0f
    //     ));
    //     samples.AddRange(preamble.Samples);

    //     foreach (var d in data)
    //     {
    //         for (int i = 0; i < 8; i++)
    //         {
    //             samples.AddRange(symbols[(d >> i) & 1]);
    //         }
    //     }

    //     samples.AddRange(Enumerable.Range(0, 48000).Select(
    //         _ => 0f
    //     ));

    //     return samples.ToArray();
    // }

    // static IReceiver GetFileReciver(string filePath)
    // {
    //     using var fileReader = new WaveFileReader(filePath);
    //     var receiver = new Receiver(
    //         fileReader.WaveFormat,
    //         new PreambleDetection(
    //             new ChirpPreamble(new ChirpSymbol(chirpOption with { SampleRate = fileReader.WaveFormat.SampleRate
    //             })), corrThreshold, smoothedEnergyFactor, maxPeakFalling
    //         ),
    //         new DPSKDemodulator(new DPSKSymbol(option with { SampleRate = fileReader.WaveFormat.SampleRate }))
    //     );

    //     using var writer = receiver.StreamIn;

    //     fileReader.CopyTo(writer);
    //     return receiver;
    // }

    // public static byte[] ConvertBytesTo01(byts[] data)
    // {
    //     var result = new byte[(int)Math.Ceiling((float)data.Length / 8)];
    //     for (int i = 0; i < data.Length; i++)
    //     {
    //         result[i / 8] |= (byte)((data[i] == '1' ? 1 : 0) << (7 - (i % 8)));
    //     }
    //     return result;
    // }

    // static IReceiver GetFileReciver(string filePath)
    // {
    //     using var fileReader = new WaveFileReader(filePath);
    //     var receiver = new Receiver(
    //         fileReader.WaveFormat,
    //         new PreambleDetection(
    //             new ChirpPreamble(new ChirpSymbol(chirpOption with { SampleRate = fileReader.WaveFormat.SampleRate
    //             })), corrThreshold, smoothedEnergyFactor, maxPeakFalling
    //         ),
    //         new DPSKDemodulator(new DPSKSymbol(option with { SampleRate = fileReader.WaveFormat.SampleRate }))
    //     );

    //     using var writer = receiver.StreamIn;

    //     fileReader.CopyTo(writer);
    //     return receiver;
    // }
    // static byte[] GenerateData(int length)
    // {
    //     var fragment = new byte[] {
    //         0b10101010,
    //         0b01010101,
    //         0b10101010,
    //         0b01010101,
    //         0b10101010,
    //         0b00000000,
    //     };

    //     var data = new List<byte> {
    //         0b00000000,
    //         0b00000000,
    //     };

    //     for (int i = 0; i < length; i++)
    //     {
    //         data.AddRange(fragment);
    //     }

    //     var totalLength = (data.Count - 2) * 8;
    //     Console.WriteLine(totalLength);

    //     data[0] = (byte)totalLength;
    //     data[1] = (byte)(totalLength >> 8);

    //     Console.WriteLine(Convert.ToString(data[0], 2).PadLeft(8, '0'));
    //     Console.WriteLine(Convert.ToString(data[1], 2).PadLeft(8, '0'));
    //     for (int i = 0; i < 40; i++)
    //     {
    //         data.Add(0b00000000);
    //     }
    //     return data.ToArray();
    // }

    // static float[] GenerateSamples(byte[] data)
    // {
    //     // var symbols = DFSKSymbol.Get(option);
    //     // var symbols = DPSKSymbolOption.Get(option);
    //     var symbols = new DPSKSymbol(option).Samples;

    //     var samples = new List<float>();

    //     // IPreamble? preamble = ChirpPreamble.Create(WaveFormat.CreateIeeeFloatWaveFormat(option.SampleRate, 1));
    //     var preamble = new ChirpPreamble(new ChirpSymbol(chirpOption));

    //     samples.AddRange(Enumerable.Range(0, 48000).Select(
    //         _ => 0f
    //     ));
    //     samples.AddRange(preamble.Samples);

    //     foreach (var d in data)
    //     {
    //         for (int i = 0; i < 8; i++)
    //         {
    //             samples.AddRange(symbols[(d >> i) & 1]);
    //         }
    //     }

    //     samples.AddRange(Enumerable.Range(0, 48000).Select(
    //         _ => 0f
    //     ));

    //     return samples.ToArray();
    // }
}
