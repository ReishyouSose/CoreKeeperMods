using UnityEngine;

namespace TomeOfMimicry
{
    public static partial class BuilderPanelUI
    {
        private const int FbookW = 290;
        private const int FbookH = 184;
        private const int FbLeftPageX = 32;
        private const int FbRightPageX = 157;
        private const int FbPageY = 22;
        private const int FbLeftPageW = 104;
        private const int FbRightPageW = 98;
        private const int FbPageSafeH = 136;
        private const int FbBottomActionY = 112;
        private const int FbBottomMetaY = 114;

        private static readonly (BookPage page, string label)[] FbTabs =
        {
            (BookPage.Library, "LIB"), (BookPage.Tools, "TOOL"), (BookPage.Options, "OPT"),
            (BookPage.Materials, "MAT"), (BookPage.Tags, "TAG"), (BookPage.Details, "INFO"),
        };

        private static BookPage _lastDrawnPage = BookPage.Library;
        private static float _flipUntil;
        private const float FlipDuration = 0.26f;

        private static void DrawFantasyBook(int s)
        {
            float screenWidth = _editorPreview ? _previewWidth : Screen.width;
            float screenHeight = _editorPreview ? _previewHeight : Screen.height;
            int top = _editorPreview ? Mathf.Max(12 * s, Mathf.RoundToInt((screenHeight - FbookH * s) * 0.5f))
                                     : TopInset * _gameScale;
            if (top + FbookH * s > screenHeight - 4 * s)
                top = Mathf.Max(4 * s, Mathf.RoundToInt(screenHeight - 4 * s - FbookH * s));

            float bookX = Mathf.Round((screenWidth - FbookW * s) * 0.5f);
            var book = new Rect(bookX, top, FbookW * s, FbookH * s);
            _screenRect = new Rect(book.x, book.y, book.width + 24 * s, book.height);

            if (_bookPage != _lastDrawnPage)
            {
                _flipUntil = Time.realtimeSinceStartup + FlipDuration;
                _lastDrawnPage = _bookPage;
            }

            bool flipping = Time.realtimeSinceStartup < _flipUntil;
            int flipFrame = 0;
            if (flipping)
            {
                float t = 1f - (_flipUntil - Time.realtimeSinceStartup) / FlipDuration;
                // Always use the reverse sheet sequence. Its reveal order exposes the
                // destination's left page before the right page, matching the content flow.
                flipFrame = Mathf.Clamp(8 - (int)(t * 9f), 0, 8);
            }

            // Render stack: static book, destination content, transparent turning-sheet overlay.
            // Page2-Page8 contain only the moving sheet; unchanged pixels are transparent.
            var basePage = UiAssets.BookPages[0];
            if (basePage != null)
                GUI.DrawTexture(book, basePage, ScaleMode.StretchToFill, true);

            bool drawPageContent = !flipping || Event.current.type == EventType.Repaint;
            if (drawPageContent)
                DrawClippedFantasyPages(book, s);

            if (flipping && flipFrame > 0 && flipFrame < 8)
            {
                var turningPage = UiAssets.BookPages[flipFrame];
                if (turningPage != null)
                    GUI.DrawTexture(book, turningPage, ScaleMode.StretchToFill, true);
            }

            if (drawPageContent)
                DrawFantasyTabs(book, s);

            var closeRect = new Rect((int)book.xMax - 25 * s, (int)book.y + 7 * s, 11 * s, 10 * s);
            if (closeRect.Contains(Event.current.mousePosition) && Event.current.type == EventType.Repaint)
                _hint = "Close the book (or press B)";
            FillRect(closeRect, closeRect.Contains(Event.current.mousePosition) ? Hex(0xCF8D78) : Hex(0xDFC09B));
            GlyphX(closeRect.center.x, closeRect.center.y, 2, TextMain, s);
            if (InvisibleClick(closeRect, "Close the book"))
                _open = false;

            string defaultFooter = _bookPage == BookPage.Tools
                ? "SELECT A TOOL - CONTINUE"
                : _bookPage == BookPage.Options
                    ? "CONFIGURE TOOL - CLOSE TO USE"
                    : _bookPage == BookPage.Library
                        ? "PREVIEW - LOAD SELECTED"
                        : "Q/E TABS - ENTER SELECT - B CLOSE";
            string footer = Time.realtimeSinceStartup < _feedbackUntil ? _feedback
                : string.IsNullOrEmpty(_hint) ? defaultFooter : _hint;
            if (!flipping)
                GUI.Label(new Rect((int)book.x + 34 * s, (int)book.yMax - 14 * s, (FbookW - 68) * s, 8 * s),
                    footer, Time.realtimeSinceStartup < _feedbackUntil ? _textReadyCenter : _fbTinyCenter);

            var e = Event.current;
            if (!flipping && _bookPage == BookPage.Library &&
                e.type == EventType.ScrollWheel && book.Contains(e.mousePosition))
            {
                int pageCount = Mathf.Max(1, Mathf.CeilToInt(BlueprintLibraryStore.Names.Count / (float)LibraryMaxRows));
                _savedScroll += e.delta.y > 0 ? LibraryMaxRows : -LibraryMaxRows;
                _savedScroll = Mathf.Clamp(_savedScroll, 0, (pageCount - 1) * LibraryMaxRows);
                e.Use();
            }
        }

