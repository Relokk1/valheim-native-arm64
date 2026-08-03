#!/usr/bin/env bash
# Удаление BepInEx и нативного запуска из Valheim
set -euo pipefail

DEFAULT_GAME="$HOME/Library/Application Support/Steam/steamapps/common/Valheim"
GAME="${1:-$DEFAULT_GAME}"

if [ ! -d "$GAME/valheim.app" ]; then
    echo "Valheim не найден в: $GAME" >&2
    exit 1
fi

for item in BepInEx libdoorstop.dylib play.sh .doorstop_version doorstop_config.ini doorstop_libs; do
    if [ -e "$GAME/$item" ]; then
        rm -rf "${GAME:?}/$item"
        echo "Удалено: $item"
    fi
done

echo
echo "Готово. Бэкапы (если были) остались в папке игры как BepInEx.backup-*"
