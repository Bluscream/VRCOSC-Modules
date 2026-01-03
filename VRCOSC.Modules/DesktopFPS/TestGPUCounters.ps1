# Test GPU Engine counters and other options

Write-Host "=== Testing GPU Engine Counters ===" -ForegroundColor Cyan
Write-Host ""

# Check GPU Engine counters
Write-Host "1. Checking GPU Engine counters..." -ForegroundColor Yellow
try {
    $gpuEngine = Get-Counter -ListSet "GPU Engine" -ErrorAction SilentlyContinue
    if ($gpuEngine) {
        Write-Host "   GPU Engine category found!" -ForegroundColor Green
        Write-Host "   Available instances:" -ForegroundColor White
        $instances = Get-Counter "\GPU Engine(*)\*" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty CounterSamples | Select-Object -ExpandProperty InstanceName -Unique | Select-Object -First 10
        foreach ($inst in $instances) {
            Write-Host "   - $inst" -ForegroundColor Gray
        }
        
        Write-Host ""
        Write-Host "   Available counters:" -ForegroundColor White
        $counters = Get-Counter "\GPU Engine(*)\*" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty CounterSamples | Select-Object -ExpandProperty Path -Unique | Select-Object -First 10
        foreach ($counter in $counters) {
            Write-Host "   - $counter" -ForegroundColor Gray
        }
    } else {
        Write-Host "   GPU Engine category not found" -ForegroundColor Red
    }
} catch {
    Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Check GPU Process Memory for VRChat
Write-Host "2. Checking GPU Process Memory for VRChat..." -ForegroundColor Yellow
$vrchatProcess = Get-Process -Name "VRChat" -ErrorAction SilentlyContinue
if ($vrchatProcess) {
    try {
        $gpuMem = Get-Counter "\GPU Process Memory(pid_$($vrchatProcess.Id)_*)\*" -ErrorAction SilentlyContinue
        if ($gpuMem) {
            Write-Host "   GPU Process Memory counters found for VRChat!" -ForegroundColor Green
            $gpuMem.CounterSamples | ForEach-Object {
                Write-Host "   - $($_.Path): $($_.CookedValue)" -ForegroundColor White
            }
        } else {
            Write-Host "   No GPU Process Memory counters for VRChat" -ForegroundColor Red
        }
    } catch {
        Write-Host "   Error: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "   VRChat process not running" -ForegroundColor Red
}

Write-Host ""

# List all available counters that might be useful
Write-Host "3. Searching for frame-related counters..." -ForegroundColor Yellow
$frameCounters = Get-Counter -ListSet "*" -ErrorAction SilentlyContinue | Where-Object { 
    $_.CounterSetName -like "*Frame*" -or 
    $_.CounterSetName -like "*FPS*" -or
    $_.CounterSetName -like "*Present*"
} | Select-Object -ExpandProperty CounterSetName -Unique

if ($frameCounters) {
    Write-Host "   Found frame-related categories:" -ForegroundColor Green
    foreach ($cat in $frameCounters) {
        Write-Host "   - $cat" -ForegroundColor White
    }
} else {
    Write-Host "   No frame-related categories found" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Test Complete ===" -ForegroundColor Cyan
