using System;
using DiceModders.Shared;

namespace DiceModders.UiTweaks;

internal static class Patches
{
    private static object _logInterface;     // LogInterface instance, captured in OnEnable
    private static long _lastChatClear;
    private static long _lastLogClear;

    // ChatInterface.Awake fires once per fresh match/scene load, so it doubles as the
    // "new match" signal: drop anything captured from the previous match.
    internal static void ChatAwakePrefix()
    {
        _logInterface = null;
    }

    internal static void LogEnablePostfix(object __instance) => _logInterface = __instance;

    internal static void LogDisablePostfix(object __instance)
    {
        if (ReferenceEquals(_logInterface, __instance)) _logInterface = null;
    }

    // Runs every frame while the match UI exists.
    internal static void ChatUpdatePostfix(object __instance)
    {
        bool chat = Plugin.AutoClearChat.On, log = Plugin.AutoClearLog.On;
        if (!chat && !log) return;

        long now = Environment.TickCount64;
        long interval = Plugin.ClearIntervalMs.Value;
        try
        {
            if (chat && now - _lastChatClear >= interval)
            {
                _lastChatClear = now;
                Reflect.Call(__instance, "Clear");          // ChatInterface.Clear
            }
            if (log && _logInterface != null && now - _lastLogClear >= interval)
            {
                _lastLogClear = now;
                Reflect.Call(_logInterface, "Clear");       // LogInterface.Clear
            }
        }
        catch (Exception e)
        {
            Plugin.L.LogError("[auto-clear] " + e.Message);
            Plugin.AutoClearChat.Flip(); // avoid spamming errors every frame
            if (Plugin.AutoClearLog.On) Plugin.AutoClearLog.Flip();
        }
    }

    // Runs after PlayerSummary.OnSyncPhase and Update so icons the game re-creates or re-enables are hidden again.
    internal static void SummaryPostfix(object __instance)
    {
        if (!Plugin.ClearAttackers.On) return;
        AttackerFlags.Hide(__instance);
    }
}
