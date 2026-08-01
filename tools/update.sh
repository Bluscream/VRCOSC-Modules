#!/usr/bin/env bash
# Copyright (c) Bluscream. Licensed under the GPL-3.0 License.
# See the LICENSE file in the repository root for full license text.
set -euo pipefail

# This script lives in tools/ but every path below (VRCOSC.Modules/..., AssemblyInfo.cs)
# is relative to the repo root, and the git operations must run there too. cd once, up
# front, so it behaves identically no matter where it is invoked from.
REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# Default options
VERSION=""
SKIP_COMMIT=false
SKIP_RELEASE=false
# Which VRCOSC install this build is for. Selects the SDK (-p:VrcoscTarget), the roaming
# dir the DLL is deployed into, and which app gets restarted afterwards. See AGENTS.md §2.
#   stable -> VRCOSC          SDK 2026.501.0   published as a normal release
#   beta   -> VRCOSC-BETA     SDK 2026.702.0   published as a GitHub PRE-RELEASE
#   dev    -> VRCOSC-DEV      SDK 2026.718.0   local deploy only, never published
TARGET=stable
# Beta-targeted builds MUST ship as GitHub pre-releases: VRCOSC's PackageSource
# .filterReleases() hides pre-releases unless AllowPreReleasePackages is set, which
# is exactly how stable installs avoid pulling a beta-SDK DLL that would crash them.
PRERELEASE=false
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
        -b|--beta|--prerelease)
            TARGET=beta
            PRERELEASE=true
            shift
            ;;
        -d|--dev)
            # Dev is never published: GitHub has only two release states (release and
            # pre-release) and beta already owns pre-release, so a third channel cannot
            # coexist on this repo. Dev is a local-deploy-only target for testing
            # modules against upstream/dev before it becomes a beta.
            TARGET=dev
            SKIP_RELEASE=true
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
            echo "Usage: $0 [-v|--version <version>] [-c|--skip-commit] [-r|--skip-release] [-p|--no-push] [-b|--beta] [-d|--dev]"
            echo "  -b, --beta   build against the beta SDK (VRCOSC-BETA) and publish as a GitHub PRE-RELEASE"
            echo "  -d, --dev    build against the dev SDK (VRCOSC-DEV); deploys locally, never published"
            exit 1
            ;;
    esac
done

# Kill the running instance before replacing its DLL.
#
# Matching "VRCOSC.exe" never worked here: the launchers run the framework-dependent
# build as `wine dotnet.exe .../VRCOSC.dll`, so the process command line contains
# VRCOSC.dll and no .exe. Match both so this works whichever way it was started.
echo "Stopping VRCOSC..."
pids=$(pgrep -f "VRCOSC\.(exe|dll)" || true)
if [ -n "$pids" ]; then
    kill $pids 2>/dev/null || true
    sleep 2
    # Anything still alive after SIGTERM would hold a file lock on the DLL.
    still=$(pgrep -f "VRCOSC\.(exe|dll)" || true)
    [ -n "$still" ] && kill -9 $still 2>/dev/null || true
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

# Roaming (config) dir per target. Each install keeps its own, and deploying a DLL into
# the wrong one is how a beta-SDK build ends up crashing stable. All three are symlinks
# out to OneDrive/.../OSC/{VRCOSC,VRCOSC-BETA,VRCOSC-DEV}, but resolve them through the
# prefixes so this keeps working if the symlinks are ever changed.
#
#   stable  VRChat's prefix, AppData/Roaming/VRCOSC
#   beta    its own prefix ($VRCOSC_BETA_PREFIX), AppData/Roaming/VRCOSC
#   dev     VRChat's prefix, AppData/Roaming/VRCOSC-Dev  (APP_NAME differs under #if DEBUG)
case "$TARGET" in
    stable)
        ROAMING_DIR="$VRC_COMPATDATA/pfx/drive_c/users/steamuser/AppData/Roaming/VRCOSC"
        ;;
    beta)
        BETA_PREFIX="${VRCOSC_BETA_PREFIX:-$HOME/.local/share/vrcosc-beta-prefix}"
        ROAMING_DIR="$BETA_PREFIX/drive_c/users/$USER/AppData/Roaming/VRCOSC"
        ;;
    dev)
        ROAMING_DIR="$VRC_COMPATDATA/pfx/drive_c/users/$USER/AppData/Roaming/VRCOSC-Dev"
        ;;
esac

if [ ! -d "$ROAMING_DIR" ]; then
    echo "Error: $TARGET config dir not found: $ROAMING_DIR"
    echo "Start that install once so it creates its config, then re-run."
    exit 1
fi

REMOTE_PKG_DIR="$ROAMING_DIR/packages/remote/bluscream.vrcosc.modules"
echo "Target: $TARGET  ->  $ROAMING_DIR"

# Branch/target correspondence. The repo has exactly two branches by design so it is
# obvious which is which: stable (default) and beta. Cutting a stable release off the beta
# branch (or vice versa) would publish the wrong code under the wrong channel, so refuse
# unless the release steps are being skipped anyway.
BRANCH=$(git rev-parse --abbrev-ref HEAD)
case "$TARGET" in
    stable) EXPECTED_BRANCH=stable ;;
    beta)   EXPECTED_BRANCH=beta ;;
    dev)    EXPECTED_BRANCH="" ;;  # dev never publishes, so any branch is fine
