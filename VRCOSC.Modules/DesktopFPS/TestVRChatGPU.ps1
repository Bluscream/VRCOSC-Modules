# Test VRChat GPU Engine counters specifically

Write-Host "=== Testing VRChat GPU Engine Counters ===" -ForegroundColor Cyan
Write-Host ""

$vrchatProcess = Get-Process -Name "VRChat" -ErrorAction SilentlyContinue
if (-not $vrchatProcess) {
    Write-Host "VRChat process not running!" -ForegroundColor Red
    exit
}

Write-Host "VRChat PID: $($vrchatProcess.Id)" -ForegroundColor Green
Write-Host ""

# Find GPU Engine 3D instances for VRChat
Write-Host "1. Finding GPU Engine 3D instances for VRChat..." -ForegroundColor Yellow
$gpu3dInstances = Get-Counter "\GPU Engine(*)\*" -ErrorAction SilentlyContinue | 
    Select-Object -ExpandProperty CounterSamples | 
    Where-Object { $_.InstanceName -like "*pid_$($vrchatProcess.Id)*" -and $_.InstanceName -like "*engtype_3d*" } |
    Select-Object -ExpandProperty InstanceName -Unique

if ($gpu3dInstances) {
    Write-Host "   Found 3D engine instances:" -ForegroundColor Green
    foreach ($inst in $gpu3dInstances) {
        Write-Host "   - $inst" -ForegroundColor White
        
        # Get running time counter
        $runningTimePath = "\GPU Engine($inst)\Running Time"
        try {
            $runningTime = Get-Counter $runningTimePath -ErrorAction SilentlyContinue
            if ($runningTime) {
                $value = $runningTime.CounterSamples[0].CookedValue
                Write-Host "     Running Time: $value ms" -ForegroundColor Cyan
            }
        } catch {
            Write-Host "     Could not read Running Time" -ForegroundColor Red
        }
        
        # Get utilization percentage
        $utilPath = "\GPU Engine($inst)\Utilization Percentage"
        try {
            $util = Get-Counter $utilPath -ErrorAction SilentlyContinue
            if ($util) {
                $utilValue = $util.CounterSamples[0].CookedValue
                Write-Host "     Utilization: $utilValue %" -ForegroundColor Cyan
            }
        } catch {
            Write-Host "     Could not read Utilization" -ForegroundColor Red
        }
    }
} else {
    Write-Host "   No 3D engine instances found for VRChat" -ForegroundColor Red
}

Write-Host ""

# Test reading running time multiple times to see delta
Write-Host "2. Testing Running Time delta (waiting 1 second between reads)..." -ForegroundColor Yellow
if ($gpu3dInstances) {
    $inst = $gpu3dInstances[0]
    $runningTimePath = "\GPU Engine($inst)\Running Time"
    
    try {
        $time1 = Get-Counter $runningTimePath -ErrorAction SilentlyContinue
        $value1 = $time1.CounterSamples[0].CookedValue
        Write-Host "   First reading: $value1 ms" -ForegroundColor White
        
        Start-Sleep -Seconds 1
        
        $time2 = Get-Counter $runningTimePath -ErrorAction SilentlyContinue
        $value2 = $time2.CounterSamples[0].CookedValue
        Write-Host "   Second reading (1s later): $value2 ms" -ForegroundColor White
        
        $delta = $value2 - $value1
        Write-Host "   Delta: $delta ms" -ForegroundColor $(if ($delta -gt 0) { "Green" } else { "Yellow" })
        
        if ($delta -gt 0) {
            # Running time is in milliseconds, delta over 1 second
            # If delta is close to 1000ms, GPU is fully utilized
            # We can estimate FPS based on this
            $gpuUtilization = ($delta / 1000.0) * 100.0
            Write-Host "   GPU Utilization (estimated): $([math]::Round($gpuUtilization, 2)) %" -ForegroundColor Cyan
        }
    } catch {
        Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=== Test Complete ===" -ForegroundColor Cyan
