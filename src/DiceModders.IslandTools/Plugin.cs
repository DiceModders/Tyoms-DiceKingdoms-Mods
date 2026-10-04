using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using DiceModders.Shared;
using UnityEngine;

namespace DiceModders.IslandTools;

/// <summary>
/// Island-focused tools: save/load an island through the real Windows clipboard, pick another
/// player's island as a target (host experiment), dump building footprints for island editors,
/// and set the island's land radius.
/// </summary>
[BepInPlugin(Guid, Name, Version)]
[BepInProcess("Dice-Kingdoms.exe")]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "org.dicemodders.dicekingdoms.islandtools";
    public const string Name = "DiceKingdoms Island Tools";
    public const string Version = "1.0.0";

    internal static ManualLogSource L;
    internal static Hotkeys Keys;

    internal static ConfigEntry<int> IslandRadius;
    internal static ConfigEntry<bool> AutoDumpBuildings;
    internal static ConfigEntry<string> LocalPlayerName;
    internal static ConfigEntry<bool> BypassPlacementWhileImporting;

    public override void Load()
    {
        L = Log;

        IslandRadius = Config.Bind("Island", "HardLimitRadius", 24,
            new ConfigDescription(
                "Land radius (HardLimit.radius) applied when an island is generated. Vanilla is 22; 0 = leave the game's value alone. " +
                "EVERY player in the lobby must use the same value, and values above ~24-26 approach the edge of the " +
                "allocated tile arrays (danger zone) - raise it in small steps.",
                new AcceptableValueRange<int>(0, 40)));
        LocalPlayerName = Config.Bind("Island", "LocalPlayerName", "",
            "Your player name as shown in the match (e.g. your Steam name). Leave empty to detect it automatically; " +
            "set it if 'Save/Load my island' reports that it cannot tell which player is you.");
        BypassPlacementWhileImporting = Config.Bind("Island", "BypassPlacementWhileImporting", true,
            "While an island code is being imported (and only then), treat every building placement as valid. " +
            "Without this the game asserts on buildings it thinks do not fit the CURRENT terrain (typically docks, or an " +
            "editor-made castle) and aborts the import halfway.");
        AutoDumpBuildings = Config.Bind("Buildings", "AutoDump", true,
            "Write BepInEx/DiceKingdoms_buildings.txt automatically the first time building types are seen in a match.");

        Keys = new Hotkeys(Config, L, "DiceKingdoms Island Tools", 20, 360);
        Keys.Bind(Config, "SaveIsland", HotkeyKey.K, "Save YOUR island to the clipboard (experimental)", IslandOps.SaveOwn, "Save my island to clipboard");
        Keys.Bind(Config, "LoadIsland", HotkeyKey.L, "Load an island from the clipboard onto your island (experimental)", IslandOps.LoadOwn, "Load island from clipboard");
        Keys.Bind(Config, "CyclePlayer", HotkeyKey.None, "Cycle which tracked player is selected", IslandOps.CyclePlayer, "Select next player");
        Keys.AddInfo(1, () => "Selected player: " + (State.SelectedName ?? "(none yet - use Select next player)"));
        Keys.Bind(Config, "LoadOnSelectedPlayer", HotkeyKey.None, "Load the clipboard onto the SELECTED player's island (host only, RISKY / untested)", IslandOps.LoadOnSelected, "Load clipboard onto selected player (RISKY)");
        Keys.Bind(Config, "DumpBuildings", HotkeyKey.F11, "Dump building types + footprints to BepInEx/DiceKingdoms_buildings.txt", BuildingDump.Run, "Dump building list to file");

        var patcher = new Patcher(Guid, L);
        patcher.Patch("IslandGrid", "Awake", typeof(Patches), prefix: nameof(Patches.IslandGridAwakePrefix));
        patcher.Patch("ChatInterface", "Awake", typeof(Patches), prefix: nameof(Patches.NewMatchPrefix));
        patcher.Patch("PlayerSummary", "Update", typeof(Patches), postfix: nameof(Patches.SummaryPostfix));
        patcher.Patch("PlayerSummary", "OnSyncPhase", typeof(Patches), postfix: nameof(Patches.SummaryPostfix));
        patcher.Patch("BuildData", "Unlocked", typeof(Patches), prefix: nameof(Patches.UnlockedPrefix));
        patcher.Patch("RulesBuilding", "OnEnable", typeof(Patches), postfix: nameof(Patches.RulesBuildingPostfix));
        patcher.Patch("BuildingTypes", "get_Types", typeof(Patches), postfix: nameof(Patches.GetTypesPostfix));
        patcher.Patch("IslandGrid", "CanPlaceBuilding", typeof(Patches), postfix: nameof(Patches.CanPlacePostfix));
        patcher.Summary();

        Keys.LogBindings();
        AddComponent<IslandToolsTicker>();
    }

    internal static void Tick()
    {
        Keys?.Poll();
        BuildingDump.AutoTick();
    }
}

public sealed class IslandToolsTicker : MonoBehaviour
{
    public IslandToolsTicker(IntPtr ptr) : base(ptr) { }
    private void Update() => Plugin.Tick();
    private void OnGUI() => Plugin.Keys?.DrawGui();
}
