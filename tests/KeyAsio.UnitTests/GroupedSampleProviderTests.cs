using KeyAsio.Core.Audio.SampleProviders;
using NAudio.Wave;

namespace KeyAsio.UnitTests;

public sealed class GroupedSampleProviderTests
{
    private sealed class Impulse(float amplitude) : ISampleProvider
    {
        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
        private bool _read;
        public int FirstRead { get; private set; } = -1;
        public int Callback { get; set; }
        public int Read(float[] buffer, int offset, int count)
        {
            if (_read) return 0;
            _read = true;
            FirstRead = Callback;
            Array.Clear(buffer, offset, count);
            buffer[offset] = amplitude;
            return count;
        }
    }

    [Fact]
    public void AudioCallbackBetweenPreparationAndSubmissionCannotSplitChord()
    {
        var left = new Impulse(.2f); var right = new Impulse(.3f);
        var output = new QueueMixingSampleProvider(left.WaveFormat) { ReadFully = true };
        var chord = new GroupedSampleProvider(left.WaveFormat);
        var buffer = new float[16];
        chord.Mixer.AddMixerInput(left);
        output.Read(buffer, 0, buffer.Length);
        Assert.All(buffer, x => Assert.Equal(0, x));
        Assert.Equal(-1, left.FirstRead);
        chord.Mixer.AddMixerInput(right);
        output.AddMixerInput(chord);
        left.Callback = right.Callback = 2;
        output.Read(buffer, 0, buffer.Length);
        Assert.Equal(.5f, buffer[0]);
        Assert.Equal(2, left.FirstRead);
        Assert.Equal(left.FirstRead, right.FirstRead);
        output.Read(buffer, 0, buffer.Length);
        Assert.All(buffer, x => Assert.Equal(0, x));
    }

    [Fact]
    public void CancelBeforeFirstCallbackDoesNotReadAnyChild()
    {
        var source = new Impulse(1);
        var group = new GroupedSampleProvider(source.WaveFormat);
        group.Mixer.AddMixerInput(source);
        Assert.Null(group.ResetAndGetSource());
        Assert.Equal(-1, source.FirstRead);
    }
}
