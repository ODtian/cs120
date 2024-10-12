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
    float[][] Samples { get; }
    // public static abstract float[][] Get(TOption symbolOption);
}

public readonly struct ChirpSymbolOption
{
    public int NumSymbols { get; init; }
    public int NumSamplesPerSymbol { get; init; }
    public int SampleRate { get; init; }
    public float FreqA { get; init; }
    public float FreqB { get; init; }
}

public readonly struct ChirpSymbol : ISymbol
{
    public float[][] Samples { get; }
    public ChirpSymbol(ChirpSymbolOption symbolOption)
    {
        var result = new float [symbolOption.NumSymbols][];
        var builder = new ChirpBuilder();

        var sig = builder.SetParameter("low", -1f)
                      .SetParameter("high", 1f)
                      .SetParameter("f0", symbolOption.FreqA)
                      .SetParameter("f1", symbolOption.FreqB)
                      .SampledAt(symbolOption.SampleRate)
                      .OfLength(symbolOption.NumSamplesPerSymbol)
                      .Build();
        result[0] = sig.Samples;

        var sig_1 = sig.Copy();
        sig_1.Reverse();
        result[1] = (sig_1 * -1).Samples;

        Samples = result;
    }
}
public readonly struct DPSKSymbolOption
{
    public int NumSymbols { get; init; }
    public int NumSamplesPerSymbol { get; init; }
    public int SampleRate { get; init; }
    public float Freq { get; init; }
}
public readonly struct DPSKSymbol : ISymbol
{
    public float[][] Samples { get; }
    public DPSKSymbol(DPSKSymbolOption symbolOption)
    {
        var result = new float [symbolOption.NumSymbols][];

        // for (int i = 0; i < symbolOption.NumSymbols; i++)
        var sig = new SineBuilder()
                      .SetParameter("low", -1f)
                      .SetParameter("high", 1f)
                      .SetParameter("freq", symbolOption.Freq)
                      .SampledAt(symbolOption.SampleRate)
                      .OfLength(symbolOption.NumSamplesPerSymbol)
                      .Build();

        result[0] = sig.Samples;

        result[1] = (sig * -1).Samples;

        Samples = result;
    }
}