<#
.SYNOPSIS
  Creates a new plugin in this repo (src\DiceModders.<Name>), wired up with the shared hotkeys / in-game window /
  patch helpers, and adds it to the solution. CI then builds and releases it automatically.

.EXAMPLE
  .\scripts\new-mod.ps1 -Name Economy -Title "DiceKingdoms Economy"
  (PowerShell blocked?  powershell -ExecutionPolicy Bypass -File .\scripts\new-mod.ps1 -Name Economy)
#>
param(
    [Parameter(Mandatory = $true)][ValidatePattern('^[A-Z][A-Za-z0-9]+$')][string]$Name,   # PascalCase, e.g. Economy
    [string]$Title,                                                                          # shown in the log and window title
    [string]$Description = "A Dice Kingdoms mod"
)
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
if (-not $Title) { $Title = "DiceKingdoms $Name" }
$dir = Join-Path $root "src\DiceModders.$Name"
if (Test-Path $dir) { throw "src\DiceModders.$Name already exists." }
New-Item -ItemType Directory -Path $dir | Out-Null

$guid = "org.dicemodders.dicekingdoms." + $Name.ToLowerInvariant()
$fill = { param($text) $text.Replace("__NAME__", $Name).Replace("__TITLE__", $Title).Replace("__GUID__", $guid).Replace("__DESC__", $Description) }

$csproj = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <AssemblyName>DiceModders.__NAME__</AssemblyName>
    <RootNamespace>DiceModders.__NAME__</RootNamespace>
    <Description>__DESC__</Description>
  </PropertyGroup>
</Project>
'@

$plugin = @'
using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using DiceModders.Shared;
using UnityEngine;

namespace DiceModders.__NAME__;

[BepInPlugin(Guid, Name, Version)]
[BepInProcess("Dice-Kingdoms.exe")]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "__GUID__";
    public const string Name = "__TITLE__";
    public const string Version = "1.0.0";

    internal static ManualLogSource L;
    internal static Hotkeys Keys;
    internal static Toggle Example;

    public override void Load()
    {
        L = Log;

        // A runtime on/off switch. Flip() is bound to a hotkey and to a button in the in-game window.
        Example = new Toggle("Example feature", false, L);

        // Window title, default window position (x, y). Hotkey = Right Ctrl + key; avoid the numpad and F1/F2/F3/F10.
        Keys = new Hotkeys(Config, L, Name, 20, 20);
        Keys.Bind(Config, "Example", HotkeyKey.None, "Toggle: example feature", Example.Flip, "Example feature", () => Example.On);
        // Keys.AddValue("Some number", 0, 100, () => 5, v => { }, null);   // slider + -100/-10/-1/+1/+10/+100 buttons
        // Keys.AddInfo(2, () => "Some text\nshown in the window");

        // Hook game methods by NAME (no offsets needed). See Patches.cs.
        var patcher = new Patcher(Guid, L);
        // patcher.Patch("SomeGameType", "SomeMethod", typeof(Patches), prefix: nameof(Patches.SomePrefix));
        patcher.Summary();

        Keys.LogBindings();
        AddComponent<__NAME__Ticker>();
    }
}

// Must have a name that is unique across ALL plugins (Unity registers injected classes by name).
public sealed class __NAME__Ticker : MonoBehaviour
{
    public __NAME__Ticker(IntPtr ptr) : base(ptr) { }
    private void Update() => Plugin.Keys?.Poll();       // hotkeys, on Unity's main thread
    private void OnGUI() => Plugin.Keys?.DrawGui();     // the in-game window
}
'@

$patches = @'
namespace DiceModders.__NAME__;

internal static class Patches
{
    // Harmony patch methods must be static. Special parameters: object __instance, ref <type> __result,
    // object[] __args (all arguments), bool return value on a prefix (false = skip the original method).
    //
    // internal static bool SomePrefix(object __instance, ref bool __result)
    // {
    //     if (!Plugin.Example.On) return true;
    //     __result = true;
    //     return false;
    // }
}
'@

Set-Content -Path (Join-Path $dir "DiceModders.$Name.csproj") -Value (& $fill $csproj) -Encoding UTF8
Set-Content -Path (Join-Path $dir "Plugin.cs") -Value (& $fill $plugin) -Encoding UTF8
Set-Content -Path (Join-Path $dir "Patches.cs") -Value (& $fill $patches) -Encoding UTF8

Push-Location $root
try { dotnet sln DiceKingdoms.Mods.sln add "src\DiceModders.$Name\DiceModders.$Name.csproj" } finally { Pop-Location }

Write-Host "Created src\DiceModders.$Name (GUID $guid)." -ForegroundColor Green
Write-Host "Next: edit src\DiceModders.$Name\Plugin.cs, then  .\scripts\build.ps1  to build and copy it into the game."
