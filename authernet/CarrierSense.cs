using System.IO.Pipelines;
using CS120.Utils;
using CS120.Utils.Wave;
using NAudio.Wave;

namespace CS120.CarrierSense;

public class CarrierSensor
(PipeReader pipeReader, WaveFormat waveFormat, int senseSamples = 220, float senseThreshold = 0.05f) : IPipeAdvance
{
    private readonly PipeViewProvider sampleBuffer = new(waveFormat, pipeReader);
    private readonly float[] buffer = new float[senseSamples];
    public PipeReader SourceReader { get; } = pipeReader;

    public bool TryAdvance()
    {
        sampleBuffer.Read(buffer, 0, buffer.Length);

        var energy = 0f;

        for (var i = 0; i < buffer.Length; i++)
        {
            energy += buffer[i] * buffer[i];
        }

        energy /= buffer.Length;

        if (energy < senseThreshold)
        {
            sampleBuffer.AdvanceSamples(buffer.Length);
            return true;
        }

        return false;
    }
}