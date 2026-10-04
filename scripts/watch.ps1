<#
.SYNOPSIS
  Auto-build: watches the source files and runs build.ps1 (build + copy to the game) every time you save.
  Stop with Ctrl+C. Close the game before saving, otherwise the DLLs are locked and cannot be copied.

.EXAMPLE
  .\scripts\watch.ps1
  powershell -ExecutionPolicy Bypass -File .\scripts\watch.ps1
#>
param(
    [string]$GameDir,
    [ValidateSet("Release", "Debug")][string]$Configuration = "Release"
)
$root = Split-Path $PSScriptRoot -Parent
$buildArgs = @{ Configuration = $Configuration }
if ($GameDir) { $buildArgs.GameDir = $GameDir }

function Get-SourceStamp {
    $files = Get-ChildItem $root -Recurse -Include *.cs, *.csproj, Directory.Build.props, Directory.Build.targets -File |
        Where-Object { $_.FullName -notmatch "\\(obj|bin|interop|\.git)\\" }
    ($files | Measure-Object -Property LastWriteTimeUtc -Maximum).Maximum
}

Write-Host "Watching $root\src for changes - Ctrl+C to stop." -ForegroundColor Cyan
$last = $null
while ($true) {
    $stamp = Get-SourceStamp
    if ($stamp -ne $last) {
        Start-Sleep -Milliseconds 800            # let the editor finish saving
        $stamp = Get-SourceStamp
        Write-Host "`n[$(Get-Date -Format T)] change detected - building..." -ForegroundColor Cyan
        try {
            & (Join-Path $PSScriptRoot "build.ps1") @buildArgs
            Write-Host "[$(Get-Date -Format T)] OK" -ForegroundColor Green
        } catch {
            Write-Host "[$(Get-Date -Format T)] FAILED: $($_.Exception.Message)" -ForegroundColor Red
        }
        $last = $stamp
    }
    Start-Sleep -Seconds 1
}
