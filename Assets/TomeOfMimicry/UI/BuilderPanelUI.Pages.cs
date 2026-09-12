using System.Collections.Generic;
using UnityEngine;

namespace TomeOfMimicry
{
    public static partial class BuilderPanelUI
    {
        private static void DrawBookLeftPage(int x, int y, int w, int s)
        {
            if (_bookPage == BookPage.Library)
            {
                DrawFantasyLibraryPage(x, y, w, s);
                return;
            }
            if (_bookPage == BookPage.Tools)
            {
                DrawToolsPage(x, y, w, s);
                return;
            }
            if (_bookPage == BookPage.Options)
            {
                DrawOptionsPage(x, y, w, s);
                return;
            }
            if (_bookPage == BookPage.Tags)
            {
                DrawTagsPage(x, y, w, s);
                return;
            }
            if (_bookPage == BookPage.Details)
            {
                DrawDetailsPage(x, y, w, s);
                return;
            }
            if (_bookPage == BookPage.Materials)
                DrawExpandedMaterials(x, y, w, s);
        }

        private static void DrawToolsPage(int x, int y, int w, int s)
        {
            var tool = BuildToolState.ActiveTool;
            string[] labels = { "BLUEPRINT", "CUT AREA", "WALL AREA", "CIRCLE / OVAL", "POLYGON" };
            string[] descriptions =
            {
                "COPY AND PLACE AREAS",
                "REMOVE PLACED CONTENT",
                "FILL AN AREA WITH WALLS",
                "GENERATE ROUND SHAPES",
                "GENERATE REGULAR SHAPES",
            };
            BuildTool[] tools = { BuildTool.Blueprint, BuildTool.Cut, BuildTool.WallArea,
                                  BuildTool.Circle, BuildTool.Polygon };
            FbBanner(new Rect(x + 13 * s, y - 2 * s, (w - 26) * s, 19 * s), "TOOLS", s, _heading);
            y += 21 * s;
            FillRect(new Rect(x + 9 * s, y, (w - 18) * s, s), Hex(0xD0A079));
            FillRect(new Rect(x + 18 * s, y + 2 * s, (w - 36) * s, s), Hex(0xE3BF94));
            y += 8 * s;

            const int rowH = 15;
            const int rowGap = 1;
            for (int i = 0; i < tools.Length; i++)
            {
                var row = new Rect(x, y + i * (rowH + rowGap) * s, (w - 2) * s, rowH * s);
                if (DrawReferenceToolRow(row, labels[i], descriptions[i], tools[i], s,
                        tool == tools[i], i == _pageFocus))
                {
                    if (tool == tools[i])
                        ConfirmBookTool(tools[i]);
                    else
                        SelectBookTool(tools[i]);
                    _pageFocus = i;
                }
            }

            var continueRect = new Rect(x + 25 * s, y + 82 * s, (w - 52) * s, 9 * s);
            if (_pageFocus == 5)
                FillRect(continueRect, Hex(0xD3A47E));
            if (SolidActionButton(continueRect, "CONTINUE", "Configure the selected tool", s,
                    tone: ButtonTone.Confirm))
                ConfirmBookTool(tool);
        }