esac
if [ -n "$EXPECTED_BRANCH" ] && [ "$BRANCH" != "$EXPECTED_BRANCH" ]; then
    if [ "$SKIP_RELEASE" = false ] || [ "$SKIP_COMMIT" = false ]; then
        echo "Error: target '$TARGET' should be built from branch '$EXPECTED_BRANCH', but you are on '$BRANCH'."
        echo "Either switch branch, or pass --skip-commit --skip-release to build/deploy locally."
        exit 1
    fi
    echo "[WARN] building '$TARGET' from branch '$BRANCH' (local only, nothing will be published)"
fi

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
#
# -p:VrcoscTarget is what actually selects the SDK. Passing it is not optional: until
# this was added, --beta only flipped the *release* to a pre-release and the build kept
# producing the stable DLL, so every "beta" release shipped stable bits that could not
# load on VRCOSC-BETA.
echo "Building project ($TARGET) in distrobox container..."
distrobox-enter -n arch -- dotnet build VRCOSC.Modules/Bluscream.Modules.csproj --configuration Release --no-incremental -p:VrcoscTarget="$TARGET"

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
mkdir -p "$REMOTE_PKG_DIR"
cp "$DLL_PATH" "$REMOTE_PKG_DIR/Bluscream.Modules.dll"
echo "[OK] Deployed DLL to $REMOTE_PKG_DIR"

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
        cp "$SRC" "$REMOTE_PKG_DIR/$NAME.dll"
        echo "[OK] Deployed $NAME.dll"
    else
        echo "[WARN] $NAME.dll not found at $SRC"
    fi
done

# Clean up any native dll from packages/remote that shouldn't be there (causes BadImageFormatException)
rm -f "$REMOTE_PKG_DIR/openxr_loader.dll"

# Deploy openxr_loader.dll to the VRCOSC main AppData Local folder (where VRCOSC.exe resides)
STEAMVR_LOADER="/run/media/system/Data/Games/Steam/steamapps/common/SteamVR/bin/win64/openxr_loader.dll"
# App install dir per target - openxr_loader.dll must sit next to the VRCOSC binary that
# will load it, so a --beta run must not drop it into the stable install.
# Dev runs straight out of the build output in source-code/dev, not an installed copy.
case "$TARGET" in
    stable) INSTALL_DIR="$VRC_COMPATDATA/pfx/drive_c/users/steamuser/AppData/Local/VRCOSC" ;;
    beta)   INSTALL_DIR="$VRC_COMPATDATA/pfx/drive_c/users/steamuser/AppData/Local/VRCOSC-beta" ;;
    dev)    INSTALL_DIR="$(cd "$REPO_ROOT/../source-code/dev" && pwd)/VRCOSC/bin/Debug/net10.0-windows10.0.26100.0" ;;
esac
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
        # Push whatever branch is checked out, not a hardcoded name. The repo has exactly
        # two branches - stable (default) and beta - and this used to say "main", which
        # stopped existing when they were renamed.
        CURRENT_BRANCH=$(git rev-parse --abbrev-ref HEAD)
        echo "Pushing $CURRENT_BRANCH to origin..."
        git push origin "$CURRENT_BRANCH"
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
    if [ "$PRERELEASE" = true ]; then
        gh release create "$VERSION" --repo Bluscream/VRCOSC-Modules --prerelease \
            --title "$VERSION - Beta" \
            --notes "Pre-release $VERSION for the VRCOSC **beta** channel (IPulseContext node API, SDK 2026.702.0). Built on Linux using Arch container.

Do not install on stable VRCOSC - it targets a different SDK and will fail to load with a TypeLoadException." "$ZIP_PATH"
        echo "[OK] PRE-RELEASE $VERSION created (beta channel only)"
    else
        gh release create "$VERSION" --repo Bluscream/VRCOSC-Modules --title "$VERSION" --notes "Release $VERSION - Built on Linux using Arch container" "$ZIP_PATH"
        echo "[OK] Release $VERSION created (stable channel)"
    fi
fi

# Restart the install this build targeted (not always stable).
case "$TARGET" in
    stable) LAUNCH_SCRIPT="$HOME/.local/bin/vrcosc" ;;
    beta)   LAUNCH_SCRIPT="$HOME/.local/bin/vrcosc-beta" ;;
    dev)    LAUNCH_SCRIPT="$HOME/.local/bin/vrcosc-dev" ;;
esac
if [ -f "$LAUNCH_SCRIPT" ]; then
    echo "Restarting VRCOSC..."
    # Launch from $HOME, not the repo. VRCOSC inherits this cwd, and modules that resolve
    # a relative Wine path would otherwise scatter stray directories through the checkout.
    # The path bug itself is fixed (LinuxUtils.GetWineHomeDir), but there is no reason to
    # hand the app a source tree as its working directory.
    (cd "$HOME" && nohup "$LAUNCH_SCRIPT" >/dev/null 2>&1 &)
    echo "[OK] Restarted VRCOSC"
fi

echo -e "\nDone! Version $VERSION"
