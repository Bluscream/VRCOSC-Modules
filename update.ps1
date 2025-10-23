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

# Step 1: Version Management using Bluscream-BuildTools
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

# Also bump AssemblyFileVersion to match
$fileVersionResult = Bump-Version -Files @($assemblyInfoPath) -Pattern 'AssemblyFileVersion\("([^"]+)"\)'
if (-not $fileVersionResult -or -not $fileVersionResult.Success) {
    throw "Failed to bump AssemblyFileVersion: $($fileVersionResult.Error)"
}
Write-Host "✓ Version bumped to $ReleaseTag" -ForegroundColor Green
Write-Host ""

# Step 2: Complete Build Workflow using Bluscream-BuildTools
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

# Step 3: Git operations using Bluscream-BuildTools
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

# Step 4: Create GitHub release (only if -Publish flag is used)
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
    
    # Create release notes with file information
    $releaseNotes = "VRCOSC Modules v$ReleaseTag`n`nChanges:`n- Update VRCOSC modules v$ReleaseTag`n`nFiles included:`n$($releaseAssets | ForEach-Object { "- $(Split-Path $_ -Leaf)" } | Out-String)"
    
    $releaseResult = GitHub-CreateRelease -Repository $repoUrl -Tag $ReleaseTag -Title "VRCOSC Modules v$ReleaseTag" -Notes $releaseNotes -Prerelease -Assets $releaseAssets
    if (-not $releaseResult -or -not $releaseResult.Success) {
        throw "Release creation failed: $($releaseResult.ErrorMessage)"
    }
    
    Write-Host "✓ Release created using Bluscream-BuildTools: $($releaseResult.ReleaseUrl)" -ForegroundColor Green
}
else {
    Write-Host "⏭️  Skipping release (use -Publish to create release)" -ForegroundColor Yellow
}

# Step 5: Build in Debug mode using Bluscream-BuildTools
Write-Host "🔧 Building in Debug mode..." -ForegroundColor Green

$debugWorkflow = Start-BuildWorkflow -ProjectPath $ProjectFile -Configuration "Debug" -Architecture "win-x64" -Framework "net8.0-windows10.0.26100.0" -AssemblyName "Bluscream.Modules" -OutputDirectory "./debug-dist/" -CleanOutput

if (-not $debugWorkflow -or -not $debugWorkflow.Success) {
    throw "Debug build workflow failed"
}

Write-Host "✓ Debug build succeeded using Bluscream-BuildTools" -ForegroundColor Green
Write-Host ""

# Step 6: Create release package if publishing
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