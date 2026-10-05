# DiceKingdoms mods (BepInEx)

BepInEx 6 (IL2CPP) plugins for **Dice Kingdoms**, split by purpose so players can install only what they want.
Ported from the original native `GameAssembly.dll` proxy loader + `DiceMod.dll`.

| Plugin | DLL | What it is for |
|---|---|---|
| **Cheats** | `DiceModders.Cheats.dll` | Changes what you're allowed to do: game dev console unlock, unlock all buildings, build anywhere (risky), force lobby ready |
| **UI Tweaks** | `DiceModders.UiTweaks.dll` | Declutter: auto-clear text chat, auto-clear event log, hide scoreboard attacker flags |
| **Performance** | `DiceModders.Performance.dll` | Skip combat particles; frame-time probe for hunting lag |
| **Disasters** | `DiceModders.Disasters.dll` | Meteorite vs. castle: slider for the "meteorite tiles needed for a certain castle hit" value, and a switch that lets even the first meteor hit the castle (**changes the simulation - every player must use the same settings**) |
| **Island Tools** | `DiceModders.IslandTools.dll` | Save/load islands via the clipboard, pick another player's island (host experiment), building footprint dump, island land radius |

Plugins are independent: install any subset. Each has its own config file in `BepInEx/config/`.

## Installing (players)

1. Install BepInEx 6 for IL2CPP and run the game once - see the wiki's
   [Mods Installation](https://dicemodders.github.io/DiceKingdoms-Modding-Wiki/guides/mod-installation/) guide.
2. Copy the plugin DLL(s) you want into `<game folder>/BepInEx/plugins/`.
3. Run the game. Config files appear in `BepInEx/config/` after the first launch.
4. Remove the old proxy loader first: delete the proxy `GameAssembly.dll`, restore `GameAssembly_original.dll`
   back to `GameAssembly.dll`, and delete the old `mods/DiceMod.dll`. BepInEx replaces all of that.

## Hotkeys

Hold **Right Ctrl** (configurable, `[Hotkeys] Modifier`) and press the key. Hotkeys only fire while the game window
is focused. Every key is rebindable in the plugin's config; set it to `None` to unbind.
F1, F2, F3 and F10 are used by the game - don't bind them.

| Key | Plugin | Action |
|---|---|---|
| F7 | Cheats | Toggle: all buildings unlocked |
| PageDown | Cheats | Toggle: ignore placement checks (**RISKY**) |
| Delete | Cheats | Toggle: force lobby "ready" (untested, host only) |
| F5 | UI Tweaks | Toggle: auto-clear text chat |
| F6 | UI Tweaks | Toggle: auto-clear event log |
| End | UI Tweaks | Toggle: clear attacker flags on scoreboard |
| F8 | Performance | Toggle: no unit combat particles |
| PageUp | Performance | Toggle: performance probe |
| P | Disasters | Toggle: castle protection OFF (any meteor, even the first, may hit the castle) |
| - / = | Disasters | Meteorite value T  -1 / +1  (hold **Left Shift** for x10, **Left Alt** for x100) |
| (unbound) | Disasters | T back to the game's value; print current values (use the window buttons or bind a key) |
| K | Island Tools | Save your island to clipboard (experimental) |
| L | Island Tools | Load island from clipboard onto your island (experimental) |
| (unbound) | Island Tools | Cycle selected player (window button) |
| (unbound) | Island Tools | Load clipboard onto the selected player's island (host only, **RISKY**, untested; window button) |
| F11 | Island Tools | Dump building types + footprints to `BepInEx/DiceKingdoms_buildings.txt` |
| **Insert** | every plugin | Show / hide that plugin's **in-game menu panel** (see below) |
| **Home** | every plugin | Print that plugin's hotkeys (with current ON/OFF state) to the console / log |

Other settings: Cheats `[DevConsole] Unlock`, UI Tweaks `ClearIntervalMs`, Island Tools `[Island] HardLimitRadius`
(default 24, vanilla 22, 0 = off; **all lobby players must use the same value**), and a `[Startup]` section in each
plugin to start with toggles already on. Toggles are runtime-only (flipping one with a hotkey is not saved), so a
risky feature never stays on across restarts by accident.

### In-game windows

Press **Right Ctrl + Insert** to show / hide the windows of all installed plugins together. Each plugin has its own
window, like a KSP mod:

* **drag the title bar** to move it,
* **drag the `::` corner** (bottom right) to resize it - rows scroll if it gets too small,
* **`-` / `+`** collapses it to just the title bar, **`x`** hides it (Insert brings it back).

Each row is a button that does the same thing as the hotkey and shows `[ON]` / `[OFF]` for toggles. Position, size and
collapsed state are saved in the plugin's `[Gui]` config section (`ShowOnStart`, `Scale`, `PanelX/Y`, `PanelWidth`,
`PanelHeight`, `Collapsed`). The windows use Unity's IMGUI, so a click on them can also reach the game underneath -
use the hotkeys if that gets in the way.