        private static void DrawClippedFantasyPages(Rect book, int s)
        {
            var leftPage = new Rect(book.x + FbLeftPageX * s, book.y + FbPageY * s,
                FbLeftPageW * s, FbPageSafeH * s);
            GUI.BeginGroup(leftPage);
            DrawBookLeftPage(0, 2 * s, FbLeftPageW, s);
            GUI.EndGroup();

            var rightPage = new Rect(book.x + FbRightPageX * s, book.y + FbPageY * s,
                FbRightPageW * s, FbPageSafeH * s);
            GUI.BeginGroup(rightPage);
            DrawFantasyRightPage(0, 0, FbRightPageW, s);
            GUI.EndGroup();
        }

        private static void DrawFantasyTabs(Rect book, int s)
        {
            var bm = UiAssets.Bookmarks;
            int x = (int)book.xMax - 9 * s;
            int y = (int)book.y + 24 * s;
            for (int i = 0; i < FbTabs.Length; i++)
            {
                var (pg, label) = FbTabs[i];
                bool enabled = IsPageEnabled(pg);
                bool sel = _bookPage == pg;
                var r = new Rect(x + (sel ? 4 * s : 0), y, 22 * s, 20 * s);
                var prev = GUI.color;
                GUI.color = !enabled ? new Color(0.6f, 0.6f, 0.6f, 0.7f)
                    : sel ? Color.white : new Color(1f, 1f, 1f, 0.82f);
                if (bm != null) DrawAtlasIcon(r, bm, 0, Mathf.Min(i, 4) * 20, 22, 19, 22, 99);
                else FillRect(r, sel ? Accent : BtnBg);
                GUI.color = prev;
                if (sel)
                    FillRect(new Rect(r.x + 2 * s, r.y + 3 * s, s, r.height - 7 * s), Hex(0x7A332C));
                GUI.Label(new Rect(r.x + 3 * s, r.y + 6 * s, 14 * s, 8 * s), label, sel ? _btnLabel : _btnLabelDim);
                if (enabled && InvisibleClick(r, label + " page"))
                {
                    _libraryMaterialPicker = false;
                    _bookPage = pg;
                    _pageFocus = 0;
                    _openDropdown = null;
                }
                y += 19 * s;
            }
        }

        private static void FbBox(Rect r, string sprite, int srcBorder, int s)
        {
            var tex = UiAssets.Sprite(sprite);
            if (tex != null)
                DrawNineSlice(r, tex, 0, 0, tex.width, tex.height, srcBorder, srcBorder,
                    tex.width, tex.height, Mathf.Min(srcBorder * s, Mathf.Min(r.width, r.height) / 2f));
            else
                FillRect(r, PanelBg);
        }

        private static void FbBanner(Rect r, string text, int s, GUIStyle style = null)
        {
            var tex = UiAssets.Sprite("fbTitle17");
            if (tex != null)
            {
                // fbTitle17 is a complete ribbon plaque. Keep its native proportions so
                // its folds and underline rails remain crisp instead of nine-slicing them.
                var plaque = new Rect(
                    Mathf.Round(r.center.x - tex.width * s * 0.5f),
                    Mathf.Round(r.center.y - tex.height * s * 0.5f),
                    tex.width * s, tex.height * s);
                DrawAtlasIcon(plaque, tex, 0, 0, tex.width, tex.height, tex.width, tex.height);

                // Center against the plaque cavity, not the transparent ribbon tails.
                // The one-pixel upward offset compensates for the pixel font's baseline.
                var label = new Rect(plaque.x + 9 * s, plaque.y,
                    (tex.width - 18) * s, 14 * s);
                GUI.Label(label, text, style ?? _textCenter);
                return;
            }
            GUI.Label(new Rect(r.x, r.y - s, r.width, r.height), text, style ?? _textCenter);
        }