        private static bool DrawReferenceToolRow(Rect r, string title, string description,
            BuildTool tool, int s, bool selected, bool focused)
        {
            var e = Event.current;
            bool hover = r.Contains(e.mousePosition);
            Color fill = selected ? Hex(0xD7B08A)
                : hover || focused ? Hex(0xDFC09B)
                : Hex(0xD9B693);
            FillRect(r, fill);
            FillRect(new Rect(r.x, r.yMax - s, r.width, s), Hex(0xE8C9A0));

            if (focused)
                FillRect(new Rect(r.x - 2 * s, r.y + 2 * s, s, r.height - 4 * s), Hex(0xC86D48));

            var iconFrame = new Rect(r.x + 2 * s, r.y + 2 * s, 11 * s, 11 * s);
            DrawReferenceIconFrame(iconFrame, s);
            DrawToolAssetIcon(InsetRect(iconFrame, 2 * s), tool, selected);

            GUI.Label(new Rect(r.x + 15 * s, r.y, r.width - 31 * s, 7 * s),
                title, selected ? _fbRowTitle : _fbRowTitleDim);
            GUI.Label(new Rect(r.x + 15 * s, r.y + 7 * s, r.width - 31 * s, 6 * s),
                description, _fbRowSub);

            var state = new Rect(r.xMax - 11 * s, r.y + 3 * s, 8 * s, 8 * s);
            FillRect(state, Hex(0xEAD8B6));
            bool unavailable = (tool == BuildTool.WallArea || tool == BuildTool.Circle ||
                                tool == BuildTool.Polygon) && !HasWallMaterial();
            if (selected)
                GlyphCheck(state.center.x - s, state.center.y - s, Hex(0x96C94C), s);
            else if (unavailable)
                GlyphX(state.center.x, state.center.y, 2, Hex(0xD65B40), s);

            if (hover && e.type == EventType.Repaint)
                _hint = unavailable
                    ? $"{title}: select a blueprint containing walls first"
                    : selected
                        ? $"Confirm {title.ToLowerInvariant()} and open options"
                        : $"Select {title.ToLowerInvariant()} tool";
            return InvisibleClick(r, selected
                ? $"Confirm {title.ToLowerInvariant()} and open options"
                : $"Select {title.ToLowerInvariant()} tool");
        }

        private static void DrawToolWorkbench(int x, int y, int w, int s, BuildTool tool)
        {
            int innerX = x + 2 * s;
            int innerW = w - 4;

            if (tool == BuildTool.Circle || tool == BuildTool.Polygon)
            {
                DrawBookShapeControls(x, y, w, s, tool == BuildTool.Polygon);
                return;
            }

            if (tool == BuildTool.Blueprint)
            {
                DrawBlueprintWorkbench(x, y, w, s);
                return;
            }

            var note = new Rect(innerX, y, innerW * s, 29 * s);
            FbTonalPanel(note, s);
            FbBookIcon(new Rect(note.xMax - 20 * s, note.y + 5 * s, 14 * s, 14 * s),
                tool == BuildTool.Cut ? 2 : 12, 0.14f, s);
            GUI.Label(InsetRect(note, 5 * s),
                tool == BuildTool.Cut
                    ? "CUT\nDrag an area in the world to remove placed content."
                    : "WALL\nDrag an area to fill with the selected wall material.",
                WrappedStyle(_textDim));

            if (tool == BuildTool.WallArea)
                DrawMaterialSourceStrip(innerX, y + 32 * s, innerW, s, BookPage.Options, BuildTool.WallArea);

            DrawToolActionButtons(innerX, y + 46 * s, innerW, s);
        }

