#!/usr/bin/env pwsh
# VRCOSC Modules - Complete Build & Release Script using Bluscream-BuildTools
# Builds, commits, and optionally publishes VRCOSC Modules with full automation

param(
    [switch]$Publish
)

$ErrorActionPreference = "Stop"

# Import Bluscream-BuildTools module (use local version)
Write-Host "📦 Loading Bluscream-BuildTools module..." -ForegroundColor Cyan
$modulePath = "P:\Powershell\Modules\Bluscream-BuildTools"
if (-not (Test-Path $modulePath)) {
    throw "Bluscream-BuildTools module not found at $modulePath"
}
Import-Module $modulePath -Force
if (-not (Get-Module Bluscream-BuildTools)) {
    throw "Failed to import Bluscream-BuildTools module"
}
Write-Host "✓ Bluscream-BuildTools module loaded" -ForegroundColor Green

# Configuration
$repoUrl = "https://github.com/Bluscream/VRCOSC-Modules"
$ProjectDir = "$PSScriptRoot\VRCOSC.Modules"
$ProjectFile = "$ProjectDir\Bluscream.Modules.csproj"
$AssemblyInfoPath = "$ProjectDir\AssemblyInfo.cs"

Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║        VRCOSC Modules - Complete Build & Release          ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Kill VRCOSC if running
Write-Host "🛑 Stopping VRCOSC..." -ForegroundColor Yellow
$vrcoscProcess = Get-Process -Name "VRCOSC" -ErrorAction SilentlyContinue
if ($vrcoscProcess) {
    Stop-Process -Name "VRCOSC" -Force
    Start-Sleep -Seconds 1
    Write-Host "✓ VRCOSC stopped" -ForegroundColor Green
}
else {
    Write-Host "✓ VRCOSC is not running" -ForegroundColor Green
}
Write-Host ""

# Version Management using Bluscream-BuildTools
Write-Host "🔢 Managing version..." -ForegroundColor Green

if (-not (Get-Command Bump-Version -ErrorAction SilentlyContinue)) {
    throw "Bump-Version command not found in Bluscream-BuildTools module"
}

$assemblyInfoPath = Join-Path $ProjectDir "AssemblyInfo.cs"
if (-not (Test-Path $assemblyInfoPath)) {
    throw "AssemblyInfo.cs not found at $assemblyInfoPath"
}

# Use Bump-Version function from Bluscream-BuildTools - bump AssemblyVersion first
$versionResult = Bump-Version -Files @($assemblyInfoPath) -Pattern 'AssemblyVersion\("([^"]+)"\)' -Backup
if (-not $versionResult -or -not $versionResult.Success) {
    throw "Failed to bump AssemblyVersion: $($versionResult.Error)"
}

$ReleaseTag = $versionResult.NewVersion

# Enforce 3-segment semver (YYYY.MDD.Build) - strip 4th segment if present
$versionParts = $ReleaseTag.Split('.')
if ($versionParts.Count -gt 3) {
    Write-Host "⚠️  Version has $($versionParts.Count) segments, enforcing 3-segment semver..." -ForegroundColor Yellow
    $ReleaseTag = $versionParts[0..2] -join '.'
    
    # Update AssemblyInfo.cs to use 3-segment version
    $assemblyInfoContent = Get-Content $assemblyInfoPath -Raw
    $assemblyInfoContent = $assemblyInfoContent -replace 'AssemblyVersion\("[^"]+"\)', "AssemblyVersion(`"$ReleaseTag`")"
    $assemblyInfoContent = $assemblyInfoContent -replace 'AssemblyFileVersion\("[^"]+"\)', "AssemblyFileVersion(`"$ReleaseTag`")"
    Set-Content -Path $assemblyInfoPath -Value $assemblyInfoContent -NoNewline
    
    Write-Host "✓ Enforced 3-segment version: $ReleaseTag" -ForegroundColor Green
}

# Also bump AssemblyFileVersion to match (if not already done)
if ($versionParts.Count -le 3) {
    $fileVersionResult = Bump-Version -Files @($assemblyInfoPath) -Pattern 'AssemblyFileVersion\("([^"]+)"\)'
    if (-not $fileVersionResult -or -not $fileVersionResult.Success) {
        throw "Failed to bump AssemblyFileVersion: $($fileVersionResult.Error)"
    }
}
Write-Host "✓ Version bumped to $ReleaseTag" -ForegroundColor Green
Write-Host ""

# Complete Build Workflow using Bluscream-BuildTools
Write-Host "📦 Starting complete build workflow..." -ForegroundColor Green

