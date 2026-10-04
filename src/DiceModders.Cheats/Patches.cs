namespace DiceModders.Cheats;

internal static class Patches
{
    // Cheats.get_Available (property getter) and Cheats.CheckAvailable (static) both gate the dev console.
    internal static void ForceTrue(ref bool __result)
    {
        if (Plugin.DevConsole.Value) __result = true;
    }

    // BuildData.Unlocked(BuildingType)
    internal static bool UnlockedPrefix(ref bool __result)
    {
        if (!Plugin.UnlockAllBuildings.On) return true;
        __result = true;
        return false; // skip original
    }

    // IslandGrid.CanPlaceBuilding(Building) - the per-tile placement check.
    internal static bool CanPlaceBuildingPrefix(ref bool __result)
    {
        if (!Plugin.BuildAnywhere.On) return true;
        __result = true;
        return false;
    }

    // Player.get_StartGameReady - read by the lobby Start button.
    internal static bool StartGameReadyPrefix(ref bool __result)
    {
        if (!Plugin.ForceLobbyReady.On) return true;
        __result = true;
        return false;
    }
}
