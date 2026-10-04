# Contributing

This repo holds several independent BepInEx plugins for Dice Kingdoms (one folder per plugin under `src/`). Players
install only the DLLs they want.

## Set up (once)

1. Install BepInEx 6 IL2CPP into the game and run the game once (creates `BepInEx\interop`). See the
   [wiki](https://dicemodders.github.io/DiceKingdoms-Modding-Wiki/guides/mod-installation/).
2. Install the .NET SDK and Git.
3. `.\scripts\build.ps1 -GameDir "<game folder>"` - links the interop folder, builds everything and copies the DLLs
   into `BepInEx\plugins`. Later runs: `.\scripts\build.ps1` (add `-Run` to start the game, or use `.\scripts\watch.ps1`).

**Never commit game files** (`interop\`, `GameAssembly.dll`, dumps, decompiled code). `.gitignore` already excludes `interop`.

## Add a new mod

```powershell
.\scripts\new-mod.ps1 -Name Economy -Title "DiceKingdoms Economy"
.\scripts\build.ps1
```

This creates `src\DiceModders.Economy\` with a working skeleton (hotkeys, in-game window, patch helper), adds it to the
solution, and CI picks it up automatically - no workflow changes needed.

Rules of thumb:

* Hook game methods **by name** with `Patcher` (no RVAs). Read/write raw fields only when the interop does not expose
  them, and note the dump.cs offset in a comment (offsets change with game updates, names rarely do).
* Anything in `src/Shared` is compiled into every plugin as `internal`. Keep injected `MonoBehaviour` classes unique per
  plugin (`<Name>Ticker`).
* Default hotkeys: Right Ctrl + key, no numpad, not F1/F2/F3/F10 (the game uses them).
* Anything that changes the shared simulation (the game compares per-tick hashes between players) must say so in its
  config description and the README: every player in a lobby needs the same settings.

## Releases

Bump the `Version` constant in the plugins that changed, then tag:

```powershell
git tag v1.1.0
git push origin v1.1.0
```

CI builds, creates `<repo-name>-v1.1.0.zip` (unzip into the game folder) and attaches it plus the single DLLs to a
GitHub release.

## CI runner (maintainers)

CI needs the game's interop DLLs, so it runs on a self-hosted Windows runner (the `dicekingdoms` label) on a PC with
the game and BepInEx 6 installed:

1. Org or repo **Settings -> Actions -> Runners -> New self-hosted runner -> Windows**; add the label `dicekingdoms`.
2. Install PowerShell 7 (`winget install Microsoft.PowerShell`).
3. In the runner folder create a file named `.env` containing
   `DICEKINGDOMS_DIR=D:\SteamLibrary\steamapps\common\Dice-Kingdoms`, then restart the runner.
4. Settings -> Actions -> General: set "Fork pull request workflows" to require approval, and keep forks off this runner.
