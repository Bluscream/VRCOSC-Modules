#!/usr/bin/env bash
# Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
# See the LICENSE file in the repository root for full license text.
set -euo pipefail

# Default options
VERSION=""
SKIP_COMMIT=false
SKIP_RELEASE=false
NO_PUSH=false

# Argument parsing
while [[ $# -gt 0 ]]; do
    case "$1" in
        -v|--version)
            VERSION="$2"
            shift 2
            ;;
        -c|--skip-commit)
            SKIP_COMMIT=true
            shift
            ;;
        -r|--skip-release)
            SKIP_RELEASE=true
            shift
            ;;
        -p|--no-push)
            NO_PUSH=true
            shift
            ;;
        *)
            echo "Unknown argument: $1"
            echo "Usage: $0 [-v|--version <version>] [-c|--skip-commit] [-r|--skip-release] [-p|--no-push]"
            exit 1
            ;;
    esac
done

# Kill VRCOSC before starting
echo "Stopping VRCOSC..."
pids=$(pgrep -f "VRCOSC.exe" || true)
if [ ! -z "$pids" ]; then
    kill $pids || true
    sleep 2
    echo "[OK] Stopped VRCOSC"
else
    echo "VRCOSC is not running"
fi

# Find VRCOSC roaming/install directory
VRC_COMPATDATA=""
SEARCH_PATHS=(
    "$HOME/.local/share/Steam"
    "$HOME/.steam/steam"
    "/run/media/system/Data/Games/Steam"
    "/media/media-automount/Data/Games/Steam"
)

for path in "${SEARCH_PATHS[@]}"; do
    if [ -d "$path/steamapps/compatdata/438100/pfx" ]; then
        VRC_COMPATDATA="$path/steamapps/compatdata/438100"
        break
    fi
done

if [ -z "$VRC_COMPATDATA" ]; then
    echo "Error: Could not locate VRChat Proton prefix (438100)."
    exit 1
fi

ROAMING_DIR="$VRC_COMPATDATA/pfx/drive_c/users/steamuser/AppData/Roaming/VRCOSC"
LOCAL_PKG_DIR="$ROAMING_DIR/packages/local"

# Calculate Version
if [ -z "$VERSION" ]; then
    echo "Getting latest release version..."
    LATEST_RELEASE=$(gh release list --limit 1 --repo Bluscream/VRCOSC-Modules --json tagName -q '.[0].tagName' 2>/dev/null || true)
    if [ ! -z "$LATEST_RELEASE" ]; then
        echo "Latest release: $LATEST_RELEASE"
    fi

    TODAY=$(date +"%Y.%m%d")
    PATCH=0

    # Match format YYYY.MMDD.PATCH
    if [[ "$LATEST_RELEASE" =~ ^([0-9]{4})\.([0-9]{4})\.([0-9]+)$ ]]; then
        RELEASE_DATE="${BASH_REMATCH[2]}"
        CURRENT_DATE=$(date +"%m%d")
        if [ "$RELEASE_DATE" = "$CURRENT_DATE" ]; then
            PATCH=$(( ${BASH_REMATCH[3]} + 1 ))
        fi
    fi
    VERSION="${TODAY}.${PATCH}"
fi

echo "Using version: $VERSION"

