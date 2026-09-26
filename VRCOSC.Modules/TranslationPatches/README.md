# Speech Translation

Your recognised speech as spoken plus its translation into a target language, and optionally other players' speech from a loopback device translated into yours (MagicChatbox / DreamChatbox speech placeholder parity)

**Repository**: https://github.com/Bluscream/VRCOSC-Modules

## How it works

VRCOSC hands every partial and final speech result to each running module that implements
`ISpeechHandler`, the same path the official Speech To Text module uses, so the raw
transcript needs no patching. `speech_text` is that transcript; `translation` is the same
text translated into the target language through Google Translate's public endpoint (cached
per text and language, one request in flight, newest text wins).

VRCOSC's own "translate" speech switch is not a post-processing step that could be
intercepted: it builds Whisper in translate-to-English mode, so with it on the source
language text never exists. With it on, `speech_text` is already English and the module logs
that once. Leave it off for real side-by-side output.

## Two-way (other players)

With "Two-way translation" enabled the module runs a second instance of the app's Whisper
audio processor on a capture device of your choice (a PipeWire/Pulse "Monitor of ..." source
of your headphones, or a virtual cable) and translates what other players say into your
language: `twoway_input` (as spoken) and `twoway_output` (translated). The app's model, GPU,
noise and confidence settings are shared. Available capture device names are logged when the
module starts with two-way on and no device matches.

## Placeholder mapping

| Neutral placeholder | Variable |
|---|---|
| `{speech_text}` | `speech_text` |
| `{translation}` | `translation` |
| `{twoway_input}` (Dream) | `twoway_input` |
| `{twoway_output}` / `{2wayout}` (Dream) | `twoway_output` |

## Module Settings

<!-- SETTINGS_TABLE_START -->
| Setting Name | Type | Description | Default |
|---|---|---|---|
| **Target language** | `TextBox` | `ISO 639-1 code your speech is translated into for the translation variable (en, de, ja, ...).` | `DefaultLanguage` |
| **Speaking window (seconds)** | `Slider` | `How long after the last recognised text the Speaking state stays active.` | `10, 1, 60, 1` |
| **Two-way translation** | `Toggle` | `Transcribe a second capture device (a loopback/monitor of your headphones, or a virtual cable) with another Whisper instance and translate what other players say into your language.` | `false` |
| **Two-way capture device** | `TextBox` | `Configure Two-way capture device` | `"Part of the capture device name to transcribe for two-way translation, e.g. \"Monitor\". The available names are logged when the module starts with two-way enabled.", "Monitor"` |
| **Two-way output language** | `TextBox` | `ISO 639-1 code other players' speech is translated into.` | `DefaultLanguage` |
<!-- SETTINGS_TABLE_END -->

## ChatBox Variables

<!-- VARIABLES_TABLE_START -->
| Variable Name | Lookup Key | Type | Description |
|---|---|---|---|
| **Speech Text (as spoken)** | `speech_text` | `string` | `ChatBox variable Speech Text (as spoken)` |
| **Translation** | `translation` | `string` | `ChatBox variable Translation` |
| **Translation Language** | `translation_language` | `string` | `ChatBox variable Translation Language` |
| **Two-way Input (others, as spoken)** | `twoway_input` | `string` | `ChatBox variable Two-way Input (others, as spoken)` |
| **Two-way Output (others, translated)** | `twoway_output` | `string` | `ChatBox variable Two-way Output (others, translated)` |
<!-- VARIABLES_TABLE_END -->

## ChatBox States

<!-- STATES_TABLE_START -->
| State Name | Lookup Key | Format | Description |
|---|---|---|---|
| **Speaking** | `speaking` | `{0}\n{1}` | `Speaking state` |
<!-- STATES_TABLE_END -->

## ChatBox Events

<!-- EVENTS_TABLE_START -->
| Event Name | Lookup Key | Title | Trigger Condition |
|---|---|---|---|
| **Spoken** | `spoken` | `{0}` | `Triggered on Spoken` |
| **Translated** | `translated` | `{0}` | `Triggered on Translated` |
| **Two-way Translated** | `twoway` | `{0}` | `Triggered on Two-way Translated` |
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
