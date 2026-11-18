using KeyAsio.Core.OsuAudio.Hitsounds.Playback;

namespace KeyAsio.Sync.AudioProviders;

/// <summary>Matches physical presses to chart heads, independently of sample availability.</summary>
internal sealed class ManiaChordGate
{
    internal readonly record struct PressResult(bool MatchesNote, bool IsChord, IReadOnlyList<SampleEvent> Samples)
    {
        public static PressResult Unmatched => new(false, false, []);
        public double HeadTime { get; init; }
        public int SingleRun { get; init; }
        public int ChordRun { get; init; }
        public bool IsNewPress { get; init; }
    }

    private sealed class Chord(double offset, int[] lanes, SampleEvent[] samples)
    {
        public double Offset { get; } = offset;
        public int[] Lanes { get; } = lanes;
        public SampleEvent[] Samples { get; } = samples;
        public HashSet<int> Pressed { get; } = new();
        public HashSet<int> Waiting { get; } = new();
        public double Deadline { get; set; } = double.PositiveInfinity;
        public bool Finished { get; set; }
        public int SingleRun { get; set; }
        public int ChordRun { get; set; }
    }

    private readonly Dictionary<int, Chord[]> _lanes;
    private readonly Chord[] _chords;
    private readonly HashSet<Chord> _pending = new();
    private readonly Queue<PressResult> _ready = new();
    private readonly List<Chord> _due = new();
    private double _lastTime = double.NegativeInfinity;
    private readonly int _keyCount;
    private readonly HashSet<double> _chordTimes;
    private readonly Dictionary<double, Chord> _byTime;

    public ManiaChordGate(IEnumerable<(double Offset, int Lane)> heads,
        IEnumerable<SampleEvent> samples, int keyCount, double startTime)
    {
        _keyCount = keyCount;
        var byOffset = samples.Where(x => x.Layer == SampleLayer.Primary)
            .GroupBy(x => x.Offset).ToDictionary(x => x.Key, x => x.ToArray());
        _chords = heads.Where(x => x.Lane >= 0 && x.Lane < keyCount && x.Offset >= startTime)
            .GroupBy(x => x.Offset).OrderBy(x => x.Key)
            .Select(x => new Chord(x.Key, x.Select(h => h.Lane).Distinct().Order().ToArray(),
                byOffset.GetValueOrDefault(x.Key) ?? []))
            .ToArray();
        _lanes = Enumerable.Range(0, keyCount).ToDictionary(lane => lane,
            lane => _chords.Where(x => x.Lanes.Contains(lane)).ToArray());
        _chordTimes = _chords.Where(x => x.Lanes.Length > 1).Select(x => x.Offset).ToHashSet();
        int run = 0, chordRun = 0;
        foreach (var group in _chords)
        {
            if (group.Lanes.Length > 1) run++;
            else chordRun++;
            group.SingleRun = run;
            group.ChordRun = chordRun;
        }
        _byTime = _chords.ToDictionary(x => x.Offset);
    }

    public bool IsChordSample(PlaybackEvent sample) =>
        sample is SampleEvent { Layer: SampleLayer.Primary } && _chordTimes.Contains(sample.Offset);

    public PressResult DescribeSample(PlaybackEvent sample) =>
        sample is SampleEvent { Layer: SampleLayer.Primary } && _byTime.TryGetValue(sample.Offset, out var group)
            ? new PressResult(true, group.Lanes.Length > 1, [])
                { HeadTime = group.Offset, SingleRun = group.SingleRun, ChordRun = group.ChordRun, IsNewPress = true }
            : PressResult.Unmatched;

    // Called under the sequencer's chord lock. No audio data is retained between presses.
    public IReadOnlyList<SampleEvent> Press(int lane, double time, double window)
        => ProcessPress(lane, time, window, groupChords: true).Samples;

