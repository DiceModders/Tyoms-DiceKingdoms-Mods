using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DiceModders.Shared;

namespace DiceModders.UiTweaks;

/// <summary>
/// Hides the "attacker" flag icons on the scoreboard (PlayerSummary).
///
/// The first version assumed the list lives at a fixed offset (+0xE8) from an older game build and only emptied it. That
/// does nothing when the offset has moved, or when the icons already exist and just stay on screen. This version:
///  1. finds the member by NAME (anything on PlayerSummary containing "attack"),
///  2. treats it as a List and switches every element's GameObject off (get_gameObject / SetActive(false)),
///  3. logs once what it found, or which members exist if nothing matches, so the right name can be added.
/// </summary>
internal static class AttackerFlags
{
    private const BindingFlags All = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static string _member;
    private static bool _useOffsetFallback;
    private static bool _reportedFound;
    private static bool _failed;

    public static void Hide(object summary)
    {
        if (_failed || summary == null) return;
        try
        {
            if (_member == null && !_useOffsetFallback) Discover(summary.GetType());
            if (_useOffsetFallback) { OffsetFallback(summary); return; }

            if (!Reflect.TryGetMember(summary, _member, out object list) || list == null) return;

            int count = Convert.ToInt32(Reflect.Call(list, "get_Count"));
            int hidden = 0;
            for (int i = 0; i < count; i++)
            {
                object element = Reflect.Call(list, "get_Item", i);
                if (element == null) continue;
                object go = Reflect.Call(element, "get_gameObject");
                if (go == null) continue;
                if (Reflect.Call(go, "get_activeSelf") is bool active && active)
                {
                    Reflect.Call(go, "SetActive", false);
                    hidden++;
                }
            }
            if (hidden > 0 && !_reportedFound)
            {
                _reportedFound = true;
                Plugin.L.LogInfo($"[scoreboard] hid {hidden} attacker icon(s) via PlayerSummary.{_member}");
            }
        }
        catch (Exception e)
        {
            _failed = true;   // stop retrying every frame
            Plugin.L.LogWarning($"[scoreboard] could not hide attacker icons via '{_member}': {e.GetType().Name}: {e.Message}. " +
                                "Please send this line - the member is probably not a List of UI elements.");
        }
    }

    private static void Discover(Type summaryType)
    {
        for (var t = summaryType; t != null && !(t.Namespace ?? "").StartsWith("Il2Cpp"); t = t.BaseType)
        {
            foreach (var p in t.GetProperties(All))
                if (p.Name.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0) { Found(p.Name, p.PropertyType); return; }
            foreach (var f in t.GetFields(All))
                if (f.Name.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0) { Found(f.Name, f.FieldType); return; }
        }

        // nothing matched: report what exists and fall back to the old offset trick
        var names = new SortedSet<string>();
        for (var t = summaryType; t != null && !(t.Namespace ?? "").StartsWith("Il2Cpp"); t = t.BaseType)
            foreach (var p in t.GetProperties(All)) names.Add(p.Name);
        Plugin.L.LogWarning("[scoreboard] no member containing 'attack' on " + summaryType.Name +
                            ". Using the old offset method. Members: " + string.Join(", ", names.Take(80)));
        _useOffsetFallback = true;
    }

    private static void Found(string name, Type type)
    {
        _member = name;
        Plugin.L.LogInfo($"[scoreboard] using PlayerSummary.{name} ({type.Name})");
    }

    // old behaviour: PlayerSummary.attackers (List<Image>) at +0xE8, List<T>._size at +0x18
    private static void OffsetFallback(object summary)
    {
        IntPtr self = Mem.PtrOf(summary);
        if (self == IntPtr.Zero) return;
        IntPtr list = Mem.ReadPtr(self, 0xE8);
        if (list != IntPtr.Zero) Mem.WriteInt(list, 0x18, 0);
    }
}
