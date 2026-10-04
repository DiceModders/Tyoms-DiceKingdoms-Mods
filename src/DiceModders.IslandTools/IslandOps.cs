using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DiceModders.Shared;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace DiceModders.IslandTools;

/// <summary>
/// Island save/load. All of this runs from the Unity main thread (hotkeys are polled in Update),
/// which is the only safe place to call game code.
/// </summary>
internal static class IslandOps
{
    // Flags that are normally true for exactly one player on a networked game (checked on Player, then Island).
    private static readonly string[] LocalFlags =
        { "IsLocalPlayer", "IsLocal", "isLocal", "IsMine", "IsMe", "IsSelf", "IsOwner" };

    public static void SaveOwn()
    {
        var target = ResolveOwn(out string name);
        if (target == null) return;

        Plugin.L.LogInfo($"[island] Exporting {name}'s IslandGrid 0x{Mem.PtrOf(target).ToInt64():X} to clipboard...");
        bool ok = ExportToClipboard(target);
        Plugin.L.LogMessage(ok ? "[island] Saved - try pasting into Notepad." : "[island] Save FAILED.");
    }

    public static void LoadOwn()
    {
        var target = ResolveOwn(out string name);
        if (target == null) return;

        Plugin.L.LogInfo($"[island] Importing clipboard onto {name}'s IslandGrid 0x{Mem.PtrOf(target).ToInt64():X} ...");
        State.BypassedChecks = 0;
        bool ok = ImportFromClipboard(target, out string error);
        Plugin.L.LogMessage(ok ? "[island] Import done" + BypassNote() + "." : "[island] Import FAILED: " + error);
    }

    public static void CyclePlayer()
    {
        int n = State.PlayerSummaries.Count;
        if (n == 0) { Plugin.L.LogWarning("[players] No players tracked yet - are you in a match?"); return; }
        State.SelectedPlayer = (State.SelectedPlayer + 1) % n;
        var island = IslandOf(State.PlayerSummaries[State.SelectedPlayer]);
        State.SelectedName = NameOf(OwnerOf(island));
        Plugin.L.LogMessage($"[players] Selected player {State.SelectedPlayer} / {n - 1}  -  {State.SelectedName}");
    }

    // Host-swap EXPERIMENT. Theory (wiki, "Island Grid"): IslandGrid is a synced behaviour with the HOST
    // as sole authoritative source, so a host-side import on another player's grid should be broadcast.
    // Never observed working live. If you are not the host this most likely only changes what YOU see.
    public static void LoadOnSelected()
    {
        int n = State.PlayerSummaries.Count;
        if (n == 0) { Plugin.L.LogWarning("[players] No players tracked yet - are you in a match?"); return; }
        if (State.SelectedPlayer < 0 || State.SelectedPlayer >= n) { Plugin.L.LogWarning("[players] Selected index out of range."); return; }

        var island = IslandOf(State.PlayerSummaries[State.SelectedPlayer]);
        var grid = GridOf(island);
        if (grid == null) { Plugin.L.LogWarning("[island] Selected player has no island / grid yet."); return; }

        Plugin.L.LogInfo($"[island] Importing clipboard onto {NameOf(OwnerOf(island))}'s IslandGrid 0x{Mem.PtrOf(grid).ToInt64():X} ...");
        State.BypassedChecks = 0;
        bool ok = ImportFromClipboard(grid, out string error);
        Plugin.L.LogMessage(ok ? "[island] Import done" + BypassNote() + " - wait a moment and check if it stuck for that player."
                               : "[island] Import FAILED: " + error);
    }

    // ---- who am I? -----------------------------------------------------------------------------------------

    /// <summary>
    /// Finds the IslandGrid of the LOCAL (human) player. The old code used "the last grid that ran Awake",
    /// which with several players is simply whichever island was created last - not necessarily yours.
    /// Order: 1) config LocalPlayerName, 2) Player.LocalPlayer, 3) a flag such as IsLocalPlayer / IsOwner true for exactly one player.
    /// If neither works we refuse to guess, and log which members exist so the name can be fixed.
    /// </summary>
    private static object ResolveOwn(out string name)
    {
        name = null;
        int n = State.PlayerSummaries.Count;
        if (n == 0) { Plugin.L.LogWarning("[island] No players tracked yet - are you in a match?"); return null; }

        var islands = new object[n];
        var owners = new object[n];
        for (int i = 0; i < n; i++)
        {
            islands[i] = IslandOf(State.PlayerSummaries[i]);
            owners[i] = OwnerOf(islands[i]);
        }

        // 1) explicit name from the config
        string wanted = Plugin.LocalPlayerName.Value?.Trim();
        if (!string.IsNullOrEmpty(wanted))
        {
            for (int i = 0; i < n; i++)
                if (string.Equals(NameOf(owners[i]), wanted, StringComparison.OrdinalIgnoreCase))
                    return Chosen(i, islands, owners, "config LocalPlayerName", out name);
            Plugin.L.LogWarning($"[island] No tracked player is named '{wanted}'. Names seen: {string.Join(", ", owners.Select(NameOf))}");
            return null;
        }

