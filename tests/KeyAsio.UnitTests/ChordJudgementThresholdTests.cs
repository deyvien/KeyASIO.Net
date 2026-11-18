using KeyAsio.Core.OsuAudio.Hitsounds.Playback;
using KeyAsio.Plugins.Contracts.Sync;
using KeyAsio.Sync.AudioProviders;

namespace KeyAsio.UnitTests;

public sealed class ChordJudgementThresholdTests
{
    private static readonly ManiaJudgementWindows Windows = ManiaJudgementWindows.Classic(8, Mods.None);
    private static SampleEvent Sample(int lane, double head = 1000, string name = "hit") =>
        PlaybackEvent.Create(Guid.NewGuid(), head, 1, (float)((lane + .5) / 2 - 1), name,
            ResourceOwner.UserSkin, SampleLayer.Primary);

    private static ManiaChordGate Gate(int lanes = 4) => new(
        Enumerable.Range(0, lanes).Select(l => (1000d, l)),
        Enumerable.Range(0, lanes).Select(l => Sample(l)), 4, 0);

    private static ManiaChordGate.PressResult Press(ManiaChordGate gate, int lane, double time) =>
        gate.ProcessPress(lane, time, Windows.Meh, true, Windows);

    private static List<ManiaChordGate.PressResult> Drain(ManiaChordGate gate, double time)
    {
        var results = new List<ManiaChordGate.PressResult>();
        gate.DrainDue(time, Windows.Meh, results);
        return results;
    }

    [Fact]
    public void CompleteChordStillReleasesTogetherBeforeDeadline()
    {
        var gate = Gate(2);
        Assert.Empty(Press(gate, 0, 990).Samples);
        Assert.Equal(2, Press(gate, 1, 1005).Samples.Count);
        Assert.Empty(Drain(gate, 1100));
    }

    [Fact]
    public void PerfectThenLateGreatThenGoodReleaseInGroupsWithoutUnpressedLanes()
    {
        var gate = Gate();
        Press(gate, 0, 995); Press(gate, 1, 1005);
        Assert.Empty(Drain(gate, 1016.499));
        var perfect = Assert.Single(Drain(gate, 1016.5));
        Assert.Equal(2, perfect.Samples.Count);
        Assert.Equal(new[] { -.75f, -.25f }, perfect.Samples.Select(x => x.Balance));
        Assert.Empty(Drain(gate, 1020));
        Assert.Empty(Press(gate, 2, 1030).Samples);
        Assert.Empty(Drain(gate, 1040.499));
        Assert.Equal(.25f, Assert.Single(Assert.Single(Drain(gate, 1040.5)).Samples).Balance);
        var final = Press(gate, 3, 1060);
        Assert.Equal(.75f, Assert.Single(final.Samples).Balance);
        Assert.Empty(Drain(gate, 1200));
    }

    [Fact]
    public void MultipleLateKeysWaitTogetherUntilTheNextDeadline()
    {
        var gate = Gate();
        Press(gate, 0, 1000); Drain(gate, 1017);
        Press(gate, 1, 1020); Press(gate, 2, 1035);
        Assert.Empty(Drain(gate, 1040));
        Assert.Equal(2, Assert.Single(Drain(gate, 1040.5)).Samples.Count);
        Assert.Empty(Drain(gate, 1200)); // The fourth lane was never pressed.
    }

    [Fact]
    public void LastRemainingKeyImmediatelyReleasesWaitingLateGroup()
    {
        var gate = Gate();
        Press(gate, 0, 1000); Drain(gate, 1017);
        Press(gate, 1, 1020); Press(gate, 2, 1030);
        Assert.Equal(3, Press(gate, 3, 1035).Samples.Count);
        Assert.Empty(Drain(gate, 1041));
    }

    [Fact]
    public void BetterJudgementShortensTheDeadlineForAnEarlyGroup()
    {
        var gate = Gate();
        Press(gate, 0, 965); // Early 300 initially waits until +40.5.
        Press(gate, 1, 990); // 320 narrows that to +16.5.
        Assert.Equal(2, Assert.Single(Drain(gate, 1016.5)).Samples.Count);
    }

