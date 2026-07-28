#!/usr/bin/env bash

# ---------------------------------------------------------------------------
# Settings baked in at deploy time by VRCOSC (LinuxHardwareStatsModule)
# GPU_INDEX  : which GPU to use (0-based)
# CPU_INDEX  : which CPU package to use (0-based)
# NET_IFACE  : specific interface to monitor, or empty = combine all non-lo
# ---------------------------------------------------------------------------
GPU_INDEX=0
CPU_INDEX=0
NET_IFACE=""

# ---------------------------------------------------------------------------
# Helper: network byte totals (single iface or combined all non-loopback)
# ---------------------------------------------------------------------------
read_net_totals() {
    if [ -n "$NET_IFACE" ]; then
        awk -v iface="$NET_IFACE:" '
            $1 == iface { print $2+0 " " $10+0; exit }
        ' /proc/net/dev
    else
        awk '
            NR > 2 {
                gsub(/:/, "", $1)
                if ($1 != "lo") { rx += $2; tx += $10 }
            }
            END { print (rx+0) " " (tx+0) }
        ' /proc/net/dev
    fi
}

# CPU Name
cpu_name=$(grep -m1 "model name" /proc/cpuinfo | cut -d: -f2 | xargs)
if [ -z "$cpu_name" ]; then
    cpu_name=$(lscpu | grep "Model name" | cut -d: -f2 | xargs)
fi
[ -z "$cpu_name" ] && cpu_name="Generic CPU"

# CPU Temp — pick the CPU_INDEX-th coretemp/k10temp/zenpower device
cpu_temp=0
_cpu_pkg_idx=0
for name_file in /sys/class/hwmon/hwmon*/name; do
    name=$(cat "$name_file" 2>/dev/null)
    if [ "$name" = "coretemp" ] || [ "$name" = "k10temp" ] || [ "$name" = "zenpower" ]; then
        if [ "$_cpu_pkg_idx" -eq "$CPU_INDEX" ]; then
            dir=$(dirname "$name_file")
            if [ -f "$dir/temp1_input" ]; then
                raw_temp=$(cat "$dir/temp1_input")
                cpu_temp=$((raw_temp / 1000))
            fi
            break
        fi
        _cpu_pkg_idx=$((_cpu_pkg_idx + 1))
    fi
done
# Fallback to ACPI thermal zone if still 0
if [ "$cpu_temp" -eq 0 ] && [ -f /sys/class/thermal/thermal_zone0/temp ]; then
    raw_temp=$(cat /sys/class/thermal/thermal_zone0/temp)
    cpu_temp=$((raw_temp / 1000))
fi

# Measure CPU usage and network rates over a 200ms window
# Also sample Intel RAPL energy counter (Intel CPUs only, package = CPU_INDEX)
read -r _ u1 n1 s1 i1 io1 ir1 si1 st1 _ _ < /proc/stat
e1=0
if echo "$cpu_name" | grep -qi "intel"; then
    _rapl="/sys/class/powercap/intel-rapl:${CPU_INDEX}/energy_uj"
    [ -f "$_rapl" ] && e1=$(cat "$_rapl" 2>/dev/null || echo 0)
fi
t1=$(date +%s%N)

# Network sample 1
read net1_rx net1_tx <<< "$(read_net_totals)"

sleep 0.2

read -r _ u2 n2 s2 i2 io2 ir2 si2 st2 _ _ < /proc/stat
e2=0
if echo "$cpu_name" | grep -qi "intel"; then
    _rapl="/sys/class/powercap/intel-rapl:${CPU_INDEX}/energy_uj"
    [ -f "$_rapl" ] && e2=$(cat "$_rapl" 2>/dev/null || echo 0)
fi
t2=$(date +%s%N)

# Network sample 2
read net2_rx net2_tx <<< "$(read_net_totals)"

