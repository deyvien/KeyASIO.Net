# Final-key chord feedback

In **Sync Mode → Mania Hitsounds**, enable **Wait until final chord key**.
The setting is saved automatically and takes effect without restarting the audio device.
It defaults to off.

For live mania play, notes whose heads share the same beatmap timestamp form a
chord. Its samples play together on the final distinct required lane press.
Single notes play immediately. A long note contributes its head, not its release.
Repeated presses in one lane cannot complete a chord. Incomplete or expired
chords remain silent, including chords containing a lane without an audible sample.

The matching window follows Practice Timeline's classic mania final scoreable
window, including OD and DT/HT/EZ/HR adjustments. Matching uses chart lanes and
nearest head time; it does not read the game's individual note judgements.
Randomized lane layouts and lazer-specific judgement-window mods are not
validated by this implementation.

Retries, map transitions, setting changes and a stopped game clock discard
pending sounds. The existing memory clock can take up to 100 ms to detect a
pause. Brief interpolation corrections do not cancel a chord.

All released samples are submitted as one mixer input, preserving their sample
volume and balance. The setting applies to both ASIO and WASAPI; it does not
change driver latency, offsets, music playback, or replay/autoplay scheduling.

Tracked by https://github.com/deyvien/KeyASIO.Net/issues/2.
