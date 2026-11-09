using NAudio.Wave;

namespace KeyAsio.Core.Audio.SampleProviders;

/// <summary>A prepared group submitted as one input, so every member starts in the same callback.</summary>
public sealed class GroupedSampleProvider(WaveFormat format) : IRecyclableProvider
{
    public QueueMixingSampleProvider Mixer { get; } = new(format);
    public WaveFormat WaveFormat => Mixer.WaveFormat;
    public int Read(float[] buffer, int offset, int count) => Mixer.Read(buffer, offset, count);

    public ISampleProvider? ResetAndGetSource()
    {
        Mixer.RemoveAllMixerInputs();
        Mixer.Read(Array.Empty<float>(), 0, 0); // Recycle children even when the group is cancelled before its first read.
        Mixer.Dispose();
        return null;
    }
}
