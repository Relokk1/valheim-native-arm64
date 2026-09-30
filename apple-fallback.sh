#!/usr/bin/env bash
# SPDX-License-Identifier: MIT
# Copyright (c) 2026 marvin-brt; see patchers/AppleNativeFallback/LICENSE.
# Explicit opt-in installation; does not reinstall BepInEx or alter game assemblies.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd -P)"
ACTION="${1:-}"
GAME="${2:-$HOME/Library/Application Support/Steam/steamapps/common/Valheim}"
SOURCE="$ROOT/bin/BepInEx/patchers/AppleNativeFallback.dll"
CHECKSUM="$ROOT/bin/BepInEx/patchers/AppleNativeFallback.sha256"
TARGET="$GAME/BepInEx/patchers/AppleNativeFallback.dll"
RECEIPT="$GAME/BepInEx/patchers/AppleNativeFallback.installed.sha256"
case "$ACTION" in enable|disable) ;; *) echo "Usage: $0 enable|disable [game-directory]" >&2; exit 1 ;; esac
if pgrep -x Valheim >/dev/null 2>&1; then
    echo "Quit Valheim before changing the Apple fallback." >&2; exit 1
fi
if [ ! -d "$GAME/valheim.app" ] || [ ! -f "$GAME/BepInEx/core/Mono.Cecil.dll" ]; then
    echo "Valheim with BepInEx was not found in: $GAME" >&2; exit 1
fi
digest() { shasum -a 256 "$1" | awk '{print $1}'; }
if [ "$ACTION" = enable ]; then
    EXPECTED="$(awk '{print $1}' "$CHECKSUM")"
    [ "$(digest "$SOURCE")" = "$EXPECTED" ] || { echo "Package checksum mismatch." >&2; exit 1; }
    if [ -e "$TARGET" ]; then
        # Can upgrade a previously managed version, but never overwrite an unknown patch.
        CURRENT="$(digest "$TARGET")"
        if [ "$CURRENT" != "$EXPECTED" ] && { [ ! -f "$RECEIPT" ] || [ "$CURRENT" != "$(cat "$RECEIPT")" ]; }; then
            echo "An unmanaged/different fallback already exists; leaving it untouched." >&2; exit 1
        fi
    fi
    mkdir -p "$(dirname "$TARGET")"
    STAGE="$(mktemp "${TARGET}.tmp.XXXXXX")"
    trap 'rm -f "$STAGE"' EXIT
    cp "$SOURCE" "$STAGE"
    chmod 644 "$STAGE"
    mv "$STAGE" "$TARGET"
    printf '%s\n' "$EXPECTED" > "$RECEIPT"
    echo "Apple fallback enabled. Start the game with its existing play.sh."
else
    if [ -e "$TARGET" ]; then
        CURRENT="$(digest "$TARGET")"
        PACKAGED="$(awk '{print $1}' "$CHECKSUM")"
        if [ "$CURRENT" != "$PACKAGED" ] && { [ ! -f "$RECEIPT" ] || [ "$CURRENT" != "$(cat "$RECEIPT")" ]; }; then
            echo "An unmanaged/different fallback exists; leaving it untouched." >&2; exit 1
        fi
        rm "$TARGET"
    fi
    rm -f "$RECEIPT"
    echo "Apple fallback disabled."
fi