        private static void DrawBlueprintWorkbench(int x, int y, int w, int s)
        {
            int innerX = x + 2 * s;
            int innerW = w - 4;
            string[] captureChoices = { "EVERYTHING", "FLOORS", "WALLS", "OBJECTS" };
            string[] placementChoices = { "REPLACE", "MERGE EMPTY" };

            var guide = new Rect(innerX, y, innerW * s, 11 * s);
            FbTonalPanel(guide, s, false);
            GUI.Label(new Rect(innerX + 4 * s, y + s, (innerW - 8) * s, 8 * s), GuideText(), _textGuide);
            y += 14 * s;

            int half = (innerW - 4) / 2;
            var captureRect = new Rect(innerX, y, half * s, 10 * s);
            var placementRect = new Rect(innerX + (half + 4) * s, y, half * s, 10 * s);
            y += 13 * s;

            int halfButton = (innerW - 4) / 2;
            if (Button(new Rect(innerX, y, halfButton * s, 10 * s), "LOAD BP",
                    "Choose a saved blueprint", s, tone: ButtonTone.Info))
                OpenBlueprintLibrary();
            if (Button(new Rect(innerX + (halfButton + 4) * s, y, halfButton * s, 10 * s), "IMPORT",
                    "Import a CKBP1 blueprint", s, tone: ButtonTone.Info, glyph: IconDown))
            {
                var imported = BlueprintSerializer.Import(GUIUtility.systemCopyBuffer);
                if (imported != null)
                {
                    bool wasPickingMaterial = _libraryMaterialPicker;
                    SelectLibraryBlueprint(imported.name, imported);
                    if (!wasPickingMaterial)
                        ShowFeedback($"Imported {imported.name}");
                }
                else ShowFeedback("Clipboard blueprint is invalid");
            }

            DrawToolActionButtons(innerX, y + 14 * s, innerW, s);
            Dropdown(captureRect, "blueprint-capture", "CAP", captureChoices,
                (int)BlueprintManager.CopyMode, s, index => BlueprintManager.CopyMode = (BlueprintCopyMode)index);
            Dropdown(placementRect, "blueprint-placement", "PLACE", placementChoices,
                BlueprintManager.PasteMode == BlueprintPasteMode.Replace ? 0 : 1, s, index =>
                    BlueprintManager.PasteMode = index == 0
                        ? BlueprintPasteMode.Replace : BlueprintPasteMode.MergeEmpty);
            DismissDropdownOutside(captureRect, placementRect,
                DropdownMenuRect(captureRect, captureChoices, s),
                DropdownMenuRect(placementRect, placementChoices, s));
        }

        private static void DrawToolActionButtons(int innerX, int y, int innerW, int s)
        {
            const int actionW = 16;
            const int actionGap = 5;
            int actionRowW = actionW * 3 + actionGap * 2;
            float tx = innerX + (innerW - actionRowW) * s * 0.5f;
            if (LucideButton(new Rect(tx, y, actionW * s, 10 * s), 5,
                    "Undo last placement (Ctrl+U)", s, BlueprintManager.CanUndo)) BlueprintManager.RequestUndo();
            if (LucideButton(new Rect(tx + (actionW + actionGap) * s, y, actionW * s, 10 * s), 6,
                    "Redo last placement (Ctrl+Shift+U)", s, BlueprintManager.CanRedo)) BlueprintManager.RequestRedo();
            if (LucideButton(new Rect(tx + 2 * (actionW + actionGap) * s, y, actionW * s, 10 * s), 7,
                    "Cancel current action", s,
                    enabled: BlueprintManager.State != GadgetState.Idle || BlueprintManager.ActiveBlueprint != null,
                    tone: ButtonTone.Danger))
                BlueprintManager.Cancel();
        }

