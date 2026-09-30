# Optional Apple native-library fallback (0.1.0)

This BepInEx 5 preloader patcher handles two unavailable Apple SDK libraries in native ARM64 Valheim on macOS. It is disabled by default. The main installer is unchanged.

## Enable or remove

With Valheim closed and this repository's BepInEx setup already installed:

```bash
./apple-fallback.sh enable
# Continue launching with the game's existing play.sh.
./apple-fallback.sh disable
```

Both commands accept a non-default game directory as the second argument. Enabling copies the packaged DLL into `BepInEx/patchers`, after checking its SHA-256. The installer records ownership so later versions can upgrade it; it refuses to overwrite or remove an unknown DLL. Disabling removes only this patch and its receipt. Game assemblies and saves are not edited.

## Exact changes and guards

For a supported Apple.Core assembly, when `AppleCoreNativeMac` cannot load or lacks `AppleCore_GetRuntimeEnvironment`, the patch replaces `Apple.Core.Availability.get_RuntimeEnvironment()` with managed IL that constructs the existing struct using the real macOS product version from `/usr/bin/sw_vers -productVersion`. The native import remains in the assembly but this getter no longer calls it.

For a supported Apple.GameKit assembly, when `GameKitWrapper` cannot load or lacks either required handler-registration export, `DefaultNSErrorHandler.Init()` and `DefaultNSExceptionHandler.Init()` become no-ops. No other GameKit methods are modified.

The patch runs before Unity startup callbacks, checks the current process is native ARM64 macOS (including a Rosetta translation check), and recognizes only these tested MVIDs:

| Assembly | Supported module identifier |
| --- | --- |
| Apple.Core | `c4ac282d-8b9c-4283-b952-e41bfb919e9c` |
| Apple.GameKit | `c92debd5-87ae-4131-a65e-d26d423ed9f0` |

Unknown assemblies are skipped with a message, without throwing an unsupported-version exception into BepInEx. Their original behavior, including any existing startup errors, remains. Do not remove this guard without validating the new build.

Native detection checks normal loader names and explicit game-root/PlugIns/Frameworks paths, including Unity bundle executables. It uses macOS's system `dlopen`/`dlsym` APIs, so incompatible architectures, unresolved dependencies reported by the loader, and missing required exports are detected. It balances each successful open with `dlclose`; this is a loader probe and can run a library's initialization routines, but does not call its exported game functions. A loadable library with the required exports keeps the original methods. Unexpected probe errors conservatively keep original methods too.

No finite path check covers every custom Mono dllmap. Custom native-library remapping should be checked separately before enabling this workaround. Symbol lookup is also not proof that every Apple SDK feature works.

The patch builds every replacement before assigning method bodies, preventing a validation failure from partially disabling the two GameKit handlers. It obtains the actual OS version and skips on query failure; it never invents a version.

This is a workaround, not an implementation of Game Center or Apple's remaining Foundation interop. Calls to other missing native functions may still fail. It does not change equipment-mod code or the separate PassivePowers transpiler. A game update that restores compatible native libraries should make this fallback unnecessary.

## Reproducible build and checks

Install the SDK pinned in the repository's `global.json`, then run:

```bash
patchers/AppleNativeFallback/build.sh
dotnet run --project patchers/AppleNativeFallback/tests/Tests.csproj -c Release
patchers/AppleNativeFallback/tests/native-fixtures.sh
patchers/AppleNativeFallback/tests/installer.sh
```

`VALHEIM_DOTNET` can select a dotnet executable for the shell scripts. The build uses pinned Microsoft .NET Framework reference assemblies, a NuGet lock file, the BepInEx/Cecil DLLs already in this repository, deterministic compilation, and normalized source paths. It needs no proprietary game DLLs. Generated build folders are ignored; only the patch DLL and checksum are packaged. Source and scripts are provided under the MIT license in `LICENSE` in this directory.

Tests exercise supported and unsupported builds, unchanged behavior for loadable/uncertain libraries, atomic validation failures, the managed getter and both disabled callbacks, serialization, OS parsing, bundle paths, installation/removal, and upgrade ownership. ARM64 macOS additionally builds synthetic ARM64, x86_64, and missing-export dylibs and checks the real native loader. None of these tests starts Unity or Valheim. CI builds on Linux and ARM64 macOS, verifies the packaged checksum against a rebuild, and runs these checks.

## Evidence and remaining rollout checks

On 2026-09-30, the initial three-method workaround was verified using native ARM64 Valheim 1.0.16 (n-40), experimental BepInEx 5.4.23.5, Jötunn 2.30.2, CDEV.EquipmentSlots 0.1.4, and SpeedyPaths 1.0.9. Startup logs showed both missing-library exceptions removed, successful macOS version detection, and all three plugins loading. The user subsequently confirmed that the mods worked in game. These are results from one Mac, not a multi-machine compatibility claim, and do not isolate which replacement caused the gameplay improvement.

Version 0.1.0 adds guards, native probing, packaging, and tests around the same method replacements. Its isolated terminal tests pass, including loading read-only copies of the installed Apple assemblies and executing the replaced getter and callbacks outside Unity; no new game session was started to test this revision. Startup reports from additional affected Macs and maintainer review are requested before wider adoption. Save/reload, multiplayer, and specific equipment-mod combinations have not been separately qualified. The patch remains an explicit opt-in.

The original failing getter and startup callback can also be inspected in [Apple's Availability.cs](https://github.com/apple/unityplugins/blob/main/plug-ins/Apple.Core/Apple.Core_Unity/Assets/Apple.Core/Runtime/Availability.cs).