        private static void FbDivider(Rect r, int s)
        {
            FillRect(new Rect(r.x, r.center.y, r.width, 1), Hex(0xD49C74));
            FillRect(new Rect(r.x + 6 * s, r.center.y + s, r.width - 12 * s, 1), Hex(0xE9C79B));
        }

        private static void FbPagePanel(Rect r, int s, bool strong = false)
        {
            FillRect(r, strong ? Hex(0xD3A47E) : Hex(0xDFC09B));
        }

        private static void FbTonalPanel(Rect r, int s, bool framed = true)
        {
            FillRect(r, framed ? Hex(0xDFC09B) : Hex(0xE3C39D));
        }

        private static void FbBookIcon(Rect r, int cell, float alpha, int s)
        {
            if (UiAssets.BookIcons == null) return;
            const int cellSize = 16;
            const int atlasW = 224;
            const int atlasH = 80;
            int sx = (cell % 14) * cellSize;
            int sy = (cell / 14) * cellSize;
            var prev = GUI.color;
            GUI.color = new Color(0.96f, 0.77f, 0.55f, alpha);
            DrawAtlasIcon(r, UiAssets.BookIcons, sx, sy, cellSize, cellSize, atlasW, atlasH);
            GUI.color = prev;
        }

        private static void FbCornerDots(Rect r, int s)
        {
            Color c = Hex(0xD7A37A);
            float d = 2 * s;
            FillRect(new Rect(r.x + 2 * s, r.y + 2 * s, d, d), c);
            FillRect(new Rect(r.xMax - 4 * s, r.y + 2 * s, d, d), c);
            FillRect(new Rect(r.x + 2 * s, r.yMax - 4 * s, d, d), c);
            FillRect(new Rect(r.xMax - 4 * s, r.yMax - 4 * s, d, d), c);
        }

        private static void FbPill(Rect r, int colorIndex, int s)
        {
            var tex = UiAssets.Sprite("fbBookmarkH");
            if (tex == null)
            {
                var prev = GUI.color;
                GUI.color = new Color(0.76f, 0.40f, 0.72f);
                FbBox(r, "fbBox57", 3, s);
                GUI.color = prev;
                return;
            }
            int sy = Mathf.Clamp(colorIndex, 0, 4) * 13;
            const int cap = 6;
            DrawAtlasIcon(new Rect(r.x, r.y, cap * s, r.height), tex, 0, sy, cap, 13, tex.width, tex.height);
            DrawAtlasIcon(new Rect(r.x + cap * s, r.y, r.width - 2 * cap * s, r.height), tex,
                cap, sy, tex.width - 2 * cap, 13, tex.width, tex.height);
            DrawAtlasIcon(new Rect(r.xMax - cap * s, r.y, cap * s, r.height), tex,
                tex.width - cap, sy, cap, 13, tex.width, tex.height);
        }

        private static void FbIconSlot(Rect r, string sprite, int s)
        {
            var tex = UiAssets.Sprite(sprite);
            if (tex != null)
                DrawAtlasIcon(r, tex, 0, 0, tex.width, tex.height, tex.width, tex.height);
            else
                FbBox(r, "fbBox44", 4, s);
        }

        private static void FbFancyDivider(Rect r, int s)
        {
            float cy = Mathf.Round(r.center.y);
            FillRect(new Rect(r.x, cy, r.width, 1), Hex(0xC6906C));
            FillRect(new Rect(r.x, cy + 1, r.width, 1), new Color(0.91f, 0.78f, 0.6f, 0.5f));
            var orn = UiAssets.Sprite("fbIns_Trinagle");
            if (orn != null)
            {
                float sz = 7 * s;
                FillRect(new Rect(r.center.x - sz, cy, sz * 2f, 2), Hex(0xC97958));
                DrawAtlasIcon(new Rect(Mathf.Round(r.center.x - sz / 2f), cy - sz / 2f, sz, sz),
                    orn, 0, 0, orn.width, orn.height, orn.width, orn.height);
            }
        }