    [Fact]
    public void LatePressCollectsOverdueGroupEvenWhenNoTickRan()
    {
        var gate = Gate(2);
        Press(gate, 0, 1000);
        var late = Press(gate, 1, 1030);
        Assert.Single(late.Samples);
        var earlier = Assert.Single(Drain(gate, 1030));
        Assert.Single(earlier.Samples);
        Assert.NotSame(late.Samples[0], earlier.Samples[0]);
        Assert.Empty(Drain(gate, 1200));
    }

    [Fact]
    public void DuplicatePressesCannotRepeatReleasedSamplesOrExtendDeadline()
    {
        var gate = Gate();
        Press(gate, 0, 1000); Press(gate, 0, 1010);
        Assert.Single(Assert.Single(Drain(gate, 1017)).Samples);
        Assert.Empty(Press(gate, 0, 1030).Samples);
        Assert.Empty(Drain(gate, 1041));
    }

    [Fact]
    public void MissingLanesRemainSilentAfterTheLastWindow()
    {
        var gate = Gate();
        Press(gate, 1, 1110); // 50 window.
        Assert.Empty(Drain(gate, 1127));
        Assert.Single(Assert.Single(Drain(gate, 1127.5)).Samples);
        Assert.Empty(Press(gate, 2, 1128).Samples);
        Assert.Empty(Drain(gate, 1300));
    }

    [Fact]
    public void UnhitChordsNeverProduceTimerAudio()
        => Assert.Empty(Drain(Gate(), 2000));

    [Fact]
    public void OffRetainsOriginalIncompleteChordSilence()
    {
        var gate = Gate();
        gate.ProcessPress(0, 1000, Windows.Meh, true);
        Assert.Empty(Drain(gate, 1200));
    }

    [Fact]
    public void ParentOffKeepsImmediateLanePlayback()
    {
        var gate = Gate();
        Assert.Single(gate.ProcessPress(0, 1000, Windows.Meh, false, Windows).Samples);
        Assert.Empty(Drain(gate, 1200));
    }

    [Fact]
    public void CancelOrBackwardClockDiscardsPendingAndReadyGroups()
    {
        var gate = Gate();
        Press(gate, 0, 1000);
        gate.CancelPending();
        Assert.Empty(Drain(gate, 1100));
        gate = Gate();
        Press(gate, 0, 1000);
        Press(gate, 1, 1030); // Enqueues the first release, then starts a late group.
        gate.CancelPending();
        Assert.Empty(Drain(gate, 1100));
        gate = Gate();
        Press(gate, 0, 1000);
        Assert.Empty(Drain(gate, 900));
        Assert.Empty(Drain(gate, 1100));
    }

    [Fact]
    public void AllSampleComponentsInPressedLaneReleaseTogether()
    {
        var gate = new ManiaChordGate([(1000d, 0), (1000d, 1)],
            [Sample(0), Sample(0, name: "clap"), Sample(1)], 4, 0);
        Press(gate, 0, 1000);
        Assert.Equal(2, Assert.Single(Drain(gate, 1017)).Samples.Count);
        Assert.Single(Press(gate, 1, 1030).Samples);
    }

    [Theory]
    [InlineData(Mods.None, 16.5, 40.5, 73.5, 103.5, 127.5)]
    [InlineData(Mods.DoubleTime, 24.5, 60.5, 109.5, 154.5, 190.5)]
    [InlineData(Mods.Nightcore, 24.5, 60.5, 109.5, 154.5, 190.5)]
    [InlineData(Mods.HalfTime, 12.5, 30.5, 54.5, 77.5, 95.5)]
    [InlineData(Mods.HardRock, 11.5, 28.5, 52.5, 73.5, 90.5)]
    [InlineData(Mods.Easy, 22.5, 56.5, 102.5, 144.5, 170)]
    public void WindowsMatchClassicTimingAndExistingLastWindow(Mods mods,
        double perfect, double great, double good, double ok, double meh)
    {
        var w = ManiaJudgementWindows.Classic(8, mods);
        Assert.Equal(new(perfect, great, good, ok, meh), w);
        Assert.Equal(perfect, w.LateEdgeFor(-perfect));
        Assert.Equal(great, w.LateEdgeFor(perfect + .001));
    }
}
