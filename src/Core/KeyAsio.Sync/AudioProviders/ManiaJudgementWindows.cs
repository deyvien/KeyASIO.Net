using KeyAsio.Plugins.Contracts.Sync;

namespace KeyAsio.Sync.AudioProviders;

/// <summary>Classic mania head windows in beatmap milliseconds, like the existing chord matcher.</summary>
internal readonly record struct ManiaJudgementWindows(double Perfect, double Great, double Good, double Ok, double Meh)
{
    public static ManiaJudgementWindows Classic(double overallDifficulty, Mods mods)
    {
        double od = Math.Clamp(overallDifficulty, 0, 10);
        double rate = (mods & (Mods.DoubleTime | Mods.Nightcore)) != 0 ? 1.5 :
            (mods & Mods.HalfTime) != 0 ? .75 : 1;
        double difficulty = (mods & Mods.Easy) != 0 ? 1 / 1.4 : (mods & Mods.HardRock) != 0 ? 1.4 : 1;
        double last = ManiaHitsoundSequencer.ChordWindow(od, mods);
        double Width(double basis) => Math.Min(last, Math.Floor(basis * rate / difficulty) + .5);
        return new(Width(16), Width(64 - 3 * od), Width(97 - 3 * od), Width(127 - 3 * od), last);
    }

    // Use the late edge, including for early hits. A better later hit can shorten a pending deadline.
    public double LateEdgeFor(double error)
    {
        error = Math.Abs(error);
        return error <= Perfect ? Perfect : error <= Great ? Great : error <= Good ? Good :
            error <= Ok ? Ok : Meh;
    }
}
