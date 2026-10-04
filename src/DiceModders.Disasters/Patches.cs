using System;

namespace DiceModders.Disasters;

internal static class Patches
{
    // Meteorite.Execute(GameState, DisasterOccurrence, Random) runs for every meteorite occurrence. Re-applying the
    // value here keeps the override alive even if the Disasters asset is reloaded between matches.
    internal static void ExecutePrefix(object __instance)
    {
        try
        {
            Meteor.Learn(__instance);
            if (Meteor.Override is int v) Meteor.TryWrite(__instance, v);
        }
        catch (Exception e) { Plugin.L.LogWarning("[meteor] " + e.GetType().Name + ": " + e.Message); }
    }

    // IslandGrid.MeteoriteNumberCastleTiles(int2 impact, int radius): "castle tiles within the impact radius".
    // Reporting 0 makes Meteorite.Execute accept every random grass tile, so the castle is no longer protected.
    internal static void CastleTilesPostfix(ref int __result)
    {
        if (Meteor.ProtectionOff.On) __result = 0;
    }
}
