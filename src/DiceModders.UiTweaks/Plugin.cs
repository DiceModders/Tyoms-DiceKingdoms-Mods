using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using DiceModders.Shared;
using UnityEngine;

namespace DiceModders.UiTweaks;

/// <summary>Declutters the in-match UI: text chat, event log and scoreboard attacker flags.</summary>
[BepInPlugin(Guid, Name, Version)]
[BepInProcess("Dice-Kingdoms.exe")]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "org.dicemodders.dicekingdoms.uitweaks";
    public const string Name = "DiceKingdoms UI Tweaks";
    public const string Version = "1.0.0";

    internal static ManualLogSource L;
    internal static Hotkeys Keys;

    internal static Toggle AutoClearChat;
    internal static Toggle AutoClearLog;
    internal static Toggle ClearAttackers;
    internal static ConfigEntry<int> ClearIntervalMs;

    public override void Load()
    {
        L = Log;

        ClearIntervalMs = Config.Bind("General", "ClearIntervalMs", 500,
            new ConfigDescription("How often auto-clear runs, in milliseconds.", new AcceptableValueRange<int>(100, 10000)));

        AutoClearChat = new Toggle("Auto-clear text chat",
            Config.Bind("Startup", "AutoClearChat", false, "Start with auto-clear of the text chat enabled.").Value, L);
        AutoClearLog = new Toggle("Auto-clear event log",
            Config.Bind("Startup", "AutoClearEventLog", false, "Start with auto-clear of the event log enabled.").Value, L);
        ClearAttackers = new Toggle("Clear scoreboard attacker flags",
            Config.Bind("Startup", "ClearAttackerFlags", false, "Start with the scoreboard attacker flags hidden.").Value, L);

        Keys = new Hotkeys(Config, L, "DiceKingdoms UI Tweaks", 20, 140);
        Keys.Bind(Config, "AutoClearChat", HotkeyKey.F5, "Toggle: auto-clear text chat", AutoClearChat.Flip, "Auto-clear text chat", () => AutoClearChat.On);
        Keys.Bind(Config, "AutoClearEventLog", HotkeyKey.F6, "Toggle: auto-clear event log", AutoClearLog.Flip, "Auto-clear event log", () => AutoClearLog.On);
        Keys.Bind(Config, "ClearAttackerFlags", HotkeyKey.End, "Toggle: clear attacker flags on the scoreboard", ClearAttackers.Flip, "Hide scoreboard attacker flags", () => ClearAttackers.On);

        var patcher = new Patcher(Guid, L);
        patcher.Patch("ChatInterface", "Awake", typeof(Patches), prefix: nameof(Patches.ChatAwakePrefix));
        patcher.Patch("ChatInterface", "Update", typeof(Patches), postfix: nameof(Patches.ChatUpdatePostfix));
        patcher.Patch("LogInterface", "OnEnable", typeof(Patches), postfix: nameof(Patches.LogEnablePostfix));
        patcher.Patch("LogInterface", "OnDisable", typeof(Patches), postfix: nameof(Patches.LogDisablePostfix));
        patcher.Patch("PlayerSummary", "OnSyncPhase", typeof(Patches), postfix: nameof(Patches.SummaryPostfix));
        patcher.Patch("PlayerSummary", "Update", typeof(Patches), postfix: nameof(Patches.SummaryPostfix));
        patcher.Summary();

        Keys.LogBindings();
        AddComponent<UiTweaksTicker>();
    }

    internal static void Tick() => Keys?.Poll();
}

public sealed class UiTweaksTicker : MonoBehaviour
{
    public UiTweaksTicker(IntPtr ptr) : base(ptr) { }
    private void Update() => Plugin.Tick();
    private void OnGUI() => Plugin.Keys?.DrawGui();
}
