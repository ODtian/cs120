using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using CS120.Extension;
using MathNet.Numerics.Data.Matlab;
using MathNet.Numerics.LinearAlgebra;
using NAudio.Wave;
using STH1123.ReedSolomon;

namespace CS120.Utils;

// public class BlockingCollectionSampleProvider
// (WaveFormat waveFormat, BlockingCollection<float> sampleBuffer) : ISampleProvider
// {
//     public WaveFormat WaveFormat { get; } = waveFormat;
//     public int ReadBlocking(float[] buffer, int offset, int count)
//     {
//         if (sampleBuffer.IsCompleted)
//         {
//             return 0;
//         }

//         // Console.WriteLine("Read: " + sampleBuffer.Count);

//         if (sampleBuffer.IsAddingCompleted)
//         {

//             count = Math.Min(count, sampleBuffer.Count);
//         }

//         sampleBuffer.GetConsumingEnumerable().TakeInto(buffer.AsSpan(offset, count));
//         return count;
//     }
//     public int Read(float[] buffer, int offset, int count)
//     {
//         if (sampleBuffer.IsCompleted)
//         {
//             return 0;
//         }

//         var bufferCount = sampleBuffer.Count;

//         if (bufferCount == 0)
//         {
//             buffer[offset] = 0;
//             return 1;
//         }

//         count = Math.Min(count, bufferCount);

//         sampleBuffer.GetConsumingEnumerable().TakeInto(buffer.AsSpan(offset, count));
//         return count;
//     }
// }

public class PipeViewProvider : IWaveProvider, ISampleProvider
{
    public PipeReader? Reader { get; }
    public ISampleProvider SampleProvider { get; }
    public WaveFormat WaveFormat { get; }

    public PipeViewProvider(WaveFormat waveFormat, PipeReader reader)
    {
        WaveFormat = waveFormat;
        Reader = reader;
        SampleProvider = this.ToSampleProvider().ToMono();
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        if (Reader == null)
        {
            return 0;
        }

        var result = Reader.ReadAtLeastAsync(count).AsTask().ConfigureAwait(false).GetAwaiter().GetResult();

        if (result.IsFinished())
        {
            Reader.AdvanceTo(result.Buffer.Start);
            return 0;
        }

        var resultBuffer = result.Buffer;
        var readed = Math.Min(count, resultBuffer.Length);

        var seq = resultBuffer.Slice(0, readed);
        seq.CopyTo(buffer.AsSpan(offset, (int)readed));
        Reader.AdvanceTo(seq.Start);
        return (int)readed;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        return SampleProvider.Read(buffer, offset, count);
    }

    public void AdvanceSamples(int numSamples)
    {
        if (Reader == null)
        {
            return;
        }
        if (Reader.TryRead(out var result))
        {
            // Console.WriteLine(result.Buffer.Length);
            Reader.AdvanceTo(
                result.Buffer.GetPosition(Math.Min(result.Buffer.Length, numSamples * WaveFormat.BitsPerSample / 8))
            );
        }
    }
}

public class NonBlockingPipeWaveProvider
(WaveFormat waveFormat, PipeReader pipeReader) : IWaveProvider
{
    private readonly PipeReader pipeReader = pipeReader;
    public WaveFormat WaveFormat { get; } = waveFormat;

    public int Read(byte[] buffer, int offset, int count)
    {
        if (pipeReader.TryRead(out var result) && !result.Buffer.IsEmpty)
        {
            var length = Math.Min(count, (int)result.Buffer.Length);

            var resultBuffer = result.Buffer.Slice(0, length);

            resultBuffer.CopyTo(buffer.AsSpan(offset, length));
            pipeReader.AdvanceTo(resultBuffer.End);
            return length;
        }
        else if (result.IsFinished())
        {
            pipeReader.AdvanceTo(result.Buffer.Start);
            return 0;
        }
        else
        {
            buffer.AsSpan(offset, count).Clear();
            return count;
        }
    }
}

// public interface IExtendable<T>
// {
//     void Extend(ReadOnlySpan<T> other);
// }

// public interface IPipeReaderBuilder<out T>
// {
//     T Build(WaveFormat waveFormat, PipeReader sampleBuffer);
// }

// public interface IPipeWriterBuilder<out T>
// {
//     T Build(WaveFormat waveFormat, PipeWriter sampleBuffer);
// }

public interface IPipeReader<T>
{
    PipeReader SourceReader { get; }

    bool TryReadTo(Span<T> dst, bool advandce = true);
}

public interface IPipeWriter<T>
{
    PipeWriter SourceWriter { get; }

    void Write(ReadOnlySpan<T> src);