# Calculate CPU Usage
prev_idle=$((i1 + io1))
idle=$((i2 + io2))
prev_non_idle=$((u1 + n1 + s1 + ir1 + si1 + st1))
non_idle=$((u2 + n2 + s2 + ir2 + si2 + st2))
prev_total=$((prev_idle + prev_non_idle))
total=$((idle + non_idle))
total_diff=$((total - prev_total))
idle_diff=$((idle - prev_idle))
if [ "$total_diff" -gt 0 ]; then
    cpu_usage=$(( (total_diff - idle_diff) * 100 / total_diff ))
else
    cpu_usage=0
fi

# Calculate CPU Power (W) — vendor-specific, single result, respects CPU_INDEX
cpu_power=0
if echo "$cpu_name" | grep -qi "intel"; then
    # Intel: RAPL energy counter delta over the 200ms sample window
    if [ "$e1" -gt 0 ] && [ "$e2" -gt "$e1" ]; then
        energy_diff=$((e2 - e1))
        time_diff_ns=$((t2 - t1))
        [ "$time_diff_ns" -gt 0 ] && cpu_power=$(( energy_diff * 1000 / time_diff_ns ))
    fi
elif echo "$cpu_name" | grep -qi "amd"; then
    # AMD: k10temp / zenpower power1_average (rolling average, no sampling needed)
    # Pick the CPU_INDEX-th matching hwmon
    _amd_cpu_idx=0
    for name_file in /sys/class/hwmon/hwmon*/name; do
        name=$(cat "$name_file" 2>/dev/null)
        if [ "$name" = "k10temp" ] || [ "$name" = "zenpower" ]; then
            if [ "$_amd_cpu_idx" -eq "$CPU_INDEX" ]; then
                dir=$(dirname "$name_file")
                if [ -f "$dir/power1_average" ]; then
                    raw_pow=$(cat "$dir/power1_average" 2>/dev/null || echo 0)
                    cpu_power=$((raw_pow / 1000000))
                fi
                break
            fi
            _amd_cpu_idx=$((_amd_cpu_idx + 1))
        fi
    done
fi

# Calculate network speeds
time_diff_ms=$(( (t2 - t1) / 1000000 ))
[ "$time_diff_ms" -le 0 ] && time_diff_ms=200

net_rx_kbps=0
net_tx_kbps=0
net_rx_total_mb=0
net_tx_total_mb=0

if [ -n "$net1_rx" ] && [ -n "$net2_rx" ] && [ "$net2_rx" -ge "$net1_rx" ] && [ "$net2_tx" -ge "$net1_tx" ]; then
    net_rx_kbps=$(( (net2_rx - net1_rx) * 1000 / time_diff_ms / 1024 ))
    net_tx_kbps=$(( (net2_tx - net1_tx) * 1000 / time_diff_ms / 1024 ))
    net_rx_total_mb=$(awk "BEGIN {printf \"%.2f\", $net2_rx / 1024 / 1024}")
    net_tx_total_mb=$(awk "BEGIN {printf \"%.2f\", $net2_tx / 1024 / 1024}")
fi

# GPU Name, Usage, Power, Temp, VRAM
gpu_name="Unknown GPU"
gpu_usage=0
gpu_power=0
gpu_temp=0
vram_total=0
vram_used=0
vram_free=0
vram_usage=0

