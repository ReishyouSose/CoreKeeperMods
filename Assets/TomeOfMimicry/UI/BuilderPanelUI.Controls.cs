using System.Collections.Generic;
using UnityEngine;

namespace TomeOfMimicry
{
    public static partial class BuilderPanelUI
    {
        private static int Row3(int x, int y, int col3, int s,
            params (string label, string tip, bool selected, bool enabled, System.Action act)[] items)
        {
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                var r = new Rect(x + i * (col3 + Gap) * s, y, col3 * s, BtnH * s);
                if (Button(r, it.label, it.tip, s, it.selected, it.enabled))
                    it.act?.Invoke();
            }
            return y + (BtnH + Gap) * s;
        }

        private static int Stepper(int x, int y, int w, int s, string label, int value, System.Action<int> set)
        {
            GUI.Label(new Rect(x, y, 40 * s, StepH * s), label, _text);
            int bx = x + (ContentW - 12 - Gap - 20 - Gap - 12) * s;
            if (Button(new Rect(bx, y, 12 * s, StepH * s), "-", $"Decrease {label.ToLower()}", s))
                set(value - 1);
            GUI.Label(new Rect(bx + (12 + Gap) * s, y, 20 * s, StepH * s), value.ToString(), _textCenter);
            if (Button(new Rect(bx + (12 + Gap + 20 + Gap) * s, y, 12 * s, StepH * s), "+", $"Increase {label.ToLower()}", s))
                set(value + 1);
            return y + (StepH + Gap) * s;
        }

        private static void Dropdown(Rect rect, string id, string prefix, string[] options,
            int selected, int s, System.Action<int> select, bool enabled = true)
        {
            selected = Mathf.Clamp(selected, 0, options.Length - 1);
            bool open = _openDropdown == id;
            var e = Event.current;
            bool hover = enabled && rect.Contains(e.mousePosition);
            if (hover && e.type == EventType.Repaint) _hint = $"Choose {prefix.ToLower()}";

            DrawDropdownHeader(rect, prefix, DropdownHeaderValue(options[selected]), s, open, hover, enabled);

            if (hover && e.type == EventType.MouseDown && e.button == 0)
            {
                _openDropdown = open ? null : id;
                e.Use();
            }

            if (!open || !enabled) return;
            var menu = DropdownMenuRect(rect, options.Length, s);
            FillRect(menu, Hex(0xE3C39D));
            for (int i = 0; i < options.Length; i++)
            {
                var optionRect = new Rect(menu.x, menu.y + i * DropdownRowH * s,
                    menu.width, DropdownRowH * s);
                bool optionHover = optionRect.Contains(e.mousePosition);
                bool optionSelected = i == selected;
                FillRect(optionRect, optionSelected ? Hex(0xD3A47E)
                    : optionHover ? Hex(0xE9CEAA)
                    : Hex(0xE3C39D));
                if (optionSelected)
                    FillRect(new Rect(optionRect.x, optionRect.yMax - s, optionRect.width, s), Accent);
                GUI.Label(new Rect(optionRect.x + 4 * s, optionRect.y,
                    optionRect.width - 8 * s, optionRect.height), options[i],
                    optionSelected ? _textAccent : _text);

                if (optionHover && e.type == EventType.MouseDown && e.button == 0)
                {
                    select(i);
                    _openDropdown = null;
                    e.Use();
                }
            }
        }

        private const int DropdownRowH = 8;

        private static void DrawDropdownHeader(Rect rect, string prefix, string value,
            int s, bool open, bool hover, bool enabled)
        {
            Color fill = !enabled ? Hex(0xD2B99B)
                : open ? Hex(0xD3A47E)
                : hover ? Hex(0xE9CEAA)
                : Hex(0xDFC09B);
            FillRect(rect, fill);

            float prefixWidth = Mathf.Min(rect.width * 0.34f, (prefix.Length * 4 + 6) * s);
            GUI.Label(new Rect(rect.x + 3 * s, rect.y, prefixWidth, rect.height), prefix, _textDim);
            var valueStyle = new GUIStyle(enabled ? _text : _textDim)
            {
                clipping = TextClipping.Clip,
                alignment = TextAnchor.MiddleLeft,
            };
            GUI.Label(new Rect(rect.x + prefixWidth + s, rect.y,
                rect.width - prefixWidth - 14 * s, rect.height), value, valueStyle);
            DrawButtonGlyph(new Rect(rect.xMax - 9 * s, rect.y, 7 * s,
                rect.height), IconDown, false, s);
        }

