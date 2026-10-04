using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using DiceModders.Shared;
using UnityEngine;

namespace DiceModders.Cheats;

/// <summary>
/// Things that change what you are allowed to do: the game's own dev console, building
/// unlocks, placement rules and the lobby Start button. Several of these are risky in
/// multiplayer - see the README before enabling them in a shared lobby.
/// </summary>
[BepInPlugin(Guid, Name, Version)]
[BepInProcess("Dice-Kingdoms.exe")]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "org.dicemodders.dicekingdoms.cheats";
    public const string Name = "DiceKingdoms Cheats";
    public const string Version = "1.0.0";

    internal static ManualLogSource L;
    internal static Hotkeys Keys;

    internal static ConfigEntry<bool> DevConsole;
    internal static Toggle UnlockAllBuildings;
    internal static Toggle BuildAnywhere;
    internal static Toggle ForceLobbyReady;

    public override void Load()
    {
        L = Log;

        DevConsole = Config.Bind("DevConsole", "Unlock", true,
            "Force Cheats.Available / Cheats.CheckAvailable to true so the game's own dev console " +
            "(including saveisland / loadisland) can be used.");

        UnlockAllBuildings = new Toggle("Unlock all buildings",
            Config.Bind("Startup", "UnlockAllBuildings", false, "Start with every building unlocked.").Value, L);
        BuildAnywhere = new Toggle("Build anywhere (RISKY)",
            Config.Bind("Startup", "BuildAnywhere", false,
                "Start with island placement checks bypassed. RISKY: may write outside the island grid " +
                "and, because placement is networked, could crash everyone in the lobby.").Value, L);
        ForceLobbyReady = new Toggle("Force lobby ready (untested)",
            Config.Bind("Startup", "ForceLobbyReady", false,
                "Start with every player reported as 'ready' so the lobby Start button unlocks. Untested; host only.").Value, L);

        Keys = new Hotkeys(Config, L, "DiceKingdoms Cheats", 20, 20);
        Keys.Bind(Config, "UnlockAllBuildings", HotkeyKey.F7, "Toggle: all buildings unlocked", UnlockAllBuildings.Flip, "Unlock all buildings", () => UnlockAllBuildings.On);
        Keys.Bind(Config, "BuildAnywhere", HotkeyKey.PageDown, "Toggle: ignore island placement checks (RISKY)", BuildAnywhere.Flip, "Build anywhere (RISKY)", () => BuildAnywhere.On);
        Keys.Bind(Config, "ForceLobbyReady", HotkeyKey.Delete, "Toggle: force lobby 'ready' (untested, host only)", ForceLobbyReady.Flip, "Force lobby ready (host, untested)", () => ForceLobbyReady.On);

        var patcher = new Patcher(Guid, L);
        patcher.Patch("Cheats", "get_Available", typeof(Patches), postfix: nameof(Patches.ForceTrue));
        patcher.Patch("Cheats", "CheckAvailable", typeof(Patches), postfix: nameof(Patches.ForceTrue));
        patcher.Patch("BuildData", "Unlocked", typeof(Patches), prefix: nameof(Patches.UnlockedPrefix));
        patcher.Patch("IslandGrid", "CanPlaceBuilding", typeof(Patches), prefix: nameof(Patches.CanPlaceBuildingPrefix));
        patcher.Patch("Player", "get_StartGameReady", typeof(Patches), prefix: nameof(Patches.StartGameReadyPrefix));
        patcher.Summary();

        Keys.LogBindings();
        AddComponent<CheatsTicker>();
    }

    internal static void Tick() => Keys?.Poll();
}

/// <summary>Per-frame hook so hotkeys are polled on Unity's main thread.</summary>
public sealed class CheatsTicker : MonoBehaviour
{
    public CheatsTicker(IntPtr ptr) : base(ptr) { }
    private void Update() => Plugin.Tick();
    private void OnGUI() => Plugin.Keys?.DrawGui();
}
