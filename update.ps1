# Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
# See the LICENSE file in the repository root for full license text.

param(
    [Parameter(Mandatory = $false)]
    [string]$Version,
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipCommit,
    
    [Parameter(Mandatory = $false)]
    [switch]$SkipRelease,
    
    [Parameter(Mandatory = $false)]
    [switch]$NoPush
)

$ErrorActionPreference = "Stop"

# Get the latest release version or use provided version
if (-not $Version) {
    Write-Host "Getting latest release version..." -ForegroundColor Cyan
    $latestRelease = gh release list --limit 1 --repo Bluscream/VRCOSC-Modules --json tagName -q '.[0].tagName' 2>$null
    if ($latestRelease) {
        Write-Host "Latest release: $latestRelease" -ForegroundColor Yellow
    }
    
    # Always use today's date for new version
    $today = Get-Date -Format "yyyy.MMdd"
    
    # If latest release exists and is from today, increment patch number
    if ($latestRelease -and $latestRelease -match '^(\d{4})\.(\d{4})\.(\d+)$') {
        $releaseDate = $Matches[2]
        if ($releaseDate -eq (Get-Date -Format "MMdd")) {
            # Same date, increment patch
            $patch = [int]$Matches[3]
            $patch++
            $Version = "$today.$patch"
        }
        else {
            # Different date, start at patch 0
            $Version = "$today.0"
        }
    }
    else {
        # No releases or invalid format, start at patch 0
        $Version = "$today.0"
    }
}

Write-Host "Using version: $Version" -ForegroundColor Green

# Update AssemblyInfo.cs
$assemblyInfoPath = "VRCOSC.Modules\AssemblyInfo.cs"
if (Test-Path $assemblyInfoPath) {
    Write-Host "Updating AssemblyInfo.cs..." -ForegroundColor Cyan
    $content = Get-Content $assemblyInfoPath -Raw
    $content = $content -replace '\[assembly: AssemblyVersion\("([^"]+)"\)\]', "[assembly: AssemblyVersion(`"$Version`")]"
    $content = $content -replace '\[assembly: AssemblyFileVersion\("([^"]+)"\)\]', "[assembly: AssemblyFileVersion(`"$Version`")]"
    Set-Content $assemblyInfoPath -Value $content -NoNewline
    Write-Host "✓ Updated AssemblyInfo.cs" -ForegroundColor Green
}
else {
    Write-Warning "AssemblyInfo.cs not found at $assemblyInfoPath"
}

# Build the project
Write-Host "Building project..." -ForegroundColor Cyan
$buildResult = dotnet build VRCOSC.Modules\Bluscream.Modules.csproj --configuration Release --no-incremental 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed!"
    $buildResult | Write-Host
    exit 1
}
Write-Host "✓ Build succeeded" -ForegroundColor Green

# Find the DLL
$dllPath = "VRCOSC.Modules\bin\Release\net10.0-windows10.0.26100.0\win-x64\Bluscream.Modules.dll"
if (-not (Test-Path $dllPath)) {
    # Fallback to non-win-x64 path
    $dllPath = "VRCOSC.Modules\bin\Release\net10.0-windows10.0.26100.0\Bluscream.Modules.dll"
    if (-not (Test-Path $dllPath)) {
        Write-Error "DLL not found at expected location: $dllPath"
        exit 1
    }
}

Write-Host "Found DLL: $dllPath" -ForegroundColor Green

# Commit changes
if (-not $SkipCommit) {
    Write-Host "Committing changes..." -ForegroundColor Cyan
    git add -A
    $commitMessage = "Release $Version

- Updated AssemblyInfo version to $Version"
    git commit -m $commitMessage
    Write-Host "✓ Committed changes" -ForegroundColor Green
    
    if (-not $NoPush) {
        Write-Host "Pushing to origin..." -ForegroundColor Cyan
        git push origin main
        Write-Host "✓ Pushed to origin" -ForegroundColor Green
    }
}
else {
    Write-Host "Skipping commit (--SkipCommit specified)" -ForegroundColor Yellow
}

# Create release
if (-not $SkipRelease) {
    Write-Host "Creating release $Version..." -ForegroundColor Cyan
    
    # Create and push tag
    git tag -a $Version -m "Release $Version"
    if (-not $NoPush) {
        git push origin $Version
    }
    
    # Copy DLL to temp location
    $tempDll = ".\Bluscream.Modules.dll"
    Copy-Item $dllPath -Destination $tempDll -Force
    
    # Create release notes
    $releaseNotes = @"
## Release $Version

- Updated assembly version to $Version
- Built with latest configuration matching CrookedToe's modules
"@
    
    # Create GitHub release
    gh release create $Version --repo Bluscream/VRCOSC-Modules --title $Version --notes $releaseNotes -- $tempDll
    
    # Cleanup
    Remove-Item $tempDll -Force -ErrorAction SilentlyContinue
    
    Write-Host "✓ Release $Version created" -ForegroundColor Green
    Write-Host "Release URL: https://github.com/Bluscream/VRCOSC-Modules/releases/tag/$Version" -ForegroundColor Cyan
}
else {
    Write-Host "Skipping release creation (--SkipRelease specified)" -ForegroundColor Yellow
}

Write-Host "`nDone! Version $Version" -ForegroundColor Green
