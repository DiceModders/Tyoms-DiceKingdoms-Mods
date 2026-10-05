<#
.SYNOPSIS
  Copies the Unity / Il2Cpp interop DLLs from the game's BepInEx\interop folder into another folder (normally a local
  clone of the PRIVATE interop repo that CI downloads). The game's own assemblies (Assembly-CSharp etc.) are NOT copied:
  our plugins find game types by name at runtime, so the build does not need them.

.EXAMPLE
  .\scripts\export-interop.ps1 -OutDir "E:\Games\Game_modding\Tyoms-DiceKingdoms-Interop"      # game folder remembered by build.ps1
  .\scripts\export-interop.ps1 -GameDir "D:\SteamLibrary\steamapps\common\Dice-Kingdoms" -OutDir "E:\Games\Game_modding\Tyoms-DiceKingdoms-Interop"
  (PowerShell blocked?  powershell -ExecutionPolicy Bypass -File .\scripts\export-interop.ps1 ...)
#>
param(
    [string]$GameDir,
    [Parameter(Mandatory = $true)][string]$OutDir
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

# game folder: parameter > the one remembered by build.ps1 > DICEKINGDOMS_DIR
if (-not $GameDir) {
    $saved = Join-Path $root ".gamedir"
    if (Test-Path $saved) { $GameDir = (Get-Content $saved -Raw).Trim() }
    elseif ($env:DICEKINGDOMS_DIR) { $GameDir = $env:DICEKINGDOMS_DIR }
}
if (-not $GameDir) { throw 'Game folder unknown. Pass -GameDir "D:\...\Dice-Kingdoms".' }

# these files are game-derived: never write them inside the public repo
$fullOut = [System.IO.Path]::GetFullPath($OutDir)
if ($fullOut.StartsWith($root, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "OutDir must be OUTSIDE this repository (it should be your clone of the PRIVATE interop repo)."
}
$src = Join-Path $GameDir "BepInEx\interop"
if (-not (Test-Path (Join-Path $src "UnityEngine.CoreModule.dll"))) {
    throw "No BepInEx\interop\UnityEngine.CoreModule.dll in '$GameDir'. Install BepInEx 6 IL2CPP and run the game once first."
}
New-Item -ItemType Directory -Force $OutDir | Out-Null

# start clean (only DLLs are removed, the .git folder stays)
Get-ChildItem $OutDir -Filter *.dll -File | Remove-Item -Force

$patterns = "UnityEngine*.dll", "Unity.*.dll", "Il2Cpp*.dll", "System*.dll", "Microsoft*.dll", "mscorlib.dll", "netstandard.dll"
foreach ($pattern in $patterns) {
    Get-ChildItem $src -Filter $pattern -File | Copy-Item -Destination $OutDir -Force
}

$files = Get-ChildItem $OutDir -Filter *.dll -File
$mb = [math]::Round((($files | Measure-Object Length -Sum).Sum) / 1MB, 1)
$big = $files | Where-Object { $_.Length -gt 90MB }
Write-Host "Exported $($files.Count) DLL(s), $mb MB, to $OutDir" -ForegroundColor Green
Write-Host "Not exported (game code): " (@(Get-ChildItem $src -Filter *.dll -File | Where-Object { $files.Name -notcontains $_.Name } | ForEach-Object Name) -join ", ")
if ($big) { Write-Warning "GitHub rejects files over 100 MB: $($big.Name -join ', ')" }
