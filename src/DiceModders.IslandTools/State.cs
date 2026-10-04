using System;
using System.Collections.Generic;

namespace DiceModders.IslandTools;

/// <summary>Objects captured by patches. Everything cross-match is cleared on a new match.</summary>
internal static class State
{
    public const int MaxTrackedPlayers = 16;
    public const int MaxSeenTypes = 512;

    /// <summary>Last IslandGrid that ran Awake. With several players this is NOT guaranteed to be yours.</summary>
    public static object LastIslandGrid;

    /// <summary>PlayerSummary instances (one per player row in the match UI), by native pointer.</summary>
    public static readonly List<IntPtr> PlayerSummaries = new();
    public static int SelectedPlayer;
    public static string SelectedName;

    public static object BuildingTypes;                       // BuildingTypes asset instance
    public static readonly List<IntPtr> SeenBuildingTypes = new(); // BuildingType* seen via BuildData.Unlocked
    public static bool AutoDumped;

    /// <summary>True only while our own import call runs, so refused placements get logged.</summary>
    public static bool Importing;

    /// <summary>How many CanPlaceBuilding refusals were overridden during the current import.</summary>
    public static int BypassedChecks;

    public static void ResetForNewMatch()
    {
        LastIslandGrid = null;
        PlayerSummaries.Clear();
        SelectedPlayer = 0;
        SelectedName = null;
    }
}
