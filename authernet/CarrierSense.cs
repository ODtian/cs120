using System.IO.Pipelines;
using CS120.Utils;
using CS120.Utils.Wave;
using NAudio.Wave;

namespace CS120.CarrierSense;

public class CarrierSensor
(PipeReader pipeReader, WaveFormat waveFormat, int senseSamples = 220, float senseThreshold = 0.5f) : IPipeAdvance
{
    private readonly PipeViewProvider sampleBuffer = new(waveFormat, pipeReader);
    private readonly float[] buffer = new float[senseSamples];
    public PipeReader SourceReader { get; } = pipeReader;

    public bool TryAdvance()
    {
        if (SourceReader.TryRead(out var s))
        {
            SourceReader.AdvanceTo(s.Buffer.Start);
        }
        else
        {
            return true;
        }

        var samples = new float[s.Buffer.Length / (waveFormat.BitsPerSample / 8) / waveFormat.Channels];
        sampleBuffer.Read(samples, 0, samples.Length);
        for (var i = 0; i < samples.Length; i++)
        {
            var energy = samples[i] * samples[i];
            if (energy > senseThreshold)
            {
                // Console.WriteLine(energy);
                sampleBuffer.AdvanceSamples(i);
                return false;
            }
            // buffer[i] = samples[i];
        }
        sampleBuffer.AdvanceSamples(samples.Length);
        return true;
        // sampleBuffer.Read(buffer, 0, buffer.Length);

        // var energy = 0f;

        // for (var i = 0; i < buffer.Length; i++)
        // {
        //     energy += buffer[i] * buffer[i];
        // }

        // energy /= buffer.Length;

        // if (energy < 0.05)
        // {
        //     sampleBuffer.AdvanceSamples(buffer.Length);
        //     return true;
        // }

        // return false;
    }
}