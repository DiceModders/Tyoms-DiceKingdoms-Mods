using System;
using System.Runtime.InteropServices;
using System.Text;

namespace DiceModders.IslandTools;

/// <summary>
/// Real Windows clipboard access. The game's own SaveIslandToClipboard/LoadIslandFromClipboard
/// go through GUIUtility.systemCopyBuffer, which is unreliable outside an OnGUI event, so we
/// call ExportIsland/ImportIsland directly and do the clipboard + base64 ourselves.
/// </summary>
internal static class Clipboard
{
    private const uint CF_TEXT = 1;
    private const uint GMEM_MOVEABLE = 0x0002;

    [DllImport("user32.dll", SetLastError = true)] private static extern bool OpenClipboard(IntPtr hWnd);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool EmptyClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool IsClipboardFormatAvailable(uint format);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr GetClipboardData(uint format);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetClipboardData(uint format, IntPtr hMem);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalAlloc(uint flags, UIntPtr bytes);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalFree(IntPtr hMem);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalUnlock(IntPtr hMem);

    public static bool SetText(string text)
    {
        if (!OpenClipboard(IntPtr.Zero)) return false;
        bool ok = false;
        try
        {
            if (!EmptyClipboard()) return false;
            byte[] bytes = Encoding.ASCII.GetBytes(text + "\0");
            IntPtr mem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes.Length);
            if (mem == IntPtr.Zero) return false;
            IntPtr dst = GlobalLock(mem);
            if (dst == IntPtr.Zero) { GlobalFree(mem); return false; }
            Marshal.Copy(bytes, 0, dst, bytes.Length);
            GlobalUnlock(mem);
            ok = SetClipboardData(CF_TEXT, mem) != IntPtr.Zero;
            if (!ok) GlobalFree(mem); // on success the clipboard owns the memory
        }
        finally { CloseClipboard(); }
        return ok;
    }

    public static bool TryGetText(out string text)
    {
        text = null;
        if (!IsClipboardFormatAvailable(CF_TEXT) || !OpenClipboard(IntPtr.Zero)) return false;
        try
        {
            IntPtr mem = GetClipboardData(CF_TEXT);
            if (mem == IntPtr.Zero) return false;
            IntPtr src = GlobalLock(mem);
            if (src == IntPtr.Zero) return false;
            try { text = Marshal.PtrToStringAnsi(src); }
            finally { GlobalUnlock(mem); }
            return text != null;
        }
        finally { CloseClipboard(); }
    }

    /// <summary>Lenient base64 decode: ignores whitespace/newlines, tolerates missing padding.</summary>
    public static bool TryDecodeBase64(string s, out byte[] data)
    {
        data = null;
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
            if (!char.IsWhiteSpace(c)) sb.Append(c);
        while (sb.Length % 4 != 0) sb.Append('=');
        try { data = Convert.FromBase64String(sb.ToString()); return true; }
        catch (FormatException) { return false; }
    }
}