if (-not (Get-Command Start-BuildWorkflow -ErrorAction SilentlyContinue)) {
    throw "Start-BuildWorkflow command not found in Bluscream-BuildTools module"
}

# Execute complete build workflow
$buildWorkflow = Start-BuildWorkflow -ProjectPath $ProjectFile -Configuration "Release" -Architecture "win-x64" -Framework "net8.0-windows10.0.26100.0" -AssemblyName "Bluscream.Modules" -OutputDirectory "./dist/" -CreateArchive -ArchiveName "VRCOSC.Modules-v$ReleaseTag" -CleanOutput

if (-not $buildWorkflow -or -not $buildWorkflow.Success) {
    throw "Build workflow failed"
}

Write-Host "✓ Complete build workflow succeeded" -ForegroundColor Green
Write-Host ""

# Git operations using Bluscream-BuildTools
Write-Host "📝 Committing changes..." -ForegroundColor Green

if (-not (Get-Command Git-CommitRepository -ErrorAction SilentlyContinue)) {
    throw "Git-CommitRepository command not found in Bluscream-BuildTools module"
}

$commitResult = Git-CommitRepository -Path $PSScriptRoot -Message "Update VRCOSC modules v$ReleaseTag"
if (-not $commitResult) {
    throw "Git commit failed"
}

$pushResult = Git-PushRepository -Path $PSScriptRoot
if (-not $pushResult) {
    throw "Git push failed"
}

Write-Host "✓ Committed and pushed using Bluscream-BuildTools" -ForegroundColor Green
Write-Host ""

# Create GitHub release (only if -Publish flag is used)
if ($Publish) {
    Write-Host "🚀 Creating GitHub release..." -ForegroundColor Green
    
    if (-not (Get-Command GitHub-CreateRelease -ErrorAction SilentlyContinue)) {
        throw "GitHub-CreateRelease command not found in Bluscream-BuildTools module"
    }
    
    # Prepare release assets from build workflow
    $releaseAssets = @()
    
    # Add all built files as individual assets
    foreach ($file in $buildWorkflow.CopiedFiles) {
        $releaseAssets += $file
    }
    
    # Add archive if it exists
    $archivePath = $buildWorkflow.ArchivePath
    if ($archivePath -and (Test-Path $archivePath)) {
        $releaseAssets += $archivePath
    }
    
    # Create release notes with file information and download links
    $fileList = $releaseAssets | ForEach-Object { 
        $fileName = Split-Path $_ -Leaf
        "- [$fileName](https://github.com/Bluscream/VRCOSC-Modules/releases/latest/download/$fileName)"
    } | Out-String
    $releaseNotes = "VRCOSC Modules v$ReleaseTag`n`nChanges:`n- Update VRCOSC modules v$ReleaseTag`n`nFiles included:`n$fileList"
    
    $releaseResult = GitHub-CreateRelease -Repository $repoUrl -Tag $ReleaseTag -Title "VRCOSC Modules v$ReleaseTag" -Notes $releaseNotes -Prerelease -Assets $releaseAssets
    if (-not $releaseResult) {
        throw "Release creation failed"
    }
    
    Write-Host "✓ Release created using Bluscream-BuildTools: $repoUrl/releases/tag/$ReleaseTag" -ForegroundColor Green
}
else {
    Write-Host "⏭️  Skipping release (use -Publish to create release)" -ForegroundColor Yellow
}

# Clean VRCOSC local packages directory and logs
Write-Host "🧹 Cleaning VRCOSC local packages directory..." -ForegroundColor Green
$vrcoscLocalDir = Join-Path $env:APPDATA "VRCOSC\packages\local"
if (Test-Path $vrcoscLocalDir) {
    $filesRemoved = 0
    Get-ChildItem -Path $vrcoscLocalDir -Filter "*.dll" | ForEach-Object {
        Remove-Item $_.FullName -Force
        $filesRemoved++
        Write-Host "  Removed: $($_.Name)" -ForegroundColor Gray
    }
    Write-Host "✓ Removed $filesRemoved DLL file(s) from local packages" -ForegroundColor Green
}
else {
    Write-Host "⚠️  Local packages directory doesn't exist yet" -ForegroundColor Yellow
}

Write-Host "🧹 Cleaning VRCOSC logs..." -ForegroundColor Green
$logsDir = Join-Path $PSScriptRoot "AppdataRoaming\logs"
if (Test-Path $logsDir) {
    $logsRemoved = 0
    Get-ChildItem -Path $logsDir -Filter "*.log" | ForEach-Object {
        Remove-Item $_.FullName -Force
        $logsRemoved++
    }
    Write-Host "✓ Removed $logsRemoved log file(s)" -ForegroundColor Green
}
else {
    Write-Host "⚠️  Logs directory doesn't exist yet" -ForegroundColor Yellow
}
Write-Host ""

