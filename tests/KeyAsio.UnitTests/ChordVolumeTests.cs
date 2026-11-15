using KeyAsio.Core.Audio;
using Moq;
using NAudio.Wave;

namespace KeyAsio.UnitTests;

public sealed class ChordVolumeTests
{
    private sealed class ConstantSource(float value, WaveFormat format) : ISampleProvider
    {
        public WaveFormat WaveFormat => format;
        public int Read(float[] buffer, int offset, int count)
        {
            Array.Fill(buffer, value, offset, count);
            return count;
        }
    }

    [Fact]
    public void ChordAndNormalGainsAreIndependentUnderMaster_AndSurviveDeviceRestart()
    {
        var description = DeviceDescription.WasapiDefault;
        var devices = new Mock<IAudioDeviceManager>();
        devices.Setup(x => x.CreateDevice(It.IsAny<DeviceDescription?>(), It.IsAny<SynchronizationContext?>()))
            .Returns(() => (Mock.Of<IWavePlayer>(), description));
        using var engine = new AudioEngine(devices.Object)
        {
            LimiterType = LimiterType.Off, MainVolume = .5f, EffectVolume = .2f, ChordVolume = .8f
        };
        // Fake output device: this exercises the real engine graph without opening any hardware.
        engine.StartDevice(description, new WaveFormat(48000, 16, 2));
        engine.EffectMixer.AddMixerInput(new ConstantSource(.1f, engine.EngineWaveFormat));
        engine.ChordMixer.AddMixerInput(new ConstantSource(.1f, engine.EngineWaveFormat));
        var output = new float[16];
        void Check(float expected)
        {
            engine.RootSampleProvider.Read(output, 0, output.Length);
            Assert.All(output, sample => Assert.Equal(expected, sample, 5));
        }
        Check(.05f);
        engine.EffectVolume = 0;
        Check(.04f); // Muting ordinary hitsounds does not mute chords.
        engine.ChordVolume = .4f;
        Check(.02f); // Changing the fader affects already active chord voices.
        engine.MainVolume = .25f;
        Check(.01f);
        engine.ChordVolume = 0;
        engine.EffectVolume = .6f;
        Check(.015f);
        engine.StopDevice();
        engine.StartDevice(description, new WaveFormat(48000, 16, 2));
        Check(.015f);
        engine.ChordVolume = .4f;
        Check(.025f);
        engine.ChordMixer.RemoveAllMixerInputs();
        Check(.015f);
    }
}
