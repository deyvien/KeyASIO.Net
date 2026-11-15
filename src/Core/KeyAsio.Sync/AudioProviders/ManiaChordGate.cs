using KeyAsio.Core.OsuAudio.Hitsounds.Playback;

namespace KeyAsio.Sync.AudioProviders;

/// <summary>Matches physical presses to chart heads, independently of sample availability.</summary>
internal sealed class ManiaChordGate
{
    internal readonly record struct PressResult(bool MatchesNote, bool IsChord, IReadOnlyList<SampleEvent> Samples)
    {
        public static PressResult Unmatched => new(false, false, []);
    }

    private sealed class Chord(double offset, int[] lanes, SampleEvent[] samples)
    {
        public double Offset { get; } = offset;
        public int[] Lanes { get; } = lanes;
        public SampleEvent[] Samples { get; } = samples;
        public HashSet<int> Pressed { get; } = new();
        public bool Finished { get; set; }
    }

    private readonly Dictionary<int, Chord[]> _lanes;
    private readonly Chord[] _chords;
    private readonly HashSet<Chord> _pending = new();
    private double _lastTime = double.NegativeInfinity;
    private readonly int _keyCount;

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
    }

    // Called under the sequencer's chord lock. No audio data is retained between presses.
    public IReadOnlyList<SampleEvent> Press(int lane, double time, double window)
        => ProcessPress(lane, time, window, groupChords: true).Samples;

    public PressResult ProcessPress(int lane, double time, double window, bool groupChords)
    {
        if (time < _lastTime) CancelPending();
        _lastTime = time;
        foreach (var expired in _pending.Where(x => time > x.Offset + window).ToArray())
        {
            expired.Finished = true;
            expired.Pressed.Clear();
            _pending.Remove(expired);
        }
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
        var held = new PressResult(true, isChord, []);
        if (nearest.Finished || !nearest.Pressed.Add(lane)) return held;
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
            return new PressResult(true, isChord, samples);
        }
        if (nearest.Pressed.Count != nearest.Lanes.Length)
        {
            _pending.Add(nearest);
            return held;
        }

        nearest.Finished = true;
        _pending.Remove(nearest);
        return new PressResult(true, isChord, nearest.Samples);
    }

    public void CancelPending()
    {
        foreach (var chord in _pending)
        {
            chord.Finished = true;
            chord.Pressed.Clear();
        }
        _pending.Clear();
    }
}
