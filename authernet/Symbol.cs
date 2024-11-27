using System.Numerics;
using NWaves.Signals;
using NWaves.Signals.Builders;

namespace CS120.Symbol;

// public interface ISymbolOption
// {
//     public int NumSymbols { get; }
//     public int NumSamplesPerSymbol { get; }
//     public int SampleRate { get; }
// }
// }
// public interface ISymbol<TOption>
//     where TOption : ISymbolOption {
//     float[][] Symbols { get; }
//     // public static abstract float[][] Get(TOption symbolOption);
// }
public interface ISymbol<T> where T : INumber<T>
{
    ReadOnlyMemory<ReadOnlyMemory<T>> Samples { get; }
    int NumSamplesPerSymbol { get; }
    // public static abstract float[][] Get(TOption symbolOption);
}

public readonly record struct ChirpSymbolOption
(int NumSymbols, float Duration, int SampleRate, float FreqA, float FreqB)
{
    public int NumSamplesPerSymbol => (int)(Duration * SampleRate);
}

public readonly struct ChirpSymbol<T> : ISymbol<T> where T : INumber<T>
{
    public ReadOnlyMemory<ReadOnlyMemory<T>> Samples { get; }
    public int NumSamplesPerSymbol { get; }
    public ChirpSymbol(ChirpSymbolOption symbolOption)
    {
        NumSamplesPerSymbol = symbolOption.NumSamplesPerSymbol;

        var result = new ReadOnlyMemory<T>[symbolOption.NumSymbols];
        var builder = new ChirpBuilder();

        var sig = builder.SetParameter("low", -1f)
                      .SetParameter("high", 1f)
                      .SetParameter("f0", symbolOption.FreqA)
                      .SetParameter("f1", symbolOption.FreqB)
                      .SampledAt(symbolOption.SampleRate)
                      .OfLength(symbolOption.NumSamplesPerSymbol)
                      .Build();

        result[0] = sig.Samples.Select(x => T.CreateChecked(x)).ToArray();
        result[1] = (sig * -1).Samples.Reverse().Select(x => T.CreateChecked(x)).ToArray();

        Samples = result;
    }

    public static implicit operator ChirpSymbol<T>(ChirpSymbolOption option) => new(option);
}

public readonly record struct DPSKSymbolOption
(int NumSymbols, int NumRedundant, int SampleRate, float Freq)
{
    public int NumSamplesPerSymbol => (int)(SampleRate / Freq * NumRedundant);
}

public readonly struct DPSKSymbol<T> : ISymbol<T> where T : INumber<T>
{
    public ReadOnlyMemory<ReadOnlyMemory<T>> Samples { get; }
    public int NumSamplesPerSymbol { get; }
    public DPSKSymbol(DPSKSymbolOption symbolOption)
    {
        NumSamplesPerSymbol = symbolOption.NumSamplesPerSymbol;

        var result = new ReadOnlyMemory<T>[symbolOption.NumSymbols];

        // for (int i = 0; i < symbolOption.NumSymbols; i++)
        var sig = new SineBuilder()
                      .SetParameter("low", -1f)
                      .SetParameter("high", 1f)
                      .SetParameter("freq", symbolOption.Freq)
                      .SampledAt(symbolOption.SampleRate)
                      .OfLength(symbolOption.NumSamplesPerSymbol)
                      .Build();

        result[0] = sig.Samples.Select(x => T.CreateChecked(x)).ToArray();

        result[1] = (sig * -1).Samples.Select(x => T.CreateChecked(x)).ToArray();

        Samples = result;
    }

    public static implicit operator DPSKSymbol<T>(DPSKSymbolOption option) => new(option);
}

public readonly record struct LineSymbolOption(int NumSymbols, int NumSamplesPerSymbol)
{ }

public readonly struct LineSymbol<T> : ISymbol<T> where T : INumber<T>
{
    public ReadOnlyMemory<ReadOnlyMemory<T>> Samples { get; }
    public int NumSamplesPerSymbol { get; }
    public LineSymbol(LineSymbolOption symbolOption)
    {
        NumSamplesPerSymbol = symbolOption.NumSamplesPerSymbol;

        var result = new ReadOnlyMemory<T>[symbolOption.NumSymbols];


        var zero = new T[symbolOption.NumSamplesPerSymbol];
        zero.AsSpan().Fill(T.CreateChecked(-1));
        result[0] = zero;


        var one = new T[symbolOption.NumSamplesPerSymbol];
        one.AsSpan().Fill(T.CreateChecked(1));
        result[1] = one;

        Samples = result;
    }

    public static implicit operator LineSymbol<T>(LineSymbolOption option) => new(option);
}