        private static void FbDarkWell(Rect r, int s)
        {
            var prev = GUI.color;
            if (UiAssets.DarkerPage != null)
            {
                GUI.color = new Color(0.83f, 0.66f, 0.50f, 1f);
                GUI.DrawTexture(r, UiAssets.DarkerPage, ScaleMode.StretchToFill, true);
            }
            else
                FillRect(r, new Color(0.62f, 0.44f, 0.31f, 1f));
            GUI.color = prev;
            FillRect(new Rect(r.x + 2 * s, r.y + 2 * s, r.width - 4 * s, s), new Color(0.30f, 0.17f, 0.11f, 0.40f));
            FillRect(new Rect(r.x + 2 * s, r.y + 2 * s, s, r.height - 4 * s), new Color(0.30f, 0.17f, 0.11f, 0.40f));
            FillRect(new Rect(r.x + 2 * s, r.yMax - 3 * s, r.width - 4 * s, s), new Color(1f, 0.92f, 0.74f, 0.30f));
            FillRect(new Rect(r.xMax - 3 * s, r.y + 2 * s, s, r.height - 4 * s), new Color(1f, 0.92f, 0.74f, 0.30f));
            Color bc = new Color(0.45f, 0.27f, 0.18f);
            const int L = 4;
            FillRect(new Rect(r.x + 2 * s, r.y + 2 * s, L * s, s), bc);
            FillRect(new Rect(r.x + 2 * s, r.y + 2 * s, s, L * s), bc);
            FillRect(new Rect(r.xMax - (2 + L) * s, r.y + 2 * s, L * s, s), bc);
            FillRect(new Rect(r.xMax - 3 * s, r.y + 2 * s, s, L * s), bc);
            FillRect(new Rect(r.x + 2 * s, r.yMax - 3 * s, L * s, s), bc);
            FillRect(new Rect(r.x + 2 * s, r.yMax - (2 + L) * s, s, L * s), bc);
            FillRect(new Rect(r.xMax - (2 + L) * s, r.yMax - 3 * s, L * s, s), bc);
            FillRect(new Rect(r.xMax - 3 * s, r.yMax - (2 + L) * s, s, L * s), bc);
        }

        private static GUIStyle _fbWhite;
        private static GUIStyle FbWhiteStyle()
        {
            if (_fbWhite == null || _fbWhite.fontSize != _text.fontSize)
                _fbWhite = new GUIStyle(_text) { normal = { textColor = new Color(1f, 0.97f, 0.92f) } };
            return _fbWhite;
        }

        private static void FbListRow(Rect r, string name, Blueprint bp, int s, bool focused)
        {
            var e = Event.current;
            bool hover = _openDropdown == null && r.Contains(e.mousePosition);
            bool active = bp != null && (_bookPage == BookPage.Library
                ? _libraryPreviewBlueprint == bp
                : BlueprintManager.ActiveBlueprint == bp);

            FillRect(r, active ? Hex(0xD7B08A)
                : focused || hover ? Hex(0xDFC09B)
                : Hex(0xD9B693));
            FillRect(new Rect(r.x, r.yMax - s, r.width, s), Hex(0xE8C9A0));

            if (focused)
                FillRect(new Rect(r.x - 2 * s, r.y + 2 * s, s, r.height - 4 * s), Hex(0xC86D48));

            float bsz = r.height - 4 * s;
            var badge = new Rect(r.x + 2 * s, r.y + 2 * s, bsz, bsz);
            DrawReferenceIconFrame(badge, s);
            DrawBlueprintMiniature(InsetRect(badge, 3 * s), bp);

            float ssz = 10 * s;
            var statR = new Rect(r.xMax - ssz - 2 * s, Mathf.Round(r.center.y) - ssz / 2f, ssz, ssz);
            FillRect(statR, Hex(0xEAD8B6));
            if (active)
                GlyphCheck(Mathf.Round(statR.center.x) - s, Mathf.Round(statR.center.y) - s,
                    Hex(0x96C94C), s);

            float tx = badge.xMax + 3 * s;
            float textW = statR.x - tx - 2 * s;
            string shortName = ReferenceTitle(name, 17);
            GUI.Label(new Rect(tx, r.y + s, textW, 6 * s), shortName,
                active || focused ? _fbRowTitle : _fbRowTitleDim);
            string sub = bp != null ? $"{bp.width}x{bp.height}  {bp.tiles.Count} tiles" : "";
            GUI.Label(new Rect(tx, r.y + 6 * s, textW, 4 * s),
                sub.ToUpperInvariant(), _fbLibrarySub);

            bool armed = _armedDelete == name && Time.realtimeSinceStartup < _armedDeleteUntil;
            if (hover && e.type == EventType.Repaint)
                _hint = _libraryMaterialPicker
                    ? $"{name} - choose as material source"
                    : armed ? $"Right-click again to delete {name}" : $"{name} - left-click load, right-click delete";
            if (!_libraryMaterialPicker && hover && e.type == EventType.MouseDown && e.button == 1)
            {
                e.Use();
                if (armed)
                {
                    BlueprintSerializer.Delete(name);
                    BlueprintLibraryStore.Forget(name);
                    if (_libraryPreviewName == name)
                    {
                        _libraryPreviewName = null;
                        _libraryPreviewBlueprint = null;
                    }
                    _armedDelete = null;
                    BlueprintLibraryStore.Invalidate();
                    ShowFeedback($"Deleted {name}");
                }
                else
                {
                    _armedDelete = name;
                    _armedDeleteUntil = Time.realtimeSinceStartup + 2f;
                }
            }
            else if (hover && e.type == EventType.MouseDown && e.button == 0 && bp != null)
            {
                e.Use();
                PreviewLibraryBlueprint(name, bp);
            }

            if (armed)
                GUI.Label(new Rect(r.xMax - 8 * s, r.y + 2 * s, 6 * s, 6 * s), "!", _textBlockedCenter);
        }

