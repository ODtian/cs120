using System.Collections;
using STH1123.ReedSolomon;

namespace CS120.Utils.Codec;

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
        if (bytes.Length < eccNums + 1)
        {
            valid = false;
            return [];
        }
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