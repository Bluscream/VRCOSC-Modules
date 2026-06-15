#!/usr/bin/env bash

# CPU Name
cpu_name=$(grep -m1 "model name" /proc/cpuinfo | cut -d: -f2 | xargs)
if [ -z "$cpu_name" ]; then
    cpu_name=$(lscpu | grep "Model name" | cut -d: -f2 | xargs)
fi
[ -z "$cpu_name" ] && cpu_name="Generic CPU"

# CPU Temp
cpu_temp=0
for name_file in /sys/class/hwmon/hwmon*/name; do
    name=$(cat "$name_file" 2>/dev/null)
    if [ "$name" = "coretemp" ] || [ "$name" = "k10temp" ] || [ "$name" = "zenpower" ]; then
        dir=$(dirname "$name_file")
        if [ -f "$dir/temp1_input" ]; then
            raw_temp=$(cat "$dir/temp1_input")
            cpu_temp=$((raw_temp / 1000))
            break
        fi
    fi
done
if [ "$cpu_temp" -eq 0 ]; then
    if [ -f /sys/class/thermal/thermal_zone0/temp ]; then
        raw_temp=$(cat /sys/class/thermal/thermal_zone0/temp)
        cpu_temp=$((raw_temp / 1000))
    fi
fi

# Measure CPU usage and CPU power (if available) over a 200ms window
read -r _ u1 n1 s1 i1 io1 ir1 si1 st1 _ _ < /proc/stat
e1=0
if [ -f /sys/class/powercap/intel-rapl:0/energy_uj ]; then
    e1=$(cat /sys/class/powercap/intel-rapl:0/energy_uj 2>/dev/null || echo 0)
fi
t1=$(date +%s%N)

sleep 0.2

read -r _ u2 n2 s2 i2 io2 ir2 si2 st2 _ _ < /proc/stat
e2=0
if [ -f /sys/class/powercap/intel-rapl:0/energy_uj ]; then
    e2=$(cat /sys/class/powercap/intel-rapl:0/energy_uj 2>/dev/null || echo 0)
fi
t2=$(date +%s%N)

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

# Calculate CPU Power (W)
cpu_power=0
if [ "$e1" -gt 0 ] && [ "$e2" -gt 0 ] && [ "$e2" -gt "$e1" ]; then
    energy_diff=$((e2 - e1)) # microjoules
    time_diff_ns=$((t2 - t1))
    if [ "$time_diff_ns" -gt 0 ]; then
        # Power (W) = energy_diff / (time_diff_ns / 1000)
        cpu_power=$(( energy_diff * 1000 / time_diff_ns ))
    fi
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
    nv_data=$(nvidia-smi --query-gpu=name,utilization.gpu,power.draw,temperature.gpu,memory.total,memory.used,memory.free --format=csv,noheader,nounits 2>/dev/null)
    if [ ! -z "$nv_data" ]; then
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
        vram_usage=$(awk "BEGIN {printf \"%.2f\", ($usd_mib / $tot_mib)}")
    fi
else
    # AMD GPU detection
    amd_hwmon=""
    for name_file in /sys/class/hwmon/hwmon*/name; do
        if [ "$(cat "$name_file" 2>/dev/null)" = "amdgpu" ]; then
            amd_hwmon=$(dirname "$name_file")
            break
        fi
    done
    
    amd_card=""
    for card_dir in /sys/class/drm/card*; do
        if [ -d "$card_dir/device" ] && [ -f "$card_dir/device/gpu_busy_percent" ]; then
            amd_card="$card_dir"
            break
        fi
    done
    
    if [ ! -z "$amd_hwmon" ] || [ ! -z "$amd_card" ]; then
        gpu_name="AMD Radeon GPU"
        lspci_name=$(lspci | grep -i vga | grep -i amd | cut -d: -f3 | xargs)
        if [ ! -z "$lspci_name" ]; then
            gpu_name="$lspci_name"
        fi
        
        if [ -f "$amd_card/device/gpu_busy_percent" ]; then
            gpu_usage=$(cat "$amd_card/device/gpu_busy_percent")
        fi
        
        if [ -f "$amd_hwmon/power1_average" ]; then
            raw_pow=$(cat "$amd_hwmon/power1_average")
            gpu_power=$((raw_pow / 1000000))
        fi
        
        if [ -f "$amd_hwmon/temp1_input" ]; then
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

# Format output file
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
EOF