        private static void DrawFantasyLibraryPage(int x, int y, int w, int s)
        {
            int pageTop = y;
            int innerX = x;
            int innerW = w - 2;

            FbBanner(new Rect(x + 13 * s, y - 2 * s, (w - 26) * s, 19 * s),
                _libraryMaterialPicker ? "MATERIAL" : "LIBRARY", s, _heading);
            y += 20 * s;
            FillRect(new Rect(x + 9 * s, y, (w - 18) * s, s), Hex(0xD0A079));
            FillRect(new Rect(x + 18 * s, y + 2 * s, (w - 36) * s, s), Hex(0xE3BF94));
            y += 6 * s;

            int half = (innerW - 2) / 2;
            var sortRect = new Rect(innerX, y, half * s, 9 * s);
            var filterRect = new Rect(innerX + (half + 2) * s, y, half * s, 9 * s);
            y += 10 * s;

            string previousSearch = _librarySearch;
            var searchPanel = new Rect(innerX, y, innerW * s, 9 * s);
            FillRect(searchPanel, Hex(0xE2C49E));
            GUI.Label(new Rect(innerX + 3 * s, y, 18 * s, 9 * s), "FIND", _fbTinyCenter);
            var searchRect = new Rect(innerX + 21 * s, y + s, (innerW - 24) * s, 7 * s);
            var se = Event.current;
            if (se.type == EventType.MouseDown && !searchRect.Contains(se.mousePosition))
                GUI.FocusControl(null);
            if (_openDropdown == null)
            {
                GUI.SetNextControlName(SearchControlName);
                _librarySearch = GUI.TextField(searchRect, _librarySearch, 24, _text);
            }
            else
                GUI.Label(searchRect, string.IsNullOrEmpty(_librarySearch) ? "Search blueprints..." : _librarySearch, _textDim);
            if (_librarySearch != previousSearch)
            {
                _savedScroll = 0;
                BlueprintLibraryStore.Invalidate();
            }
            y += 11 * s;

            float rowH = 11 * s;
            int end = Mathf.Min(BlueprintLibraryStore.Names.Count, _savedScroll + LibraryMaxRows);
            for (int i = _savedScroll; i < end; i++)
            {
                string name = BlueprintLibraryStore.Names[i];
                BlueprintLibraryStore.Blueprints.TryGetValue(name, out var bp);
                int local = i - _savedScroll;
                FbListRow(new Rect(innerX, y + local * rowH,
                    innerW * s, rowH - 1 * s), name, bp, s, local == _libraryFocus);
            }

            if (BlueprintLibraryStore.Names.Count == 0)
                GUI.Label(new Rect(innerX + 6 * s, y + 16 * s, (innerW - 12) * s, 22 * s),
                    string.IsNullOrEmpty(_librarySearch)
                        ? "No saved blueprints yet.\nImport one or capture a new plan."
                        : "No blueprints match this filter.",
                    WrappedStyle(_textDimCenter));

            int navY = pageTop + 102 * s;
            int page = BlueprintLibraryStore.Names.Count == 0 ? 1 : (_savedScroll / LibraryMaxRows) + 1;
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(BlueprintLibraryStore.Names.Count / (float)LibraryMaxRows));
            if (ReferenceActionButton(new Rect(x, navY, 15 * s, 8 * s), "<", s, _savedScroll > 0,
                    "Previous blueprint page"))
            {
                _savedScroll = Mathf.Max(0, _savedScroll - LibraryMaxRows);
                _libraryFocus = 0;
            }
            GUI.Label(new Rect(x + 17 * s, navY, (w - 34) * s, 8 * s), $"PAGE {page}/{pageCount}", _fbTinyCenter);
            if (ReferenceActionButton(new Rect(x + (w - 17) * s, navY, 15 * s, 8 * s), ">", s,
                    _savedScroll + LibraryMaxRows < BlueprintLibraryStore.Names.Count,
                    "Next blueprint page"))
            {
                _savedScroll = Mathf.Min((pageCount - 1) * LibraryMaxRows, _savedScroll + LibraryMaxRows);
                _libraryFocus = 0;
            }

