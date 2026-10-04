using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using DiceModders.Shared;
using UnityEngine;

namespace DiceModders.Disasters;

/// <summary>
/// Rule tweaks for disasters. These change the SIMULATION (the game compares per-tick hashes between players), so every
/// player in a lobby must use the same values, or the game will report a desync.
/// </summary>
[BepInPlugin(Guid, Name, Version)]
[BepInProcess("Dice-Kingdoms.exe")]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "org.dicemodders.dicekingdoms.disasters";
    public const string Name = "DiceKingdoms Disasters";
    public const string Version = "1.0.0";

    internal static ManualLogSource L;
    internal static Hotkeys Keys;

    public override void Load()
    {
        L = Log;

        Meteor.Setting = Config.Bind("Meteorite", "TilesToFullyAllowHitCastle", -1,
            new ConfigDescription(
                "Value forced into Meteorite.meteoriteTilesToFullyAllowHitCastle (T, the game uses 128). -1 = use the game's own value. " +
                "The chance that a meteor may hit the castle is (meteorite tiles on the island) / T, so a LOWER T exposes the castle after " +
                "fewer impacts and a higher T protects it longer. T cannot make the FIRST fall dangerous (0 tiles = chance 0) - use " +
                "CastleProtectionOff for that. Every player in the lobby must use the same value.",
                new AcceptableValueRange<int>(-1, Meteor.SliderMax)));
        if (Meteor.Setting.Value >= 0) Meteor.Override = Meteor.Setting.Value;

        Meteor.ProtectionOff = new Toggle("Meteor castle protection OFF",
            Config.Bind("Startup", "CastleProtectionOff", false,
                "Start with the meteor castle protection switched off: every meteor may hit the castle, even the first. " +
                "Every player in the lobby must use the same setting.").Value, L);

        Keys = new Hotkeys(Config, L, "DiceKingdoms Disasters", 460, 20);
        Keys.Bind(Config, "CastleProtectionOff", HotkeyKey.P, "Toggle: any meteor may hit the castle, even the first one",
                  Meteor.ProtectionOff.Flip, "Castle protection OFF (first meteor can hit)", () => Meteor.ProtectionOff.On);
        Keys.AddValue("T = meteorite tiles for a certain castle hit", 0, Meteor.SliderMax, Meteor.Current, Meteor.Set, Meteor.Commit);
        Keys.AddInfo(4, Meteor.Info);
        Keys.Bind(Config, "CastleProtectionDown", HotkeyKey.Minus, "Meteorite T -1 (hold Left Shift for -10, Left Alt for -100)",
                  () => Meteor.Adjust(-1), "T -1", inMenu: false);
        Keys.Bind(Config, "CastleProtectionUp", HotkeyKey.Equal, "Meteorite T +1 (hold Left Shift for +10, Left Alt for +100)",
                  () => Meteor.Adjust(+1), "T +1", inMenu: false);
        Keys.Bind(Config, "CastleProtectionReset", HotkeyKey.None, "Meteorite: back to the game's value of T", Meteor.Reset, "T: back to the game's value");
        Keys.Bind(Config, "CastleProtectionShow", HotkeyKey.None, "Meteorite: print current values to the log", Meteor.Show, "Print meteor values", inMenu: false);

        var patcher = new Patcher(Guid, L);
        patcher.Patch("Meteorite", "Execute", typeof(Patches), prefix: nameof(Patches.ExecutePrefix));
        patcher.Patch("IslandGrid", "MeteoriteNumberCastleTiles", typeof(Patches), postfix: nameof(Patches.CastleTilesPostfix));
        patcher.Summary();

        Keys.LogBindings();
        L.LogWarning("Disaster rules change the simulation: every player in the lobby must use the same settings.");
        AddComponent<DisastersTicker>();
    }

    internal static void Tick() => Keys?.Poll();
}

public sealed class DisastersTicker : MonoBehaviour
{
    public DisastersTicker(IntPtr ptr) : base(ptr) { }
    private void Update() => Plugin.Tick();
    private void OnGUI() => Plugin.Keys?.DrawGui();
}
