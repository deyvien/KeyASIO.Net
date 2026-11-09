using KeyAsio.Core.OsuAudio.Hitsounds.Playback;
using KeyAsio.Plugins.Contracts.Sync;
using KeyAsio.Sync.AudioProviders;

namespace KeyAsio.UnitTests;

public sealed class ManiaChordGateTests
{
    private static SampleEvent Sample(double time, string name = "normal-hitnormal") =>
        PlaybackEvent.Create(Guid.NewGuid(), time, 1, 0, name, ResourceOwner.UserSkin, SampleLayer.Primary);

    [Fact]
    public void FinalDistinctLaneReleasesEverySampleOnce()
    {
        SampleEvent[] samples = [Sample(1000), Sample(1000, "normal-hitclap"), Sample(1000)];
        var gate = new ManiaChordGate([(1000, 0), (1000, 2)], samples, 4, 0);
        Assert.Empty(gate.Press(0, 980, 130.5));
        Assert.Empty(gate.Press(0, 990, 130.5));
        Assert.Equal(samples, gate.Press(2, 1030, 130.5));
        Assert.Empty(gate.Press(2, 1031, 130.5));
        Assert.Empty(gate.Press(0, 1032, 130.5));
    }

    [Fact]
    public void SingleNoteIsImmediateAndDoesNotCompleteNearbyChord()
    {
        var single = Sample(1100);
        var gate = new ManiaChordGate([(1000, 0), (1000, 1), (1100, 0)],
            [Sample(1000), Sample(1000), single], 4, 0);
        Assert.Empty(gate.Press(1, 980, 130.5));
        Assert.Equal([single], gate.Press(0, 1090, 130.5));
    }

    [Fact]
    public void ConsecutiveChordsAreIndependent()
    {
        var first = Sample(1000); var second = Sample(1150);
        var gate = new ManiaChordGate([(1000, 0), (1000, 1), (1150, 0), (1150, 1)],
            [first, second], 4, 0);
        Assert.Empty(gate.Press(0, 990, 130.5));
        Assert.Equal([first], gate.Press(1, 1020, 130.5));
        Assert.Empty(gate.Press(1, 1140, 130.5));
        Assert.Equal([second], gate.Press(0, 1180, 130.5));
    }

    [Theory]
    [InlineData(1130.5, true)]
    [InlineData(1130.501, false)]
    public void IncompleteChordCannotReleaseAfterLastScoreableInstant(double last, bool releases)
    {
        var sample = Sample(1000);
        var gate = new ManiaChordGate([(1000, 0), (1000, 1)], [sample], 4, 0);
        Assert.Empty(gate.Press(0, 990, 130.5));
        Assert.Equal(releases ? 1 : 0, gate.Press(1, last, 130.5).Count);
    }

    [Fact]
    public void TooEarlyPressDoesNotCountAndUnrelatedLaneDoesNotCompleteChord()
    {
        var gate = new ManiaChordGate([(1000, 0), (1000, 1)], [Sample(1000)], 4, 0);
        Assert.Empty(gate.Press(0, 800, 130.5));
        Assert.Empty(gate.Press(2, 990, 130.5));
        Assert.Empty(gate.Press(1, 1000, 130.5));
        Assert.Single(gate.Press(0, 1010, 130.5));
    }

    [Fact]
    public void SilentLaneStillRequiredAndMultipleSamplesDoNotAddRequiredKeys()
    {
        var gate = new ManiaChordGate([(1000, 0), (1000, 0), (1000, 1)],
            [Sample(1000), Sample(1000)], 4, 0);
        Assert.Empty(gate.Press(0, 990, 130.5));
        Assert.Equal(2, gate.Press(1, 1010, 130.5).Count);
    }

    [Fact]
    public void CancellationDiscardsPartialChordWithoutReplayingFinishedOnes()
    {
        var gate = new ManiaChordGate([(1000, 0), (1000, 1), (2000, 2)],
            [Sample(1000), Sample(2000)], 4, 0);
        gate.Press(0, 990, 130.5);
        gate.CancelPending();
        Assert.Empty(gate.Press(1, 1010, 130.5));
        Assert.Empty(gate.Press(0, 1020, 130.5));
        Assert.Single(gate.Press(2, 2000, 130.5));
        gate.CancelPending();
        Assert.Empty(gate.Press(2, 2001, 130.5));
    }

    [Fact]
    public void SeekExcludesEarlierHeadsAndNewMapCannotReleaseOldChord()
    {
        var gate = new ManiaChordGate([(1000, 0), (1000, 1), (2000, 0), (2000, 1)],
            [Sample(1000), Sample(2000)], 4, 1500);
        Assert.Empty(gate.Press(0, 1000, 130.5));
        Assert.Empty(gate.Press(1, 1000, 130.5));
        Assert.Empty(gate.Press(0, 1990, 130.5));
        var replacement = new ManiaChordGate([(2000, 0), (2000, 1)], [Sample(2000)], 4, 0);
        Assert.Empty(replacement.Press(1, 2010, 130.5));
    }

    [Theory]
    [InlineData(8, Mods.None, 127.5)]
    [InlineData(8, Mods.DoubleTime, 190.5)]
    [InlineData(8, Mods.Nightcore, 190.5)]
    [InlineData(8, Mods.HalfTime, 95.5)]
    [InlineData(8, Mods.HardRock, 90.5)]
    [InlineData(8, Mods.Easy, 170)]
    public void ChordWindowMatchesPracticeTimeline(double od, Mods mods, double expected)
        => Assert.Equal(expected, ManiaHitsoundSequencer.ChordWindow(od, mods));
}
