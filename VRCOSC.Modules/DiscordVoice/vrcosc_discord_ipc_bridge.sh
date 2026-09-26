#!/usr/bin/env bash
# Bridges Discord's native IPC socket to a localhost TCP port so VRCOSC, running under Wine,
# can speak Discord RPC. Wine's named pipes (\\.\pipe\discord-ipc-N) are its own and never
# reach the Unix socket the Linux Discord client listens on, so the module connects to
# 127.0.0.1:<port> instead and this script forwards the byte stream with socat.
#
# Usage: vrcosc_discord_ipc_bridge.sh <port>
# Idempotent: exits 0 immediately when something already listens on the port.
# Deployed to ~/.local/bin by the DiscordVoice module (LinuxHardwareStats pattern).
set -euo pipefail

PORT="${1:-6890}"

if ! command -v socat >/dev/null 2>&1; then
    echo "socat not installed" >&2
    exit 2
fi

if ss -ltn 2>/dev/null | awk '{print $4}' | grep -q ":${PORT}\$"; then
    exit 0
fi

runtime="${XDG_RUNTIME_DIR:-/run/user/$(id -u)}"
candidates=()
for base in "$runtime" "$runtime/app/com.discordapp.Discord" "$runtime/snap.discord" "$runtime/snap.discord-canary" "/tmp" "${TMPDIR:-/tmp}"; do
    for i in 0 1 2 3 4 5 6 7 8 9; do
        candidates+=("$base/discord-ipc-$i")
    done
done

sock=""
for c in "${candidates[@]}"; do
    if [ -S "$c" ]; then
        sock="$c"
        break
    fi
done

if [ -z "$sock" ]; then
    echo "no discord-ipc-* socket found (is the official Discord client running?)" >&2
    exit 3
fi

nohup socat "TCP-LISTEN:${PORT},bind=127.0.0.1,reuseaddr,fork" "UNIX-CONNECT:${sock}" >/dev/null 2>&1 &
disown
echo "bridging ${sock} to 127.0.0.1:${PORT}"