        private static void DrawOptionsPage(int x, int y, int w, int s)
        {
            int pageTop = y;
            var tool = BuildToolState.ActiveTool;

            FbBanner(new Rect(x + 13 * s, y - 2 * s, (w - 26) * s, 19 * s), "OPTIONS", s, _heading);
            y += 20 * s;
            FillRect(new Rect(x + 9 * s, y, (w - 18) * s, s), Hex(0xD0A079));
            FillRect(new Rect(x + 18 * s, y + 2 * s, (w - 36) * s, s), Hex(0xE3BF94));
            y += 7 * s;

            var activeRow = new Rect(x, y, (w - 2) * s, 17 * s);
            FillRect(activeRow, Hex(0xD7B08A));
            FillRect(new Rect(activeRow.x, activeRow.yMax - s, activeRow.width, s), Hex(0xE8C9A0));
            var iconFrame = new Rect(activeRow.x + 2 * s, activeRow.y + 2 * s, 13 * s, 13 * s);
            DrawReferenceIconFrame(iconFrame, s);
            DrawToolAssetIcon(InsetRect(iconFrame, 3 * s), tool, true);
            const int backColumnW = 20;
            GUI.Label(new Rect(activeRow.x + 17 * s, activeRow.y + s,
                    activeRow.width - (17 + backColumnW) * s, 7 * s),
                TitleText(), _fbRowTitle);
            GUI.Label(new Rect(activeRow.x + 17 * s, activeRow.y + 8 * s,
                    activeRow.width - (17 + backColumnW) * s, 6 * s),
                "SELECTED TOOL SETTINGS", _fbRowSub);
            FillRect(new Rect(activeRow.xMax - backColumnW * s, activeRow.y + 2 * s,
                s, activeRow.height - 4 * s), Hex(0xC99A73));
            var back = new Rect(activeRow.xMax - 17 * s, activeRow.y + 4 * s, 14 * s, 9 * s);
            int optionsMax = tool == BuildTool.Blueprint ? 4
                : tool == BuildTool.Circle || tool == BuildTool.Polygon ? 5
                : tool == BuildTool.WallArea ? 3 : 2;
            if (SolidActionButton(back, "BACK", "Return to tool selection", s,
                    selected: _pageFocus == optionsMax))
            {
                _bookPage = BookPage.Tools;
                return;
            }

            y += 20 * s;
            DrawToolWorkbench(x, y, w, s, tool);
            GUI.Label(new Rect(x, pageTop + FbBottomMetaY * s, (w - 2) * s, 7 * s),
                $"FOCUS: {OptionsFocusLabel(tool, _pageFocus)}", _fbTinyCenter);
        }

        private static string OptionsFocusLabel(BuildTool tool, int focus)
        {
            string[] labels = tool switch
            {
                BuildTool.Blueprint => new[] { "CAPTURE MODE", "PLACE MODE", "LIBRARY", "UNDO", "BACK" },
                BuildTool.Circle => new[] { "WIDTH", "HEIGHT", "HOLLOW", "ANCHOR", "MATERIAL", "BACK" },
                BuildTool.Polygon => new[] { "WIDTH", "SIDES", "HOLLOW", "ANCHOR", "MATERIAL", "BACK" },
                BuildTool.WallArea => new[] { "MATERIAL", "UNDO", "CANCEL", "BACK" },
                _ => new[] { "UNDO", "CANCEL", "BACK" },
            };
            return labels[Mathf.Clamp(focus, 0, labels.Length - 1)];
        }

        private static void DrawTagsPage(int x, int y, int w, int s)
        {
            var bp = BlueprintManager.ActiveBlueprint;
            if (bp == null)
            {
                GUI.Label(new Rect(x, y, w * s, 20 * s), "Select a saved or captured blueprint first.",
                    WrappedStyle(_textDim));
                return;
            }

            DrawReferencePageHeader(x, ref y, w, "TAGS", s);
            var nameRow = new Rect(x, y, (w - 2) * s, 17 * s);
            DrawReferenceInfoRow(nameRow, 10, ReferenceTitle(bp.name, 20), TagSummary(bp), s);
            y += 20 * s;

            for (int i = 0; i < TagChoices.Length; i++)
            {
                string tag = TagChoices[i];
                bool selected = BlueprintLibraryStore.HasTag(bp, tag);
                var tagRect = new Rect(x, y + i * 15 * s, (w - 2) * s, 13 * s);
                FillRect(tagRect, selected ? Hex(0xD3A47E)
                    : i == _pageFocus ? Hex(0xE9CEAA) : Hex(0xDFC09B));
                FillRect(new Rect(tagRect.x + 3 * s, tagRect.yMax - s,
                    tagRect.width - 6 * s, s), Hex(0xE8C9A0));
                GUI.Label(new Rect(tagRect.x + 5 * s, tagRect.y, tagRect.width - 22 * s, tagRect.height),
                    tag, selected ? _fbRowTitle : _fbRowTitleDim);
                var state = new Rect(tagRect.xMax - 13 * s, tagRect.y + 2 * s, 9 * s, 9 * s);
                FillRect(state, Hex(0xEAD8B6));
                if (selected) GlyphCheck(state.center.x - s, state.center.y - s, ReadyCol, s);
                if (InvisibleClick(tagRect, $"Toggle {tag} tag"))
                {
                    ToggleTag(bp, tag);
                    _pageFocus = i;
                }
            }
            y += 62 * s;
            if (SolidActionButton(new Rect(x + 22 * s, y, (w - 46) * s, 9 * s),
                    "SAVE TAGS", "Persist tags", s, tone: ButtonTone.Confirm,
                    selected: _pageFocus == TagChoices.Length))
            {
                BlueprintSerializer.Save(bp);
                BlueprintLibraryStore.Put(bp);
                BlueprintLibraryStore.Invalidate();
                ShowFeedback("Tags saved");
            }
        }