if command -v nvidia-smi &>/dev/null; then
    # NVIDIA: use -i GPU_INDEX to select the card
    nv_data=$(nvidia-smi -i "$GPU_INDEX" \
        --query-gpu=name,utilization.gpu,power.draw,temperature.gpu,memory.total,memory.used,memory.free \
        --format=csv,noheader,nounits 2>/dev/null)
    if [ -n "$nv_data" ]; then
        IFS=',' read -r nv_name nv_util nv_power nv_temp nv_mem_total nv_mem_used nv_mem_free <<< "$nv_data"
        gpu_name=$(echo "$nv_name" | xargs)
        gpu_usage=$(echo "$nv_util" | xargs)
        gpu_power=$(echo "$nv_power" | cut -d. -f1 | xargs)
        gpu_temp=$(echo "$nv_temp" | xargs)
        tot_mib=$(echo "$nv_mem_total" | xargs)
        vram_total=$(awk "BEGIN {printf \"%.2f\", $tot_mib / 1024}")
        usd_mib=$(echo "$nv_mem_used" | xargs)
        vram_used=$(awk "BEGIN {printf \"%.2f\", $usd_mib / 1024}")
        fre_mib=$(echo "$nv_mem_free" | xargs)
        vram_free=$(awk "BEGIN {printf \"%.2f\", $fre_mib / 1024}")
        vram_usage=$(awk "BEGIN {printf \"%.4f\", ($usd_mib / $tot_mib)}")
    fi
