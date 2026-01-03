# Test refresh rate detection

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
"@

Write-Host "=== Testing Refresh Rate Detection ===" -ForegroundColor Cyan
Write-Host ""

try {
    $devMode = New-Object DisplayInfo+DEVMODE
    $devMode.dmSize = [System.Runtime.InteropServices.Marshal]::SizeOf($devMode)
    if ([DisplayInfo]::EnumDisplaySettings($null, -1, [ref]$devMode)) {
        Write-Host "Display Refresh Rate: $($devMode.dmDisplayFrequency) Hz" -ForegroundColor Green
        Write-Host "Resolution: $($devMode.dmPelsWidth) x $($devMode.dmPelsHeight)" -ForegroundColor White
        
        # Calculate what 72 FPS would be
        $refreshRate = $devMode.dmDisplayFrequency
        $capped = $refreshRate * 1.2
        Write-Host ""
        Write-Host "If refresh rate is $refreshRate Hz:" -ForegroundColor Yellow
        Write-Host "  Refresh Rate * 1.2 = $capped Hz" -ForegroundColor White
    } else {
        Write-Host "Could not get display refresh rate" -ForegroundColor Red
    }
} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "Stack: $($_.ScriptStackTrace)" -ForegroundColor Gray
}

Write-Host ""

# Also try WMI
Write-Host "Trying WMI method..." -ForegroundColor Yellow
try {
    $videoControllers = Get-WmiObject -Class Win32_VideoController | Where-Object { $_.CurrentRefreshRate -ne $null -and $_.CurrentRefreshRate -gt 0 }
    foreach ($vc in $videoControllers) {
        Write-Host "Video Controller: $($vc.Name)" -ForegroundColor White
        Write-Host "  Current Refresh Rate: $($vc.CurrentRefreshRate) Hz" -ForegroundColor Green
    }
} catch {
    Write-Host "WMI Error: $($_.Exception.Message)" -ForegroundColor Red
}