### How the meteor / castle rule works

From the decompiled `Meteorite.Execute`: the game counts the meteorite ground tiles already on the target island and
rolls a random integer in `[0, T)` where `T` = `meteoriteTilesToFullyAllowHitCastle` (128 in the game). A random impact
spot that overlaps the castle (radius 4) is only accepted when `roll < meteorite tiles on the island`, so the chance of a
castle hit is about `tiles / T`. With 0 tiles (the first fall) it is zero **whatever T is**; T only controls how fast the
chance ramps up (a radius-4 impact leaves up to 49 tiles). The pickaxe removes meteorite tiles, which lowers the chance
again. The Disasters plugin can change T (slider / buttons / `[Meteorite] TilesToFullyAllowHitCastle`) or switch the
protection off completely.

### Hiding the BepInEx console

Edit `BepInEx/config/BepInEx.cfg` and set, under `[Logging.Console]`:

```
Enabled = false
```

Everything is still written to `BepInEx/LogOutput.log`, and the in-game menu keeps working.

Logs go to the BepInEx console / `BepInEx/LogOutput.txt` (the old `DiceMod_log.txt` and custom console are gone).
At startup each plugin logs every patch as `[patch] OK` / `FAIL` and lists its active hotkeys.

## Building (developers)

Follows the wiki's [Project Setup](https://dicemodders.github.io/DiceKingdoms-Modding-Wiki/guides/modding/project-setup/).

1. Install BepInEx 6 IL2CPP into the game and run it once so `BepInEx/interop/` is generated.
2. Install the .NET SDK (6.0 or newer).
3. Link the interop folder into the repo root (it is git-ignored):

   ```powershell
   mklink /D "C:\path\to\repo\interop" "C:\path\to\game\BepInEx\interop"
   ```
   ```bash
   ln -s "/path/to/game/BepInEx/interop" "/path/to/repo/interop"
   ```
4. **Easiest:** use the scripts (they build *and* copy the DLLs into the game):

   ```powershell
   .\scripts\build.ps1 -GameDir "D:\SteamLibrary\steamapps\common\Dice-Kingdoms"   # first time (remembered in .gamedir)
   .\scripts\build.ps1                # build + copy
   .\scripts\build.ps1 -Run           # build + copy + start the game
   .\scripts\watch.ps1                # auto build + copy every time you save a file (close the game first)
   ```
   In VS Code the same things are under **Terminal -> Run Build Task** (`Ctrl+Shift+B`) and **Run Task**.
   If PowerShell blocks scripts, prefix with `powershell -ExecutionPolicy Bypass -File`.

   Or by hand - build everything (or one project):

   ```bash
   dotnet new sln -n DiceKingdoms.Mods   # optional, once
   dotnet sln add src/*/*.csproj
   dotnet build -c Release
   ```
   Output: `src/<Plugin>/bin/Release/net6.0/DiceModders.<Plugin>.dll`.
5. Optional auto-copy into the game on every build:

   ```bash
   dotnet build -p:DiceKingdomsDir="C:\path\to\game"
   ```

### Layout

```
Directory.Build.props      shared settings: net6.0, BepInEx.Unity.IL2CPP package, interop/*.dll reference
Directory.Build.targets    optional copy-to-game step
src/Shared/                small helpers compiled into EVERY plugin as internal types
    Hotkeys.cs  Overlay.cs (in-game menu)  Toggle.cs  Patcher.cs  GameTypes.cs  Reflect.cs  Mem.cs
scripts/                   build.ps1 (build + copy)  watch.ps1 (auto build)  link-interop.ps1
.vscode/tasks.json         the same actions as VS Code tasks
src/DiceModders.Cheats/        Plugin.cs (config, hotkeys, patch list)  Patches.cs
src/DiceModders.UiTweaks/      Plugin.cs  Patches.cs
src/DiceModders.Performance/   Plugin.cs  Patches.cs
src/DiceModders.IslandTools/   Plugin.cs  Patches.cs  State.cs  IslandOps.cs  Clipboard.cs  BuildingDump.cs
```