            int footerY = pageTop + FbBottomActionY * s;
            int footerHalf = (w - 6) / 2;
            bool canLoad = _libraryPreviewBlueprint != null &&
                (!_libraryMaterialPicker || BlueprintHasWall(_libraryPreviewBlueprint));
            if (SolidActionButton(new Rect(x, footerY, footerHalf * s, 8 * s),
                    _libraryMaterialPicker ? "USE MATERIAL" : "LOAD SELECTED",
                    "Load the previewed blueprint", s, canLoad, ButtonTone.Confirm))
                SelectLibraryBlueprint(_libraryPreviewName ?? _libraryPreviewBlueprint.name, _libraryPreviewBlueprint);
            if (SolidActionButton(new Rect(x + (footerHalf + 4) * s, footerY, footerHalf * s, 8 * s),
                    "IMPORT", "Import a CKBP1 blueprint from the clipboard", s, true, ButtonTone.Info))
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

            Dropdown(sortRect, "library-sort", "SORT", SortChoices, (int)_librarySort, s, index =>
            {
                _librarySort = (LibrarySort)index;
                _savedScroll = 0;
                BlueprintLibraryStore.Invalidate();
            });
            int filterIndex = System.Array.IndexOf(FilterChoices, _tagFilter);
            Dropdown(filterRect, "library-filter", "TAG", FilterChoices, Mathf.Max(0, filterIndex), s, index =>
            {
                _tagFilter = FilterChoices[index];
                _savedScroll = 0;
                BlueprintLibraryStore.Invalidate();
            });
            DismissDropdownOutside(sortRect, filterRect,
                DropdownMenuRect(sortRect, SortChoices, s),
                DropdownMenuRect(filterRect, FilterChoices, s));
        }

