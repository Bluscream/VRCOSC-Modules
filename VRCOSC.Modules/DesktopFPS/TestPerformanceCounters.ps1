# Test script to query available performance counters for FPS measurement

Write-Host "=== Testing Performance Counters for FPS Measurement ===" -ForegroundColor Cyan
Write-Host ""

# Test 1: List all performance counter categories
Write-Host "1. Checking for GPU-related performance counter categories..." -ForegroundColor Yellow
$categories = Get-Counter -ListSet "*Direct3D*" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty CounterSetName
if ($categories) {
    Write-Host "   Found Direct3D categories:" -ForegroundColor Green
    foreach ($cat in $categories) {
        Write-Host "   - $cat" -ForegroundColor White
    }
} else {
    Write-Host "   No Direct3D categories found" -ForegroundColor Red
}

Write-Host ""

# Test 2: Try NVIDIA Direct3D Driver
Write-Host "2. Testing NVIDIA Direct3D Driver..." -ForegroundColor Yellow
try {
    $nvidiaCounter = New-Object System.Diagnostics.PerformanceCounter("NVIDIA Direct3D Driver", "D3D FPS", "CPU", $true)
    $nvidiaCounter.NextValue() | Out-Null
    $nvidiaValue = $nvidiaCounter.NextValue()
    Write-Host "   NVIDIA Counter Value: $nvidiaValue" -ForegroundColor $(if ($nvidiaValue -gt 0) { "Green" } else { "Red" })
    $nvidiaCounter.Dispose()
} catch {
    Write-Host "   NVIDIA counter not available: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 3: Try AMD Direct3D Driver
Write-Host "3. Testing AMD Direct3D Driver..." -ForegroundColor Yellow
try {
    $amdCounter = New-Object System.Diagnostics.PerformanceCounter("AMD Direct3D Driver", "D3D FPS", "CPU", $true)
    $amdCounter.NextValue() | Out-Null
    $amdValue = $amdCounter.NextValue()
    Write-Host "   AMD Counter Value: $amdValue" -ForegroundColor $(if ($amdValue -gt 0) { "Green" } else { "Red" })
    $amdCounter.Dispose()
} catch {
    Write-Host "   AMD counter not available: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 4: Try Intel Direct3D Driver
Write-Host "4. Testing Intel Direct3D Driver..." -ForegroundColor Yellow
try {
    $intelCounter = New-Object System.Diagnostics.PerformanceCounter("Intel Direct3D Driver", "D3D FPS", "CPU", $true)
    $intelCounter.NextValue() | Out-Null
    $intelValue = $intelCounter.NextValue()
    Write-Host "   Intel Counter Value: $intelValue" -ForegroundColor $(if ($intelValue -gt 0) { "Green" } else { "Red" })
    $intelCounter.Dispose()
} catch {
    Write-Host "   Intel counter not available: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 5: List all available counter categories (filtered)
Write-Host "5. Searching for all GPU/Graphics related categories..." -ForegroundColor Yellow
$allCategories = Get-Counter -ListSet "*" -ErrorAction SilentlyContinue | Where-Object { 
    $_.CounterSetName -like "*GPU*" -or 
    $_.CounterSetName -like "*Graphics*" -or 
    $_.CounterSetName -like "*Direct3D*" -or 
    $_.CounterSetName -like "*NVIDIA*" -or 
    $_.CounterSetName -like "*AMD*" -or 
    $_.CounterSetName -like "*Intel*" -or
    $_.CounterSetName -like "*Frame*" -or
    $_.CounterSetName -like "*FPS*"
} | Select-Object -ExpandProperty CounterSetName -Unique

if ($allCategories) {
    Write-Host "   Found related categories:" -ForegroundColor Green
    foreach ($cat in $allCategories) {
        Write-Host "   - $cat" -ForegroundColor White
    }
} else {
    Write-Host "   No GPU/Graphics categories found" -ForegroundColor Red
}

Write-Host ""

# Test 6: Check VRChat process
Write-Host "6. Checking VRChat process..." -ForegroundColor Yellow
$vrchatProcess = Get-Process -Name "VRChat" -ErrorAction SilentlyContinue
if ($vrchatProcess) {
    Write-Host "   VRChat process found: PID $($vrchatProcess.Id)" -ForegroundColor Green
    Write-Host "   CPU Time: $($vrchatProcess.CPU)" -ForegroundColor White
    Write-Host "   Total Processor Time: $($vrchatProcess.TotalProcessorTime)" -ForegroundColor White
} else {
    Write-Host "   VRChat process not running" -ForegroundColor Red
}

Write-Host ""

# Test 7: Check display refresh rate
Write-Host "7. Checking display refresh rate..." -ForegroundColor Yellow
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class DisplayInfo {
    [DllImport("user32.dll")]
    public static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);
    
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct DEVMODE {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;
        public short dmSpecVersion;
        public short dmDriverVersion;
        public short dmSize;
        public short dmDriverExtra;
        public int dmFields;
        public int dmPositionX;
        public int dmPositionY;
        public int dmDisplayOrientation;
        public int dmDisplayFixedOutput;
        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel;
        public int dmPelsWidth;
        public int dmPelsHeight;
        public int dmDisplayFlags;
        public int dmDisplayFrequency;
        public int dmICMMethod;
        public int dmICMIntent;
        public int dmMediaType;
        public int dmDitherType;
        public int dmReserved1;
        public int dmReserved2;
        public int dmPanningWidth;
        public int dmPanningHeight;
    }
}
"@ -ErrorAction SilentlyContinue

try {
    $devMode = New-Object DisplayInfo+DEVMODE
    $devMode.dmSize = [System.Runtime.InteropServices.Marshal]::SizeOf($devMode)
    if ([DisplayInfo]::EnumDisplaySettings($null, -1, [ref]$devMode)) {
        Write-Host "   Display Refresh Rate: $($devMode.dmDisplayFrequency) Hz" -ForegroundColor Green
    } else {
        Write-Host "   Could not get display refresh rate" -ForegroundColor Red
    }
} catch {
    Write-Host "   Error getting display refresh rate: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host "=== Test Complete ===" -ForegroundColor Cyan
