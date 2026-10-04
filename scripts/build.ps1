<#
.SYNOPSIS
  Builds all DiceKingdoms plugins and copies the DLLs into the game's BepInEx\plugins folder.

.EXAMPLE
  .\scripts\build.ps1 -GameDir "D:\SteamLibrary\steamapps\common\Dice-Kingdoms"   # first time: remembers the path
  .\scripts\build.ps1                                                             # afterwards
  .\scripts\build.ps1 -Run                                                        # build, copy and start the game

  If PowerShell refuses to run scripts:  powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
#>
param(
    [string]$GameDir,
    [ValidateSet("Release", "Debug")][string]$Configuration = "Release",
    [switch]$Run
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$gameDirFile = Join-Path $root ".gamedir"      # git-ignored; remembers your game folder

# --- find the game folder: parameter > saved file > DICEKINGDOMS_DIR environment variable ---------------
if ($GameDir) { Set-Content -Path $gameDirFile -Value $GameDir -Encoding UTF8 }
elseif (Test-Path $gameDirFile) { $GameDir = (Get-Content $gameDirFile -Raw).Trim() }
elseif ($env:DICEKINGDOMS_DIR) { $GameDir = $env:DICEKINGDOMS_DIR }
if (-not $GameDir) { throw "Game folder unknown. Run once:  .\scripts\build.ps1 -GameDir `"D:\...\Dice-Kingdoms`"" }

$exe = Join-Path $GameDir "Dice-Kingdoms.exe"
$plugins = Join-Path $GameDir "BepInEx\plugins"
if (-not (Test-Path $exe)) { throw "Dice-Kingdoms.exe not found in '$GameDir'." }
if (-not (Test-Path $plugins)) { throw "'$plugins' does not exist. Install BepInEx 6 IL2CPP and run the game once first." }

# --- make sure the interop link exists --------------------------------------------------------------------
if (-not (Test-Path (Join-Path $root "interop\UnityEngine.CoreModule.dll"))) {
    & (Join-Path $PSScriptRoot "link-interop.ps1") $GameDir
}

# --- build ---------------------------------------------------------------------------------------------------
Push-Location $root
try {
    dotnet build DiceKingdoms.Mods.sln -c $Configuration --nologo -v q "-p:NoWarn=NU1603"
    if ($LASTEXITCODE -ne 0) { throw "Build failed (see errors above)." }
} finally { Pop-Location }

# --- copy ---------------------------------------------------------------------------------------------------
if (Get-Process -Name "Dice-Kingdoms" -ErrorAction SilentlyContinue) {
    Write-Warning "The game is running: Windows may refuse to overwrite loaded DLLs. Close the game, then run this again."
}
$copied = 0
Get-ChildItem (Join-Path $root "src") -Recurse -Filter "DiceModders.*.dll" |
    Where-Object { $_.FullName -match "\\bin\\$Configuration\\" } |
    ForEach-Object {
        try { Copy-Item $_.FullName $plugins -Force; $copied++; Write-Host "  copied $($_.Name)" }
        catch { Write-Warning "Could not copy $($_.Name): $($_.Exception.Message)" }
    }
Write-Host "Done: $copied plugin(s) copied to $plugins" -ForegroundColor Green

if ($Run) {
    Write-Host "Starting the game..."
    Start-Process -FilePath $exe -WorkingDirectory $GameDir
}
