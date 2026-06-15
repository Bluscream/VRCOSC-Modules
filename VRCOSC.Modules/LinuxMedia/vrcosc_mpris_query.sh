#!/usr/bin/env bash

# Helper to find the active player
get_player() {
    dbus-send --session --dest=org.freedesktop.DBus --type=method_call --print-reply /org/freedesktop/DBus org.freedesktop.DBus.ListNames | grep -o 'org.mpris.MediaPlayer2.[a-zA-Z0-9._-]*' | head -n 1
}

echo "$(date): Executed with args: $@" >> ~/.vrcosc_mpris_debug.log

if [ "$1" = "control" ]; then
    player=$(get_player)
    if [ -z "$player" ]; then
        exit 0
    fi
    case "$2" in
        play)
            dbus-send --session --dest="$player" /org/mpris/MediaPlayer2 org.mpris.MediaPlayer2.Player.Play
            ;;
        pause)
            dbus-send --session --dest="$player" /org/mpris/MediaPlayer2 org.mpris.MediaPlayer2.Player.Pause
            ;;
        next)
            dbus-send --session --dest="$player" /org/mpris/MediaPlayer2 org.mpris.MediaPlayer2.Player.Next
            ;;
        previous)
            dbus-send --session --dest="$player" /org/mpris/MediaPlayer2 org.mpris.MediaPlayer2.Player.Previous
            ;;
        position)
            # $3 is targetPosition in microseconds
            dbus-send --session --dest="$player" /org/mpris/MediaPlayer2 org.mpris.MediaPlayer2.Player.SetPosition objectpath:"/org/mpris/MediaPlayer2" x11:"$3"
            ;;
        volume)
            # $3 is targetVolume double (0.0 to 1.0)
            dbus-send --session --dest="$player" /org/mpris/MediaPlayer2 org.freedesktop.DBus.Properties.Set string:org.mpris.MediaPlayer2.Player string:Volume variant:double:"$3"
            ;;
    esac
    exit 0
fi

# Default behavior: query status
player=$(get_player)
if [ ! -z "$player" ]; then
    status=$(dbus-send --session --print-reply --dest="$player" /org/mpris/MediaPlayer2 org.freedesktop.DBus.Properties.Get string:org.mpris.MediaPlayer2.Player string:PlaybackStatus | grep -o 'string "[^"]*"' | cut -d'"' -f2)
    metadata=$(dbus-send --session --print-reply --dest="$player" /org/mpris/MediaPlayer2 org.freedesktop.DBus.Properties.Get string:org.mpris.MediaPlayer2.Player string:Metadata)
    title=$(echo "$metadata" | grep -A 1 'string "xesam:title"' | tail -n 1 | grep -o 'variant *string "[^"]*"' | cut -d'"' -f2)
    artist=$(echo "$metadata" | grep -A 2 'string "xesam:artist"' | grep -v 'xesam:artist' | grep -o 'string "[^"]*"' | cut -d'"' -f2)
    len=$(echo "$metadata" | grep -A 1 'string "mpris:length"' | tail -n 1 | grep -o 'int64 [0-9]*' | awk '{print $2}')
    pos=$(dbus-send --session --print-reply --dest="$player" /org/mpris/MediaPlayer2 org.freedesktop.DBus.Properties.Get string:org.mpris.MediaPlayer2.Player string:Position | grep -o 'int64 [0-9]*' | awk '{print $2}')
    vol=$(dbus-send --session --print-reply --dest="$player" /org/mpris/MediaPlayer2 org.freedesktop.DBus.Properties.Get string:org.mpris.MediaPlayer2.Player string:Volume | grep -o 'double [0-9.]*' | awk '{print $2}')
    echo -e "$status\n$title\n$artist\n$len\n$pos\n$player\n$vol" > ~/.vrcosc_mpris.txt
else
    echo "Stopped" > ~/.vrcosc_mpris.txt
fi
