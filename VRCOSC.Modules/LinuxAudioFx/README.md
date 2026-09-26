# Linux Audio FX

Soundboard (Soundux, Kenku, ...) and voice changer (EasyEffects, PipeWire filters) state on Linux hosts — MagicChatbox soundpad_* / voicemod_* counterparts

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

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