# Build in Debug mode using Bluscream-BuildTools
Write-Host "🔧 Building in Debug mode..." -ForegroundColor Green

$debugWorkflow = Start-BuildWorkflow -ProjectPath $ProjectFile -Configuration "Debug" -Architecture "win-x64" -Framework "net8.0-windows10.0.26100.0" -AssemblyName "Bluscream.Modules" -OutputDirectory "./debug-dist/" -CleanOutput

if (-not $debugWorkflow -or -not $debugWorkflow.Success) {
    throw "Debug build workflow failed"
}

Write-Host "✓ Debug build succeeded using Bluscream-BuildTools" -ForegroundColor Green

# Copy Debug DLL to VRCOSC local packages directory
Write-Host "📋 Copying Debug DLL to VRCOSC..." -ForegroundColor Green
$debugDllSource = Join-Path $debugWorkflow.OutputDirectory "Bluscream.Modules.dll"
$vrcoscLocalDir = Join-Path $env:APPDATA "VRCOSC\packages\local"
if (-not (Test-Path $vrcoscLocalDir)) {
    New-Item -ItemType Directory -Path $vrcoscLocalDir -Force | Out-Null
}
if (Test-Path $debugDllSource) {
    Copy-Item -Path $debugDllSource -Destination (Join-Path $vrcoscLocalDir "Bluscream.Modules.dll") -Force
    Write-Host "✓ Debug DLL copied to VRCOSC local packages" -ForegroundColor Green
}
else {
    Write-Host "⚠️  Debug DLL not found at $debugDllSource" -ForegroundColor Yellow
}
Write-Host ""

# Create release package if publishing
if ($Publish) {
    Write-Host "📦 Creating release package..." -ForegroundColor Green
    
    if (-not (Get-Command New-ReleasePackage -ErrorAction SilentlyContinue)) {
        throw "New-ReleasePackage command not found in Bluscream-BuildTools module"
    }
    
    $releasePackage = New-ReleasePackage -ReleaseInfo $buildWorkflow -Version $ReleaseTag -ReleaseNotes "VRCOSC Modules v$ReleaseTag - Complete build with all dependencies" -CreateArchives
    
    if (-not $releasePackage -or -not $releasePackage.Success) {
        throw "Release package creation failed"
    }
    
    Write-Host "✓ Release package created successfully" -ForegroundColor Green
    Write-Host ""
}

# Summary
Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║                    ✓ ALL DONE!                             ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""

Write-Host "📊 Build Summary:" -ForegroundColor Cyan
Write-Host "  Version: $ReleaseTag" -ForegroundColor Gray
Write-Host "  Release Files: $($buildWorkflow.CopiedFiles.Count)" -ForegroundColor Gray
Write-Host "  Archive: $($buildWorkflow.ArchivePath)" -ForegroundColor Gray
Write-Host "  Debug Files: $($debugWorkflow.CopiedFiles.Count)" -ForegroundColor Gray

if ($Publish) {
    Write-Host "📦 Release: $repoUrl/releases/tag/$ReleaseTag" -ForegroundColor Magenta
    if ($releasePackage.ArchivePath) {
        Write-Host "📁 Release Package: $($releasePackage.ArchivePath)" -ForegroundColor Magenta
    }
}

Write-Host "📍 Release Files: $($buildWorkflow.OutputDirectory)" -ForegroundColor Cyan
Write-Host "📍 Debug Files: $($debugWorkflow.OutputDirectory)" -ForegroundColor Cyan
Write-Host "📍 Local VRCOSC: %APPDATA%\VRCOSC\packages\local\Bluscream.Modules.dll (Debug)" -ForegroundColor Cyan
Write-Host ""

# Start VRCOSC
Write-Host "🚀 Starting VRCOSC..." -ForegroundColor Green
$vrcoscPath = "$env:LOCALAPPDATA\VRCOSC\VRCOSC.bat"
if (Test-Path $vrcoscPath) {
    Start-Process -FilePath $vrcoscPath -WorkingDirectory (Split-Path $vrcoscPath)
    Write-Host "✓ VRCOSC started" -ForegroundColor Green
}
else {
    Write-Host "⚠️  VRCOSC.bat not found at $vrcoscPath" -ForegroundColor Yellow
    Write-Host "   Please start VRCOSC manually to test the modules" -ForegroundColor Yellow
}
Write-Host ""