else
    # AMD GPU detection — pick the GPU_INDEX-th amdgpu hwmon and DRM card
    amd_hwmon=""
    _amd_hwmon_idx=0
    for name_file in /sys/class/hwmon/hwmon*/name; do
        if [ "$(cat "$name_file" 2>/dev/null)" = "amdgpu" ]; then
            if [ "$_amd_hwmon_idx" -eq "$GPU_INDEX" ]; then
                amd_hwmon=$(dirname "$name_file")
                break
            fi
            _amd_hwmon_idx=$((_amd_hwmon_idx + 1))
        fi
    done

    amd_card=""
    _amd_card_idx=0
    for card_dir in /sys/class/drm/card*; do
        if [ -d "$card_dir/device" ] && [ -f "$card_dir/device/gpu_busy_percent" ]; then
            if [ "$_amd_card_idx" -eq "$GPU_INDEX" ]; then
                amd_card="$card_dir"
                break
            fi
            _amd_card_idx=$((_amd_card_idx + 1))
        fi
    done

    if [ -n "$amd_hwmon" ] || [ -n "$amd_card" ]; then
        # GPU name: prefer lspci SDevice (board marketing name)
        # For multi-GPU, pick the GPU_INDEX-th VGA/3D/Display device
        gpu_name=""
        if command -v lspci &>/dev/null; then
            gpu_name=$(lspci -vmm 2>/dev/null | awk -v idx="$GPU_INDEX" '
                /^Class:.*VGA|^Class:.*3D|^Class:.*Display/ { count++; in_gpu=(count-1 == idx+0); next }
                in_gpu && /^SDevice:/ { sub(/^SDevice:[[:space:]]+/, ""); print; exit }
                /^$/ { in_gpu=0 }
            ')
        fi
        if [ -n "$gpu_name" ]; then
            echo "$gpu_name" | grep -qi "^AMD" || gpu_name="AMD $gpu_name"
        fi
        # Fallbacks
        if [ -z "$gpu_name" ] && [ -n "$amd_card" ] && [ -f "$amd_card/device/product_name" ]; then
            gpu_name=$(cat "$amd_card/device/product_name" 2>/dev/null | xargs)
        fi
        if [ -z "$gpu_name" ] && command -v lspci &>/dev/null; then
            gpu_name=$(lspci 2>/dev/null | grep -iE "VGA|3D|Display" | grep -i amd | grep -oP '\[([^\]]+)\]' | tail -1 | tr -d '[]')
        fi
        [ -z "$gpu_name" ] && gpu_name="AMD Radeon GPU"

        if [ -f "$amd_card/device/gpu_busy_percent" ]; then
            gpu_usage=$(cat "$amd_card/device/gpu_busy_percent")
        fi

        # Power: prefer power1_input (instantaneous), fall back to power1_average
        if [ -f "$amd_hwmon/power1_input" ]; then
            raw_pow=$(cat "$amd_hwmon/power1_input" 2>/dev/null || echo 0)
            gpu_power=$((raw_pow / 1000000))
        elif [ -f "$amd_hwmon/power1_average" ]; then
            raw_pow=$(cat "$amd_hwmon/power1_average" 2>/dev/null || echo 0)
            gpu_power=$((raw_pow / 1000000))
        fi

        # GPU temp: prefer junction (temp2) over edge (temp1)
        if [ -f "$amd_hwmon/temp2_input" ]; then
            raw_temp=$(cat "$amd_hwmon/temp2_input")
            gpu_temp=$((raw_temp / 1000))
        elif [ -f "$amd_hwmon/temp1_input" ]; then
            raw_temp=$(cat "$amd_hwmon/temp1_input")
            gpu_temp=$((raw_temp / 1000))
        fi

        if [ -f "$amd_card/device/mem_info_vram_total" ] && [ -f "$amd_card/device/mem_info_vram_used" ]; then
            raw_total=$(cat "$amd_card/device/mem_info_vram_total")
            raw_used=$(cat "$amd_card/device/mem_info_vram_used")
            vram_total=$(awk "BEGIN {printf \"%.2f\", $raw_total / 1024 / 1024 / 1024}")
            vram_used=$(awk "BEGIN {printf \"%.2f\", $raw_used / 1024 / 1024 / 1024}")
            vram_free=$(awk "BEGIN {printf \"%.2f\", ($raw_total - $raw_used) / 1024 / 1024 / 1024}")
            vram_usage=$(awk "BEGIN {printf \"%.4f\", ($raw_used / $raw_total)}")
        fi
    fi
fi

# RAM Info
mem_total_kb=$(grep "MemTotal:" /proc/meminfo | awk '{print $2}')
mem_avail_kb=$(grep "MemAvailable:" /proc/meminfo | awk '{print $2}')
mem_used_kb=$((mem_total_kb - mem_avail_kb))

ram_total=$(awk "BEGIN {printf \"%.2f\", $mem_total_kb / 1024 / 1024}")
ram_used=$(awk "BEGIN {printf \"%.2f\", $mem_used_kb / 1024 / 1024}")
ram_free=$(awk "BEGIN {printf \"%.2f\", $mem_avail_kb / 1024 / 1024}")
ram_usage=$(awk "BEGIN {printf \"%.4f\", ($mem_used_kb / $mem_total_kb)}")

# System temp — ACPI thermal zone (ambient / motherboard)
system_temp=0
for name_file in /sys/class/hwmon/hwmon*/name; do
    if [ "$(cat "$name_file" 2>/dev/null)" = "acpitz" ]; then
        dir=$(dirname "$name_file")
        if [ -f "$dir/temp1_input" ]; then
            raw_temp=$(cat "$dir/temp1_input")
            system_temp=$((raw_temp / 1000))
            break
        fi
    fi
done

# Max temp — highest reading across every hwmon temp sensor on the system
max_temp=0
for temp_file in /sys/class/hwmon/hwmon*/temp*_input; do
    [ -f "$temp_file" ] || continue
    raw=$(cat "$temp_file" 2>/dev/null || echo 0)
    temp_c=$((raw / 1000))
    [ "$temp_c" -gt "$max_temp" ] && max_temp=$temp_c
done

# ---------------------------------------------------------------------------
# Output (22 lines, 0-indexed)
# 0-15  : original fields (backward compatible)
# 16-19 : network speeds and totals
# 20    : system_temp (ACPI / motherboard)
# 21    : max_temp (highest across all hwmon sensors)
# ---------------------------------------------------------------------------
cat <<EOF > ~/.vrcosc_hwstats.txt
$cpu_usage
$cpu_power
$cpu_temp
$gpu_usage
$gpu_power
$gpu_temp
$ram_usage
$ram_total
$ram_used
$ram_free
$vram_usage
$vram_total
$vram_used
$vram_free
$cpu_name
$gpu_name
$net_rx_kbps
$net_tx_kbps
$net_rx_total_mb
$net_tx_total_mb
$system_temp
$max_temp
EOF
