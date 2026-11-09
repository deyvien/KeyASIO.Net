using Coosu.Beatmap;
using KeyAsio.Configuration;
using KeyAsio.Core.Audio;
using KeyAsio.Core.Audio.Caching;
using KeyAsio.Core.Audio.SampleProviders;
using KeyAsio.Core.OsuAudio.Hitsounds.Playback;
using KeyAsio.Plugins.Contracts.Sync;
using KeyAsio.Sync;
using KeyAsio.Sync.Abstractions;
using KeyAsio.Sync.AudioProviders;
using KeyAsio.Sync.Models;
using KeyAsio.Sync.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NAudio.Wave;

namespace KeyAsio.UnitTests;

public sealed class ManiaChordIntegrationTests
{
    [Fact]
    public async Task ParsedHoldHeadAndTap_ReleaseCachedSamplesTogether_AndToggleCancelsPending()
    {
        var folder = Directory.CreateTempSubdirectory().FullName;
        try
        {
            var mapPath = Path.Combine(folder, "test.osu");
            await File.WriteAllTextAsync(mapPath, """
                osu file format v14
                [General]
                AudioFilename: test.wav
                Mode: 3
                [Metadata]
                Title: Chord fixture
                Artist: Test
                Creator: Test
                Version: Test
                [Difficulty]
                CircleSize: 4
                OverallDifficulty: 8
                HPDrainRate: 5
                ApproachRate: 5
                SliderMultiplier: 1.4
                SliderTickRate: 1
                [TimingPoints]
                0,500,4,2,0,100,1,0
                [HitObjects]
                64,192,1000,128,0,1500:0:0:0:0:
                320,192,1000,1,0,0:0:0:0:
                64,192,2000,1,0,0:0:0:0:
                320,192,2000,1,0,0:0:0:0:
                448,192,3000,1,0,0:0:0:0:
                """);
            var format = new WaveFormat(48000, 16, 2);
            using (var writer = new WaveFileWriter(Path.Combine(folder, "test.wav"), format))
                writer.WriteSamples(Enumerable.Repeat(.2f, 64).ToArray(), 0, 64);

            var settings = new AppSettings();
            settings.Sync.Playback.WaitForFinalChordKey = true;
            var ctx = new SyncSessionContext(settings) { OsuStatus = OsuMemoryStatus.Playing, IsStarted = true };
            var mixer = new QueueMixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2)) { ReadFully = true };
            var engine = new Mock<IPlaybackEngine>();
            engine.SetupGet(x => x.CurrentDevice).Returns(Mock.Of<IWavePlayer>());
            engine.SetupGet(x => x.EngineWaveFormat).Returns(format);
            engine.SetupGet(x => x.SourceWaveFormat).Returns(format);
            engine.SetupGet(x => x.EffectMixer).Returns(mixer);
            var runtime = new Mock<IPlaybackRuntimeState>();
            runtime.SetupGet(x => x.SelectedSkinFolder).Returns(folder);
            using var audio = new GameplayAudioService(NullLogger<GameplayAudioService>.Instance, ctx, settings,
                engine.Object, new AudioCacheManager(NullLogger<AudioCacheManager>.Instance), runtime.Object,
                Mock.Of<ISkinResourceProvider>());
            audio.SetContext(folder, "test.wav");
            var loader = new BeatmapHitsoundLoader(NullLogger<BeatmapHitsoundLoader>.Instance, settings, audio);
            var sfx = new SfxPlaybackService(NullLogger<SfxPlaybackService>.Instance, engine.Object, settings);
            var session = new GameplaySessionManager(NullLogger<GameplaySessionManager>.Instance, audio,
                engine.Object, ctx, loader, sfx) { OsuFile = await OsuFile.ReadFromFileAsync(mapPath) };
            var sequencer = new ManiaHitsoundSequencer(NullLogger<ManiaHitsoundSequencer>.Instance,
                settings, ctx, engine.Object, audio, session);
            foreach (var (time, balance) in new[] {(1000, -.75f), (1000, .25f), (2000, -.75f), (2000, .25f), (3000, .75f)})
                session.KeyList.Add(PlaybackEvent.Create(Guid.NewGuid(), time, 1, balance, "test.wav", ResourceOwner.Beatmap, SampleLayer.Primary));
            audio.PrecacheHitsoundsRangeInBackground(0, 4000, session.KeyList);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!session.KeyList.All(x => audio.TryGetAudioByNode(x, out _)))
                await Task.Delay(10, timeout.Token);
            sequencer.SeekTo(0);
            var buffer = new List<PlaybackInfo>();
            void Press(int lane, int time)
            {
                buffer.Clear(); ctx.BaseMemoryTime = time;
                sequencer.ProcessInteraction(buffer, lane, 4);
            }

            Press(0, 990);
            Assert.Empty(buffer);
            Press(2, 1020);
            Assert.Equal(2, buffer.Count);
            sfx.DispatchChord(buffer);
            var output = new float[16];
            mixer.Read(output, 0, output.Length);
            Assert.Contains(output, x => Math.Abs(x) > .1f);
            Press(0, 1490); // Hold tail is not another chord head.
            Assert.Empty(buffer);
            Press(0, 1990);
            Assert.Empty(buffer);
            settings.Sync.Playback.WaitForFinalChordKey = false;
            settings.Sync.Playback.WaitForFinalChordKey = true;
            Press(2, 2020);
            Assert.Empty(buffer);
            Press(3, 3000);
            Assert.Single(buffer);

            // A stopped game clock cancels a pending chord; resuming cannot release it.
            sequencer.SeekTo(0);
            Press(0, 990);
            await Task.Delay(150);
            buffer.Clear();
            sequencer.ProcessInteraction(buffer, 2, 4);
            Assert.Empty(buffer);
            Press(2, 1020);
            Assert.Empty(buffer);

            // Physical input must not add a gated chord during replay/autoplay.
            sequencer.SeekTo(0);
            ctx.IsReplay = true;
            Press(0, 990);
            Assert.Empty(buffer);
            ctx.IsReplay = false;
            Press(2, 1020);
            Assert.Empty(buffer);

            // Default-off still takes the original immediate lane path.
            settings.Sync.Playback.WaitForFinalChordKey = false;
            sequencer.SeekTo(0);
            Press(0, 1000);
            Assert.Single(buffer);
        }
        finally { Directory.Delete(folder, true); }
    }
}
