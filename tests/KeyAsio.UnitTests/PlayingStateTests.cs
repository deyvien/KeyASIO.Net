using KeyAsio.Configuration.Models;
using KeyAsio.Sync.States;

namespace KeyAsio.UnitTests;

public sealed class PlayingStateTests
{
    [Theory]
    [InlineData(GameClientType.Stable, 1, 1, 0, true)]
    [InlineData(GameClientType.Stable, 1, 10, 2, true)]
    [InlineData(GameClientType.Stable, 0, 10, 0, false)]
    [InlineData(GameClientType.Stable, 1, 0, 0, false)]
    [InlineData(GameClientType.Stable, 1, 2, 3, false)]
    [InlineData(GameClientType.Stable, 1, 2, -1, false)]
    [InlineData(GameClientType.Lazer, 0, 1, 0, true)]
    [InlineData(GameClientType.Lazer, 0, 10, 2, false)]
    [InlineData(GameClientType.Lazer, 0, 0, 0, false)]
    [InlineData(GameClientType.Lazer, 0, 2, 3, false)]
    public void RampComboBreakIncludesShortCombosAndPreservesSourceGuards(
        GameClientType client, int score, int oldCombo, int newCombo, bool expected)
        => Assert.Equal(expected, PlayingState.IsComboBreak(client, score, oldCombo, newCombo));

    [Theory]
    [InlineData(0, 30, 0, false)]
    [InlineData(1, 19, 0, false)]
    [InlineData(1, 20, 0, true)]
    [InlineData(1, 30, 29, true)]
    [InlineData(1, 30, 30, false)]
    public void StableComboBreak_PreservesMemoryWorkarounds(int score, int oldCombo, int newCombo, bool expected)
    {
        Assert.Equal(expected,
            PlayingState.ShouldPlayComboBreak(GameClientType.Stable, score, oldCombo, newCombo));
    }

    [Theory]
    [InlineData(0, 20, 0, false)]
    [InlineData(0, 21, 0, true)]
    [InlineData(0, 30, 29, false)]
    [InlineData(0, 30, 30, false)]
    public void LazerComboBreak_DoesNotRequireScoreOrJudgements(int score, int oldCombo, int newCombo, bool expected)
    {
        Assert.Equal(expected,
            PlayingState.ShouldPlayComboBreak(GameClientType.Lazer, score, oldCombo, newCombo));
    }
}
