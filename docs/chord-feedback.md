# Final-key chord feedback

In **Sync Mode → Mania Hitsounds**, enable **Wait until final chord key**.
The setting is saved automatically and takes effect without restarting the audio device.
It defaults to off. **Only play hitsounds near note timing** is a separate
setting in the same section, also off by default.

For live mania play, notes whose heads share the same beatmap timestamp form a
chord. Its samples play together on the final distinct required lane press.
Single notes and ordinary taps keep KeyASIO's normal immediate lane-sample feedback.
A long note contributes its head, not its release.
Repeated presses in one lane cannot complete a chord. Incomplete or expired
groups are not released, including groups containing a lane without an audible sample.

Enable **Only play hitsounds near note timing** to silence taps outside a
playable note's hit window. It works with chord grouping on or off: with grouping
off, matched notes sound individually; with grouping on, chords wait for their
final key. Leaving note-only filtering off preserves the existing fallback to
the next available lane sample, including early taps and taps between notes.
This filters by beatmap timing, not by whether a note is visible on the screen.

The matching window follows Practice Timeline's classic mania final scoreable
window, including OD and DT/HT/EZ/HR adjustments. Matching uses chart lanes and
nearest head time; it does not read the game's individual note judgements.
Randomized lane layouts and lazer-specific judgement-window mods are not
validated by this implementation.

Retries, map transitions, setting changes and a stopped game clock discard
pending sounds. The existing memory clock can take up to 100 ms to detect a
pause. Brief interpolation corrections do not cancel a chord.

All grouped samples are submitted as one mixer input, preserving their sample
volume and balance. The setting applies to both ASIO and WASAPI; it does not
change driver latency, offsets, music playback, or replay/autoplay scheduling.
Physical taps no longer have an additional replay-flag mute in the chord path;
this follows the normal immediate path and does not repair replay detection itself.

Tracked by https://github.com/deyvien/KeyASIO.Net/issues/2.
Independent note filtering: https://github.com/deyvien/KeyASIO.Net/issues/4.

## Separate chord volume

Under **Sync Mode → Mania Hitsounds → Wait until final chord key**, enable
the child **Use separate chord volume**, then open
the bottom-left volume box and adjust **Chords**. The normal effects slider
continues to control single notes, free taps and other effects. Chords have an
independent level, so muting normal effects does not mute chords. Master volume
still affects both. The chord fader follows the existing 100%/150% volume range.

The option defaults to off, with a saved chord level of 100%. Off restores the
shared effects level and disables the Chords fader without forgetting its value.
It only takes effect while final-key grouping is on. Turning the parent off
disables the child control and chord fader and restores shared effects volume,
without forgetting the saved child choice or chord level. A chord is defined by multiple chart lanes at the same head time,
not by how many audio samples a note contains.

Tracked by https://github.com/deyvien/KeyASIO.Net/issues/6.

## Fallback hitsounds

Enable **Sync Mode → Mania Hitsounds → Fallback hitsounds for silent notes**
to use a default normal hit when a note's samples are missing, empty, digitally
silent, or all have zero authored volume. It defaults to off and saves automatically.

An audible component anywhere in that note prevents an added fallback. When all
components are silent, only the first component receives the replacement, so a
multi-sample note does not get several fallback sounds. Unprepared samples are
not assumed silent. Storyboard samples and loop/control events are excluded.

The fallback uses the game's default normal hitsound when available, otherwise
KeyASIO's built-in generated normal hit. A fixed 25% fallback gain makes the
replacement quieter before the normal/chord volume and
Master controls, along with the existing chord grouping and note-timing options.
It does not bypass a user-muted volume. Ignore line volumes is respected when
deciding whether the original sample is audible; it does not bypass the quieter fallback gain.

Tracked by https://github.com/deyvien/KeyASIO.Net/issues/8.
