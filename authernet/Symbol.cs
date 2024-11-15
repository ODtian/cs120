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
public interface ISymbol
{
    ReadOnlyMemory<ReadOnlyMemory<float>> Samples { get; }
    // public static abstract float[][] Get(TOption symbolOption);
}

public readonly record struct ChirpSymbolOption
(int NumSymbols, float Duration, int SampleRate, float FreqA, float FreqB)
{
    public int NumSamplesPerSymbol => (int)(Duration * SampleRate);
}

public readonly struct ChirpSymbol : ISymbol
{
    public ReadOnlyMemory<ReadOnlyMemory<float>> Samples { get; }
    public ChirpSymbolOption Option { get; }
    public ChirpSymbol(ChirpSymbolOption symbolOption)
    {
        Option = symbolOption;

        var result = new ReadOnlyMemory<float>[Option.NumSymbols];
        var builder = new ChirpBuilder();

        var sig = builder.SetParameter("low", -1f)
                      .SetParameter("high", 1f)
                      .SetParameter("f0", Option.FreqA)
                      .SetParameter("f1", Option.FreqB)
                      .SampledAt(Option.SampleRate)
                      .OfLength(Option.NumSamplesPerSymbol)
                      .Build();

        result[0] = sig.Samples;
        result[1] = (sig * -1).Samples.Reverse().ToArray();

        Samples = result;
    }

    public static implicit operator ChirpSymbol(ChirpSymbolOption option) => new(option);
}

public readonly record struct DPSKSymbolOption
(int NumSymbols, int NumRedundant, int SampleRate, float Freq)
{
    public int NumSamplesPerSymbol => (int)(SampleRate / Freq * NumRedundant);
}

public readonly struct DPSKSymbol : ISymbol
{
    public ReadOnlyMemory<ReadOnlyMemory<float>> Samples { get; }

    public DPSKSymbolOption Option { get; }
    public DPSKSymbol(DPSKSymbolOption symbolOption)
    {
        Option = symbolOption;
        var result = new ReadOnlyMemory<float>[Option.NumSymbols];

        // for (int i = 0; i < symbolOption.NumSymbols; i++)
        var sig = new SineBuilder()
                      .SetParameter("low", -1f)
                      .SetParameter("high", 1f)
                      .SetParameter("freq", Option.Freq)
                      .SampledAt(Option.SampleRate)
                      .OfLength(Option.NumSamplesPerSymbol)
                      .Build();

        result[0] = sig.Samples;

        result[1] = (sig * -1).Samples;

        Samples = result;
    }

    public static implicit operator DPSKSymbol(DPSKSymbolOption option) => new(option);
}

public readonly record struct LineSymbolOption
(int NumSymbols, int NumSamplesPerSymbol)
{
}

public readonly struct LineSymbol : ISymbol
{
    public ReadOnlyMemory<ReadOnlyMemory<float>> Samples { get; }
    public LineSymbolOption Option { get; }
    public LineSymbol(LineSymbolOption option)
    {
        Option = option;

        var result = new ReadOnlyMemory<float>[2];

        result[0] = Enumerable.Repeat(1f, option.NumSamplesPerSymbol).ToArray();
        result[1] = Enumerable.Repeat(-1f, option.NumSamplesPerSymbol).ToArray();

        Samples = result;
    }

    public static implicit operator LineSymbol(LineSymbolOption option) => new(option);
}