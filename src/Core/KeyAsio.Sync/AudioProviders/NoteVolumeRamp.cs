using KeyAsio.Configuration.Models;

namespace KeyAsio.Sync.AudioProviders;

/// <summary>Counts distinct played note/chord heads within one same-type run.</summary>
internal sealed class NoteVolumeRamp
{
    private int _segment = -1;
    private int _count;
    private int _target;
    private SingleNoteRampCurve _curve;
    private readonly Dictionary<double, float> _recentGains = new();
    private readonly Queue<double> _recentHeads = new();

    public float Advance(int segment, double head, SingleNoteRampCurve curve, int target)
    {
        target = Math.Clamp(target, 1, 1000);
        if (_segment != segment || _curve != curve || _target != target) Reset();
        _segment = segment;
        _curve = curve;
        _target = target;
        // Chord lanes may interleave with a following chord. Reuse the assigned gain
        // even when this is no longer the most recently played head. Seeks reset explicitly.
        if (_recentGains.TryGetValue(head, out var gain)) return gain;
        _count = Math.Min(_count + 1, target);
        var nextGain = target == 1 || _count == target ? 1 : curve == SingleNoteRampCurve.Exponential
            ? (float)Math.Pow(target, (double)(_count - target) / (target - 1))
            : (float)_count / target;
        _recentGains[head] = nextGain;
        _recentHeads.Enqueue(head);
        if (_recentHeads.Count > 128) _recentGains.Remove(_recentHeads.Dequeue());
        return nextGain;
    }

    public void Reset()
    {
        _segment = -1;
        _count = 0;
        _recentGains.Clear();
        _recentHeads.Clear();
    }
}
