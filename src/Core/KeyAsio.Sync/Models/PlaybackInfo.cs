using KeyAsio.Core.Audio.Caching;
using KeyAsio.Core.OsuAudio.Hitsounds.Playback;

namespace KeyAsio.Sync.Models;

public readonly record struct PlaybackInfo(CachedAudio? CachedAudio, PlaybackEvent PlaybackEvent)
{
    public bool IsChord { get; init; }
    public bool IsFallback { get; init; }
    public float? SingleNoteRampGain { get; init; }
}