        // 2) Player.LocalPlayer (the game's own pointer to the local player; seen in the Player members list)
        IntPtr local = IntPtr.Zero;
        try
        {
            local = Mem.PtrOf(Reflect.CallStatic(GameTypes.Find("Player"), "get_LocalPlayer"));
            if (local == IntPtr.Zero)
                foreach (var o in owners)
                    if (o != null) { local = Mem.PtrOf(Reflect.Call(o, "get_LocalPlayer")); break; }   // instance-property variant
        }
        catch (Exception) { }
        if (local != IntPtr.Zero)
            for (int i = 0; i < n; i++)
                if (Mem.PtrOf(owners[i]) == local)
                    return Chosen(i, islands, owners, "Player.LocalPlayer", out name);

        // 3) a flag that is true for exactly one player
        foreach (var targets in new[] { owners, islands })
            foreach (string flag in LocalFlags)
            {
                int hit = -1, hits = 0;
                bool any = false;
                for (int i = 0; i < n; i++)
                {
                    bool? v = TryFlag(targets[i], flag);
                    if (v == null) continue;
                    any = true;
                    if (v == true) { hits++; hit = i; }
                }
                if (any && hits == 1) return Chosen(hit, islands, owners, flag, out name);
            }

        // 4) give up loudly instead of touching the wrong island
        Plugin.L.LogWarning("[island] Could not tell which player is YOU, so nothing was changed. " +
                            "Fix: set [Island] LocalPlayerName in the Island Tools config to your in-game name. " +
                            "Names seen: " + string.Join(", ", owners.Select(NameOf)));
        Plugin.L.LogWarning("[island] Player members that might identify you: " + DescribeMembers(owners.FirstOrDefault(o => o != null)));
        return null;
    }

    private static object Chosen(int index, object[] islands, object[] owners, string how, out string name)
    {
        name = NameOf(owners[index]);
        Plugin.L.LogInfo($"[island] You are player #{index} '{name}' (found via {how}).");
        return GridOf(islands[index]);
    }

    private static string BypassNote()
        => State.BypassedChecks > 0 ? $" ({State.BypassedChecks} placement check(s) overridden)" : "";

    private static bool? TryFlag(object target, string flag)
    {
        if (target == null) return null;
        foreach (string method in new[] { "get_" + flag, flag })
        {
            try { if (Reflect.Call(target, method) is bool b) return b; }
            catch (MissingMethodException) { }
            catch (Exception) { }
        }
        return null;
    }

    private static string DescribeMembers(object o)
    {
        if (o == null) return "(no player object)";
        var names = new SortedSet<string>();
        var rx = new Regex("local|owner|mine|self|host|client|steam|(^|_)me($|[A-Z])", RegexOptions.IgnoreCase);
        for (var t = o.GetType(); t != null && !(t.Namespace ?? "").StartsWith("Il2Cpp"); t = t.BaseType)
            foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                           BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                if (rx.IsMatch(m.Name)) names.Add(m.Name);
        return names.Count == 0 ? "(none matched)" : string.Join(", ", names.Take(30));
    }

    // ---- helpers ---------------------------------------------------------------------------------------------

    private static bool ExportToClipboard(object grid)
    {
        try
        {
            var result = Reflect.Call(grid, "ExportIsland") as Il2CppStructArray<byte>;
            if (result == null) return false;
            return Clipboard.SetText(Convert.ToBase64String(result.ToArray()));
        }
        catch (Exception e) { Plugin.L.LogError("[island] export: " + e); return false; }
    }

    private static bool ImportFromClipboard(object grid, out string error)
    {
        error = null;
        if (!Clipboard.TryGetText(out string text)) { error = "the clipboard has no text."; return false; }
        if (!Clipboard.TryDecodeBase64(text, out byte[] bytes)) { error = "the clipboard text is not an island code (it is not valid base64)."; return false; }
        try
        {
            State.Importing = true;
            Reflect.Call(grid, "ImportIsland", new Il2CppStructArray<byte>(bytes));
            return true;
        }
        catch (Exception e)
        {
            string first = (e.Message ?? "").Split('\n')[0].Trim();
            bool refused = (e.Message ?? "").Contains("CanPlaceBuilding");
            Plugin.L.LogError("[island] import: " + e.GetType().Name + ": " + first);
            error = refused
                ? "the game refused a building from the island code (the [import] REFUSED line above says which one) and stopped halfway, " +
                  "so the island may be half-loaded - restart the match. Retry after the match is fully running, " +
                  "or set [Island] BypassPlacementWhileImporting = true, or fix that building in the island code."
                : "the game rejected the island: " + first;
            return false;
        }
        finally { State.Importing = false; }
    }

    private static object IslandOf(IntPtr playerSummary)
    {
        IntPtr island = Mem.ReadPtr(playerSummary, 0xD0);                      // PlayerSummary.island
        return Reflect.Wrap(GameTypes.Find("Island"), island);
    }

    private static object GridOf(object island)
    {
        if (island == null) return null;
        IntPtr grid = Mem.ReadPtr(Mem.PtrOf(island), 0x98);                    // Island.grid
        return Reflect.Wrap(GameTypes.Find("IslandGrid"), grid);
    }

    private static object OwnerOf(object island)
    {
        if (island == null) return null;
        try { return Reflect.Call(island, "get_Owner"); }                      // Island.get_Owner
        catch (Exception) { return null; }
    }

    private static string NameOf(object player)
    {
        if (player == null) return "(no owner yet)";
        try
        {
            string name = Reflect.Call(player, "get_UserName")?.ToString();    // Player.get_UserName
            return string.IsNullOrEmpty(name) ? "(unnamed)" : name;
        }
        catch (Exception e) { return "(error: " + e.Message + ")"; }
    }
}
