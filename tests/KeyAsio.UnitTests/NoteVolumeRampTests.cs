using KeyAsio.Configuration.Models;
using KeyAsio.Core.OsuAudio.Hitsounds.Playback;
using KeyAsio.Sync.AudioProviders;

namespace KeyAsio.UnitTests;

public sealed class NoteVolumeRampTests
{
    [Fact]
    public void InterleavedChordLanesReuseTheirAssignedGain()
    {
        var ramp = new NoteVolumeRamp();
        Assert.Equal(1f / 3, ramp.Advance(0, 1000, SingleNoteRampCurve.Linear, 3), 6);
        Assert.Equal(2f / 3, ramp.Advance(0, 1100, SingleNoteRampCurve.Linear, 3), 6);
        Assert.Equal(1f / 3, ramp.Advance(0, 1000, SingleNoteRampCurve.Linear, 3), 6);
        Assert.Equal(2f / 3, ramp.Advance(0, 1100, SingleNoteRampCurve.Linear, 3), 6);
        Assert.Equal(1, ramp.Advance(0, 1200, SingleNoteRampCurve.Linear, 3));
    }

    [Fact]
    public void ChordRunChangesAtSinglesNotAtDifferentChordSizes()
    {
        var gate = new ManiaChordGate([(1000, 0), (1000, 1), (1500, 0), (1500, 1), (1500, 2),
            (2000, 3), (2500, 0), (2500, 1)], [], 4, 0);
        var a = gate.ProcessPress(0, 1000, 130, false);
        var b = gate.ProcessPress(0, 1500, 130, false);
        var c = gate.ProcessPress(0, 2500, 130, false);
        Assert.Equal(a.ChordRun, b.ChordRun);
        Assert.NotEqual(b.ChordRun, c.ChordRun);
    }

    [Fact]
    public void LinearThreeReachesCeilingOnThirdNoteAndStaysThere()
    {
        var ramp = new NoteVolumeRamp();
        Assert.Equal(1f / 3, ramp.Advance(0, 1000, SingleNoteRampCurve.Linear, 3), 6);
        Assert.Equal(2f / 3, ramp.Advance(0, 1100, SingleNoteRampCurve.Linear, 3), 6);
        Assert.Equal(1, ramp.Advance(0, 5000, SingleNoteRampCurve.Linear, 3)); // Spacing does not define a stream.
        Assert.Equal(1, ramp.Advance(0, 7000, SingleNoteRampCurve.Linear, 3));
    }

    [Fact]
    public void ExponentialTenHasConstantRatioAndReachesCeilingOnTenth()
    {
        var ramp = new NoteVolumeRamp();
        var gains = Enumerable.Range(1, 11)
            .Select(i => ramp.Advance(0, i * 100, SingleNoteRampCurve.Exponential, 10)).ToArray();
        Assert.Equal(.1f, gains[0], 6);
        Assert.Equal(1, gains[9]);
        Assert.Equal(1, gains[10]);
        float ratio = gains[1] / gains[0];
        for (int i = 1; i < 10; i++)
        {
            Assert.True(gains[i] > gains[i - 1]);
            Assert.Equal(ratio, gains[i] / gains[i - 1], 5);
        }
    }

    [Fact]
    public void SameHeadCountsOnceButNewChartSegmentStartsOver()
    {
        var ramp = new NoteVolumeRamp();
        Assert.Equal(.25f, ramp.Advance(0, 1000, SingleNoteRampCurve.Linear, 4));
        Assert.Equal(.25f, ramp.Advance(0, 1000, SingleNoteRampCurve.Linear, 4));
        Assert.Equal(.5f, ramp.Advance(0, 1001, SingleNoteRampCurve.Linear, 4));
        Assert.Equal(.25f, ramp.Advance(1, 2000, SingleNoteRampCurve.Linear, 4));
        ramp.Reset();
        Assert.Equal(.25f, ramp.Advance(1, 3000, SingleNoteRampCurve.Linear, 4));
    }

    [Fact]
    public void RetryAndCurveOrCountChangesResetProgress()
    {
        var ramp = new NoteVolumeRamp();
        ramp.Advance(0, 2000, SingleNoteRampCurve.Linear, 3);
        ramp.Advance(0, 3000, SingleNoteRampCurve.Linear, 3);
        ramp.Reset(); // The sequencer explicitly resets the ramp on seek/retry.
        Assert.Equal(1f / 3, ramp.Advance(0, 1000, SingleNoteRampCurve.Linear, 3), 6);
        Assert.Equal(.1f, ramp.Advance(0, 4000, SingleNoteRampCurve.Linear, 10), 6);
        Assert.Equal(.1f, ramp.Advance(0, 5000, SingleNoteRampCurve.Exponential, 10), 6);
    }

    [Theory]
    [InlineData(SingleNoteRampCurve.Linear, 1)]
    [InlineData(SingleNoteRampCurve.Exponential, 1)]
    [InlineData(SingleNoteRampCurve.Linear, 0)]
    [InlineData(SingleNoteRampCurve.Exponential, -5)]
    public void OneNoteOrInvalidLowCountIsFullVolume(SingleNoteRampCurve curve, int count)
        => Assert.Equal(1, new NoteVolumeRamp().Advance(0, 1000, curve, count));

    [Fact]
    public void ChartChordsBreakRunsEvenIfTheyAreNeverPressed()
    {
        SampleEvent Sample(double time) => PlaybackEvent.Create(Guid.NewGuid(), time, 1, 0,
            "test.wav", ResourceOwner.Beatmap, SampleLayer.Primary);
        var gate = new ManiaChordGate([(1000, 0), (1100, 2), (1500, 0), (1500, 1), (2000, 3)],
            [Sample(1000), Sample(1100), Sample(1500), Sample(1500), Sample(2000)], 4, 0);
        var a = gate.ProcessPress(0, 1000, 130, true);
        var b = gate.ProcessPress(2, 1100, 130, true);
        var c = gate.ProcessPress(3, 2000, 130, true);
        Assert.Equal(a.SingleRun, b.SingleRun);
        Assert.NotEqual(b.SingleRun, c.SingleRun);
        Assert.True(a.IsNewPress);
        Assert.False(gate.ProcessPress(3, 2001, 130, true).IsNewPress);
    }
}
