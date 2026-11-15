using KeyAsio.Configuration.Models;

namespace KeyAsio.Sync.AudioProviders;

/// <summary>Counts distinct played heads in a chart segment containing no chords.</summary>
internal sealed class SingleNoteVolumeRamp
{
    private int _segment = -1;
    private int _count;
    private double _lastHead = double.NaN;
    private float _lastGain = 1;
    private int _target;
    private SingleNoteRampCurve _curve;

    public float Advance(int segment, double head, SingleNoteRampCurve curve, int target)
    {
        target = Math.Clamp(target, 1, 1000);
        if (_segment != segment || head < _lastHead || _curve != curve || _target != target) Reset();
        _segment = segment;
        _curve = curve;
        _target = target;
        if (head == _lastHead) return _lastGain; // Multiple samples of the same note share one step.
        _lastHead = head;
        _count = Math.Min(_count + 1, target);
        _lastGain = target == 1 || _count == target ? 1 : curve == SingleNoteRampCurve.Exponential
            ? (float)Math.Pow(target, (double)(_count - target) / (target - 1))
            : (float)_count / target;
        return _lastGain;
    }

    public void Reset()
    {
        _segment = -1;
        _count = 0;
        _lastHead = double.NaN;
        _lastGain = 1;
    }
}
