# Racing prototype audio

All WAV files in this folder are original procedural synthesis from `Tools/generate_kart_audio.py`.
No external recordings, model service, API key, or purchased sample pack was used.
Format: mono 44,100 Hz, 16-bit PCM. Four loops are periodic; one-shots have short edge fades.
The default game master SFX volume is 65%; M mutes/unmutes, and the pause menu has a volume slider.

## Connected to current gameplay

- `engine_idle_loop`, `engine_drive_loop`: blended throttle / speed engine layers, variable pitch.
- `tyre_skid_loop`: braking and lateral slip, grounded only.
- `wind_loop`: speed-dependent wind.
- `collision_01`, `collision_02`, `collision_03`: alternating impact sounds, strength from normal collision velocity.
- `landing`: landing thump.
- `reset`: vehicle reset confirmation.
- `ui_pause`, `ui_resume`: pause/resume; UI cues can play while the racing audio is paused.
- `gas_ready`: a full drift energy charge, one cue per stored charge.
- `nitro_burst`, `mini_boost`, `air_boost`: Q nitro and W contextual boosts; landing boost uses the mini-boost cue over the landing thump.

## Later gameplay hooks

- `countdown_tick`, `race_start`: race start flow.

The bank already holds these clips. Reserved clips do not imply the associated gameplay is implemented.

Preview: `NeonRush/Captures/racing-sfx-preview.wav`.
QC metrics: `NeonRush/Captures/audio-quality-report.json`.