        private static string DropdownHeaderValue(string value) => value switch
        {
            "EVERYTHING" => "ALL",
            "MERGE EMPTY" => "MERGE",
            _ => value,
        };

        private static Rect DropdownMenuRect(Rect header, int optionCount, int s) =>
            new Rect(header.x, header.yMax, header.width, optionCount * DropdownRowH * s);

        private static Rect DropdownMenuRect(Rect header, string[] options, int s) =>
            DropdownMenuRect(header, options.Length, s);

        private static void DismissDropdownOutside(params Rect[] protectedRects)
        {
            if (_openDropdown == null) return;
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0) return;
            foreach (var rect in protectedRects)
                if (rect.Contains(e.mousePosition)) return;
            _openDropdown = null;
            e.Use();
        }

        private enum ButtonTone { Neutral, Confirm, Info, Highlight, Danger }

        private static bool SolidActionButton(Rect r, string label, string tooltip, int s,
            bool enabled = true, ButtonTone tone = ButtonTone.Neutral, bool selected = false)
            => Button(r, label, tooltip, s, selected: selected, enabled: enabled, tone: tone);

        private static bool Button(Rect r, string label, string tooltip, int s,
            bool selected = false, bool enabled = true, bool accent = false,
            ButtonTone tone = ButtonTone.Neutral, int glyph = -1, bool glyphFlipX = false,
            bool dropdownControl = false)
        {
            var e = Event.current;
            if (_openDropdown != null && !dropdownControl &&
                e.type == EventType.MouseDown && e.button == 0) return false;
            bool hover = enabled && r.Contains(e.mousePosition);
            if (hover && e.type == EventType.Repaint) _hint = tooltip;

            Color fill;
            if (!enabled) fill = Hex(0xD2B99B);
            else if (selected || accent) fill = Hex(0xD3A47E);
            else switch (tone)
            {
                case ButtonTone.Confirm: fill = Hex(0xB9C986); break;
                case ButtonTone.Info: fill = Hex(0xB8C7C0); break;
                case ButtonTone.Danger: fill = Hex(0xCF8D78); break;
                case ButtonTone.Highlight: fill = Hex(0xD8B77B); break;
                default: fill = Hex(0xDFC09B); break;
            }
            if (hover && enabled)
                fill = Color.Lerp(fill, Hex(0xF0D7B3), 0.42f);
            FillRect(r, fill);

            bool lightText = false;
            var labelStyle = !enabled ? _btnLabelDim : lightText ? _btnLabelLight : _btnLabel;
            bool hasLabel = !string.IsNullOrEmpty(label);
            if (glyph >= 0 && hasLabel)
            {
                float box = r.height;
                DrawButtonGlyph(new Rect(r.x + 2 * s, r.y, box, r.height), glyph, lightText, s, glyphFlipX);
                GUI.Label(new Rect(r.x + box, r.y, r.width - box - 2 * s, r.height), label, labelStyle);
            }
            else if (glyph >= 0)
                DrawButtonGlyph(r, glyph, lightText, s, glyphFlipX);
            else if (hasLabel)
                GUI.Label(r, label, labelStyle);

            if (enabled && hover && e.type == EventType.MouseDown && e.button == 0)
            {
                e.Use();
                return true;
            }
            return false;
        }

        private static void DrawButtonGlyph(Rect r, int cell, bool light, int s, bool flipX = false)
        {
            Color c = light ? new Color(0.96f, 0.93f, 0.84f) : TextMain;
            float cx = Mathf.Round(r.center.x);
            float cy = Mathf.Round(r.center.y);
            const int rad = 3;
            if (cell == IconClose) GlyphX(cx, cy, rad, c, s);
            else if (cell == IconUp) GlyphArrow(cx, cy, rad, 0, -1, c, s);
            else if (cell == IconDown) GlyphArrow(cx, cy, rad, 0, 1, c, s);
            else GlyphArrow(cx, cy, rad, flipX ? -1 : 1, 0, c, s);
        }

        private static void GlyphX(float cx, float cy, int rad, Color c, int s)
        {
            for (int i = -rad; i <= rad; i++)
            {
                FillRect(new Rect(cx + i * s, cy + i * s, s, s), c);
                FillRect(new Rect(cx + i * s, cy - i * s, s, s), c);
            }
        }

        private static void GlyphArrow(float cx, float cy, int rad, int dirX, int dirY, Color c, int s)
        {
            for (int j = 0; j <= rad; j++)
            {
                int hh = rad - j;
                if (dirX != 0)
                {
                    float px = cx + dirX * (j - rad / 2f) * s;
                    FillRect(new Rect(px, cy - hh * s, s, (2 * hh + 1) * s), c);
                }
                else
                {
                    float py = cy + dirY * (j - rad / 2f) * s;
                    FillRect(new Rect(cx - hh * s, py, (2 * hh + 1) * s, s), c);
                }
            }
        }

        private static bool LucideButton(Rect rect, int cell, string tooltip, int s,
            bool enabled = true, ButtonTone tone = ButtonTone.Neutral)
        {
            bool clicked = Button(rect, "", tooltip, s, enabled: enabled, tone: tone);
            Color color = !enabled ? TextDim : tone == ButtonTone.Danger ? Color.white : TextMain;
            DrawLucideIcon(InsetRect(rect, 2 * s), cell, color);
            return clicked;
        }

        private static void DrawLucideIcon(Rect rect, int cell, Color color)
        {
            float cx = Mathf.Round(rect.center.x), cy = Mathf.Round(rect.center.y);
            int u = Mathf.Max(1, (int)(rect.width / 12f));
            switch (cell)
            {
                case 5: GlyphArrow(cx, cy, 3, -1, 0, color, u); break;
                case 6: GlyphArrow(cx, cy, 3, 1, 0, color, u); break;
                case 7: GlyphX(cx, cy, 3, color, u); break;
                default: GlyphX(cx, cy, 2, color, u); break;
            }
        }

        private static void GlyphCheck(float cx, float cy, Color c, int s)
        {
            FillRect(new Rect(cx - 2 * s, cy, s, s), c);
            FillRect(new Rect(cx - 1 * s, cy + s, s, s), c);
            FillRect(new Rect(cx, cy + 2 * s, s, s), c);
            FillRect(new Rect(cx + 1 * s, cy + s, s, s), c);
            FillRect(new Rect(cx + 2 * s, cy, s, s), c);
            FillRect(new Rect(cx + 3 * s, cy - s, s, s), c);
        }

        private static int DividerLine(int x, int y, int w, int s)
        {
            FillRect(new Rect(x, y + Gap * s, w, 1 * s), DividerCol);
            return y + (Gap + 1 + SectGap) * s;
        }

        private static void FillRect(Rect r, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static Rect Expand(Rect r, float px) =>
            new Rect(r.x - px, r.y - px, r.width + 2 * px, r.height + 2 * px);

        private static void DrawCursor(Vector2 m, int s)
        {
            int k = Mathf.Max(1, s / 2);
            if (UiAssets.Cursor != null)
            {
                GUI.DrawTexture(new Rect(m.x - 2 * k, m.y - 2 * k, 16 * k, 16 * k), UiAssets.Cursor);
            }
            else
            {
                FillRect(new Rect(m.x - 4 * k, m.y - k / 2f, 8 * k, k), TextMain);
                FillRect(new Rect(m.x - k / 2f, m.y - 4 * k, k, 8 * k), TextMain);
            }
        }
    }
}

