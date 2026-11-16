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

## Volume ramp-up

Under **Sync Mode → Mania Hitsounds → Volume ramp-up**, enable **Single notes**,
**Chords**, or both. Choose the shared **Linear** or **Exponential** ramp mode
and **Count to maximum** (1–1000, default 3). Both toggles default to off;
existing single-note choices and curve/count preferences are preserved.

Consecutive heads of the same type share one count across lanes, samples,
spacing and rhythmic values. There is no stream-pattern detector. Chords
reset the single-note run; singles reset the chord run, even when the
separating note is skipped. Chord width does not change its type: a two-key
chord followed by a three-key chord advances the same chord run.

A chord counts once, and every lane/sample receives the same ramp gain.
With **Wait until final chord key** enabled, it advances only when completed
and released. Otherwise its first played lane advances the count; later
lanes reuse that gain, including interleaved lanes from adjacent chords.
Multiple sound components count once. Free taps do not advance the count.
Seek, retry, map, pause and ramp-setting changes reset progress.

The nested **Reset on combo break** option restarts both enabled ramps at
their first step after a gameplay combo break. It defaults to off and is
available while at least one note-type toggle is on. It includes short combos
and works even when combo-break sound effects are disabled. Pending final-key
chords remain intact. Stable uses a combo decrease with a nonzero score;
lazer uses a positive combo dropping to zero, following their existing source
semantics. Detection follows the game's reported combo update, so audio that
has already started is not changed retroactively.

Both curves start at 1/N of the note's authored volume and reach its full
authored volume on event N. Linear uses equal steps: N=3 gives 1/3, 2/3, 1.
Exponential uses a constant multiplier: N=10 rises from 10% to 100% by its
tenth event. Later consecutive events remain at their authored level.

Each note's own mapper volume is the ceiling, including when Ignore line
volumes is on; Master and hitsound faders still apply afterward. Fallback
sounds retain their quieter cap and cannot exceed the mapper ceiling while
ramped, so an authored zero remains silent in this mode.

Enable the nested **Override mapper volume to 100%** option to use a 100%
ceiling instead for either enabled ramp. It defaults to off, preserving the
mapper-volume behavior described above. This also lets zero-volume notes sound:
audible original samples are retained, and missing/silent samples can use the
existing fallback option. Fallback still has its quieter 25% cap before ramp
gain; your Master, effects and chord faders still apply. Notes without an active
ramp and free taps are unaffected. Both nested options are disabled when
neither note type is enabled, without forgetting their saved choices.

Tracked by https://github.com/deyvien/KeyASIO.Net/issues/11.
Chord support and general controls: https://github.com/deyvien/KeyASIO.Net/issues/13.
Optional combo-break reset: https://github.com/deyvien/KeyASIO.Net/issues/15.
Optional mapper-volume override: https://github.com/deyvien/KeyASIO.Net/issues/16.
