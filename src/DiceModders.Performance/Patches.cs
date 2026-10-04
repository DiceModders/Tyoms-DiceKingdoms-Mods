using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace DiceModders.Performance;

internal static class Patches
{
    // ---- particles ---------------------------------------------------------------------------
    internal static long SpawnCalls;
    internal static long SpawnSkipped;

    // UnitVisual.SpawnParticles(ParticleSystem prefab): parry / die / convert effects only.
    internal static bool SpawnParticlesPrefix()
    {
        SpawnCalls++;
        if (!Plugin.NoParticles.On) return true;
        SpawnSkipped++;
        return false;
    }

    // ---- probe ---------------------------------------------------------------------------------
    internal static readonly (string Type, string Method)[] ProbeTargets =
    {
        ("UnitVisual", "Update"),
        ("ProjectileVisual", "Update"),
        ("CombatInterface", "Update"),
        ("CombatGrid", "PrepareSimStep"),
        ("CombatGrid", "SimulationStep"),
        ("GameState", "Update"),   // called once per frame: used as the frame counter
    };

    private sealed class ProbeData
    {
        public string Name;
        public long Ticks;
        public long Calls;
    }

    private static readonly Dictionary<MethodBase, ProbeData> Data = new();

    internal static void ProbePrefix(out long __state)
        => __state = Plugin.Probe.On ? Stopwatch.GetTimestamp() : 0;

    internal static void ProbePostfix(MethodBase __originalMethod, long __state)
    {
        if (__state == 0) return;
        long elapsed = Stopwatch.GetTimestamp() - __state;
        if (!Data.TryGetValue(__originalMethod, out var d))
        {
            d = new ProbeData { Name = __originalMethod.DeclaringType?.Name + "." + __originalMethod.Name };
            Data[__originalMethod] = d;
        }
        d.Ticks += elapsed;
        d.Calls++;
    }

    internal static void ReportProbes(double windowSeconds)
    {
        ProbeData frameCounter = null;
        foreach (var d in Data.Values)
            if (d.Name == "GameState.Update") frameCounter = d;
        if (frameCounter == null || frameCounter.Calls < 1) return; // not in a game scene

        double frames = frameCounter.Calls;
        double tickToMs = 1000.0 / Stopwatch.Frequency;
        Plugin.L.LogMessage($"[probe] {frames / windowSeconds:F1} fps  ({1000.0 * windowSeconds / frames:F1} ms/frame)");

        foreach (var d in Data.Values)
        {
            if (d != frameCounter && d.Calls > 0)
            {
                double totalMs = d.Ticks * tickToMs;
                // Plain string first: BepInEx's log-message string handler does not support alignment (",-28").
                string line = $"        {d.Name,-28} {d.Calls / frames,7:F1} calls/frame  {totalMs / frames,6:F2} ms/frame  ({1000.0 * totalMs / d.Calls:F1} us/call)";
                Plugin.L.LogMessage(line);
            }
            d.Ticks = 0; d.Calls = 0;
        }
    }
}
