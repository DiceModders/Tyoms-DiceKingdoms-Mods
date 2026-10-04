using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace DiceModders.Shared;

/// <summary>
/// In-game panel drawn with Unity's IMGUI (OnGUI), in the style of KSP mod windows:
///  * drag the title bar to MOVE it,
///  * drag the bottom-right corner to RESIZE it (rows scroll when the window is too small),
///  * [-] collapses it to just the title bar, [x] hides it (Insert brings it back).
/// Rows are buttons (same as a hotkey), number rows (slider + -100/-10/-1/+1/+10/+100 buttons) and short info text.
/// Move/resize/collapse are done with plain mouse events instead of GUI.Window, so no IL2CPP delegates are involved.
/// Layout is saved in the plugin's config ([Gui] section). Each plugin has its own window.
/// </summary>
internal sealed partial class Hotkeys
{
    private const float TitleHeight = 22f;
    private const float RowHeight = 24f;
    private const float InfoLineHeight = 18f;
    private const float Padding = 6f;
    private const float GripSize = 16f;
    private const float MinWidth = 260f;
    private const float MinHeight = 80f;

    private enum DragMode { None, Move, Resize }
    private enum RowKind { Button, Value, Info }

    private sealed class Row
    {
        public RowKind Kind;
        public Binding Binding;                  // Button
        public string Label;                     // Value
        public int Min, Max;
        public Func<int> Get;
        public Action<int> Set;
        public Action Commit;                    // called when a change is finished (saves config)
        public int[] Steps;
        public bool Dirty;
        public long DirtyAt;
        public Func<string> Info;                // Info
        public int Lines;
    }

    private readonly List<Row> _rows = new();

    private bool _menuOpen;
    private bool _collapsed;
    private ConfigEntry<float> _guiScale;
    private ConfigEntry<int> _cfgX, _cfgY, _cfgW, _cfgH;
    private ConfigEntry<bool> _cfgCollapsed;

    // Runtime layout in GUI units (screen pixels / scale). _h <= 0 means "fit the content".
    private float _gx, _gy, _w, _h;
    private DragMode _drag;
    private Vector2 _dragStartMouse, _dragStartPos, _dragStartSize;
    private Vector2 _scroll;

    /// <summary>A number row: label + current value, a slider, and step buttons.</summary>
    public void AddValue(string label, int min, int max, Func<int> get, Action<int> set, Action commit = null, int[] steps = null)
    {
        _rows.Add(new Row
        {
            Kind = RowKind.Value, Label = label, Min = min, Max = max, Get = get, Set = set, Commit = commit,
            Steps = steps ?? new[] { -100, -10, -1, 1, 10, 100 },
        });
    }

    /// <summary>A few lines of read-only text (re-evaluated every frame).</summary>
    public void AddInfo(int lines, Func<string> text) => _rows.Add(new Row { Kind = RowKind.Info, Lines = lines, Info = text });

    private void InitGui(ConfigFile cfg, int defaultX, int defaultY)
    {
        _menuOpen = cfg.Bind("Gui", "ShowOnStart", false, "Open this plugin's in-game window when the game starts.").Value;
        _guiScale = cfg.Bind("Gui", "Scale", 1.0f,
            new ConfigDescription("Size of the in-game window text/buttons (1.0 = normal, 1.5 = 50% bigger). Needs a restart.",
                new AcceptableValueRange<float>(0.5f, 3f)));
        _cfgX = cfg.Bind("Gui", "PanelX", defaultX, "Window position from the left edge, in screen pixels (drag the title bar to change).");
        _cfgY = cfg.Bind("Gui", "PanelY", defaultY, "Window position from the top edge, in screen pixels.");
        _cfgW = cfg.Bind("Gui", "PanelWidth", 420, "Window width (drag the bottom-right corner to change).");
        _cfgH = cfg.Bind("Gui", "PanelHeight", 0, "Window height; 0 = fit all rows automatically.");
        _cfgCollapsed = cfg.Bind("Gui", "Collapsed", false, "Show only the title bar.");

        float scale = _guiScale.Value;
        _gx = _cfgX.Value / scale;
        _gy = _cfgY.Value / scale;
        _w = Mathf.Max(MinWidth, _cfgW.Value);
        _h = _cfgH.Value;
        _collapsed = _cfgCollapsed.Value;
    }

