#!/usr/bin/env bash
# vrcosc_audiofx.sh — host-side probe for the VRCOSC "Linux Audio FX" module.
#
# Writes ~/.vrcosc_audiofx.json describing
#   * the currently playing PipeWire output stream(s) that belong to a soundboard
#     application (MagicChatbox "soundpad_sound" counterpart), and
#   * the state of the voice-changer chain: EasyEffects (input preset) and any
#     PipeWire filter-chain / noise-cancellation source nodes (Voicemod counterpart).
#
# Data sources, in order of preference:
#   pw-dump + jq                      full PipeWire graph
#   pactl list sink-inputs (text)     PulseAudio compatibility layer, no jq needed
# EasyEffects state comes from gsettings (native install) or the Flatpak keyfile.
# Every tool is optional; missing ones are reported in the "tools" object so the
# module can log the gap once and keep its variables empty.
#
# Settings baked in at deploy time by LinuxAudioFxModule:
SOUNDBOARD_APPS="Soundux|Kenku|Sound Board|Soundboard|Ducky|Zedd"
ROLE_HINT="Sound ?Board"
OUT_FILE="$HOME/.vrcosc_audiofx.json"

has() { command -v "$1" >/dev/null 2>&1; }

# Escape a value for embedding in a JSON string literal.
json_str() {
    printf '%s' "$1" | tr -d '\000-\010\013\014\016-\037' | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g' -e 's/\t/\\t/g' | tr -d '\n'
}

# ---------------------------------------------------------------------------
# Soundboard streams  →  lines of "<app>\t<name>\t<role>"
# ---------------------------------------------------------------------------
streams=""
stream_source="none"
if has pw-dump && has jq; then
    stream_source="pw-dump"
    streams=$(pw-dump 2>/dev/null | jq -r '
        .[]
        | select(.type == "PipeWire:Interface:Node")
        | select(.info.props["media.class"] == "Stream/Output/Audio")
        | select(.info.state == "running")
        | [ (.info.props["application.name"] // .info.props["node.name"] // ""),
            (.info.props["media.title"] // .info.props["media.name"] // ""),
            (.info.props["media.role"] // "") ]
        | @tsv' 2>/dev/null)
elif has pactl; then
    stream_source="pactl"
    streams=$(LC_ALL=C pactl list sink-inputs 2>/dev/null | awk '
        function flush() {
            if (seen && corked == "no") printf "%s\t%s\t%s\n", app, name, role
            app = ""; name = ""; role = ""; corked = ""; seen = 0
        }
        /^Sink Input #/ { flush(); seen = 1; next }
        /^[[:space:]]*Corked:/ { corked = $2; next }
        /application\.name = / { sub(/.*application\.name = "/, ""); sub(/"$/, ""); app = $0; next }
        /media\.title = /      { sub(/.*media\.title = "/, "");      sub(/"$/, ""); name = $0; next }
        /media\.name = /       { if (name == "") { sub(/.*media\.name = "/, ""); sub(/"$/, ""); name = $0 }; next }
        /media\.role = /       { sub(/.*media\.role = "/, "");       sub(/"$/, ""); role = $0; next }
        END { flush() }')
fi

sb_app=""
sb_sound=""
streams_json=""
while IFS=$'\t' read -r app name role; do
    [ -z "$app" ] && [ -z "$name" ] && continue
    match=0
    if printf '%s' "$app" | grep -Eiq "^(${SOUNDBOARD_APPS})"; then match=1; fi
    if [ -n "$ROLE_HINT" ] && printf '%s' "$role" | grep -Eiq "${ROLE_HINT}"; then match=1; fi
    streams_json="${streams_json:+$streams_json,}{\"app\":\"$(json_str "$app")\",\"name\":\"$(json_str "$name")\",\"role\":\"$(json_str "$role")\",\"soundboard\":$([ $match -eq 1 ] && echo true || echo false)}"
    if [ $match -eq 1 ] && [ -z "$sb_app" ]; then
        sb_app="$app"
        sb_sound="$name"
        # Kenku/Soundux name streams after the clip; fall back to the app when the
        # stream carries only a generic "Playback" label.
        case "$sb_sound" in ""|Playback|playStream|"ALSA Playback"|"audio stream"*) sb_sound="$app" ;; esac
    fi
done <<< "$streams"

# ---------------------------------------------------------------------------
# Voice chain: EasyEffects + PipeWire filter nodes
# ---------------------------------------------------------------------------
ee_running=false
ee_preset=""
ee_source="none"
if has busctl && busctl --user list 2>/dev/null | grep -q '^com\.github\.wwmm\.easyeffects '; then
    ee_running=true
elif has pgrep && pgrep -x easyeffects >/dev/null 2>&1; then
    ee_running=true
fi
if has gsettings && gsettings list-schemas 2>/dev/null | grep -qx 'com.github.wwmm.easyeffects'; then
    ee_source="gsettings"
    ee_preset=$(gsettings get com.github.wwmm.easyeffects last-loaded-input-preset 2>/dev/null | sed -e "s/^'//" -e "s/'$//")
fi
if [ -z "$ee_preset" ]; then
    keyfile="$HOME/.var/app/com.github.wwmm.easyeffects/config/glib-2.0/settings/keyfile"
    if [ -f "$keyfile" ]; then
        ee_source="flatpak-keyfile"
        ee_preset=$(awk -F= '/^\[com\/github\/wwmm\/easyeffects\]/{s=1;next} /^\[/{s=0} s && $1=="last-loaded-input-preset"{print $2}' "$keyfile" | sed -e "s/^'//" -e "s/'$//")
    fi
fi

filters=""
if has pw-dump && has jq; then
    filters=$(pw-dump 2>/dev/null | jq -r '
        .[]
        | select(.type == "PipeWire:Interface:Node")
        | select((.info.props["media.class"] // "") | test("^Audio/(Source|Sink)"))
        | select(((.info.props["factory.name"] // "") | test("filter-chain|echo-cancel"))
                 or ((.info.props["node.name"] // "") | test("rnnoise|noise|filter|echo|voice|deep"; "i"))
                 or ((.info.props["node.description"] // "") | test("rnnoise|noise|filter|echo|voice"; "i")))
        | (.info.props["node.description"] // .info.props["node.name"])' 2>/dev/null | sort -u)
fi
filters_json=""
while IFS= read -r f; do
    [ -z "$f" ] && continue
    filters_json="${filters_json:+$filters_json,}\"$(json_str "$f")\""
done <<< "$filters"

tool() { has "$1" && echo true || echo false; }

cat > "$OUT_FILE.tmp" <<JSON
{"soundboard":{"playing":$([ -n "$sb_app" ] && echo true || echo false),"app":"$(json_str "$sb_app")","sound":"$(json_str "$sb_sound")","source":"$stream_source"},
 "streams":[$streams_json],
 "voice":{"easyeffects_running":$ee_running,"input_preset":"$(json_str "$ee_preset")","preset_source":"$ee_source","filters":[$filters_json]},
 "tools":{"pw-dump":$(tool pw-dump),"pactl":$(tool pactl),"jq":$(tool jq),"gsettings":$(tool gsettings),"busctl":$(tool busctl)}}
JSON
mv -f "$OUT_FILE.tmp" "$OUT_FILE"
