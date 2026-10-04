using System;
using DiceModders.Shared;

namespace DiceModders.IslandTools;

internal static class Patches
{
    // IslandGrid.generationParameters is an embedded struct at +0x1E8; HardLimit.radius is its
    // first field. This is plain serialized data that Awake never writes, and setting it is
    // idempotent (an absolute SET, not a multiply), so it stays correct if Awake re-runs
    // during an import. See the wiki's "Island Grid" page for why we do NOT resize arrays.
    internal static void IslandGridAwakePrefix(object __instance)
    {
        IntPtr p = Mem.PtrOf(__instance);
        int radius = Plugin.IslandRadius.Value;
        if (p != IntPtr.Zero && radius > 0) Mem.WriteInt(p, 0x1E8, radius);
        State.LastIslandGrid = __instance;
    }

    // ChatInterface.Awake fires once per fresh match: drop pointers into the previous match.
    internal static void NewMatchPrefix()
    {
        State.ResetForNewMatch();
        Plugin.L.LogInfo("[capture] new match - cleared cross-match state");
    }

    internal static void SummaryPostfix(object __instance)
    {
        IntPtr p = Mem.PtrOf(__instance);
        if (p == IntPtr.Zero || State.PlayerSummaries.Contains(p)) return;
        if (State.PlayerSummaries.Count >= State.MaxTrackedPlayers) return;
        State.PlayerSummaries.Add(p);
        Plugin.L.LogInfo($"[players] tracked PlayerSummary #{State.PlayerSummaries.Count - 1} = 0x{p.ToInt64():X}");
    }

    // BuildData.Unlocked(BuildingType) is called repeatedly by the bottom-bar panels, so it is a
    // reliable place to learn live BuildingType pointers (used by the dump and the import diagnostic).
    internal static void UnlockedPrefix(object[] __args)
    {
        if (__args == null || __args.Length < 1) return;
        IntPtr type = Mem.PtrOf(__args[0]);
        if (type == IntPtr.Zero || State.SeenBuildingTypes.Count >= State.MaxSeenTypes) return;
        if (!State.SeenBuildingTypes.Contains(type)) State.SeenBuildingTypes.Add(type);
    }

    // RulesBuilding holds the shared BuildingTypes asset at +0x18.
    internal static void RulesBuildingPostfix(object __instance)
    {
        IntPtr p = Mem.PtrOf(__instance);
        IntPtr bt = Mem.ReadPtr(p, 0x18);
        if (bt != IntPtr.Zero) State.BuildingTypes = Reflect.Wrap(GameTypes.Find("BuildingTypes"), bt);
    }

    internal static void GetTypesPostfix(object __instance)
    {
        if (__instance != null) State.BuildingTypes = __instance;
    }

    // Diagnostic: which building did the game refuse while WE were importing an island?
    internal static void CanPlacePostfix(object[] __args, ref bool __result)
    {
        if (__result || !State.Importing || __args == null || __args.Length < 1) return;
        IntPtr building = Mem.PtrOf(__args[0]);
        if (building == IntPtr.Zero) return;

        if (Plugin.BypassPlacementWhileImporting.Value)
        {
            __result = true;                 // let the import continue; the imported tiles define the final terrain
            State.BypassedChecks++;
        }

        var sb = new System.Text.StringBuilder($"[import] CanPlaceBuilding REFUSED building 0x{building.ToInt64():X}: ints");
        for (int i = 0; i < 20; i++) sb.Append(' ').Append(Mem.ReadInt(building, 0x10 + i * 4));
        for (int i = 0; i < 12; i++)
        {
            IntPtr q = Mem.ReadPtr(building, 0x10 + i * 8);
            if (q != IntPtr.Zero && State.SeenBuildingTypes.Contains(q))
                sb.Append($" | +0x{0x10 + i * 8:X} is type '{BuildingDump.NameOf(q)}'");
        }
        Plugin.L.LogWarning(sb.ToString());
    }
}