    // ---- "Insert" shows/hides the windows of ALL installed plugins together ------------------------------------
    // Plugins are separate DLLs, so they share the state through AppDomain data (a counter + the wanted state).

    private const string SharedOpenKey = "DiceModders.Menu.Open";
    private const string SharedStampKey = "DiceModders.Menu.Stamp";
    private int _seenStamp;

    private void BroadcastMenu()
    {
        try
        {
            bool open = !(AppDomain.CurrentDomain.GetData(SharedOpenKey) is bool b && b);
            int stamp = (AppDomain.CurrentDomain.GetData(SharedStampKey) is int s ? s : 0) + 1;
            AppDomain.CurrentDomain.SetData(SharedOpenKey, open);
            AppDomain.CurrentDomain.SetData(SharedStampKey, stamp);
        }
        catch (Exception) { _menuOpen = !_menuOpen; }   // shared state unavailable: toggle only this plugin
    }

    private void SyncMenu()
    {
        try
        {
            if (AppDomain.CurrentDomain.GetData(SharedStampKey) is int stamp && stamp != _seenStamp)
            {
                _seenStamp = stamp;
                _menuOpen = AppDomain.CurrentDomain.GetData(SharedOpenKey) is bool b && b;
            }
        }
        catch (Exception) { }
    }

    // ---- drawing ------------------------------------------------------------------------------------------------

    private float ContentHeight()
    {
        float h = 0f;
        foreach (var r in _rows)
            h += r.Kind == RowKind.Value ? RowHeight * 3f
               : r.Kind == RowKind.Info ? InfoLineHeight * r.Lines + 6f
               : RowHeight;
        return h + Padding + GripSize;
    }

    /// <summary>Call from a MonoBehaviour's OnGUI.</summary>
    public void DrawGui()
    {
        if (!_menuOpen) return;

        float scale = _guiScale.Value;
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        try
        {
            float contentH = ContentHeight();
            float bodyH = _collapsed ? 0f : (_h > 0f ? Mathf.Max(MinHeight - TitleHeight, _h - TitleHeight) : contentH);
            float winH = TitleHeight + bodyH;

            var window = new Rect(_gx, _gy, _w, winH);
            var dragZone = new Rect(_gx, _gy, _w - 50f, TitleHeight);
            var grip = new Rect(_gx + _w - GripSize, _gy + winH - GripSize, GripSize, GripSize);

            // Mouse handling first, so a title-bar / corner click is consumed before any button sees it.
            HandleMouse(Event.current, dragZone, grip, winH, scale);

            GUI.Box(window, "");
            GUI.Box(new Rect(_gx, _gy, _w, TitleHeight), _title);
            if (GUI.Button(new Rect(_gx + _w - 48f, _gy + 2f, 22f, TitleHeight - 4f), _collapsed ? "+" : "-"))
            {
                _collapsed = !_collapsed;
                _cfgCollapsed.Value = _collapsed;
            }
            if (GUI.Button(new Rect(_gx + _w - 24f, _gy + 2f, 22f, TitleHeight - 4f), "x"))
                _menuOpen = false;

            if (_collapsed) return;

            var body = new Rect(_gx, _gy + TitleHeight, _w, bodyH);
            bool needScroll = contentH > bodyH;
            var view = new Rect(0f, 0f, needScroll ? _w - 18f : _w, contentH);
            _scroll = GUI.BeginScrollView(body, _scroll, view);

            float x = Padding;
            float width = view.width - Padding * 2f;
            float y = Padding * 0.5f;
            foreach (var r in _rows)
            {
                if (r.Kind == RowKind.Button) { DrawButtonRow(r.Binding, x, y, width); y += RowHeight; }
                else if (r.Kind == RowKind.Value) { DrawValueRow(r, x, y, width); y += RowHeight * 3f; }
                else { DrawInfoRow(r, x, y, width); y += InfoLineHeight * r.Lines + 6f; }
            }
            GUI.EndScrollView();

            GUI.Box(grip, "::");   // resize handle
        }
        finally { GUI.matrix = oldMatrix; }
    }

