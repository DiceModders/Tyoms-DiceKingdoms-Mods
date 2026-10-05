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

## CI and the private interop repo (maintainers)

The plugins compile against Unity / Il2Cpp interop DLLs that BepInEx generates from the game. They can not be in this
public repo, so CI downloads them from a **private** repo (`Tyoms-DiceKingdoms-Interop`; the name is set in
`.github/workflows/build.yml` as `INTEROP_REPO`) using a read-only deploy key stored as the secret `INTEROP_DEPLOY_KEY`.
That repo holds only the Unity / Il2Cpp / system interop DLLs - **not** the game's own assemblies (`Assembly-CSharp`
etc.), because our code finds game types by name at runtime.

Rules: keep that repo private with forking disabled, give it to as few people as possible, and keep write access to this
repo to trusted maintainers (a workflow can read the secret). Pull requests from forks get no secrets, so CI skips them;
a maintainer can review the change and push it to a branch of this repo to get a build. Never switch the workflow to
`pull_request_target`.

**One-time setup**

1. Create the private repo in the org (forking off), clone it next to this one, and fill it:
   `.\scripts\export-interop.ps1 -GameDir "<game folder>" -OutDir "<clone folder>"`, then commit and push.
2. Create a deploy key *without* a passphrase:
   `cmd /c 'ssh-keygen -t ed25519 -C dicemodders-ci -f "%TEMP%\interop_ci" -N ""'`
3. Private repo -> Settings -> Deploy keys -> Add: paste `interop_ci.pub`, leave "Allow write access" **off**.
4. This repo -> Settings -> Secrets and variables -> Actions -> New repository secret `INTEROP_DEPLOY_KEY`: paste the
   whole content of the private file `interop_ci` (including the BEGIN/END lines). Then delete both key files.
5. Settings -> Actions -> General: require approval for workflows from outside collaborators.

**After a game update:** start the game once so BepInEx regenerates `BepInEx\interop`, run `export-interop.ps1` into the
clone again, commit and push. The next CI run uses it.
