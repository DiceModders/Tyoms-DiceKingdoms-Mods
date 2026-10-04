using System;
using System.IO;
using System.Text;
using BepInEx;
using DiceModders.Shared;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace DiceModders.IslandTools;

/// <summary>
/// Writes "id: name | tier=T hp=H | [[dx,dy],...]" per building type, for island-code editors.
/// BuildingType layout (wiki "Buildings"): localPositions int2[] @+0x18, tier @+0x78, maxHealth @+0x80.
/// </summary>
internal static class BuildingDump
{
    private static string OutputPath => Path.Combine(Paths.BepInExRootPath, "DiceKingdoms_buildings.txt");

    /// <summary>Runs once automatically as soon as the BuildingTypes asset or any BuildingType has been seen.</summary>
    private static long _nextAutoTry;

    public static void AutoTick()
    {
        if (State.AutoDumped || !Plugin.AutoDumpBuildings.Value) return;
        if (State.BuildingTypes == null && State.SeenBuildingTypes.Count == 0) return;   // not in a match yet
        long now = Environment.TickCount64;
        if (now < _nextAutoTry) return;           // retry at most every 5 s if a dump attempt fails
        _nextAutoTry = now + 5000;
        State.AutoDumped = TryRun();
    }

    public static void Run() => TryRun();

    /// <summary>
    /// The RulesBuilding.OnEnable / BuildingTypes.get_Types patches miss objects created before the plugin loaded
    /// (wiki, "Known quirks"). Ask Unity for live instances instead: first BuildingTypes itself, then RulesBuilding,
    /// which holds the BuildingTypes pointer at +0x18.
    /// </summary>
    private static bool TryFindViaUnity()
    {
        try
        {
            var typesType = GameTypes.Find("BuildingTypes");
            if (typesType != null)
            {
                var found = Resources.FindObjectsOfTypeAll(Il2CppType.From(typesType));
                if (found != null && found.Length > 0)
                {
                    State.BuildingTypes = Reflect.Wrap(typesType, Mem.PtrOf(found[0]));
                    Plugin.L.LogInfo("[buildings] found the BuildingTypes asset via Unity.");
                    return true;
                }
            }
            var rulesType = GameTypes.Find("RulesBuilding");
            if (rulesType != null)
            {
                var found = Resources.FindObjectsOfTypeAll(Il2CppType.From(rulesType));
                if (found != null && found.Length > 0)
                {
                    IntPtr bt = Mem.ReadPtr(Mem.PtrOf(found[0]), 0x18);
                    if (bt != IntPtr.Zero && typesType != null)
                    {
                        State.BuildingTypes = Reflect.Wrap(typesType, bt);
                        Plugin.L.LogInfo("[buildings] found the BuildingTypes asset via RulesBuilding.");
                        return true;
                    }
                }
            }
        }
        catch (Exception e) { Plugin.L.LogWarning("[buildings] Unity lookup failed: " + e.GetType().Name + ": " + e.Message); }
        return false;
    }

    private static bool TryRun()
    {
        try
        {
            if (State.BuildingTypes == null) TryFindViaUnity();
            if (State.BuildingTypes == null)
            {
                Plugin.L.LogWarning(State.SeenBuildingTypes.Count > 0
                    ? "[buildings] BuildingTypes asset not captured yet (only individual types were seen). Try again after loading into a match."
                    : "[buildings] No building types seen yet - wait until the bottom-bar panels are visible in a match, then dump again.");
                return false;
            }

            IntPtr arr = Mem.PtrOf(Reflect.Call(State.BuildingTypes, "get_Types")); // BuildingType[]
            if (arr == IntPtr.Zero) { Plugin.L.LogWarning("[buildings] get_Types() returned null."); return false; }

            int len = Mem.ArrayLength(arr);
            Plugin.L.LogInfo($"[buildings] {len} building types");
            var file = new StringBuilder();
            for (int i = 0; i < len; i++)
            {
                IntPtr bt = Mem.ArrayElem(arr, i);
                string line = bt == IntPtr.Zero ? $"{i}: (null)" : Describe(i, bt);
                file.Append(line).Append("\r\n");
                Plugin.L.LogInfo("  " + line);
            }
            File.WriteAllText(OutputPath, file.ToString());
            Plugin.L.LogMessage($"[buildings] Saved {OutputPath} - load that file in the web editor (Buildings tab).");
            return true;
        }
        catch (Exception e) { Plugin.L.LogError("[buildings] dump failed: " + e); return false; }
    }

    public static string NameOf(IntPtr buildingType)
    {
        var wrapper = Reflect.Wrap(GameTypes.Find("BuildingType"), buildingType);
        return Reflect.Call(wrapper, "get_DisplayName")?.ToString() ?? "?";   // BuildingType.get_DisplayName
    }

    private static string Describe(int index, IntPtr bt)
    {
        string name = NameOf(bt);

        IntPtr shapeArr = Mem.ReadPtr(bt, 0x18);
        var shape = new StringBuilder("[");
        int slen = Mem.ArrayLength(shapeArr);
        for (int j = 0; j < slen; j++)
        {
            int x = Mem.ReadInt(shapeArr, 0x20 + j * 8);
            int y = Mem.ReadInt(shapeArr, 0x20 + j * 8 + 4);
            shape.Append('[').Append(x).Append(',').Append(y).Append(']');
            if (j + 1 < slen) shape.Append(',');
        }
        shape.Append(']');

        int tier = Mem.ReadInt(bt, 0x78);
        int hp = Mem.ReadInt(bt, 0x80);
        return $"{index}: {name} | tier={tier} hp={hp} | {shape}";
    }
}