    public PressResult ProcessPress(int lane, double time, double window, bool groupChords,
        ManiaJudgementWindows? judgementWindows = null)
    {
        Advance(time, window);
        if (!_lanes.TryGetValue(lane, out var notes) || notes.Length == 0) return PressResult.Unmatched;

        int low = 0, high = notes.Length;
        while (low < high)
        {
            int mid = (low + high) / 2;
            if (notes[mid].Offset < time) low = mid + 1;
            else high = mid;
        }

        Chord? nearest = null;
        double distance = double.PositiveInfinity;
        for (int i = Math.Max(0, low - 1); i <= Math.Min(notes.Length - 1, low); i++)
        {
            double candidateDistance = Math.Abs(notes[i].Offset - time);
            if (candidateDistance < distance)
            {
                nearest = notes[i];
                distance = candidateDistance;
            }
        }

        if (nearest == null || distance > window) return PressResult.Unmatched;
        bool isChord = nearest.Lanes.Length > 1;
        var held = new PressResult(true, isChord, [])
            { HeadTime = nearest.Offset, SingleRun = nearest.SingleRun, ChordRun = nearest.ChordRun };
        if (nearest.Finished || !nearest.Pressed.Add(lane)) return held;
        held = held with { IsNewPress = true };
        if (!groupChords)
        {
            nearest.Finished = nearest.Pressed.Count == nearest.Lanes.Length;
            if (nearest.Finished) _pending.Remove(nearest);
            else _pending.Add(nearest);
            // Independent note-only mode releases just the pressed lane, never the whole chord.
            var samples = isChord
                ? nearest.Samples.Where(x => Math.Clamp((int)Math.Round((x.Balance + 1d) / 2 * _keyCount - .5),
                    0, _keyCount - 1) == lane).ToArray()
                : nearest.Samples;
            return held with { Samples = samples };
        }
        if (isChord && judgementWindows is { } windows)
        {
            nearest.Waiting.Add(lane);
            nearest.Deadline = Math.Min(nearest.Deadline, nearest.Offset + windows.LateEdgeFor(time - nearest.Offset));
        }
        if (nearest.Pressed.Count != nearest.Lanes.Length)
        {
            _pending.Add(nearest);
            return held;
        }

        nearest.Finished = true;
        _pending.Remove(nearest);
        return judgementWindows.HasValue && isChord ? ReleaseWaiting(nearest) : held with { Samples = nearest.Samples };
    }

    public void DrainDue(double time, double window, List<PressResult> results)
    {
        Advance(time, window);
        while (_ready.TryDequeue(out var result)) results.Add(result);
    }

    private void Advance(double time, double window)
    {
        if (time < _lastTime) CancelPending();
        _lastTime = time;
        _due.Clear();
        foreach (var chord in _pending)
            if (time >= chord.Deadline || time > chord.Offset + window) _due.Add(chord);
        _due.Sort(static (a, b) => a.Deadline != b.Deadline
            ? a.Deadline.CompareTo(b.Deadline) : a.Offset.CompareTo(b.Offset));
        foreach (var chord in _due)
        {
            if (time >= chord.Deadline && chord.Waiting.Count > 0)
                _ready.Enqueue(ReleaseWaiting(chord));
            if (time > chord.Offset + window)
            {
                chord.Finished = true;
                _pending.Remove(chord);
            }
        }
    }

    private PressResult ReleaseWaiting(Chord chord)
    {
        var samples = chord.Samples.Where(x => chord.Waiting.Contains(Math.Clamp(
            (int)Math.Round((x.Balance + 1d) / 2 * _keyCount - .5), 0, _keyCount - 1))).ToArray();
        chord.Waiting.Clear();
        chord.Deadline = double.PositiveInfinity;
        return new(true, true, samples)
            { HeadTime = chord.Offset, SingleRun = chord.SingleRun, ChordRun = chord.ChordRun, IsNewPress = true };
    }

    public void CancelPending()
    {
        _ready.Clear();
        foreach (var chord in _pending)
        {
            chord.Finished = true;
            chord.Pressed.Clear();
            chord.Waiting.Clear();
            chord.Deadline = double.PositiveInfinity;
        }
        _pending.Clear();
    }
}
