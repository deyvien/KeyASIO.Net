using KeyAsio.Configuration;
using KeyAsio.Core.Audio;
using KeyAsio.Core.Audio.Caching;
using KeyAsio.Core.Audio.SampleProviders;
using KeyAsio.Core.Audio.SampleProviders.BalancePans;
using KeyAsio.Core.OsuAudio.Hitsounds.Playback;
using KeyAsio.Sync;
using KeyAsio.Sync.Abstractions;
using KeyAsio.Sync.Models;
using KeyAsio.Sync.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NAudio.Wave;

namespace KeyAsio.UnitTests;

public sealed class FallbackHitsoundTests
{
    private sealed class Fixture : IDisposable
    {
        public string Folder { get; } = Directory.CreateTempSubdirectory().FullName;
        public AppSettings Settings { get; } = new();
        public GameplayAudioService Audio { get; }
        public SfxPlaybackService Sfx { get; }
        public QueueMixingSampleProvider Mixer { get; }
        private readonly WaveFormat _format = new(48000, 16, 2);

        public Fixture(bool defaultResource = true)
        {
            Settings.Sync.Playback.BalanceMode = BalanceMode.Off;
            var context = new SyncSessionContext(Settings) { IsStarted = true };
            Mixer = new QueueMixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2)) { ReadFully = true };
            var engine = new Mock<IPlaybackEngine>();
            engine.SetupGet(x => x.CurrentDevice).Returns(Mock.Of<IWavePlayer>());
            engine.SetupGet(x => x.SourceWaveFormat).Returns(_format);
            engine.SetupGet(x => x.EngineWaveFormat).Returns(_format);
            engine.SetupGet(x => x.EffectMixer).Returns(Mixer);
            engine.SetupGet(x => x.ChordMixer).Returns(Mixer);
            var runtime = new Mock<IPlaybackRuntimeState>();
            runtime.SetupGet(x => x.SelectedSkinFolder).Returns(Folder);
            var resources = new Mock<ISkinResourceProvider>();
            Write("default.wav", .25f);
            byte[] bytes = File.ReadAllBytes(Path.Combine(Folder, "default.wav"));
            if (defaultResource) resources.Setup(x => x.TryGetStableResource("normal-hitnormal", out bytes)).Returns(true);
            Audio = new GameplayAudioService(NullLogger<GameplayAudioService>.Instance, context, Settings,
                engine.Object, new AudioCacheManager(NullLogger<AudioCacheManager>.Instance), runtime.Object, resources.Object);
            Audio.SetContext(Folder, null);
            Sfx = new SfxPlaybackService(NullLogger<SfxPlaybackService>.Instance, engine.Object, Settings, Audio);
        }

        public void Write(string name, float value, int length = 64)
        {
            using var writer = new WaveFileWriter(Path.Combine(Folder, name), _format);
            writer.WriteSamples(Enumerable.Repeat(value, length).ToArray(), 0, length);
        }

        public async Task Cache(params SampleEvent[] notes)
        {
            Audio.PrecacheHitsoundsRangeInBackground(0, 2000, notes);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!notes.All(x => Audio.TryGetAudioByNode(x, out _))) await Task.Delay(10, timeout.Token);
        }

        public PlaybackInfo Playback(SampleEvent note)
        {
            Assert.True(Audio.TryGetAudioByNode(note, out var audio));
            return new PlaybackInfo(audio, note);
        }

        public float Render()
        {
            var data = new float[16];
            Mixer.Read(data, 0, data.Length);
            return data[0];
        }

        public void Dispose() { Audio.Dispose(); Directory.Delete(Folder, true); }
    }

    private static SampleEvent Note(string filename, Guid? id = null, float volume = 1,
        SampleLayer layer = SampleLayer.Primary) => PlaybackEvent.Create(id ?? Guid.NewGuid(), 1000, volume,
        0, filename, ResourceOwner.Beatmap, layer);

    [Theory]
    [InlineData("missing")]
    [InlineData("empty")]
    [InlineData("silent")]
    [InlineData("zero-volume")]
    [InlineData("silent", true)]
    public async Task OptionalFallbackMakesSilentNotesAudible(string reason, bool ignoreLineVolumes = false)
    {
        using var f = new Fixture();
        f.Settings.Sync.Filters.IgnoreLineVolumes = ignoreLineVolumes;
        if (reason != "missing") f.Write("note.wav", reason == "zero-volume" ? .125f : 0, reason == "empty" ? 0 : 64);
        var note = Note("note.wav", volume: reason == "zero-volume" ? 0 : 1);
        f.Audio.RegisterNoteSamples([note]);
        await f.Cache(note);
        var original = f.Playback(note);
        Assert.False(f.Settings.Sync.Playback.UseFallbackHitsounds);
        Assert.Equal(original, f.Audio.ResolveFallback(original));
        f.Sfx.DispatchPlayback(original);
        Assert.Equal(0, f.Render());
        f.Settings.Sync.Playback.UseFallbackHitsounds = true;
        Assert.True(f.Audio.ResolveFallback(original).IsFallback);
        f.Sfx.DispatchPlayback(original);
        Assert.Equal(.0625f, f.Render(), 3); // Default sample amplitude .25 at fixed 25% fallback gain.
        f.Settings.Sync.Playback.UseFallbackHitsounds = false;
        Assert.Equal(original, f.Audio.ResolveFallback(original));
    }

    [Theory]
    [InlineData(false, .5f)]
    [InlineData(true, .5f)]
    [InlineData(true, 1f)]
    [InlineData(true, .5f, true)]
    public async Task RampedNoteUsesMapperVolumeAsCeilingEvenWhenLineVolumesAreIgnored(bool ignore, float gain, bool chord = false)
    {
        using var f = new Fixture();
        f.Write("note.wav", .25f);
        var note = Note("note.wav", volume: .4f);
        f.Audio.RegisterNoteSamples([note]);
        await f.Cache(note);
        f.Settings.Sync.Filters.IgnoreLineVolumes = ignore;
        f.Sfx.DispatchPlayback(f.Playback(note) with { VolumeRampGain = gain, IsChord = chord });
        Assert.Equal(.25f * .4f * gain, f.Render(), 3);
    }

    [Theory]
    [InlineData(1f, .03125f)]
    [InlineData(.1f, .0125f)]
    [InlineData(0f, 0f)]
    public async Task RampedFallbackKeepsQuietCapAndMapperCeiling(float mapperVolume, float expected)
    {
        using var f = new Fixture();
        f.Settings.Sync.Playback.UseFallbackHitsounds = true;
        f.Settings.Sync.Filters.IgnoreLineVolumes = true;
        var note = Note("missing.wav", volume: mapperVolume);
        f.Audio.RegisterNoteSamples([note]);
        await f.Cache(note);
        f.Sfx.DispatchPlayback(f.Playback(note) with { VolumeRampGain = .5f });
        Assert.InRange(Math.Abs(expected - f.Render()), 0, 1f / 32768); // Allow one 16-bit PCM quantization step.
    }

    [Fact]
    public async Task AudibleSiblingSuppressesFallbackAndIsNotDoubled()
    {
        using var f = new Fixture();
        f.Settings.Sync.Playback.UseFallbackHitsounds = true;
        f.Write("silent.wav", 0); f.Write("audible.wav", .125f);
        var id = Guid.NewGuid(); var silent = Note("silent.wav", id); var audible = Note("audible.wav", id);
        f.Audio.RegisterNoteSamples([silent, audible]);
        await f.Cache(silent, audible);
        Assert.False(f.Audio.ResolveFallback(f.Playback(silent)).IsFallback);
        Assert.False(f.Audio.ResolveFallback(f.Playback(audible)).IsFallback);
        f.Sfx.DispatchChord([f.Playback(silent), f.Playback(audible)]);
        Assert.Equal(.125f, f.Render(), 3);
    }

    [Fact]
    public async Task MultipleSilentComponentsReceiveOneFallbackPerNote()
    {
        using var f = new Fixture();
        f.Settings.Sync.Playback.UseFallbackHitsounds = true;
        f.Write("silent.wav", 0);
        var id = Guid.NewGuid(); var a = Note("silent.wav", id); var b = Note("missing.wav", id);
        f.Audio.RegisterNoteSamples([a, b]);
        await f.Cache(a, b);
        Assert.True(f.Audio.ResolveFallback(f.Playback(a)).IsFallback);
        Assert.False(f.Audio.ResolveFallback(f.Playback(b)).IsFallback);
        f.Sfx.DispatchChord([f.Playback(a), f.Playback(b)]);
        Assert.Equal(.0625f, f.Render(), 3);
    }

    [Fact]
    public async Task UnpreparedSiblingAndStoryboardAreNotAssumedSilentNotes()
    {
        using var f = new Fixture();
        f.Settings.Sync.Playback.UseFallbackHitsounds = true;
        var id = Guid.NewGuid(); var a = Note("missing.wav", id); var b = Note("later.wav", id);
        var storyboard = Note("story.wav", layer: SampleLayer.Sampling);
        f.Audio.RegisterNoteSamples([a, b, storyboard]);
        await f.Cache(a, storyboard);
        Assert.False(f.Audio.ResolveFallback(f.Playback(a)).IsFallback);
        Assert.False(f.Audio.ResolveFallback(f.Playback(storyboard)).IsFallback);
        f.Audio.RegisterNoteSamples([b]);
        Assert.False(f.Audio.ResolveFallback(f.Playback(a)).IsFallback);
    }

    [Fact]
    public async Task InternalDefaultIsAvailableWithoutExtractedGameResources()
    {
        using var f = new Fixture(defaultResource: false);
        f.Settings.Sync.Playback.UseFallbackHitsounds = true;
        var note = Note("missing.wav");
        f.Audio.RegisterNoteSamples([note]);
        await f.Cache(note);
        var result = f.Audio.ResolveFallback(f.Playback(note) with { IsChord = true });
        Assert.True(result.IsFallback);
        Assert.True(result.IsChord);
        Assert.StartsWith("internal://dynamic/", result.CachedAudio!.SourceHash);
    }

    [Fact]
    public async Task IgnoringAuthoredVolumeKeepsAnAudibleSampleInsteadOfAddingFallback()
    {
        using var f = new Fixture();
        f.Settings.Sync.Playback.UseFallbackHitsounds = true;
        f.Write("note.wav", .125f);
        var note = Note("note.wav", volume: 0);
        f.Audio.RegisterNoteSamples([note]);
        await f.Cache(note);
        Assert.True(f.Audio.ResolveFallback(f.Playback(note)).IsFallback);
        f.Settings.Sync.Filters.IgnoreLineVolumes = true;
        Assert.False(f.Audio.ResolveFallback(f.Playback(note)).IsFallback);
        f.Sfx.DispatchPlayback(f.Playback(note));
        Assert.Equal(.125f, f.Render(), 3);
    }
}