    ValueTask<FlushResult> FlushAsync(CancellationToken ct)
    {
        return SourceWriter.FlushAsync(ct);
    }
}

public interface IPipeAdvance
{
    PipeReader SourceReader { get; }
    bool TryAdvance();
}

// public class BufferWriter
// (PipeWriter pipeWriter) : IPipeWriter<byte>
// {
//     public PipeWriter SourceWriter { get; } = pipeWriter;

//     public void Write(ReadOnlySpan<byte> dataBuffer)
//     {
//         SourceWriter.Write(dataBuffer);
//     }
// }

public static class Codec4B5B
{
    private static readonly Dictionary<byte, byte> EncodeTable = new(
    ) { { 0b0000, 0b11110 },
        { 0b0001, 0b01001 },
        { 0b0010, 0b10100 },
        { 0b0011, 0b10101 },
        { 0b0100, 0b01010 },
        { 0b0101, 0b01011 },
        { 0b0110, 0b01110 },
        { 0b0111, 0b01111 },
        { 0b1000, 0b10010 },
        { 0b1001, 0b10011 },
        { 0b1010, 0b10110 },
        { 0b1011, 0b10111 },
        { 0b1100, 0b11010 },
        { 0b1101, 0b11011 },
        { 0b1110, 0b11100 },
        { 0b1111, 0b11101 } };

    private static readonly Dictionary<byte, byte> DecodeTable = new(
    ) { { 0b11110, 0b0000 },
        { 0b01001, 0b0001 },
        { 0b10100, 0b0010 },
        { 0b10101, 0b0011 },
        { 0b01010, 0b0100 },
        { 0b01011, 0b0101 },
        { 0b01110, 0b0110 },
        { 0b01111, 0b0111 },
        { 0b10010, 0b1000 },
        { 0b10011, 0b1001 },
        { 0b10110, 0b1010 },
        { 0b10111, 0b1011 },
        { 0b11010, 0b1100 },
        { 0b11011, 0b1101 },
        { 0b11100, 0b1110 },
        { 0b11101, 0b1111 } };

    public static byte[] Encode(byte[] data)
    {
        // List<byte> encodedData = [];
        var resultLengthInBits = data.Length * 2 * 5;
        var result = new BitArray(resultLengthInBits);

        // var encodedData = new BitArray(new bool[data.Length * 2]);

        for (int i = 0; i < data.Length; i++)
        {
            var highNibble = (byte)(data[i] >> 4);
            var lowNibble = (byte)(data[i] & 0b1111);

            var low = EncodeTable[lowNibble];
            var high = EncodeTable[highNibble];

            result[i * 5 * 2] = (low & 0b1) != 0;
            result[i * 5 * 2 + 1] = (low & 0b10) != 0;
            result[i * 5 * 2 + 2] = (low & 0b100) != 0;
            result[i * 5 * 2 + 3] = (low & 0b1000) != 0;
            result[i * 5 * 2 + 4] = (low & 0b10000) != 0;

            result[i * 5 * 2 + 5] = (high & 0b1) != 0;
            result[i * 5 * 2 + 6] = (high & 0b10) != 0;
            result[i * 5 * 2 + 7] = (high & 0b100) != 0;
            result[i * 5 * 2 + 8] = (high & 0b1000) != 0;
            result[i * 5 * 2 + 9] = (high & 0b10000) != 0;
        }
        var resultByte = new byte[(int)Math.Ceiling((float)resultLengthInBits / 8)];
        result.CopyTo(resultByte, 0);
        return resultByte;
    }

    public static byte[] Decode(byte[] data)
    {
        if (data.Length % 2 != 0)
        {
            throw new ArgumentException("Encoded data length must be even.");
        }
        var dataBit = new BitArray(data);
        var result = new byte[(int)(data.Length * 0.8)];
        for (int i = 0; i < result.Length; i++)
        {
            byte high = 0b0;
            byte low = 0b0;
            low |= (byte)(dataBit[i * 5 * 2] ? 0b1 : 0b0);
            low |= (byte)(dataBit[i * 5 * 2 + 1] ? 0b10 : 0b0);
            low |= (byte)(dataBit[i * 5 * 2 + 2] ? 0b100 : 0b0);
            low |= (byte)(dataBit[i * 5 * 2 + 3] ? 0b1000 : 0b0);
            low |= (byte)(dataBit[i * 5 * 2 + 4] ? 0b10000 : 0b0);

            high |= (byte)(dataBit[i * 5 * 2 + 4] ? 0b1 : 0b0);
            high |= (byte)(dataBit[i * 5 * 2 + 5] ? 0b10 : 0b0);
            high |= (byte)(dataBit[i * 5 * 2 + 6] ? 0b100 : 0b0);
            high |= (byte)(dataBit[i * 5 * 2 + 7] ? 0b1000 : 0b0);
            high |= (byte)(dataBit[i * 5 * 2 + 7] ? 0b10000 : 0b0);

            result[i] = (byte)(DecodeTable[high] << 4 + DecodeTable[low]);
        }
        // dataBit.
        // List<byte> decodedData = [];

        // for (int i = 0; i < data.Length; i += 2)
        // {
        //     byte highNibble = DecodeTable[data[i]];
        //     byte lowNibble = DecodeTable[data[i + 1]];

        //     byte decodedByte = (byte)((highNibble << 4) | lowNibble);
        //     decodedData.Add(decodedByte);
        // }

        return result;
    }
}

