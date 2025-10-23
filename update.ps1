#!/usr/bin/env pwsh
# Build, commit, and optionally publish VRCOSC Modules
param(
    [switch]$Publish
)

$ErrorActionPreference = "Stop"

# Configuration
$repoUrl = "https://github.com/Bluscream/VRCOSC-Modules"
$ProjectDir = "$PSScriptRoot\VRCOSC.Modules"
$ProjectFile = "$ProjectDir\Bluscream.Modules.csproj"
$AssemblyInfoPath = "$ProjectDir\AssemblyInfo.cs"
$SourceDll = "$ProjectDir\bin\Release\net8.0-windows10.0.26100.0\Bluscream.Modules.dll"
$ReleaseDll = "$PSScriptRoot\VRCOSC.Modules.dll"

Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║        VRCOSC Modules - Build & Release Script            ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Bump version in AssemblyInfo.cs
Write-Host "🔢 Bumping version..." -ForegroundColor Green

$assemblyContent = Get-Content $AssemblyInfoPath -Raw
$versionPattern = '\[assembly: AssemblyVersion\("(\d+)\.(\d+)\.(\d+)"\)\]'

if ($assemblyContent -match $versionPattern) {
    $year = $Matches[1]
    $monthDay = $Matches[2]
    $build = [int]$Matches[3]
    
    # Get current date
    $currentDate = Get-Date
    $newYear = $currentDate.ToString("yyyy")
    $newMonthDay = $currentDate.ToString("MMdd")
    
    # If date changed, reset build number, otherwise increment
    if ($year -ne $newYear -or $monthDay -ne $newMonthDay) {
        $newBuild = 1
    }
    else {
        $newBuild = $build + 1
    }
    
    $oldVersion = "$year.$monthDay.$build"
    $newVersion = "$newYear.$newMonthDay.$newBuild"
    
    Write-Host "  Old version: $oldVersion" -ForegroundColor Yellow
    Write-Host "  New version: $newVersion" -ForegroundColor Green
    
    # Update both AssemblyVersion and AssemblyFileVersion
    $assemblyContent = $assemblyContent -replace '\[assembly: AssemblyVersion\("[\d\.]+"\)\]', "[assembly: AssemblyVersion(`"$newVersion`")]"
    $assemblyContent = $assemblyContent -replace '\[assembly: AssemblyFileVersion\("[\d\.]+"\)\]', "[assembly: AssemblyFileVersion(`"$newVersion`")]"
    
    Set-Content $AssemblyInfoPath -Value $assemblyContent -NoNewline
    Write-Host "✓ Version bumped to $newVersion" -ForegroundColor Green
    
    $ReleaseTag = $newVersion
}
else {
    Write-Host "⚠️  Could not parse version from AssemblyInfo.cs" -ForegroundColor Yellow
    $ReleaseTag = (Get-Date -Format "yyyy.MMdd.HHmm")
}

Write-Host ""

# Step 1: Build in Release mode
Write-Host "📦 Building v$ReleaseTag in Release mode..." -ForegroundColor Green

Push-Location $ProjectDir
try {
    dotnet build $ProjectFile -c Release | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Release build failed with exit code $LASTEXITCODE"
    }
    Write-Host "✓ Release build succeeded" -ForegroundColor Green
}
finally {
    Pop-Location
}

# Step 2: Copy and rename DLL for release
Write-Host "📋 Copying DLL for release..." -ForegroundColor Green

if (Test-Path $SourceDll) {
    Copy-Item $SourceDll $ReleaseDll -Force
    Write-Host "✓ Copied: $SourceDll" -ForegroundColor Green
    Write-Host "     → $ReleaseDll" -ForegroundColor Green
}
else {
    throw "Release DLL not found: $SourceDll"
}

# Step 3: Git commit and push
Write-Host "📝 Committing changes..." -ForegroundColor Green

Push-Location $PSScriptRoot
try {
    # Check for changes
    $status = git status --porcelain
    if ($status) {
        git add .
        git commit -m "Update VRCOSC modules v$ReleaseTag"
        Write-Host "✓ Committed: Update VRCOSC modules v$ReleaseTag" -ForegroundColor Green
        
        $currentBranch = git rev-parse --abbrev-ref HEAD
        git push origin $currentBranch
        Write-Host "✓ Pushed to origin/$currentBranch" -ForegroundColor Green
    }
    else {
        Write-Host "⚠️  No changes to commit" -ForegroundColor Yellow
    }
}
finally {
    Pop-Location
}

# Step 4: Create GitHub release (only if -Publish flag is used)
if ($Publish) {
    Write-Host "🚀 Creating GitHub release..." -ForegroundColor Green
    
    # Check if gh CLI is available
    $hasGhCli = $null -ne (Get-Command gh -ErrorAction SilentlyContinue)
    if (-not $hasGhCli) {
        Write-Host "⚠️  GitHub CLI (gh) not found - release will be skipped" -ForegroundColor Yellow
        Write-Host "   Install from: https://cli.github.com/" -ForegroundColor Yellow
    }
    else {
        Push-Location $PSScriptRoot
        try {
            $tag = $ReleaseTag
            $title = "VRCOSC Modules v$tag"
            $notes = "VRCOSC Modules v$tag`n`nChanges:`n- Update VRCOSC modules v$ReleaseTag"
            
            # Create release
            Write-Host "Creating release: $tag" -ForegroundColor Cyan
            $releaseOutput = gh release create $tag --title $title --notes $notes --prerelease "$ReleaseDll#VRCOSC.Modules.dll" 2>&1
            
            if ($LASTEXITCODE -eq 0) {
                Write-Host "✓ Release created: $tag" -ForegroundColor Green
                Write-Host "   Attached: VRCOSC.Modules.dll" -ForegroundColor Green
            }
            else {
                Write-Host "⚠️  Release creation failed (exit code: $LASTEXITCODE)" -ForegroundColor Yellow
                Write-Host "   Opening GitHub release page..." -ForegroundColor Cyan
                Start-Process "$repoUrl/releases/new?prerelease=1&tag=$Tag&title=VRCOSC+Modules+v$Tag"
                Start-Process "explorer.exe" -ArgumentList "/select,`"$ReleaseDll`""
            }
        }
        catch {
            Write-Host "⚠️  Release creation failed: $_" -ForegroundColor Yellow
            Start-Process "$repoUrl/releases/new?prerelease=1&tag=$ReleaseTag&title=VRCOSC+Modules+v$ReleaseTag"
        }
        finally {
            Pop-Location
        }
    }
}
else {
    Write-Host "⏭️  Skipping release (use -Publish to create release)" -ForegroundColor Yellow
}

# Step 5: Build in Debug mode
Write-Host "🔧 Building in Debug mode..." -ForegroundColor Green

Push-Location $ProjectDir
try {
    dotnet build $ProjectFile -c Debug | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw "Debug build failed with exit code $LASTEXITCODE"
    }
    Write-Host "✓ Debug build succeeded" -ForegroundColor Green
    Write-Host "   Local VRCOSC copy is now Debug build" -ForegroundColor Cyan
}
finally {
    Pop-Location
}

# Summary
Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║                    ✓ ALL DONE!                             ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""

if ($Publish) {
    Write-Host "📦 Release: $repoUrl/releases/tag/$ReleaseTag" -ForegroundColor Magenta
}
Write-Host "📍 Release DLL: $ReleaseDll" -ForegroundColor Cyan
Write-Host "📍 Local VRCOSC: %APPDATA%\VRCOSC\packages\local\Bluscream.Modules.dll (Debug)" -ForegroundColor Cyan
Write-Host ""