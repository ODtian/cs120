using System.Numerics;
using NWaves.Signals;
using NWaves.Signals.Builders;

namespace Aether.NET.Symbol;

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
public interface ISymbol<T>
    where T : INumber<T> {
    ReadOnlyMemory<ReadOnlyMemory<T>> Samples { get; }
    int NumSamplesPerSymbol { get; }
}

public readonly record struct ChirpSymbolOption
(float Duration, int SampleRate, float FreqA, float FreqB, float Amp = 1.0f)
{
    public int NumSamplesPerSymbol => (int)(Duration * SampleRate);
}

public readonly struct ChirpSymbol<T> : ISymbol<T>
    where T : INumber<T>
{
    public ReadOnlyMemory<ReadOnlyMemory<T>> Samples { get; }
    public int NumSamplesPerSymbol { get; }
    public ChirpSymbol(ChirpSymbolOption symbolOption)
    {
        NumSamplesPerSymbol = symbolOption.NumSamplesPerSymbol;

        var result = new ReadOnlyMemory<T>[2];
        var builder = new ChirpBuilder();

        var sig = builder.SetParameter("low", -symbolOption.Amp)
                      .SetParameter("high", symbolOption.Amp)
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

public readonly struct ChirpSymbol2<T> : ISymbol<T>
    where T : INumber<T>
{
    public ReadOnlyMemory<ReadOnlyMemory<T>> Samples { get; }
    public int NumSamplesPerSymbol { get; }

    public ChirpSymbol2(ChirpSymbolOption symbolOption)
    {
        NumSamplesPerSymbol = symbolOption.NumSamplesPerSymbol;

        var result = new ReadOnlyMemory<T>[2];
        var buf = new double[symbolOption.NumSamplesPerSymbol];
        for (int i = 0; i < symbolOption.NumSamplesPerSymbol; i++)
            buf[i] = (symbolOption.FreqA +
                      i * (symbolOption.FreqB - symbolOption.FreqA) / symbolOption.NumSamplesPerSymbol) /
                     symbolOption.SampleRate;

        for (int i = 1; i < symbolOption.NumSamplesPerSymbol; i++)
            buf[i] += buf[i - 1];

        for (int i = 0; i < symbolOption.NumSamplesPerSymbol; i++)
            buf[i] = symbolOption.Amp * Math.Sin(buf[i] * 2.0 * Math.PI);

        var zero = new T[symbolOption.NumSamplesPerSymbol];

        for (int i = 0; i < symbolOption.NumSamplesPerSymbol; i++)
            zero[i] = T.CreateChecked(buf[i]);

        result[0] = zero;

        var one = new T[symbolOption.NumSamplesPerSymbol];
        for (int i = 0; i < symbolOption.NumSamplesPerSymbol; i++)
            one[i] = -zero[symbolOption.NumSamplesPerSymbol - i - 1];

        result[1] = one;

        Samples = result;
    }

    public static implicit operator ChirpSymbol2<T>(ChirpSymbolOption option) => new(option);
}

public readonly record struct DPSKSymbolOption
(int NumRedundant, int SampleRate, float Freq, float Amp = 1.0f)
{
    public int NumSamplesPerSymbol => (int)(SampleRate / Freq * NumRedundant);
}

public readonly struct DPSKSymbol<T> : ISymbol<T>
    where T : INumber<T>
{
    public ReadOnlyMemory<ReadOnlyMemory<T>> Samples { get; }
    public int NumSamplesPerSymbol { get; }
    public DPSKSymbol(DPSKSymbolOption symbolOption)
    {
        NumSamplesPerSymbol = symbolOption.NumSamplesPerSymbol;

        var result = new ReadOnlyMemory<T>[2];

        // for (int i = 0; i < symbolOption.NumSymbols; i++)
        var sig = new SineBuilder()
                      .SetParameter("low", -symbolOption.Amp)
                      .SetParameter("high", symbolOption.Amp)
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

public readonly record struct LineSymbolOption
(int NumSamplesPerSymbol, float Amp = 1.0f)
{
}

public readonly struct LineSymbol<T> : ISymbol<T>
    where T : INumber<T>
{
    public ReadOnlyMemory<ReadOnlyMemory<T>> Samples { get; }
    public int NumSamplesPerSymbol { get; }
    public LineSymbol(LineSymbolOption symbolOption)
    {
        NumSamplesPerSymbol = symbolOption.NumSamplesPerSymbol;

        var result = new ReadOnlyMemory<T>[2];

        var zero = new T[symbolOption.NumSamplesPerSymbol];
        zero.AsSpan().Fill(T.CreateChecked(-symbolOption.Amp));
        result[0] = zero;

        var one = new T[symbolOption.NumSamplesPerSymbol];
        one.AsSpan().Fill(T.CreateChecked(symbolOption.Amp));
        result[1] = one;

        Samples = result;
    }

    public static implicit operator LineSymbol<T>(LineSymbolOption option) => new(option);
}

public readonly record struct TriSymbolOption
(int NumSamplesPerSymbol, float Amp = 1.0f)
{
}

public readonly struct TriSymbol<T> : ISymbol<T>
    where T : INumber<T>
{
    public ReadOnlyMemory<ReadOnlyMemory<T>> Samples { get; }
    public int NumSamplesPerSymbol { get; }
    public TriSymbol(TriSymbolOption symbolOption)
    {
        NumSamplesPerSymbol = symbolOption.NumSamplesPerSymbol;
        var result = new ReadOnlyMemory<T>[2];

        var interval = T.CreateChecked(1f / (symbolOption.NumSamplesPerSymbol - 1));

        var zero = new T[symbolOption.NumSamplesPerSymbol];
        for (int i = 0; i < symbolOption.NumSamplesPerSymbol; i++)
            zero[i] = T.CreateChecked(-i) * interval;

        result[0] = zero;

        var one = new T[symbolOption.NumSamplesPerSymbol];
        for (int i = 0; i < symbolOption.NumSamplesPerSymbol; i++)
            one[i] = T.CreateChecked(i) * interval;

        result[1] = one;

        Samples = result;
    }

    public static implicit operator TriSymbol<T>(TriSymbolOption option) => new(option);
}