        private static void DrawFantasyRightPage(int x, int y, int w, int s)
        {
            bool libraryPreview = _bookPage == BookPage.Library && _libraryPreviewBlueprint != null;
            var bp = libraryPreview ? _libraryPreviewBlueprint : BlueprintManager.ActiveBlueprint;
            var portrait = new Rect(x, y, (w - 1) * s, 56 * s);
            var portraitArt = UiAssets.Sprite("BlueprintPreviewArt");
            if (portraitArt != null)
            {
                DrawAtlasIcon(portrait, portraitArt, 0, 0, portraitArt.width, portraitArt.height,
                    portraitArt.width, portraitArt.height);
                if (bp != null)
                    DrawBlueprintPortraitFootprint(portrait, bp,
                        libraryPreview ? 0 : BlueprintManager.BlueprintRotation,
                        !libraryPreview && BlueprintManager.BlueprintFlipped, s);
            }
            else
            {
                DrawReferencePortraitFrame(portrait, s);
                if (bp != null)
                    DrawBlueprintPortraitFootprint(portrait, bp,
                        libraryPreview ? 0 : BlueprintManager.BlueprintRotation,
                        !libraryPreview && BlueprintManager.BlueprintFlipped, s);
                else
                {
                    FbBookIcon(new Rect(portrait.center.x - 9 * s, portrait.y + 9 * s, 18 * s, 18 * s),
                        8, 0.48f, s);
                    GUI.Label(new Rect(portrait.x + 9 * s, portrait.y + 30 * s,
                        portrait.width - 18 * s, 14 * s),
                        "NO BLUEPRINT SELECTED\nCHOOSE ONE FROM THE LIBRARY",
                        WrappedStyle(_fbBodyCenter));
                }
            }
            y += 59 * s;

            if (bp == null)
            {
                DrawEmptyRightPageDetails(x, y, w, s);
                return;
            }

            var badge = new Rect(x, y, 13 * s, 13 * s);
            DrawReferenceIconFrame(badge, s);
            DrawBlueprintMiniature(InsetRect(badge, 3 * s), bp);
            string title = ReferenceTitle(bp.name, 22);
            GUI.Label(new Rect(badge.xMax + 2 * s, y, (w - 17) * s, 12 * s), title, _fbName);
            y += 14 * s;

            string desc = ReferenceBlueprintDescription(bp);
            GUI.Label(new Rect(x, y, w * s, 25 * s), desc, WrappedStyle(_fbBody));
            y += 25 * s;

            FillRect(new Rect(x, y, (w - 1) * s, s), Hex(0xC99A73));
            y += 4 * s;

            if (libraryPreview)
            {
                var token = new Rect(x, y, 17 * s, 17 * s);
                DrawReferenceIconFrame(token, s);
                DrawReferenceCostIcon(InsetRect(token, 3 * s), default);
                GUI.Label(new Rect(token.xMax + 3 * s, y + 3 * s, 25 * s, 9 * s), "PREVIEW", _fbTiny);
            }
            else
                DrawReferenceCostSlots(x, y, w, s);

            float actionY = y + 2 * s;
            float actionH = 10 * s;
            const int actionGap = 2;
            const int transformW = 13;
            const int useW = 23;
            int useX = w - useW - 1;
            int mirrorX = useX - actionGap - transformW;
            int rotateX = mirrorX - actionGap - transformW;
            var rotateRect = new Rect(x + rotateX * s, actionY, transformW * s, actionH);
            var mirrorRect = new Rect(x + mirrorX * s, actionY, transformW * s, actionH);
            if (!libraryPreview && SolidActionButton(rotateRect, "ROT", "Rotate blueprint 90 degrees", s))
                BlueprintManager.SetRotation(BlueprintManager.BlueprintRotation + 90);
            if (!libraryPreview && SolidActionButton(mirrorRect, "MIR", "Mirror blueprint horizontally", s,
                    selected: BlueprintManager.BlueprintFlipped))
                BlueprintManager.ToggleFlip();

            bool blocked = TileCostTracker.IsBlocked;
            var action = new Rect(x + useX * s, actionY, useW * s, actionH);
            string actionLabel = libraryPreview ? "LOAD" : blocked ? "MISSING" : "USE";
            if (SolidActionButton(action, actionLabel,
                    libraryPreview ? "Load this blueprint" : "Close the book and use this blueprint",
                    s, libraryPreview || !blocked, libraryPreview ? ButtonTone.Confirm : ButtonTone.Neutral))
            {
                if (libraryPreview)
                    SelectLibraryBlueprint(_libraryPreviewName ?? bp.name, bp);
                else
                {
                    BuildToolState.ActiveTool = BuildTool.Blueprint;
                    _open = false;
                }
            }
        }

        private static void DrawReferencePortraitFrame(Rect r, int s)
        {
            FillRect(r, Hex(0xE9D0A7));
            DrawRectOutline(r, s, Hex(0xC58E69));
            DrawRectOutline(InsetRect(r, 2 * s), s, Hex(0xD7AA82));

            Color ornament = Hex(0xC99A73);
            for (int i = 0; i < 5; i++)
            {
                int px = i % 2 == 0 ? 7 + i * 17 : 18 + i * 15;
                int py = i % 2 == 0 ? 7 : 12;
                FillRect(new Rect(r.x + px * s, r.y + py * s, s, s), ornament);
                FillRect(new Rect(r.xMax - (px - 2) * s, r.y + (py + 3) * s, s, s), ornament);
            }

            float patternY = r.yMax - 7 * s;
            for (int i = 0; i < 30; i++)
            {
                if ((i & 1) == 0)
                    FillRect(new Rect(r.x + (3 + i * 3) * s, patternY, 2 * s, s), Hex(0xD6AA83));
                if ((i & 1) == 1)
                    FillRect(new Rect(r.x + (3 + i * 3) * s, patternY + 2 * s, 2 * s, s), Hex(0xD6AA83));
            }
        }

        private static void DrawReferenceIconFrame(Rect r, int s)
        {
            FillRect(r, Hex(0xEAD2AA));
            DrawRectOutline(r, s, Hex(0xA96F54));
            FillRect(new Rect(r.x + 2 * s, r.y + 2 * s, 3 * s, s), Hex(0xD3A17A));
            FillRect(new Rect(r.x + 2 * s, r.y + 2 * s, s, 3 * s), Hex(0xD3A17A));
        }

