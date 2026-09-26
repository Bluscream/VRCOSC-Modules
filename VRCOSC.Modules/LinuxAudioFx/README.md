# Linux Audio FX Module

Linux counterpart of MagicChatbox's **Soundpad** and **Voicemod** integrations. Neither
Windows app exists on Linux, so the module maps their ChatBox placeholders onto what a
PipeWire desktop actually has: soundboard apps playing through PipeWire and a voice
chain built from EasyEffects presets or PipeWire filter-chain nodes.

Module id: `linuxaudiofxmodule` — variables resolve as `{bluscream.linuxaudiofxmodule_<key>}`.

## MagicChatbox mapping

| MagicChatbox placeholder | Variable key | Linux source | Value |
|---|---|---|---|
| `{soundpad_sound}` | `soundpad_sound` | PipeWire `Stream/Output/Audio` node in state `running` whose `application.name` matches the *Soundboard apps* list (or whose `media.role` looks like "Sound Board") | `media.title` / `media.name` of that stream — the clip name for Soundux and Kenku; empty when idle |
| — | `soundpad_app` | same stream | the `application.name` of the soundboard app; empty when idle |
| `{voicemod_voice}` | `voicemod_voice` | EasyEffects `last-loaded-input-preset` (gsettings for native installs, `~/.var/app/com.github.wwmm.easyeffects/.../keyfile` for the Flatpak) while EasyEffects is running; otherwise the first PipeWire filter-chain / echo-cancel / RNNoise source node | preset or filter name, `None` when nothing is active |
| `{voicemod_sound}` | `voicemod_sound` | last soundboard sound | the last `soundpad_sound`, kept for *Hold last sound* seconds after playback stops (MagicChatbox keeps the Voicemod soundboard sound for a few seconds the same way) |

Streams whose name is a generic label (`Playback`, `ALSA Playback`, `audio stream #n`) report the
app name as the sound so the ChatBox never shows an empty clip while something is playing.

## States and events

| Kind | Lookup | Default format |
|---|---|---|
| State | `playing` | `🔊 {soundpad_sound} ({soundpad_app})` / `🎙 {voicemod_voice}` |
| State | `idle` | `🎙 {voicemod_voice}` |
| Event | `soundstarted` | `🔊 {soundpad_sound}` — fired when a new soundboard sound starts |

## Settings

| Setting | Type | Default | Notes |
|---|---|---|---|
| Poll interval (ms) | slider 250–10000 | 1000 | how often `vrcosc_audiofx.sh` runs |
| Soundboard apps | text | `Soundux\|Kenku\|Sound Board\|Soundboard\|Ducky\|Zedd` | pipe-separated, case-insensitive prefixes matched against `application.name`; baked into the script, so restart the module after changing it |
| Match media.role hint | toggle | on | also match any stream with `media.role` ≈ "Sound Board"; restart the module after changing |
| Hold last sound (s) | slider 1–60 | 5 | how long `voicemod_sound` survives after the sound stops |

## How it works

VRCOSC runs under Wine, so the module deploys the embedded `vrcosc_audiofx.sh` to
`~/.local/bin/` on the host (via `Z:\home\<user>\...`) and runs it through the Wine bash
bridge (`LinuxUtils.RunHostScript`), exactly like the Linux Hardware Stats module. The
script writes `~/.vrcosc_audiofx.json`:

```json
{"soundboard":{"playing":true,"app":"Soundux","sound":"Airhorn.mp3","source":"pw-dump"},
 "streams":[{"app":"Soundux","name":"Airhorn.mp3","role":"Music","soundboard":true}],
 "voice":{"easyeffects_running":false,"input_preset":"","preset_source":"none","filters":[]},
 "tools":{"pw-dump":true,"pactl":true,"jq":true,"gsettings":true,"busctl":true}}
```

Data sources in order of preference: `pw-dump` + `jq`, then `pactl list sink-inputs`
(text, no `jq` needed). EasyEffects is detected on the session bus (`busctl --user list`)
or by process name. Any missing tool is listed in `tools`; the module logs the gap once
and leaves the affected variables empty rather than failing.

To test on the host without a real soundboard:

```bash
pw-play --properties '{ application.name = "Soundux", media.name = "Airhorn.mp3" }' /usr/share/sounds/alsa/Front_Center.wav &
~/.local/bin/vrcosc_audiofx.sh && jq .soundboard ~/.vrcosc_audiofx.json
```

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Poll interval (ms)** | `Slider` | `How often the host is asked for the PipeWire / EasyEffects state.` | `1000, 250, 10000, 250` |
| **Soundboard apps** | `TextBox` | `Pipe-separated, case-insensitive list of application names (regex prefixes) whose output streams count as soundboard playback. Requires a module restart.` | `"Soundux|Kenku|Sound Board|Soundboard|Ducky|Zedd"` |
| **Match media.role hint** | `Toggle` | `Configure Match media.role hint` | `"Also treat any stream whose media.role looks like \"Sound Board\" as soundboard playback. Requires a module restart.", true` |
| **Hold last sound (s)** | `Slider` | `How long voicemod_sound keeps the last soundboard sound name after it stopped playing.` | `5, 1, 60` |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Soundboard sound (soundpad_sound)** | `soundpad_sound` | `string` | `ChatBox variable Soundboard sound (soundpad_sound)` |
| **Soundboard app (soundpad_app)** | `soundpad_app` | `string` | `ChatBox variable Soundboard app (soundpad_app)` |
| **Voice preset (voicemod_voice)** | `voicemod_voice` | `string` | `ChatBox variable Voice preset (voicemod_voice)` |
| **Last sound, held (voicemod_sound)** | `voicemod_sound` | `string` | `ChatBox variable Last sound, held (voicemod_sound)` |
<!-- VARIABLES_TABLE_END -->

## ChatBox States

<!-- STATES_TABLE_START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Playing** | `playing` | `🔊 {0} ({1})\n🎙 {2}` | `Playing state` |
| **Idle** | `idle` | `🎙 {0}` | `Idle state` |
<!-- STATES_TABLE_END -->

## ChatBox Events

<!-- EVENTS_TABLE_START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **Sound started** | `soundstarted` | `🔊 {0}` | `Triggered on Sound started` |
<!-- EVENTS_TABLE_END -->

## Avatar OSC Parameters

<!-- OSC_PARAMETERS_TABLE_START -->
| OSC Parameter Path | Type | Direction | Description |
|---|---|---|---|
| _None_ | — | — | — |
<!-- OSC_PARAMETERS_TABLE_END -->

## Nodes Overview

<!-- NODES_TABLE_START -->
| Node Name | Inputs | Outputs | Description |
|---|---|---|---|
| _None_ | — | — | — |
<!-- NODES_TABLE_END -->
