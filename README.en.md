# Valheim Native ARM64

[Русский](README.md) | [English](README.en.md)

Run Valheim mods natively on an Apple Silicon Mac — **without Rosetta**.

Two scripts: install and play. For a full mod manager with a Thunderstore browser,
see [Macheim Native](https://github.com/relokk1/macheim-native).

## Installation

```bash
git clone https://github.com/relokk1/valheim-native-arm64.git
cd valheim-native-arm64
./install.sh
```

The installer looks for Valheim in the default Steam directory. If your game is
installed elsewhere, pass its directory as an argument:

```bash
./install.sh "/path/to/Valheim"
```

## Launching the game

```bash
"$HOME/Library/Application Support/Steam/steamapps/common/Valheim/play.sh"
```

The installer copies `play.sh` into the game directory. You can also launch it by
double-clicking it after making it executable.

## Installing mods

Extract each mod's `.dll` files into:

```text
<Valheim directory>/BepInEx/plugins/
```

Install dependencies manually; the script does not resolve them. For automatic
dependency resolution, use [Macheim Native](https://github.com/relokk1/macheim-native).

## Checking that the game runs natively

While the game is running:

```bash
vmmap $(pgrep -f "MacOS/Valheim") | grep "Code Type"
```

The output should include `Code Type: ARM64`.

## Why this is needed

Standard BepInEx uses MonoMod v22, which cannot create hooks on ARM64. Apple
processors enforce W^X protection on JIT memory pages: memory can be writable or
executable, but not both at once. Conventional Valheim modding setups on macOS
therefore run the game through Rosetta.

This project installs BepInEx 5.4.23.5 rebuilt with MonoMod v25 and HarmonyX 2.16.1,
plus UnityDoorstop 4.5.0, the first release with a native arm64 slice.

## Included files

```text
bin/BepInEx/core/      ARM64-compatible BepInEx assemblies (MonoMod v25 + HarmonyX 2.16.1)
bin/libdoorstop.dylib  UnityDoorstop 4.5.0 universal binary
install.sh            Installer
play.sh               Launch script (copied into the game directory)
```

The BepInEx core is built from the
[bbauti/BepInEx `codex/macos-arm64-valheim` branch](https://github.com/bbauti/BepInEx/tree/codex/macos-arm64-valheim).

## Limitations

- Modded objects may appear pink because the mods' asset bundles lack Metal
  shaders. This is a limitation of the mod ecosystem that only the mod authors
  can fix. It does not affect functionality.
- Mods that ship their own native libraries for Windows or Linux will not work.
- Diagnostic logs are written to `<Valheim directory>/BepInEx/LogOutput.log`.

## Uninstalling

```bash
./uninstall.sh
```

This removes BepInEx, Doorstop, and `play.sh`. The installer automatically backs
up any previous BepInEx installation as `BepInEx.backup-<date>`.

## License

MIT
