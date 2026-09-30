# Valheim Native ARM64

Запуск модов Valheim нативно на Mac с Apple Silicon — **без Rosetta**.

Два скрипта: поставить и играть. Если нужен полноценный мод-менеджер с браузером Thunderstore —
смотрите [Macheim Native](https://github.com/relokk1/macheim-native).

## Установка

```bash
git clone https://github.com/relokk1/valheim-native-arm64.git
cd valheim-native-arm64
./install.sh
```

Скрипт сам найдёт Valheim в стандартной папке Steam. Если игра лежит в другом месте:

```bash
./install.sh "/путь/к/папке/Valheim"
```

## Запуск

```bash
"$HOME/Library/Application Support/Steam/steamapps/common/Valheim/play.sh"
```

Скрипт `play.sh` кладётся в папку с игрой при установке — можно запускать двойным кликом,
предварительно сделав его исполняемым.

## Установка модов

Распакуйте `.dll` мода в:

```
<папка Valheim>/BepInEx/plugins/
```

Зависимости придётся ставить вручную — скрипт их не разрешает. Для автоматического
разрешения зависимостей используйте [Macheim Native](https://github.com/relokk1/macheim-native).

## Проверка, что запуск нативный

Пока игра запущена:

```bash
vmmap $(pgrep -f "MacOS/Valheim") | grep "Code Type"
```

Должно быть `Code Type: ARM64`.

## Почему это нужно

Стандартный BepInEx использует MonoMod v22, который не умеет создавать хуки на ARM64:
процессоры Apple держат JIT-страницы под защитой W^X (память либо записываемая, либо
исполняемая, но не обе сразу). Поэтому все существующие способы модить Valheim на Mac
запускают игру через эмуляцию Rosetta.

Здесь ставится BepInEx 5.4.23.5, пересобранный под MonoMod v25 + HarmonyX 2.16.1, и
UnityDoorstop 4.5.0 — первый релиз с нативным arm64-слайсом.

## Что внутри

```
bin/BepInEx/core/    сборки BepInEx под ARM64 (MonoMod v25 + HarmonyX 2.16.1)
bin/libdoorstop.dylib  UnityDoorstop 4.5.0 universal
install.sh           установщик
play.sh              скрипт запуска (копируется в папку игры)
```

Ядро BepInEx собрано из ветки
[bbauti/BepInEx `codex/macos-arm64-valheim`](https://github.com/bbauti/BepInEx/tree/codex/macos-arm64-valheim).

## Ограничения

- Объекты из модов могут отображаться розовыми — в AssetBundle'ах модов нет Metal-шейдеров.
  Это ограничение экосистемы, чинится только авторами модов. На функциональность не влияет.
- Моды с собственными нативными библиотеками под Windows/Linux работать не будут.
- Логи для диагностики: `<папка Valheim>/BepInEx/LogOutput.log`

## Необязательный обход отсутствующих библиотек Apple

Если в нативной ARM64-версии возникают ошибки `AppleCoreNativeMac` или
`GameKitWrapper`, доступен отдельный патчер. Он выключен по умолчанию.
Закройте Valheim перед установкой или удалением:

```bash
./apple-fallback.sh enable
./apple-fallback.sh disable
```

Для нестандартной папки игры укажите путь вторым аргументом. Патчер поддерживает
только проверенные сборки Apple.Core и Apple.GameKit; неизвестные версии
пропускаются с сообщением в журнале. Это обход конкретных ошибок запуска,
а не реализация всех функций Apple SDK.

Исходный код, воспроизводимая сборка, тесты и ограничения описаны в
[документации патчера](patchers/AppleNativeFallback/README.md).

## Откат

```bash
./uninstall.sh
```

Удаляет BepInEx, doorstop и play.sh. Бэкап предыдущей установки install.sh создаёт
автоматически в `BepInEx.backup-<дата>`.

## Лицензия

MIT
