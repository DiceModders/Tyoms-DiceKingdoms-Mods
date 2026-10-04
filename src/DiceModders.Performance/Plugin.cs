using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using DiceModders.Shared;
using UnityEngine;

namespace DiceModders.Performance;

/// <summary>Lag-hunting tools: skip combat particles, and time key game methods per frame.</summary>
[BepInPlugin(Guid, Name, Version)]
[BepInProcess("Dice-Kingdoms.exe")]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "org.dicemodders.dicekingdoms.performance";
    public const string Name = "DiceKingdoms Performance";
    public const string Version = "1.0.0";

    internal static ManualLogSource L;
    internal static Hotkeys Keys;
    internal static Patcher PatcherInstance;

    internal static Toggle NoParticles;
    internal static Toggle Probe;
    internal static ConfigEntry<int> ProbeIntervalMs;

    private static bool _probePatched;
    private static long _lastProbeReport;
    private static long _lastParticleReport;
    private static long _lastSpawnCalls;

    public override void Load()
    {
        L = Log;

        ProbeIntervalMs = Config.Bind("Probe", "ReportIntervalMs", 2000,
            new ConfigDescription("How often the probe prints its report.", new AcceptableValueRange<int>(500, 30000)));

        NoParticles = new Toggle("No unit combat particles",
            Config.Bind("Startup", "NoParticles", false, "Start with parry/die/convert particle effects skipped.").Value, L);
        Probe = new Toggle("Performance probe",
            Config.Bind("Startup", "Probe", false, "Start with the frame-time probe running.").Value, L);

        Keys = new Hotkeys(Config, L, "DiceKingdoms Performance", 20, 260);
        Keys.Bind(Config, "NoParticles", HotkeyKey.F8, "Toggle: skip unit combat particles", NoParticles.Flip, "No combat particles", () => NoParticles.On);
        Keys.Bind(Config, "Probe", HotkeyKey.PageUp, "Toggle: performance probe (prints a report periodically)", ToggleProbe, "Performance probe (see log)", () => Probe.On);

        PatcherInstance = new Patcher(Guid, L);
        PatcherInstance.Patch("UnitVisual", "SpawnParticles", typeof(Patches), prefix: nameof(Patches.SpawnParticlesPrefix));
        PatcherInstance.Summary();

        // The probe patches are only installed once it is first switched on.
        if (Probe.On) EnsureProbePatched();

        Keys.LogBindings();
        AddComponent<PerformanceTicker>();
    }

    private static void ToggleProbe()
    {
        if (!Probe.On) EnsureProbePatched();
        Probe.Flip();
        _lastProbeReport = Environment.TickCount64;
    }

    private static void EnsureProbePatched()
    {
        if (_probePatched) return;
        _probePatched = true;
        foreach (var (type, method) in Patches.ProbeTargets)
            PatcherInstance.Patch(type, method, typeof(Patches), prefix: nameof(Patches.ProbePrefix), postfix: nameof(Patches.ProbePostfix));
    }

    internal static void Tick()
    {
        Keys?.Poll();

        long now = Environment.TickCount64;
        if (Probe.On && now - _lastProbeReport >= ProbeIntervalMs.Value)
        {
            Patches.ReportProbes((now - _lastProbeReport) / 1000.0);
            _lastProbeReport = now;
        }
        if (NoParticles.On && now - _lastParticleReport >= 5000)
        {
            long calls = Patches.SpawnCalls;
            if (calls != _lastSpawnCalls)
            {
                L.LogInfo($"[particles] SpawnParticles called {calls - _lastSpawnCalls}x in the last 5s (skipped so far: {Patches.SpawnSkipped})");
                _lastSpawnCalls = calls;
            }
            _lastParticleReport = now;
        }
    }
}

public sealed class PerformanceTicker : MonoBehaviour
{
    public PerformanceTicker(IntPtr ptr) : base(ptr) { }
    private void Update() => Plugin.Tick();
    private void OnGUI() => Plugin.Keys?.DrawGui();
}
