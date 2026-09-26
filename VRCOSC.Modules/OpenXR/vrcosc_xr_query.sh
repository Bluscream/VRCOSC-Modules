#!/usr/bin/env bash
# Queries the running Monado-based OpenXR runtime (WiVRn or Monado) through libmonado for
# device battery state and application focus, and writes the result as JSON to
# $HOME/.vrcosc_openxr.json. Deployed to ~/.local/bin and invoked by the OpenXR Stats module.
#
# OpenXR itself has no battery API, and the runtime only tells a session about its OWN
# focus, so both come from libmonado's out-of-band admin interface instead. libmonado is a
# host-side shared library that cannot be loaded from inside Wine, hence this script.
set -uo pipefail

OUT="$HOME/.vrcosc_openxr.json"
TMP="$OUT.tmp.$$"
LOCK="$HOME/.vrcosc_openxr.lock"

# mnd_root_create blocks indefinitely while the runtime is up but no headset is connected
# (WiVRn accepts the IPC connection and then waits for a session). Bound every attempt, and
# never let a second probe start while a blocked one is still waiting.
PROBE_TIMEOUT="${PROBE_TIMEOUT:-8}"
exec 9>"$LOCK"
if ! flock -n 9; then
    exit 0
fi

PY=$(cat <<'PYEOF'
import ctypes, json, os, sys, time

candidates = [a for a in sys.argv[1:] if a] + [
    "/app/lib/wivrn/libmonado_wivrn.so",
    "/usr/lib64/libmonado_wivrn.so", "/usr/lib/libmonado_wivrn.so",
    "/usr/lib/x86_64-linux-gnu/libmonado_wivrn.so",
    "/usr/lib64/libmonado.so", "/usr/lib/libmonado.so",
    "/usr/lib/x86_64-linux-gnu/libmonado.so",
]

def fail(msg):
    print(json.dumps({"ok": False, "error": msg, "ts": time.time()}))
    sys.exit(0)

lib = None
load_error = ""
for path in candidates:
    if not os.path.exists(path):
        continue
    try:
        lib = ctypes.CDLL(path)
        break
    except OSError as e:
        load_error = str(e)
if lib is None:
    fail("libmonado not found (" + load_error + ")" if load_error else "libmonado not found")

root = ctypes.c_void_p()
if lib.mnd_root_create(ctypes.byref(root)) != 0:
    fail("mnd_root_create failed: is the runtime (WiVRn/Monado) running with a headset connected?")

out = {"ok": True, "ts": time.time(), "devices": {}, "clients": []}

def device_for_role(role):
    idx = ctypes.c_int32(-1)
    if lib.mnd_root_get_device_from_role(root, role.encode(), ctypes.byref(idx)) != 0 or idx.value < 0:
        return None
    dev = {"index": idx.value}
    dev_id = ctypes.c_uint32()
    name = ctypes.c_char_p()
    if lib.mnd_root_get_device_info(root, ctypes.c_uint32(idx.value), ctypes.byref(dev_id), ctypes.byref(name)) == 0 and name.value:
        dev["name"] = name.value.decode(errors="replace")
    present = ctypes.c_bool()
    charging = ctypes.c_bool()
    charge = ctypes.c_float()
    r = lib.mnd_root_get_device_battery_status(root, ctypes.c_uint32(idx.value), ctypes.byref(present), ctypes.byref(charging), ctypes.byref(charge))
    if r == 0:
        dev.update(present=bool(present.value), charging=bool(charging.value), charge=max(0.0, min(1.0, float(charge.value))))
    else:
        dev["battery_error"] = int(r)
    return dev

for role in ("head", "left", "right", "gamepad"):
    dev = device_for_role(role)
    if dev is not None:
        out["devices"][role] = dev

if lib.mnd_root_update_client_list(root) == 0:
    count = ctypes.c_uint32()
    if lib.mnd_root_get_number_clients(root, ctypes.byref(count)) == 0:
        for i in range(count.value):
            cid = ctypes.c_uint32()
            if lib.mnd_root_get_client_id_at_index(root, ctypes.c_uint32(i), ctypes.byref(cid)) != 0:
                continue
            name = ctypes.c_char_p()
            flags = ctypes.c_uint32()
            lib.mnd_root_get_client_name(root, cid, ctypes.byref(name))
            lib.mnd_root_get_client_state(root, cid, ctypes.byref(flags))
            out["clients"].append({"id": cid.value, "name": (name.value or b"").decode(errors="replace"), "flags": flags.value})

lib.mnd_root_destroy(ctypes.byref(root))
print(json.dumps(out))
PYEOF
)

run_in_wivrn_flatpak() {
    printf '%s' "$PY" | timeout -s KILL "$PROBE_TIMEOUT" flatpak run --command=python3 io.github.wivrn.wivrn - /app/lib/wivrn/libmonado_wivrn.so
}

run_on_host() {
    command -v python3 >/dev/null 2>&1 || return 1
    printf '%s' "$PY" | timeout -s KILL "$PROBE_TIMEOUT" python3 -
}

# The WiVRn flatpak carries its own libmonado and python; when it is installed it is the
# runtime we are talking to, so it is authoritative and the host path is only for native
# Monado/WiVRn installs.
if command -v flatpak >/dev/null 2>&1 && flatpak info io.github.wivrn.wivrn >/dev/null 2>&1; then
    run_in_wivrn_flatpak > "$TMP" 2>/dev/null
else
    run_on_host > "$TMP" 2>/dev/null
fi

if [ -s "$TMP" ]; then
    mv -f "$TMP" "$OUT"
else
    rm -f "$TMP"
    printf '{"ok": false, "error": "probe timed out after %ss (headset not connected?) or produced no output", "ts": %s}\n' "$PROBE_TIMEOUT" "$(date +%s)" > "$OUT"
fi