        private static void DrawDetailsPage(int x, int y, int w, int s)
        {
            var bp = BlueprintManager.ActiveBlueprint;
            if (bp == null)
            {
                GUI.Label(new Rect(x, y, w * s, 20 * s), "No active blueprint.", _textDimCenter);
                return;
            }
            DrawReferencePageHeader(x, ref y, w, "DETAILS", s);
            DrawReferenceInfoRow(new Rect(x, y, (w - 2) * s, 17 * s), 4,
                ReferenceTitle(bp.name, 20), TagSummary(bp), s);
            y += 20 * s;
            string[] labels = { "SIZE", "TILES", "OBJECTS", "UNSUPPORTED" };
            string[] values = { $"{bp.width} X {bp.height}", bp.tiles.Count.ToString(),
                bp.objects.Count.ToString(), bp.unsupportedObjectCount.ToString() };
            for (int i = 0; i < labels.Length; i++)
            {
                var row = new Rect(x, y + i * 14 * s, (w - 2) * s, 12 * s);
                FillRect(row, i % 2 == 0 ? Hex(0xDFC09B) : Hex(0xE3C39D));
                GUI.Label(new Rect(row.x + 4 * s, row.y, 52 * s, row.height), labels[i], _fbRowTitleDim);
                GUI.Label(new Rect(row.x + 58 * s, row.y, row.width - 62 * s, row.height),
                    values[i], _fbValueRight);
            }
            y += 59 * s;
            int half = (w - 6) / 2;
            var saveRect = new Rect(x, y, half * s, 9 * s);
            var exportRect = new Rect(x + (half + 4) * s, y, half * s, 9 * s);
            if (SolidActionButton(saveRect, "SAVE", "Save this blueprint", s,
                    tone: ButtonTone.Confirm, selected: _pageFocus == 0))
            {
                _pageFocus = 0;
                if (BlueprintSerializer.Save(bp))
                {
                    BlueprintLibraryStore.Put(bp);
                    BlueprintLibraryStore.Invalidate();
                    ShowFeedback($"Saved {bp.name}");
                }
            }
            if (SolidActionButton(exportRect, "EXPORT", "Copy portable blueprint data", s,
                    tone: ButtonTone.Info, selected: _pageFocus == 1))
            {
                _pageFocus = 1;
                GUIUtility.systemCopyBuffer = BlueprintSerializer.Export(bp) ?? "";
                ShowFeedback("Blueprint copied to clipboard");
            }
        }

