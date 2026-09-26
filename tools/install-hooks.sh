#!/usr/bin/env bash
# Install this repo's git hooks.
#
# Hooks live in tools/ so they are version-controlled; .git/hooks is not. Symlinking
# rather than copying means edits to tools/pre-commit take effect immediately and cannot
# silently drift from what is committed.
set -euo pipefail

REPO_ROOT="$(git rev-parse --show-toplevel)"
HOOK_DIR="$(git rev-parse --git-path hooks)"
mkdir -p "$HOOK_DIR"

for hook in pre-commit; do
    src="$REPO_ROOT/tools/$hook"
    dst="$HOOK_DIR/$hook"

    [ -f "$src" ] || { echo "missing $src"; exit 1; }
    chmod +x "$src"

    if [ -e "$dst" ] && [ ! -L "$dst" ]; then
        mv "$dst" "$dst.replaced-$(date +%Y%m%d%H%M%S)"
        echo "backed up existing $hook"
    fi

    ln -sfn "$src" "$dst"
    echo "installed $hook -> tools/$hook"
done