        private static string ReferenceTitle(string value, int limit)
        {
            if (string.IsNullOrWhiteSpace(value)) return "UNTITLED BLUEPRINT";
            string upper = SingleLine(value).ToUpperInvariant();
            return upper.Length <= limit ? upper : upper.Substring(0, limit - 2) + "..";
        }

        private static string ReferenceBlueprintDescription(Blueprint bp)
        {
            if (!string.IsNullOrWhiteSpace(bp.description))
            {
                string custom = SingleLine(bp.description).ToUpperInvariant();
                return custom.Length <= 104 ? custom : custom.Substring(0, 102) + "..";
            }

            return "A RECORDED BUILDING PLAN, READY TO PLACE OR ADAPT. " +
                   $"{bp.width} X {bp.height} - {bp.tiles.Count} TILES - {bp.objects.Count} OBJECTS.";
        }

        private static string SingleLine(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            return value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        }

        private static void DrawEmptyRightPageDetails(int x, int y, int w, int s)
        {
            var badge = new Rect(x, y, 13 * s, 13 * s);
            DrawReferenceIconFrame(badge, s);
            GUI.Label(new Rect(badge.xMax + 2 * s, y, (w - 17) * s, 12 * s),
                "NO ACTIVE PLAN", _fbName);
            y += 17 * s;
            GUI.Label(new Rect(x, y, w * s, 18 * s),
                "SELECT A BLUEPRINT TO VIEW ITS FOOTPRINT, COST, AND PLACEMENT CONTROLS.",
                WrappedStyle(_fbBody));
            y += 22 * s;
            FillRect(new Rect(x, y, (w - 1) * s, s), Hex(0xC99A73));
            var disabled = new Color(0.82f, 0.72f, 0.60f, 0.72f);
            FillRect(new Rect(x, y + 5 * s, 17 * s, 17 * s), disabled);
            FillRect(new Rect(x + 44 * s, y + 7 * s, 13 * s, 10 * s), disabled);
            FillRect(new Rect(x + 59 * s, y + 7 * s, 13 * s, 10 * s), disabled);
            FillRect(new Rect(x + 74 * s, y + 7 * s, 23 * s, 10 * s), disabled);
        }

        private static void DrawReferenceCostSlots(int x, int y, int w, int s)
        {
            var costs = TileCostTracker.CurrentCost;
            int shown = Mathf.Min(2, costs.Count);
            for (int i = 0; i < shown; i++)
            {
                var entry = costs[i];
                var slot = new Rect(x + i * 21 * s, y, 17 * s, 17 * s);
                DrawReferenceIconFrame(slot, s);
                DrawReferenceCostIcon(InsetRect(slot, 3 * s), entry.objectId);
                string count = TileCostTracker.IsCreativeMode
                    ? entry.needed.ToString()
                    : $"{entry.available}/{entry.needed}";
                GUI.Label(new Rect(slot.x, slot.yMax - 5 * s, slot.width, 5 * s), count,
                    entry.HasEnough || TileCostTracker.IsCreativeMode ? _fbCountReady : _fbCountBlocked);
            }

            if (shown == 0)
            {
                var slot = new Rect(x, y, 17 * s, 17 * s);
                DrawReferenceIconFrame(slot, s);
                GlyphCheck(slot.center.x - s, slot.center.y - s, ReadyCol, s);
                GUI.Label(new Rect(slot.xMax + 3 * s, y + 3 * s, 26 * s, 9 * s),
                    TileCostTracker.IsActive ? "NO COST" : "READY", _fbTiny);
            }
            else if (costs.Count > shown)
                GUI.Label(new Rect(x + 42 * s, y + 4 * s, 12 * s, 8 * s),
                    $"+{costs.Count - shown}", _fbTinyCenter);
        }

        private static void DrawReferenceCostIcon(Rect r, ObjectID _)
        {
            Color ink = Hex(0xA96F54);
            float u = Mathf.Max(1f, r.width / 8f);
            FillRect(new Rect(r.center.x - 2 * u, r.y + u, 4 * u, u), ink);
            FillRect(new Rect(r.center.x - 3 * u, r.y + 2 * u, 6 * u, 3 * u), ink);
            FillRect(new Rect(r.center.x - 2 * u, r.y + 5 * u, 4 * u, u), ink);
        }

        private static bool ReferenceActionButton(Rect r, string label, int s, bool enabled,
            string tooltip = null)
            => SolidActionButton(r, label, tooltip ?? label, s, enabled);
    }
}