        private static void DrawExpandedMaterials(int x, int y, int w, int s)
        {
            var cost = TileCostTracker.CurrentCost;
            DrawReferencePageHeader(x, ref y, w, "MATERIALS", s);
            if (!TileCostTracker.IsActive || cost.Count == 0)
            {
                var empty = new Rect(x, y, (w - 2) * s, 38 * s);
                FillRect(empty, Hex(0xDFC09B));
                GUI.Label(InsetRect(empty, 6 * s), "HOLD A BLUEPRINT TO PREVIEW ITS MATERIALS.",
                    WrappedStyle(_textDimCenter));
                return;
            }
            bool blocked = TileCostTracker.IsBlocked;
            var status = new Rect(x, y, (w - 2) * s, 13 * s);
            FillRect(status, blocked ? Hex(0xCF8D78) : Hex(0xB9C986));
            GUI.Label(status, TileCostTracker.IsCreativeMode ? "CREATIVE - NO COST"
                : blocked ? "MISSING MATERIALS" : "ALL MATERIALS READY",
                blocked ? _textBlockedCenter : _textReadyCenter);
            y += 16 * s;
            const int rowsPerPage = 6;
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(cost.Count / (float)rowsPerPage));
            _materialPage = Mathf.Clamp(_materialPage, 0, pageCount - 1);
            int start = _materialPage * rowsPerPage;
            int end = Mathf.Min(cost.Count, start + rowsPerPage);
            for (int i = start; i < end; i++)
            {
                var entry = cost[i];
                bool enough = TileCostTracker.IsCreativeMode || entry.HasEnough;
                var row = new Rect(x, y, (w - 2) * s, 10 * s);
                FillRect(row, i % 2 == 0 ? Hex(0xDFC09B) : Hex(0xE3C39D));
                if (enough) GlyphCheck(row.x + 6 * s, row.y + 4 * s, ReadyCol, s);
                else GlyphX(row.x + 7 * s, row.y + 6 * s, 2, BlockedCol, s);
                GUI.Label(new Rect(row.x + 15 * s, row.y, row.width - 42 * s, row.height), FormatItemName(entry.objectId),
                    enough ? _text : _textBlocked);
                GUI.Label(new Rect(row.xMax - 25 * s, row.y, 21 * s, row.height),
                    TileCostTracker.IsCreativeMode ? $"x{entry.needed}" : $"{entry.available}/{entry.needed}",
                    enough ? _textReadyRight : _textBlockedRight);
                y += 11 * s;
            }

            int navY = y;
            if (SolidActionButton(new Rect(x, navY, 15 * s, 8 * s), "<", "Previous material page", s,
                    enabled: _materialPage > 0)) _materialPage--;
            GUI.Label(new Rect(x + 17 * s, navY, (w - 36) * s, 8 * s),
                $"PAGE {_materialPage + 1}/{pageCount}", _textDimCenter);
            if (SolidActionButton(new Rect(x + (w - 17) * s, navY, 15 * s, 8 * s), ">", "Next material page", s,
                    enabled: _materialPage + 1 < pageCount)) _materialPage++;
        }

        private static void DrawReferencePageHeader(int x, ref int y, int w, string title, int s)
        {
            FbBanner(new Rect(x + 13 * s, y - 2 * s, (w - 26) * s, 19 * s), title, s, _heading);
            y += 20 * s;
            FillRect(new Rect(x + 9 * s, y, (w - 18) * s, s), Hex(0xD0A079));
            FillRect(new Rect(x + 18 * s, y + 2 * s, (w - 36) * s, s), Hex(0xE3BF94));
            y += 7 * s;
        }

        private static void DrawReferenceInfoRow(Rect row, int icon, string title, string sub, int s)
        {
            FillRect(row, Hex(0xD7B08A));
            var frame = new Rect(row.x + 2 * s, row.y + 2 * s, 13 * s, 13 * s);
            DrawReferenceIconFrame(frame, s);
            FbBookIcon(InsetRect(frame, 2 * s), icon, 0.9f, s);
            GUI.Label(new Rect(row.x + 17 * s, row.y + s, row.width - 20 * s, 7 * s), title, _fbRowTitle);
            GUI.Label(new Rect(row.x + 17 * s, row.y + 8 * s, row.width - 20 * s, 6 * s), sub, _fbRowSub);
        }
    }
}

