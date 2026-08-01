#!/usr/bin/env bash
# Pack the VRCOSC SDKs that the beta and dev module targets build against.
#
# Upstream stopped publishing VolcanicArts.VRCOSC.SDK to nuget.org after 2026.501.0, so
# anything newer has to be packed from source. Only the stable target uses nuget.org.
#
#   target  worktree            pinned to                         SDK version
#   beta    source-code/beta    commit of the latest GitHub       <tag of that
#                               PRE-release (what VRCOSC-BETA      prerelease>
#                               actually installs via Velopack)
#   dev     source-code/dev     origin/dev HEAD                   2026.<mmdd of HEAD>.0
#
# Why the version is always forced: VRCOSC's Directory.Build.props derives GlobalVersion
# from DateTime.Now. Packing two different branches on the same day therefore produces
# the SAME version number, and NuGet silently serves whichever it cached first. That is
# how the beta target once ended up compiled against the feat/cli fork while still
# building cleanly - the bug only surfaced at runtime as a TypeLoadException.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SRC="$REPO_ROOT/source-code"
FEED="$REPO_ROOT/local-nuget-feed"
GH_REPO="VolcanicArts/VRCOSC"

pack() { # <worktree> <version>
    echo "==> packing $2 from $1"
    ( cd "$1" && distrobox-enter -n arch -- dotnet pack VRCOSC.App/VRCOSC.App.csproj \
        -c Release -p:GlobalVersion="$2.0" -p:EnableWindowsTargeting=true -o "$FEED" \
        2>&1 | grep -E "error|Successfully created" )
}

mkdir -p "$FEED"

# --- beta: follow the latest GitHub pre-release ------------------------------------
# Velopack pulls the beta channel from this repo's pre-releases (see VelopackUpdater.cs:
# GithubSource(repo_url, null, allowPreRelease) with ExplicitChannel="beta"), so the tag
# of the newest pre-release is exactly what VRCOSC-BETA is running.
BETA_TAG=$(curl -fsSL "https://api.github.com/repos/$GH_REPO/releases?per_page=30" \
    | python3 -c 'import json,sys; print(next(r["tag_name"] for r in json.load(sys.stdin) if r["prerelease"]))')
echo "latest pre-release: $BETA_TAG"

git -C "$SRC/cli" fetch origin --tags --prune
git -C "$SRC/beta" checkout --detach "$BETA_TAG"
pack "$SRC/beta" "$BETA_TAG"

# --- dev: follow origin/dev HEAD ----------------------------------------------------
git -C "$SRC/dev" merge --ff-only origin/dev
DEV_VER=$(git -C "$SRC/dev" log -1 --date=format:'%Y.%-m%d' --format='%ad').0
pack "$SRC/dev" "$DEV_VER"

echo
echo "feed contents:"
for f in "$FEED"/*.nupkg; do
    python3 - "$f" <<'PY'
import re, sys, zipfile
z = zipfile.ZipFile(sys.argv[1])
s = z.read([n for n in z.namelist() if n.endswith('.nuspec')][0]).decode('utf-8-sig')
v = re.search(r'<version>(.*?)</version>', s).group(1)
m = re.search(r'<repository[^>]*commit="(.*?)"', s)
print(f"  {v:<14} commit={m.group(1)[:8] if m else 'NONE'}")
PY
done
echo
echo "Now pin these in VRCOSC.Modules/Bluscream.Modules.csproj (VrcoscSdkVersion)."
