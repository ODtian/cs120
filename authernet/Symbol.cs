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
    public ChirpSymbolOption Option { get; }
    public ChirpSymbol(ChirpSymbolOption symbolOption)
    {
        Option = symbolOption;

        var result = new ReadOnlyMemory<T>[Option.NumSymbols];
        var builder = new ChirpBuilder();

        var sig = builder.SetParameter("low", -1f)
                      .SetParameter("high", 1f)
                      .SetParameter("f0", Option.FreqA)
                      .SetParameter("f1", Option.FreqB)
                      .SampledAt(Option.SampleRate)
                      .OfLength(Option.NumSamplesPerSymbol)
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

    public DPSKSymbolOption Option { get; }
    public DPSKSymbol(DPSKSymbolOption symbolOption)
    {
        Option = symbolOption;
        var result = new ReadOnlyMemory<T>[Option.NumSymbols];

        // for (int i = 0; i < symbolOption.NumSymbols; i++)
        var sig = new SineBuilder()
                      .SetParameter("low", -1f)
                      .SetParameter("high", 1f)
                      .SetParameter("freq", Option.Freq)
                      .SampledAt(Option.SampleRate)
                      .OfLength(Option.NumSamplesPerSymbol)
                      .Build();

        result[0] = sig.Samples.Select(x => T.CreateChecked(x)).ToArray();

        result[1] = (sig * -1).Samples.Select(x => T.CreateChecked(x)).ToArray();

        Samples = result;
    }

    public static implicit operator DPSKSymbol<T>(DPSKSymbolOption option) => new(option);
}