    private void DrawButtonRow(Binding b, float x, float y, float width)
    {
        string state = b.State == null ? "" : (b.State() ? "[ON]   " : "[OFF]  ");
        string text = state + b.Label + "    (" + KeyText(b) + ")";
        if (GUI.Button(new Rect(x, y, width, RowHeight - 2f), text))
        {
            try { b.Action(); }
            catch (Exception e) { _log.LogError($"[menu] {b.Name} failed: {e}"); }
        }
    }

    private void DrawValueRow(Row r, float x, float y, float width)
    {
        try
        {
            int current = r.Get();
            GUI.Label(new Rect(x, y, width, RowHeight), r.Label + ":  " + current);

            float f = GUI.HorizontalSlider(new Rect(x, y + RowHeight + 4f, width, RowHeight - 8f), current, r.Min, r.Max);
            int dragged = (int)Math.Round(f);
            if (dragged != current)
            {
                r.Set(dragged);
                r.Dirty = true;
                r.DirtyAt = Environment.TickCount64;
                current = dragged;
            }

            float bw = width / r.Steps.Length;
            for (int i = 0; i < r.Steps.Length; i++)
            {
                int step = r.Steps[i];
                string text = step > 0 ? "+" + step : step.ToString();
                if (GUI.Button(new Rect(x + i * bw, y + RowHeight * 2f, bw - 2f, RowHeight - 2f), text))
                {
                    r.Set(Math.Clamp(current + step, r.Min, r.Max));
                    r.Commit?.Invoke();
                }
            }

            // slider changes are saved to the config half a second after the last movement (not on every frame)
            if (r.Dirty && Environment.TickCount64 - r.DirtyAt > 500)
            {
                r.Dirty = false;
                r.Commit?.Invoke();
            }
        }
        catch (Exception e) { _log.LogError($"[menu] {r.Label} failed: {e.Message}"); }
    }

    private void DrawInfoRow(Row r, float x, float y, float width)
    {
        string text;
        try { text = r.Info(); } catch (Exception) { text = "?"; }
        GUI.Label(new Rect(x, y, width, InfoLineHeight * r.Lines + 6f), text);
    }

    private void HandleMouse(Event e, Rect dragZone, Rect grip, float winH, float scale)
    {
        if (e == null) return;
        EventType type = e.type;

        if (type == EventType.MouseDown && e.button == 0)
        {
            if (!_collapsed && grip.Contains(e.mousePosition))
            {
                _drag = DragMode.Resize;
                _dragStartMouse = e.mousePosition;
                _dragStartSize = new Vector2(_w, winH);
                e.Use();
            }
            else if (dragZone.Contains(e.mousePosition))
            {
                _drag = DragMode.Move;
                _dragStartMouse = e.mousePosition;
                _dragStartPos = new Vector2(_gx, _gy);
                e.Use();
            }
        }
        else if (type == EventType.MouseDrag && _drag != DragMode.None)
        {
            Vector2 d = e.mousePosition - _dragStartMouse;
            if (_drag == DragMode.Move)
            {
                _gx = _dragStartPos.x + d.x;
                _gy = _dragStartPos.y + d.y;
            }
            else
            {
                _w = Mathf.Clamp(_dragStartSize.x + d.x, MinWidth, 1400f);
                _h = Mathf.Clamp(_dragStartSize.y + d.y, MinHeight, 1400f);
            }
            e.Use();
        }
        else if (type == EventType.MouseUp && _drag != DragMode.None)
        {
            _drag = DragMode.None;
            SaveLayout(scale);
            e.Use();
        }
    }

    private void SaveLayout(float scale)
    {
        // keep at least part of the title bar on screen
        _gx = Mathf.Clamp(_gx, 60f - _w, Screen.width / scale - 60f);
        _gy = Mathf.Clamp(_gy, 0f, Screen.height / scale - TitleHeight);

        _cfgX.Value = (int)(_gx * scale);
        _cfgY.Value = (int)(_gy * scale);
        _cfgW.Value = (int)_w;
        _cfgH.Value = _h > 0f ? (int)_h : 0;
    }
}