public static class CodecRS
{
    // public static int EccNums { get; set; } = 7;
    // private static int eccNums = Program.eccNums;
    public static readonly GenericGF rs = new(285, 256, 1);
    public static readonly ReedSolomonEncoder encoder = new(rs);
    public static readonly ReedSolomonDecoder decoder = new(rs);

    public const byte magic = 0b10101010;
    // public static void SetEccNums(int eccNums)
    // {
    //     CodecRS.eccNums = eccNums;
    // }
    public static byte[] Encode(ReadOnlySpan<byte> bytes, int eccNums)
    {
        var data = new byte[bytes.Length + 1 + eccNums];
        var toEncode = new int[data.Length];

        for (int i = 0; i < bytes.Length; i++)
        {
            toEncode[i] = data[i] = bytes[i];
        }

        toEncode[bytes.Length] = data[bytes.Length] = magic;

        encoder.Encode(toEncode, eccNums);
        for (int i = bytes.Length + 1; i < data.Length; i++)
        {
            data[i] = (byte)toEncode[i];
        }
        return data;
    }

    public static byte[] Decode(ReadOnlySpan<byte> bytes, int eccNums, out bool valid)
    {
        var data = new byte[bytes.Length - 1 - eccNums];
        var toDecode = new int[bytes.Length];
        for (int i = 0; i < bytes.Length; i++)
        {
            toDecode[i] = bytes[i];
        }
        valid = decoder.Decode(toDecode, eccNums);
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)toDecode[i];
        }
        return data;
    }
}

public static class ModulateHelper
{
    public static byte DotProductDemodulateByte(ReadOnlySpan<float> samples, ReadOnlySpan<float> symbol)
    {
        byte result = 0;
        for (byte i = 0; i < 8; i++)
        {

            var energy = 0f;
            for (int j = 0; j < symbol.Length; j++)
            {
                energy += samples[j + i * symbol.Length] * symbol[j];
            }

            result |= energy < 0f ? (byte)(1 << i) : (byte)0;
        }

        return result;
    }

    public static void DotProductDemodulateByte(ReadOnlySpan<float> samples, ReadOnlySpan<float> symbol, Span<byte> dst)
    {
        dst.Clear();
        for (int i = 0; i < dst.Length; i++)
        {
            for (int j = 0; j < 8; j++)
            {

                var energy = 0f;
                for (int k = 0; k < symbol.Length; k++)
                {
                    energy += samples[k + j * symbol.Length] * symbol[k];
                }

                dst[i] |= energy < 0f ? (byte)(1 << j) : (byte)0;
            }
        }
    }

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
        var data = new List<byte> {};

        while (data.Count * 8 < length)
        {
            data.Add((byte)random.Next(0, 256));
        }

        int bitsToKeep = length - (data.Count - 1) * 8;
        data[^1] &= (byte)((1 << bitsToKeep) - 1);

        return [..data];
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
        var matrix = Matrix<float>.Build.DenseOfRowMajor(1, data.Count, [..data]);
        MatlabWriter.Write(matFile, matrix, "audio_rec");
    }

    public static void GenerateMatlabSendData(float[] samples, string matFile)
    {
        var matrix = Matrix<float>.Build.DenseOfRowMajor(1, samples.Length, [..samples]);
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

public static partial class FileHelper
{
    public static async IAsyncEnumerable<byte[]> ReadFileChunk(FileInfo file, int chunkSize, bool binaryTxt = false, [
        EnumeratorCancellation
    ] CancellationToken ct = default)
    {
        var dataPipe = new Pipe(new PipeOptions(pauseWriterThreshold: 0));
        var task = Task.Run(
            () =>
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

public static class BinaryIntegerSizeTrait<T>
    where T : IBinaryInteger<T>
{
    public static readonly int Size = T.Zero.GetByteCount();
}