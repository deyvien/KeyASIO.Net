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
using KeyAsio.Sync.States;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NAudio.Wave;

namespace KeyAsio.UnitTests;

public sealed class ManiaChordIntegrationTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(true, false, true, true)]
    [InlineData(false, true, true, true)]
    public async Task TapFeedbackAndChordGroupingAreIndependent(bool grouped, bool noteOnly, bool separateVolume, bool fallback = false)
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
                64,192,2500,1,0,0:0:0:0:
                320,192,2500,1,0,0:0:0:0:
                448,192,2500,1,0,0:0:0:0:
                448,192,3000,1,0,0:0:0:0:
                448,192,4000,1,0,0:0:0:0:
                64,192,5000,1,0,0:0:0:0:
                192,192,5100,1,0,0:0:0:0:
                64,192,6000,1,0,0:0:0:0:
                320,192,6000,1,0,0:0:0:0:
                320,192,6500,1,0,0:0:0:0:
                192,192,7000,1,0,0:0:0:0:
                448,192,7000,1,0,0:0:0:0:
                64,192,8000,1,0,0:0:0:0:
                192,192,9000,1,0,0:0:0:0:
                """);
            var format = new WaveFormat(48000, 16, 2);
            using (var writer = new WaveFileWriter(Path.Combine(folder, "test.wav"), format))
                writer.WriteSamples(Enumerable.Repeat(fallback ? 0 : .2f, 64).ToArray(), 0, 64);
            using (var writer = new WaveFileWriter(Path.Combine(folder, "default.wav"), format))
                writer.WriteSamples(Enumerable.Repeat(.2f, 64).ToArray(), 0, 64);

            var settings = new AppSettings();
            settings.Sync.Playback.WaitForFinalChordKey = grouped;
            settings.Sync.Playback.OnlyPlayMatchingNotes = noteOnly;
            settings.Sync.Playback.UseSeparateChordVolume = separateVolume;
            settings.Sync.Playback.UseFallbackHitsounds = fallback;
            var ctx = new SyncSessionContext(settings) { OsuStatus = OsuMemoryStatus.Playing, IsStarted = true };
            var mixer = new QueueMixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2)) { ReadFully = true };
            var chordMixer = new QueueMixingSampleProvider(mixer.WaveFormat) { ReadFully = true };
            var engine = new Mock<IPlaybackEngine>();
            engine.SetupGet(x => x.CurrentDevice).Returns(Mock.Of<IWavePlayer>());
            engine.SetupGet(x => x.EngineWaveFormat).Returns(format);
            engine.SetupGet(x => x.SourceWaveFormat).Returns(format);
            engine.SetupGet(x => x.EffectMixer).Returns(mixer);
            engine.SetupGet(x => x.ChordMixer).Returns(chordMixer);
            var runtime = new Mock<IPlaybackRuntimeState>();
            runtime.SetupGet(x => x.SelectedSkinFolder).Returns(folder);
            byte[] defaultBytes = File.ReadAllBytes(Path.Combine(folder, "default.wav"));
            var resources = new Mock<ISkinResourceProvider>();
            resources.Setup(x => x.TryGetStableResource("normal-hitnormal", out defaultBytes)).Returns(true);
            using var audio = new GameplayAudioService(NullLogger<GameplayAudioService>.Instance, ctx, settings,
                engine.Object, new AudioCacheManager(NullLogger<AudioCacheManager>.Instance), runtime.Object,
                resources.Object);
            audio.SetContext(folder, "test.wav");
            var loader = new BeatmapHitsoundLoader(NullLogger<BeatmapHitsoundLoader>.Instance, settings, audio);
            var sfx = new SfxPlaybackService(NullLogger<SfxPlaybackService>.Instance, engine.Object, settings, audio);
            var session = new GameplaySessionManager(NullLogger<GameplaySessionManager>.Instance, audio,
                engine.Object, ctx, loader, sfx) { OsuFile = await OsuFile.ReadFromFileAsync(mapPath) };
            var sequencer = new ManiaHitsoundSequencer(NullLogger<ManiaHitsoundSequencer>.Instance,
                settings, ctx, engine.Object, audio, session);
            foreach (var (time, balance) in new[] {(1000, -.75f), (1000, .25f), (2000, -.75f), (2000, .25f), (2500, -.75f), (2500, .25f), (2500, .75f), (3000, .75f), (4000, .75f), (5000, -.75f), (5100, -.25f), (6000, -.75f), (6000, .25f), (6500, .25f), (7000, -.25f), (7000, .75f), (8000, -.75f), (9000, -.25f)})
                session.KeyList.Add(PlaybackEvent.Create(Guid.NewGuid(), time, 1, balance, "test.wav", ResourceOwner.Beatmap, SampleLayer.Primary));
            audio.RegisterNoteSamples(session.KeyList);
            audio.PrecacheHitsoundsRangeInBackground(0, 10000, session.KeyList);
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

            // Before a note's window, normal mode selects the same next-lane sample as upstream.
            Press(0, 500);
            Assert.Equal(noteOnly ? 0 : 1, buffer.Count);
            if (!noteOnly)
            {
                Assert.Same(session.KeyList[0], buffer[0].PlaybackEvent);
                Assert.False(buffer[0].IsChord); // Free taps do not inherit the upcoming sample's chord level.
            }
            Press(3, 510);
            Assert.Equal(noteOnly ? 0 : 1, buffer.Count);
            if (!noteOnly) Assert.Same(session.KeyList.First(x => x.Offset == 2500 && x.Balance == .75f), buffer[0].PlaybackEvent);

            Press(0, 990);
            Assert.Equal(grouped ? 0 : 1, buffer.Count);
            Press(2, 1020);
            Assert.Equal(grouped ? 2 : 1, buffer.Count);
            if (grouped || noteOnly) Assert.All(buffer, x => Assert.True(x.IsChord));
            if (grouped) sfx.DispatchChord(buffer);
            else foreach (var item in buffer) sfx.DispatchPlayback(item);
            var output = new float[16];
            mixer.Read(output, 0, output.Length);
            if (grouped && separateVolume) Assert.All(output, x => Assert.Equal(0, x));
            else Assert.Contains(output, x => Math.Abs(x) > .01f);
            chordMixer.Read(output, 0, output.Length);
            if (grouped && separateVolume) Assert.Contains(output, x => Math.Abs(x) > .01f);
            else Assert.All(output, x => Assert.Equal(0, x));

            Press(0, 1490); // Hold tail is not another chord head; unfiltered tapping still works.
            Assert.Equal(noteOnly ? 0 : 1, buffer.Count);
            Press(0, 1990);
            Assert.Equal(grouped ? 0 : 1, buffer.Count);
            Press(2, 2020);
            Assert.Equal(grouped ? 2 : 1, buffer.Count);
            Press(3, 3000);
            Assert.Single(buffer);
            Assert.False(buffer[0].IsChord);
            Press(3, 3001); // Repeated single taps retain the normal next-sample fallback unless filtered.
            Assert.Equal(noteOnly ? 0 : 1, buffer.Count);
            if (!noteOnly) Assert.Same(session.KeyList.First(x => x.Offset == 4000), buffer[0].PlaybackEvent);

            // Switching policies cancels a partial group, including one started ungrouped.
            sequencer.SeekTo(0);
            settings.Sync.Playback.WaitForFinalChordKey = false;
            settings.Sync.Playback.OnlyPlayMatchingNotes = true;
            Press(0, 990);
            Assert.Single(buffer);
            settings.Sync.Playback.WaitForFinalChordKey = true;
            Press(2, 1020);
            Assert.Empty(buffer);

            // A stopped clock clears a partial chord, without changing normal tap feedback.
            sequencer.SeekTo(0);
            Press(0, 990);
            await Task.Delay(150);
            buffer.Clear();
            sequencer.ProcessInteraction(buffer, 2, 4);
            Assert.Empty(buffer);
            Press(2, 1020);
            Assert.Empty(buffer);

            // Physical input remains usable with the same replay flag the immediate path tolerates.
            sequencer.SeekTo(0);
            ctx.IsReplay = true;
            Press(0, 990);
            Assert.Empty(buffer);
            Press(2, 1020);
            Assert.Equal(2, buffer.Count);
            ctx.IsReplay = false;

            settings.Sync.Playback.WaitForFinalChordKey = false;
            settings.Sync.Playback.OnlyPlayMatchingNotes = false;
            sequencer.SeekTo(0);
            Press(0, 1000);
            Assert.Single(buffer);

            // The ramp counts actual single-note heads across keys and arbitrary spacing.
            settings.Sync.Playback.RampSingleNoteVolume = true;
            settings.Sync.Playback.SingleNoteRampNotes = 3;
            settings.Sync.Playback.WaitForFinalChordKey = grouped;
            sequencer.SeekTo(0);
            Press(0, 500);
            Assert.All(buffer, x => Assert.Null(x.VolumeRampGain));
            Press(3, 3000);
            Assert.Equal(1f / 3, Assert.Single(buffer).VolumeRampGain!.Value, 6);
            Press(3, 3001);
            Assert.All(buffer, x => Assert.Null(x.VolumeRampGain)); // Free repeats do not advance the ramp.
            Press(3, 4000);
            Assert.Equal(2f / 3, Assert.Single(buffer).VolumeRampGain!.Value, 6);
            Press(0, 5000);
            Assert.Equal(1, Assert.Single(buffer).VolumeRampGain);
            Press(1, 5100);
            Assert.Equal(1, Assert.Single(buffer).VolumeRampGain);
            Press(0, 6000);
            Assert.All(buffer, x => Assert.Null(x.VolumeRampGain));
            Press(2, 6020);
            Assert.All(buffer, x => Assert.Null(x.VolumeRampGain)); // Chords themselves are unchanged.
            Press(2, 6500);
            Assert.Equal(1f / 3, Assert.Single(buffer).VolumeRampGain!.Value, 6);
            Press(0, 8000); // Unplayed chord at 7000 still separates the runs.
            Assert.Equal(1f / 3, Assert.Single(buffer).VolumeRampGain!.Value, 6);
            sequencer.ClearChordState();
            Press(1, 9000);
            Assert.Equal(1f / 3, Assert.Single(buffer).VolumeRampGain!.Value, 6);
            sequencer.SeekTo(0);
            Press(3, 3000);
            Assert.Equal(1f / 3, Assert.Single(buffer).VolumeRampGain!.Value, 6);
            settings.Sync.Playback.SingleNoteRampNotes = 10;
            Press(3, 4000);
            Assert.Equal(.1f, Assert.Single(buffer).VolumeRampGain!.Value, 6);
            settings.Sync.Playback.SingleNoteRampCurve = KeyAsio.Configuration.Models.SingleNoteRampCurve.Exponential;
            Press(0, 5000);
            Assert.Equal(.1f, Assert.Single(buffer).VolumeRampGain!.Value, 6);
            settings.Sync.Playback.RampSingleNoteVolume = false;
            Press(1, 5100);
            Assert.All(buffer, x => Assert.Null(x.VolumeRampGain));

            // Chords use the same configured curve/count, once per chord rather than once per lane.
            settings.Sync.Playback.RampChordVolume = true;
            settings.Sync.Playback.SingleNoteRampCurve = KeyAsio.Configuration.Models.SingleNoteRampCurve.Linear;
            settings.Sync.Playback.SingleNoteRampNotes = 3;
            settings.Sync.Playback.WaitForFinalChordKey = grouped;
            settings.Sync.Playback.OnlyPlayMatchingNotes = true;
            sequencer.SeekTo(0);
            void CheckChordGain(float gain)
            {
                Assert.NotEmpty(buffer);
                Assert.All(buffer, x => { Assert.True(x.IsChord); Assert.Equal(gain, x.VolumeRampGain!.Value, 6); });
            }
            Press(0, 990);
            if (grouped) Assert.Empty(buffer); else CheckChordGain(1f / 3);
            Press(2, 1010);
            CheckChordGain(1f / 3);
            Press(0, 1990);
            if (grouped) Assert.Empty(buffer); else CheckChordGain(2f / 3);
            Press(2, 2010);
            CheckChordGain(2f / 3);
            Press(0, 2490);
            if (grouped) Assert.Empty(buffer); else CheckChordGain(1);
            Press(2, 2500);
            if (grouped) Assert.Empty(buffer); else CheckChordGain(1);
            Press(3, 2520);
            CheckChordGain(1); // Three-key chord still counts as one event.
            Press(3, 3000);
            Assert.Null(Assert.Single(buffer).VolumeRampGain); // Single toggle is off.
            Press(0, 5990);
            Press(2, 6020);
            CheckChordGain(1f / 3);
            Press(1, 6990); // Skipped single at 6500 still breaks the chord run.
            Press(3, 7020);
            CheckChordGain(1f / 3);
            sequencer.SeekTo(0);
            Press(0, 990); // Leave the first chord incomplete.
            Press(0, 1990);
            Press(2, 2010);
            CheckChordGain(grouped ? 1f / 3 : 2f / 3);
            settings.Sync.Playback.SingleNoteRampCurve = KeyAsio.Configuration.Models.SingleNoteRampCurve.Exponential;
            settings.Sync.Playback.SingleNoteRampNotes = 10;
            sequencer.SeekTo(0);
            Press(0, 990); Press(2, 1010);
            CheckChordGain(.1f);
            Press(0, 1990); Press(2, 2010);
            CheckChordGain((float)Math.Pow(10, -8d / 9));

            // Actual PlayingState combo handling resets both types, even with its SFX muted.
            session.InitializeProviders(sequencer, sequencer, sequencer, sequencer);
            settings.Sync.Filters.DisableComboBreakSfx = true;
            var playing = new PlayingState(NullLogger<PlayingState>.Instance, settings, engine.Object,
                loader, sfx, runtime.Object, session, audio);
            ctx.Score = 100;
            settings.Sync.Playback.SingleNoteRampCurve = KeyAsio.Configuration.Models.SingleNoteRampCurve.Linear;
            settings.Sync.Playback.SingleNoteRampNotes = 3;
            settings.Sync.Playback.RampSingleNoteVolume = true;
            foreach (bool reset in new[] { false, true })
            {
                settings.Sync.Playback.ResetVolumeRampOnComboBreak = reset;
                sequencer.SeekTo(0);
                Press(3, 3000);
                Press(3, 4000);
                Assert.Equal(2f / 3, Assert.Single(buffer).VolumeRampGain!.Value, 6);
                playing.OnComboChanged(ctx, 2, 0); // Below the sound-effect threshold.
                Press(0, 5000);
                Assert.Equal(reset ? 1f / 3 : 1, Assert.Single(buffer).VolumeRampGain!.Value, 6);
                sequencer.SeekTo(0);
                Press(0, 990); Press(2, 1010);
                Press(0, 1990); Press(2, 2010);
                CheckChordGain(2f / 3);
                playing.OnComboChanged(ctx, 2, 0);
                Press(0, 2490); Press(2, 2500); Press(3, 2520);
                CheckChordGain(reset ? 1f / 3 : 1);
            }
            // Resetting a ramp must not cancel a chord already waiting for its final key.
            settings.Sync.Playback.WaitForFinalChordKey = true;
            sequencer.SeekTo(0);
            Press(0, 990); Press(2, 1010);
            Press(0, 1990);
            Assert.Empty(buffer);
            playing.OnComboChanged(ctx, 1, 0);
            Press(2, 2010);
            Assert.Equal(2, buffer.Count);
            CheckChordGain(1f / 3);

            // Results-screen/stopped-session notifications cannot change an active ramp.
            foreach (bool stopped in new[] { false, true })
            {
                sequencer.SeekTo(0);
                Press(3, 3000);
                ctx.OsuStatus = stopped ? OsuMemoryStatus.Playing : OsuMemoryStatus.ResultsScreen;
                ctx.IsStarted = !stopped;
                playing.OnComboChanged(ctx, 1, 0);
                ctx.IsStarted = true;
                ctx.OsuStatus = OsuMemoryStatus.Playing;
                Press(3, 4000);
                Assert.Equal(2f / 3, Assert.Single(buffer).VolumeRampGain!.Value, 6);
            }
            settings.Sync.Playback.RampSingleNoteVolume = false;
            settings.Sync.Playback.RampChordVolume = false;

            settings.Sync.Playback.UseSeparateChordVolume = true;
            settings.Sync.Playback.WaitForFinalChordKey = true;
            sequencer.SeekTo(0);
            ctx.BaseMemoryTime = 1000;
            buffer.Clear();
            sequencer.ProcessAutoPlay(buffer, false);
            Assert.Equal(2, buffer.Count);
            Assert.All(buffer, x => Assert.True(x.IsChord));
            sfx.DispatchChord(buffer);
            session.Stop();
            chordMixer.Read(output, 0, output.Length);
            Assert.All(output, x => Assert.Equal(0, x)); // Pending chord audio is cleared on session stop.
        }
        finally { Directory.Delete(folder, true); }
    }
}