Shared code is *compiled into* each plugin rather than shipped as a separate DLL, so players never have to install a
"core" mod. Because each plugin ships its own copy, Shared types must stay `internal`, and every injected
`MonoBehaviour` (`CheatsTicker`, ...) has a unique name per plugin.

To split into separate repos later, copy `Directory.Build.*` and `src/Shared/` into each repo (or turn Shared into a git
submodule / NuGet package).

## How the port works

* **No RVAs, no MinHook.** Methods are patched with Harmony by *type name + method name* (`Patcher`), so a game update
  that moves code around does not break them. Types are looked up by name in the interop assemblies (`GameTypes`).
* **Main thread only.** Each plugin adds one tiny `MonoBehaviour` whose `Update` polls hotkeys. This replaces the old
  hotkey thread, log thread, request flags and `AllocConsole` console.
* **Field offsets still exist** for a few members (`Mem`): `IslandGrid.generationParameters` `+0x1E8`,
  `PlayerSummary.island` `+0xD0` / `attackers` `+0xE8`, `Island.grid` `+0x98`, `BuildingType` fields,
  `RulesBuilding.BuildingTypes` `+0x18`. These come from the wiki's
  [Internals Reference](https://dicemodders.github.io/DiceKingdoms-Modding-Wiki/reference/internals/) and are
  **build-specific** - re-check them after a game update.
* Game methods are called by name through reflection on the interop wrappers (`Reflect`), including the private
  `IslandGrid.ImportIsland`.

### Changes from the old native mod

| Old | Now |
|---|---|
| Proxy `GameAssembly.dll` + `mods/DiceMod.dll` | BepInEx plugins in `BepInEx/plugins/` |
| Console window, INSERT to show/hide, HOME for help | BepInEx console (enable in `BepInEx.cfg`); hotkeys are printed at startup |
| `DiceMod_log.txt` | `BepInEx/LogOutput.txt` |
| `DiceMod_buildings.txt` next to the DLL | `BepInEx/DiceKingdoms_buildings.txt` |
| Heap scan for `BuildingType[]` | Removed. The dump now needs the `BuildingTypes` asset (captured via `RulesBuilding.OnEnable` / `get_Types`) |
| F4 "pickaxe clears any nature" (`NatureTile.Cleanable` hook) | **Dropped.** The wiki confirms `CanRemoveNature` does not call `Cleanable`, so the hook did nothing. A real version needs a patch on `BuildData.CanRemoveNature` (RVA `0x6288B0`, inline check) - see TODO |
| `ChatInterface`/`LogInterface` RVA hooks | Same methods, patched by name |

## Known limitations / TODO

* **Not compiled against the real interop assemblies yet.** The code type-checks against stubs of the BepInEx / Harmony /
  Il2CppInterop APIs, but game type and member names were taken from the old mod and the wiki. First build + first run
  is the real test; failing patches are reported precisely in the log.
* Harmony can only patch methods the game actually calls through IL2CPP; methods inlined into other native code never
  fire (the same limit as the old native hooks).
* Harmony prefix/postfix patches on IL2CPP methods require a recent BepInEx 6 build.
* Pickaxe-anywhere (see table above) is not ported.
* The old "load island onto a selected player" and "build anywhere" features remain **experimental/risky**: both touch
  networked state. Test alone first.
* `IslandGrid` captured by `Awake` is simply the *last* one created; with several players it is not guaranteed to be yours.
* Windows only (hotkeys use `user32`; the game is Windows-only).

## Repository workflow

* **Add a mod:** `.\scripts\new-mod.ps1 -Name Economy` creates `src/DiceModders.Economy` and adds it to the solution; CI
  builds and releases it automatically.
* **Release:** `git tag v1.1.0 && git push origin v1.1.0` - CI attaches `<repo-name>-v1.1.0.zip` (unzip into the
  game folder) and the single DLLs to a GitHub release.
* More in [CONTRIBUTING.md](CONTRIBUTING.md) (setup, rules, CI and the private interop repo).