# Clear logs folder
LOGS_DIR="$ROAMING_DIR/logs"
if [ -d "$LOGS_DIR" ]; then
    echo "Clearing logs folder..."
    rm -f "$LOGS_DIR"/* 2>/dev/null || true
    echo "[OK] Cleared logs"
fi

# Update AssemblyInfo.cs
ASSEMBLY_INFO="VRCOSC.Modules/AssemblyInfo.cs"
if [ -f "$ASSEMBLY_INFO" ]; then
    echo "Updating AssemblyInfo.cs..."
    # Replace AssemblyVersion
    sed -i -E "s/AssemblyVersion\(\"[^\"]+\"\)/AssemblyVersion(\"$VERSION\")/g" "$ASSEMBLY_INFO"
    # Replace AssemblyFileVersion
    sed -i -E "s/AssemblyFileVersion\(\"[^\"]+\"\)/AssemblyFileVersion(\"$VERSION\")/g" "$ASSEMBLY_INFO"
    echo "[OK] Updated AssemblyInfo.cs"
else
    echo "Warning: AssemblyInfo.cs not found at $ASSEMBLY_INFO"
fi

# Build the project
echo "Building project in distrobox container..."
distrobox-enter -n arch -- dotnet build VRCOSC.Modules/Bluscream.Modules.csproj --configuration Release --no-incremental

DLL_PATH="VRCOSC.Modules/bin/Release/net10.0-windows10.0.26100.0/win-x64/Bluscream.Modules.dll"
if [ ! -f "$DLL_PATH" ]; then
    # Fallback path
    DLL_PATH="VRCOSC.Modules/bin/Release/net10.0-windows10.0.26100.0/Bluscream.Modules.dll"
    if [ ! -f "$DLL_PATH" ]; then
        echo "Error: Compiled DLL not found."
        exit 1
    fi
fi

# Deploy locally
mkdir -p "$LOCAL_PKG_DIR"
cp "$DLL_PATH" "$LOCAL_PKG_DIR/Bluscream.Modules.dll"
echo "[OK] Deployed DLL to $LOCAL_PKG_DIR"

# Deploy Silk.NET dependency DLLs (not copied by the build since VRCOSC is the host app)
NUGET_CACHE="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
SILK_VERSION="2.22.0"
SILK_TFM="net5.0"   # best available target under the Silk 2.22.0 packages
declare -A SILK_PKGS=(
    ["Silk.NET.OpenXR"]="silk.net.openxr"
    ["Silk.NET.OpenXR.Extensions.EXT"]="silk.net.openxr.extensions.ext"
    ["Silk.NET.Core"]="silk.net.core"
    ["Silk.NET.Maths"]="silk.net.maths"
)
for NAME in "${!SILK_PKGS[@]}"; do
    PKG="${SILK_PKGS[$NAME]}"
    SRC="$NUGET_CACHE/$PKG/$SILK_VERSION/lib/$SILK_TFM/$NAME.dll"
    if [ -f "$SRC" ]; then
        cp "$SRC" "$LOCAL_PKG_DIR/$NAME.dll"
        echo "[OK] Deployed $NAME.dll"
    else
        echo "[WARN] $NAME.dll not found at $SRC"
    fi
done

# Clean up any native dll from packages/local that shouldn't be there (causes BadImageFormatException)
rm -f "$LOCAL_PKG_DIR/openxr_loader.dll"

# Deploy openxr_loader.dll to the VRCOSC main AppData Local folder (where VRCOSC.exe resides)
STEAMVR_LOADER="/run/media/system/Data/Games/Steam/steamapps/common/SteamVR/bin/win64/openxr_loader.dll"
INSTALL_DIR="$VRC_COMPATDATA/pfx/drive_c/users/steamuser/AppData/Local/VRCOSC"
if [ -f "$STEAMVR_LOADER" ]; then
    if [ -d "$INSTALL_DIR" ]; then
        cp "$STEAMVR_LOADER" "$INSTALL_DIR/openxr_loader.dll"
        echo "[OK] Deployed openxr_loader.dll to $INSTALL_DIR"
    else
        echo "[WARN] VRCOSC install directory not found at $INSTALL_DIR"
    fi
else
    echo "[WARN] openxr_loader.dll not found in SteamVR at $STEAMVR_LOADER"
fi

# Create staging directory for GitHub Release zip
STAGING_DIR="VRCOSC.Modules/bin/Release/net10.0-windows10.0.26100.0/staging"
if [[ "$DLL_PATH" == *"win-x64"* ]]; then
    STAGING_DIR="VRCOSC.Modules/bin/Release/net10.0-windows10.0.26100.0/win-x64/staging"
fi
rm -rf "$STAGING_DIR"
mkdir -p "$STAGING_DIR"
cp "$DLL_PATH" "$STAGING_DIR/Bluscream.Modules.dll"

for NAME in "${!SILK_PKGS[@]}"; do
    PKG="${SILK_PKGS[$NAME]}"
    SRC="$NUGET_CACHE/$PKG/$SILK_VERSION/lib/$SILK_TFM/$NAME.dll"
    if [ -f "$SRC" ]; then
        cp "$SRC" "$STAGING_DIR/$NAME.dll"
    fi
done

ZIP_PATH="$(dirname "$DLL_PATH")/Bluscream.Modules.zip"
rm -f "$ZIP_PATH"
(cd "$STAGING_DIR" && zip -q -r "../$(basename "$ZIP_PATH")" .)
rm -rf "$STAGING_DIR"
echo "[OK] Created release zip at $ZIP_PATH"

# Git operations
if [ "$SKIP_COMMIT" = false ]; then
    echo "Committing changes..."
    git add -A
    COMMIT_MSG="Release $VERSION

- Updated AssemblyInfo version to $VERSION"
    git commit -m "$COMMIT_MSG"
    echo "[OK] Committed changes"

    if [ "$NO_PUSH" = false ]; then
        echo "Pushing to origin..."
        git push origin main
        echo "[OK] Pushed to origin"
    fi
fi

# Create Release
if [ "$SKIP_RELEASE" = false ]; then
    echo "Creating release $VERSION..."
    git tag -a "$VERSION" -m "Release $VERSION"
    if [ "$NO_PUSH" = false ]; then
        git push origin "$VERSION"
    fi

    # Create GitHub release and upload ZIP
    gh release create "$VERSION" --repo Bluscream/VRCOSC-Modules --title "$VERSION" --notes "Release $VERSION - Built on Linux using Arch container" "$ZIP_PATH"
    echo "[OK] Release $VERSION created"
fi

# Restart VRCOSC
LAUNCH_SCRIPT="$HOME/.local/bin/vrcosc"
if [ -f "$LAUNCH_SCRIPT" ]; then
    echo "Restarting VRCOSC..."
    nohup "$LAUNCH_SCRIPT" >/dev/null 2>&1 &
    echo "[OK] Restarted VRCOSC"
fi

echo -e "\nDone! Version $VERSION"
