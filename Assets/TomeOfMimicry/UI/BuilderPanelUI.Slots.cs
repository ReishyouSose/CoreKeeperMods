using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace TomeOfMimicry
{
    public static partial class BuilderPanelUI
    {
        private static void DrawBlueprintMiniature(Rect rect, Blueprint bp)
        {
            FillRect(rect, Hex(0xE9A574));
            if (bp == null || bp.width <= 0 || bp.height <= 0) return;
            float cell = Mathf.Max(1f, Mathf.Min(rect.width / bp.width, rect.height / bp.height));
            float ox = rect.x + (rect.width - bp.width * cell) * 0.5f;
            float oy = rect.y + (rect.height - bp.height * cell) * 0.5f;
            foreach (var tile in bp.tiles)
            {
                Color color = tile.hasWall ? Hex(0x7D4338)
                    : tile.hasFloor ? Hex(0xC97958)
                    : tile.hasWire ? Hex(0xE0B23D)
                    : Hex(0xA86B53);
                FillRect(new Rect(ox + tile.relativePos.x * cell,
                    oy + tile.relativePos.z * cell, Mathf.Max(1f, cell), Mathf.Max(1f, cell)), color);
            }
            foreach (var obj in bp.objects)
                FillRect(new Rect(ox + obj.relativePos.x * cell,
                    oy + obj.relativePos.y * cell, Mathf.Max(1f, cell), Mathf.Max(1f, cell)), Accent);
        }

        private static void DrawBlueprintCutoutMiniature(Rect rect, Blueprint bp,
            int rotation = 0, bool flipped = false)
        {
            if (bp == null || bp.width <= 0 || bp.height <= 0) return;

            var size = BlueprintManager.GetRotatedSize(bp, rotation);
            float cell = Mathf.Max(1f, Mathf.Min(rect.width / size.x, rect.height / size.y));
            float ox = rect.x + (rect.width - size.x * cell) * 0.5f;
            float oy = rect.y + (rect.height - size.y * cell) * 0.5f;
            float line = Mathf.Max(1f, Mathf.Floor(cell * 0.18f));
            Color floorFill = new Color(0.72f, 0.46f, 0.32f, 0.42f);
            Color wallFill = new Color(0.58f, 0.34f, 0.24f, 0.62f);
            Color wireFill = new Color(0.82f, 0.56f, 0.34f, 0.58f);
            Color cutLight = new Color(0.88f, 0.67f, 0.47f, 0.82f);
            Color cutMid = new Color(0.66f, 0.40f, 0.29f, 0.62f);
            Color cutShadow = new Color(0.43f, 0.24f, 0.18f, 0.52f);
            Color objectMark = new Color(0.64f, 0.39f, 0.28f, 0.76f);

            var floorCells = new HashSet<Vector2Int>();
            var wallCells = new HashSet<Vector2Int>();
            var wireCells = new HashSet<Vector2Int>();
            foreach (var tile in bp.tiles)
            {
                var transformed = BlueprintManager.GetRotatedRelPos(bp, rotation, flipped,
                    new int2(tile.relativePos.x, tile.relativePos.z));
                var pos = new Vector2Int(transformed.x, transformed.y);
                if (tile.hasWall) wallCells.Add(pos);
                else if (tile.hasWire) wireCells.Add(pos);
                else if (tile.hasFloor) floorCells.Add(pos);
            }

            foreach (var tile in bp.tiles)
            {
                var transformed = BlueprintManager.GetRotatedRelPos(bp, rotation, flipped,
                    new int2(tile.relativePos.x, tile.relativePos.z));
                var pos = new Vector2Int(transformed.x, transformed.y);
                var cellRect = new Rect(ox + transformed.x * cell,
                    oy + transformed.y * cell, Mathf.Max(1f, cell), Mathf.Max(1f, cell));
                if (tile.hasWall)
                    DrawCutoutCell(cellRect, line, wallFill, cutLight, cutMid, cutShadow, pos, wallCells);
                else if (tile.hasFloor || tile.hasWire)
                    DrawCutoutCell(InsetRect(cellRect, line), line,
                        tile.hasWire ? wireFill : floorFill, cutLight, cutMid, cutShadow,
                        pos, tile.hasWire ? wireCells : floorCells);
            }

            foreach (var obj in bp.objects)
            {
                var transformed = BlueprintManager.GetRotatedRelPos(bp, rotation, flipped, obj.relativePos);
                var cellRect = new Rect(ox + transformed.x * cell,
                    oy + transformed.y * cell, Mathf.Max(1f, cell), Mathf.Max(1f, cell));
                var objectRect = InsetRect(cellRect, Mathf.Max(1f, cell * 0.24f));
                FillRect(new Rect(objectRect.x + line, objectRect.y + line,
                    objectRect.width, objectRect.height), cutShadow);
                FillRect(objectRect, objectMark);
                DrawRectOutline(objectRect, line, cutLight);
            }
        }

        private static void DrawBlueprintPortraitFootprint(Rect portrait, Blueprint bp,
            int rotation, bool flipped, int s)
        {
            if (bp == null || bp.width <= 0 || bp.height <= 0) return;

            Color parchment = Hex(0xD7B594);
            Color shadow = Hex(0xC09473);
            Color highlight = Hex(0xE7D5B3);

            // Preserve the original outer frame while replacing its sword scene.
            var canvas = new Rect(portrait.x + 2 * s, portrait.y + 2 * s,
                portrait.width - 4 * s, portrait.height - 4 * s);
            FillRect(canvas, parchment);
            FillRect(new Rect(canvas.x, canvas.yMax - s, canvas.width, s), shadow);
            FillRect(new Rect(canvas.x + s, canvas.y, canvas.width - 2 * s, s), highlight);

            DrawPortraitSparkle(canvas.x + 7 * s, canvas.y + 7 * s, s, shadow);
            DrawPortraitSparkle(canvas.xMax - 9 * s, canvas.y + 10 * s, s, shadow);
            FillRect(new Rect(canvas.x + 15 * s, canvas.y + 16 * s, s, s), shadow);
            FillRect(new Rect(canvas.xMax - 18 * s, canvas.y + 5 * s, s, s), shadow);

            var occupied = new HashSet<Vector2Int>();
            var objectCells = new HashSet<Vector2Int>();
            var wallCells = new HashSet<Vector2Int>();
            var wireCells = new HashSet<Vector2Int>();
            foreach (var tile in bp.tiles)
            {
                var transformed = BlueprintManager.GetRotatedRelPos(bp, rotation, flipped,
                    new int2(tile.relativePos.x, tile.relativePos.z));
                var pos = new Vector2Int(transformed.x, transformed.y);
                occupied.Add(pos);
                if (tile.hasWall)
                    wallCells.Add(pos);
                else if (tile.hasWire)
                    wireCells.Add(pos);
            }
            foreach (var obj in bp.objects)
            {
                var transformed = BlueprintManager.GetRotatedRelPos(bp, rotation, flipped, obj.relativePos);
                var pos = new Vector2Int(transformed.x, transformed.y);
                occupied.Add(pos);
                objectCells.Add(pos);
            }
            if (occupied.Count == 0) return;

            var size = BlueprintManager.GetRotatedSize(bp, rotation);
            var art = new Rect(canvas.x + 8 * s, canvas.y + 5 * s,
                canvas.width - 16 * s, canvas.height - 11 * s);
            float cell = Mathf.Max(s, Mathf.Floor(Mathf.Min(
                art.width / Mathf.Max(1, size.x),
                art.height / Mathf.Max(1, size.y)) / s) * s);
            float separator = cell >= 3 * s ? s : 0f;
            float tileSize = cell - separator;
            float footprintW = Mathf.Max(tileSize, (size.x - 1) * cell + tileSize);
            float footprintH = Mathf.Max(tileSize, (size.y - 1) * cell + tileSize);
            float ox = Mathf.Round((art.x + (art.width - footprintW) * 0.5f) / s) * s;
            float oy = Mathf.Round((art.y + (art.height - footprintH) * 0.5f) / s) * s;
            float edge = s;

            // Flood-fill empty cells from outside the blueprint bounds. This distinguishes
            // the true exterior from enclosed rooms/courtyards, so the drop shadow cannot
            // appear as random dark borders inside a wall-only footprint.
            var outside = new HashSet<Vector2Int>();
            var pending = new Queue<Vector2Int>();
            var outsideStart = new Vector2Int(-1, -1);
            outside.Add(outsideStart);
            pending.Enqueue(outsideStart);
            void QueueOutside(int px, int py)
            {
                var neighbour = new Vector2Int(px, py);
                if (px < -1 || py < -1 || px > size.x || py > size.y ||
                    occupied.Contains(neighbour) || !outside.Add(neighbour))
                    return;
                pending.Enqueue(neighbour);
            }
            while (pending.Count > 0)
            {
                var empty = pending.Dequeue();
                QueueOutside(empty.x - 1, empty.y);
                QueueOutside(empty.x + 1, empty.y);
                QueueOutside(empty.x, empty.y - 1);
                QueueOutside(empty.x, empty.y + 1);
            }

            // One-pixel down/right shadow only on the exterior contour.
            foreach (var pos in occupied)
            {
                var r = new Rect(ox + pos.x * cell, oy + pos.y * cell, tileSize, tileSize);
                bool exteriorRight = outside.Contains(new Vector2Int(pos.x + 1, pos.y));
                bool exteriorBottom = outside.Contains(new Vector2Int(pos.x, pos.y + 1));
                if (exteriorRight)
                    FillRect(new Rect(r.xMax, r.y + edge, edge, r.height), shadow);
                if (exteriorBottom)
                    FillRect(new Rect(r.x + edge, r.yMax, r.width, edge), shadow);
                if (exteriorRight && exteriorBottom)
                    FillRect(new Rect(r.xMax, r.yMax, edge, edge), shadow);
            }
            foreach (var pos in occupied)
            {
                var r = new Rect(ox + pos.x * cell, oy + pos.y * cell, tileSize, tileSize);
                Color fill = wallCells.Contains(pos) ? highlight
                    : wireCells.Contains(pos) ? Hex(0xD8B77B)
                    : Hex(0xDFC09B);
                FillRect(r, fill);
            }

            foreach (var pos in objectCells)
            {
                if (cell >= 4 * s)
                {
                    var r = new Rect(ox + pos.x * cell, oy + pos.y * cell, tileSize, tileSize);
                    FillRect(InsetRect(r, Mathf.Max(edge * 2f, s)), shadow);
                }
            }
        }

        private static void DrawPortraitSparkle(float x, float y, int s, Color color)
        {
            FillRect(new Rect(x + s, y, s, 3 * s), color);
            FillRect(new Rect(x, y + s, 3 * s, s), color);
        }

        private static void DrawCutoutCell(Rect r, float t, Color fill, Color light,
            Color mid, Color shadow, Vector2Int pos, HashSet<Vector2Int> group)
        {
            FillRect(new Rect(r.x + t, r.y + t, r.width, r.height), shadow);
            FillRect(InsetRect(r, t), fill);

            bool left = !group.Contains(new Vector2Int(pos.x - 1, pos.y));
            bool right = !group.Contains(new Vector2Int(pos.x + 1, pos.y));
            bool top = !group.Contains(new Vector2Int(pos.x, pos.y - 1));
            bool bottom = !group.Contains(new Vector2Int(pos.x, pos.y + 1));

            if (top) FillRect(new Rect(r.x, r.y, r.width, t), light);
            if (left) FillRect(new Rect(r.x, r.y, t, r.height), light);
            if (right) FillRect(new Rect(r.xMax - t, r.y + t, t, r.height), shadow);
            if (bottom) FillRect(new Rect(r.x + t, r.yMax - t, r.width, t), shadow);

            if (!top && !left)
                FillRect(new Rect(r.x, r.y, t, t), mid);
            if (!right && !bottom)
                FillRect(new Rect(r.xMax - t, r.yMax - t, t, t), mid);
        }

        private static void DrawEmptyBlueprintMark(Rect rect, int s)
        {
            Color ink = new Color(0.94f, 0.98f, 1f, 0.82f);
            FillRect(new Rect(rect.x + 3 * s, rect.y + 3 * s, 17 * s, 12 * s), ink);
            FillRect(new Rect(rect.x + 7 * s, rect.y + 7 * s, 17 * s, 12 * s), Hex(0x23659A));
            FillRect(new Rect(rect.x + 10 * s, rect.y + 10 * s, 17 * s, 12 * s), ink);
            FillRect(new Rect(rect.x + 12 * s, rect.y + 12 * s, 13 * s, 8 * s), Hex(0x23659A));
        }

        private static bool DrawToolSelector(Rect rect, string label, BuildTool tool, int s, bool selected, bool focused)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            var frame = new Rect(rect.x + 2 * s, rect.y + s, rect.width - 4 * s, rect.height - 2 * s);
            if (selected || focused || hover)
            {
                Color fill = selected ? new Color(0.86f, 0.60f, 0.31f, 0.36f)
                    : new Color(0.86f, 0.60f, 0.31f, 0.22f);
                FillRect(InsetRect(frame, s), fill);
                DrawRectOutline(InsetRect(frame, s), 1, selected ? Hex(0xDFA444) : Hex(0xC6906C));
                FillRect(new Rect(frame.x + 4 * s, frame.yMax - 3 * s,
                    frame.width - 8 * s, s), selected ? Hex(0xDFA444) : Hex(0xC6906C));
            }

            DrawToolAssetIcon(new Rect(rect.center.x - 6 * s, rect.y + 3 * s, 12 * s, 12 * s),
                tool, selected);
            GUI.Label(new Rect(rect.x + s, rect.yMax - 8 * s, rect.width - 2 * s, 6 * s), label,
                selected ? _textCenter : _textDimCenter);
            return InvisibleClick(rect, $"Select {label} tool");
        }

        private static void DrawToolAssetIcon(Rect rect, BuildTool tool, bool selected)
        {
            Color c = selected ? Accent : Hex(0x7D4338);
            var inner = InsetRect(rect, rect.width * 0.2f);
            float cx = Mathf.Round(inner.center.x), cy = Mathf.Round(inner.center.y);
            int u = Mathf.Max(1, (int)(rect.width / 14f));
            switch (tool)
            {
                case BuildTool.Blueprint: DrawRectOutline(inner, u, c); break;
                case BuildTool.Cut: GlyphX(cx, cy, 3, c, u); break;
                case BuildTool.WallArea: FillRect(InsetRect(inner, inner.width * 0.18f), c); break;
                case BuildTool.Circle: DrawRing(cx, cy, inner.width * 0.45f, c, u); break;
                default: GlyphArrow(cx, cy, 3, 0, -1, c, u); break;
            }
        }

        private static void DrawRectOutline(Rect r, float t, Color c)
        {
            FillRect(new Rect(r.x, r.y, r.width, t), c);
            FillRect(new Rect(r.x, r.yMax - t, r.width, t), c);
            FillRect(new Rect(r.x, r.y, t, r.height), c);
            FillRect(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        private static void DrawRing(float cx, float cy, float rad, Color c, int u)
        {
            const int steps = 18;
            for (int i = 0; i < steps; i++)
            {
                float a = i / (float)steps * Mathf.PI * 2f;
                FillRect(new Rect(cx + Mathf.Cos(a) * rad - u * 0.5f, cy + Mathf.Sin(a) * rad - u * 0.5f, u, u), c);
            }
        }

        private static void DrawBookCard(Rect rect, int s) => FbBox(rect, "fbBox12", 5, s);

        private static void DrawBookOutlinePanel(Rect rect, int s) => FbBox(rect, "fbBox44", 5, s);

        private static void DrawBookFilledPanel(Rect rect, int s) => FbBox(rect, "fbBox2", 3, s);

        private static void DrawBookFrame(Rect rect, int s, BookFrameStyle style = BookFrameStyle.Card)
        {
            string sprite = style switch
            {
                BookFrameStyle.Outline => "fbBox44",
                BookFrameStyle.Filled => "fbBox2",
                _ => "fbBox12",
            };
            FbBox(rect, sprite, 5, s);
        }

        private static Rect InsetRect(Rect rect, float amount) => new Rect(
            rect.x + amount, rect.y + amount,
            Mathf.Max(0f, rect.width - amount * 2f), Mathf.Max(0f, rect.height - amount * 2f));

        private static bool InvisibleClick(Rect r, string tooltip)
        {
            var e = Event.current;
            bool hover = r.Contains(e.mousePosition);
            if (hover && e.type == EventType.Repaint) _hint = tooltip;
            if (hover && e.type == EventType.MouseDown && e.button == 0)
            {
                e.Use();
                return true;
            }
            return false;
        }
    }
}

