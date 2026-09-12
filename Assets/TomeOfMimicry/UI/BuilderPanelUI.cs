using System.Collections.Generic;
using UnityEngine;

namespace TomeOfMimicry
{
    public static partial class BuilderPanelUI
    {
        private static readonly Color PanelBg   = Hex(0xD98F62);
        private static readonly Color PanelEdge = Hex(0x5B302B);
        private static readonly Color BtnBg     = Hex(0xB9654F);
        private static readonly Color BtnHover  = Hex(0xD98262);
        private static readonly Color BtnMuted  = Hex(0x9C7767);
        private static readonly Color Accent    = Hex(0x8F3F3C);
        private static readonly Color TextMain  = Hex(0x592D2B);
        private static readonly Color TextDim   = Hex(0x98604E);
        private static readonly Color GuideCol  = Hex(0x7D4338);
        private static readonly Color DividerCol = Hex(0xC97958);
        private static readonly Color ReadyCol  = Hex(0x31884A);
        private static readonly Color BlockedCol = Hex(0xC73545);

        private const int Pad      = 6;
        private const int Edge     = 2;
        private const int ContentW = 148;
        private const int PanelW   = ContentW + Pad * 2;
        private const int BtnH     = 12;
        private const int LabelH   = 8;
        private const int GuideH   = 10;
        private const int HintH    = 10;
        private const int StepH    = 11;
        private const int Gap      = 2;
        private const int SectGap  = 4;
        private const int TopInset = 58;
        private const int LeftInset = 8;

        private const int LibraryMaxRows = 5;
        private const int MaterialMaxRows = 6;

        private const int IconNext = 1, IconClose = 16, IconUp = 9, IconDown = 8, IconRefresh = 30;

        private static bool _open = true;
        private static string _hint = "";
        private static Rect _screenRect;
        private static int _savedScroll;
        private static string _armedDelete;
        private static float _armedDeleteUntil;
        private static BookPage _bookPage = BookPage.Library;
        private static LibrarySort _librarySort = LibrarySort.Newest;
        private static string _tagFilter = "ALL";
        private static string _librarySearch = "";
        private static string _openDropdown;
        private static string _feedback = "";
        private static float _feedbackUntil;
        private static int _libraryFocus;
        private static Blueprint _libraryPreviewBlueprint;
        private static string _libraryPreviewName;
        private static int _pageFocus;
        private static int _materialPage;
        private static float _nextPadInput;
        private static bool _isEditingText;
        private static bool _libraryMaterialPicker;
        private static BookPage _materialPickerReturnPage;
        private static BuildTool _materialPickerTool;

        private enum BookPage { Library, Tools, Options, Materials, Tags, Details }
        private enum LibrarySort { Newest, Name, Size }
        private enum BookFrameStyle { Card, Outline, Filled }
        private static readonly string[] TagChoices = { "BASE", "FARM", "AUTO", "DECOR" };
        private static readonly string[] SortChoices = { "NEWEST", "NAME", "SIZE" };
        private static readonly string[] FilterChoices = { "ALL", "BASE", "FARM", "AUTO", "DECOR" };
        private const string SearchControlName = "TomeOfMimicry.LibrarySearch";

        private static int _gameScale = 2;
        private static bool _editorPreview;
        private static float _previewWidth;
        private static float _previewHeight;
        private static int _styleScale = -1;
        private static GUIStyle _text, _textDim, _textAccent, _textGuide, _textCenter,
                                _textDimCenter, _textReady, _textBlocked,
                                _textReadyCenter, _textBlockedCenter,
                                _textReadyRight, _textBlockedRight,
                                _btnLabel, _btnLabelDim, _btnLabelLight, _heading,
                                _blueprintText, _blueprintTextCenter,
                                _blueprintTextDim, _blueprintTextDimCenter,
                                _fbRowTitle, _fbRowTitleDim, _fbRowSub, _fbLibrarySub,
                                _fbName, _fbBody, _fbBodyCenter,
                                _fbTiny, _fbTinyCenter, _fbCountReady, _fbCountBlocked,
                                _fbValueRight;

        public static bool IsMouseOver
        {
            get
            {
                if (!BlueprintManager.IsGadgetEquipped || !DebugUI.IsInWorld) return false;
                var p = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
                return _screenRect.Contains(p);
            }
        }

        public static bool IsOpen => _open;
        public static bool IsEditingText => _isEditingText;

        public static void ToggleOpen() => _open = !_open;

        public static void Draw()
        {
            if (Manager.ui != null && Manager.ui.isAnyInventoryShowing) { _screenRect = default; return; }
            if (Manager.menu != null && Manager.menu.IsAnyMenuActive()) { _screenRect = default; return; }

            var e = Event.current;
            _isEditingText = _open && GUI.GetNameOfFocusedControl() == SearchControlName;
            int gameScale = Mathf.Max(2, Mathf.RoundToInt(Screen.height / 180f));
            int s = Mathf.Max(2, Mathf.RoundToInt(Screen.height / 360f));
            _gameScale = gameScale;
            EnsureStyles(s);
            RefreshSavedNames();

            if (_open && !IsPageEnabled(_bookPage))
                _bookPage = BookPage.Tools;

            if (e.type == EventType.Repaint) _hint = "";

            if (_open)
                HandleBookNavigation(e);

            if (_open) DrawPanel(s);
            else DrawChip(s);
            _isEditingText = _open && GUI.GetNameOfFocusedControl() == SearchControlName;

            if (_isEditingText && e.type == EventType.KeyDown)
                e.Use();

            if (_screenRect.Contains(e.mousePosition))
            {
                DrawCursor(e.mousePosition, gameScale);

                if (e.type == EventType.MouseDown || e.type == EventType.MouseUp)
                    e.Use();
            }
        }

        public static void DrawEditorPreview(float width, float height)
        {
            _editorPreview = true;
            _previewWidth = width;
            _previewHeight = height;
            _open = true;

            try
            {
                int s = Mathf.Max(2, Mathf.FloorToInt(Mathf.Min(width / 280f, height / 170f)));
                _gameScale = s;
                EnsureStyles(s);
                if (Event.current.type == EventType.Repaint) _hint = "";
                HandleBookNavigation(Event.current);
                DrawFantasyBook(s);
            }
            finally
            {
                _editorPreview = false;
            }
        }

        private static void DrawChip(int s)
        {
            var r = new Rect(LeftInset * _gameScale, TopInset * _gameScale, 24 * s, 14 * s);
            _screenRect = r;

            var e = Event.current;
            bool hover = r.Contains(e.mousePosition);
            FillRect(Expand(r, s), PanelEdge);
            FillRect(r, hover ? BtnHover : Accent);
            GUI.Label(r, "BG", _btnLabel);

            GUI.Label(new Rect(r.x, r.yMax + 1 * s, 60 * s, 8 * s),
                hover ? "Click or press B" : "Press B", _textDim);

            if (hover && e.type == EventType.MouseDown && e.button == 0)
            {
                _open = true;
                e.Use();
            }
        }

        private static void DrawPanel(int s)
        {
            DrawFantasyBook(s);
        }

        private static readonly BookPage[] PageOrder =
        {
            BookPage.Library, BookPage.Tools, BookPage.Options,
            BookPage.Materials, BookPage.Tags, BookPage.Details
        };

        private static bool IsPageEnabled(BookPage page) => page switch
        {
            BookPage.Details or BookPage.Materials or BookPage.Tags
                => BlueprintManager.ActiveBlueprint != null,
            _ => true,
        };

        private static void HandleBookNavigation(Event e)
        {
            if (IsEditingText || Time.realtimeSinceStartup < _flipUntil) return;

            bool previousTab = e.type == EventType.KeyDown && e.keyCode == KeyCode.Q;
            bool nextTab = e.type == EventType.KeyDown && e.keyCode == KeyCode.E;
            bool activate = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.Space);
            bool remove = e.type == EventType.KeyDown && (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace);
            bool left = e.type == EventType.KeyDown && e.keyCode == KeyCode.LeftArrow;
            bool right = e.type == EventType.KeyDown && e.keyCode == KeyCode.RightArrow;
            bool up = e.type == EventType.KeyDown && e.keyCode == KeyCode.UpArrow;
            bool down = e.type == EventType.KeyDown && e.keyCode == KeyCode.DownArrow;

            if (e.type == EventType.Repaint && Time.realtimeSinceStartup >= _nextPadInput)
            {
                previousTab |= Input.GetKeyDown(KeyCode.JoystickButton4);
                nextTab |= Input.GetKeyDown(KeyCode.JoystickButton5);
                activate |= Input.GetKeyDown(KeyCode.JoystickButton0);
                remove |= Input.GetKeyDown(KeyCode.JoystickButton2);
                float horizontal = Input.GetAxisRaw("Horizontal");
                float vertical = Input.GetAxisRaw("Vertical");
                left |= horizontal < -0.65f;
                right |= horizontal > 0.65f;
                up |= vertical > 0.65f;
                down |= vertical < -0.65f;
                if (previousTab || nextTab || activate || remove || left || right || up || down)
                    _nextPadInput = Time.realtimeSinceStartup + 0.18f;
            }

            if (previousTab || nextTab)
            {
                CyclePage(nextTab ? 1 : -1);
                if (e.type == EventType.KeyDown) e.Use();
                return;
            }

            if (_bookPage == BookPage.Library && BlueprintLibraryStore.Names.Count > 0)
            {
                int visible = Mathf.Min(LibraryMaxRows, BlueprintLibraryStore.Names.Count - _savedScroll);
                _libraryFocus = Mathf.Clamp(_libraryFocus, 0, Mathf.Max(0, visible - 1));
                if (up) _libraryFocus = Mathf.Max(0, _libraryFocus - 1);
                if (down) _libraryFocus = Mathf.Min(visible - 1, _libraryFocus + 1);
                if (left && _savedScroll > 0)
                {
                    _savedScroll = Mathf.Max(0, _savedScroll - LibraryMaxRows);
                    _libraryFocus = 0;
                }
                if (right && _savedScroll + LibraryMaxRows < BlueprintLibraryStore.Names.Count)
                {
                    _savedScroll += LibraryMaxRows;
                    _libraryFocus = 0;
                }
                if (activate)
                {
                    string name = BlueprintLibraryStore.Names[_savedScroll + _libraryFocus];
                    if (BlueprintLibraryStore.Blueprints.TryGetValue(name, out var bp))
                    {
                        if (_libraryPreviewBlueprint == bp)
                            SelectLibraryBlueprint(name, bp);
                        else
                            PreviewLibraryBlueprint(name, bp);
                    }
                }
                if (remove && !_libraryMaterialPicker) DeleteFocusedBlueprint();
                if ((left || right || up || down || activate || remove) && e.type == EventType.KeyDown) e.Use();
                return;
            }

            if (_bookPage == BookPage.Tools)
            {
                if (left || up) _pageFocus = Mathf.Max(0, _pageFocus - 1);
                if (right || down) _pageFocus = Mathf.Min(5, _pageFocus + 1);
                if (activate)
                {
                    BuildTool[] tools = { BuildTool.Blueprint, BuildTool.Cut, BuildTool.WallArea, BuildTool.Circle, BuildTool.Polygon };
                    if (_pageFocus == 5)
                        ConfirmBookTool(BuildToolState.ActiveTool);
                    else
                        SelectBookTool(tools[_pageFocus]);
                }
                return;
            }

            if (_bookPage == BookPage.Options)
            {
                HandleOptionsNavigation(left, right, up, down, activate);
                return;
            }

            if (_bookPage == BookPage.Tags && BlueprintManager.ActiveBlueprint != null)
            {
                if (left || up) _pageFocus = Mathf.Max(0, _pageFocus - 1);
                if (right || down) _pageFocus = Mathf.Min(TagChoices.Length, _pageFocus + 1);
                if (activate)
                {
                    var bp = BlueprintManager.ActiveBlueprint;
                    if (_pageFocus < TagChoices.Length)
                    {
                        ToggleTag(bp, TagChoices[_pageFocus]);
                        ShowFeedback($"Toggled {TagChoices[_pageFocus]} tag");
                    }
                    else if (BlueprintSerializer.Save(bp))
                    {
                        BlueprintLibraryStore.Put(bp);
                        BlueprintLibraryStore.Invalidate();
                        ShowFeedback("Tags saved");
                    }
                }
                return;
            }

            if (_bookPage == BookPage.Details && BlueprintManager.ActiveBlueprint != null)
            {
                if (left) _pageFocus = 0;
                if (right) _pageFocus = 1;
                if (activate)
                {
                    var bp = BlueprintManager.ActiveBlueprint;
                    if (_pageFocus == 0 && BlueprintSerializer.Save(bp))
                    {
                        BlueprintLibraryStore.Put(bp);
                        BlueprintLibraryStore.Invalidate();
                        ShowFeedback($"Saved {bp.name}");
                    }
                    else if (_pageFocus == 1)
                    {
                        GUIUtility.systemCopyBuffer = BlueprintSerializer.Export(bp) ?? "";
                        ShowFeedback("Blueprint copied to clipboard");
                    }
                }
                return;
            }

            if (_bookPage == BookPage.Materials)
            {
                int pages = Mathf.Max(1, Mathf.CeilToInt(TileCostTracker.CurrentCost.Count / 6f));
                if (left) _materialPage = Mathf.Max(0, _materialPage - 1);
                if (right) _materialPage = Mathf.Min(pages - 1, _materialPage + 1);
            }
        }

        private static void CyclePage(int direction)
        {
            _libraryMaterialPicker = false;
            int index = System.Array.IndexOf(PageOrder, _bookPage);
            for (int i = 0; i < PageOrder.Length; i++)
            {
                index = (index + direction + PageOrder.Length) % PageOrder.Length;
                if (!IsPageEnabled(PageOrder[index])) continue;
                _bookPage = PageOrder[index];
                _pageFocus = 0;
                return;
            }
        }

        private static void DeleteFocusedBlueprint()
        {
            if (BlueprintLibraryStore.Names.Count == 0) return;
            string name = BlueprintLibraryStore.Names[Mathf.Clamp(_savedScroll + _libraryFocus, 0, BlueprintLibraryStore.Names.Count - 1)];
            bool armed = _armedDelete == name && Time.realtimeSinceStartup < _armedDeleteUntil;
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
                ShowFeedback($"Delete {name}? Press delete again");
            }
        }

        private static void ShowFeedback(string message, float duration = 2.2f)
        {
            _feedback = message;
            _feedbackUntil = Time.realtimeSinceStartup + duration;
        }

        private static void SelectBookTool(BuildTool tool)
        {
            BuildToolState.ActiveTool = tool;
            _openDropdown = null;
            _bookPage = BookPage.Tools;
            _pageFocus = ToolIndex(tool);
            ShowFeedback($"Selected {TitleText()}");
        }

        private static void HandleOptionsNavigation(bool left, bool right, bool up, bool down, bool activate)
        {
            int max = BuildToolState.ActiveTool switch
            {
                BuildTool.Blueprint => 4,
                BuildTool.Circle or BuildTool.Polygon => 5,
                BuildTool.WallArea => 3,
                _ => 2,
            };
            if (left || up) _pageFocus = Mathf.Max(0, _pageFocus - 1);
            if (right || down) _pageFocus = Mathf.Min(max, _pageFocus + 1);
            if (!activate) return;

            var tool = BuildToolState.ActiveTool;
            if (_pageFocus == max)
            {
                _bookPage = BookPage.Tools;
                _pageFocus = ToolIndex(tool);
                return;
            }
            if (tool == BuildTool.Blueprint)
            {
                if (_pageFocus == 0)
                    BlueprintManager.CopyMode = (BlueprintCopyMode)(((int)BlueprintManager.CopyMode + 1) % 4);
                else if (_pageFocus == 1)
                    BlueprintManager.PasteMode = BlueprintManager.PasteMode == BlueprintPasteMode.Replace
                        ? BlueprintPasteMode.MergeEmpty : BlueprintPasteMode.Replace;
                else if (_pageFocus == 2) OpenBlueprintLibrary();
                else if (_pageFocus == 3) BlueprintManager.RequestUndo();
            }
            else if (tool == BuildTool.Circle || tool == BuildTool.Polygon)
            {
                if (_pageFocus == 0) BuildToolState.ShapeWidth = Mathf.Clamp(BuildToolState.ShapeWidth + 1, 3, 101);
                else if (_pageFocus == 1)
                {
                    if (tool == BuildTool.Polygon)
                        BuildToolState.PolygonSides = Mathf.Clamp(BuildToolState.PolygonSides + 1, 3, 16);
                    else BuildToolState.ShapeHeight = Mathf.Clamp(BuildToolState.ShapeHeight + 1, 3, 101);
                }
                else if (_pageFocus == 2) BuildToolState.Hollow = !BuildToolState.Hollow;
                else if (_pageFocus == 3)
                    BuildToolState.Anchor = BuildToolState.Anchor == ShapeAnchor.Cursor ? ShapeAnchor.Player : ShapeAnchor.Cursor;
                else if (_pageFocus == 4) BeginMaterialPicker(BookPage.Options, tool);
            }
            else if (tool == BuildTool.WallArea && _pageFocus == 0)
                BeginMaterialPicker(BookPage.Options, tool);
            else if (_pageFocus == 0)
                BlueprintManager.RequestUndo();
        }

        private static void ConfirmBookTool(BuildTool tool)
        {
            BuildToolState.ActiveTool = tool;
            _openDropdown = null;
            if (tool == BuildTool.Circle || tool == BuildTool.Polygon)
            {
                if (HasWallMaterial())
                {
                    _bookPage = BookPage.Options;
                    _pageFocus = 0;
                }
                else BeginMaterialPicker(BookPage.Options, tool);
            }
            else if (tool == BuildTool.WallArea && !HasWallMaterial())
                BeginMaterialPicker(BookPage.Options, tool);
            else
            {
                _bookPage = BookPage.Options;
                _pageFocus = 0;
            }
        }

        private static int ToolIndex(BuildTool tool) => tool switch
        {
            BuildTool.Blueprint => 0,
            BuildTool.Cut => 1,
            BuildTool.WallArea => 2,
            BuildTool.Circle => 3,
            BuildTool.Polygon => 4,
            _ => 0,
        };

        private static void OpenBlueprintLibrary()
        {
            _libraryMaterialPicker = false;
            _bookPage = BookPage.Library;
            _pageFocus = 0;
            _openDropdown = null;
            _libraryPreviewBlueprint = BlueprintManager.ActiveBlueprint;
            _libraryPreviewName = _libraryPreviewBlueprint?.name;
            ShowFeedback("Preview a blueprint, then load it");
        }

        private static void BeginMaterialPicker(BookPage returnPage, BuildTool tool)
        {
            _libraryMaterialPicker = true;
            _materialPickerReturnPage = returnPage;
            _materialPickerTool = tool;
            _bookPage = BookPage.Library;
            _pageFocus = 0;
            _libraryPreviewBlueprint = BlueprintManager.ActiveBlueprint;
            _libraryPreviewName = _libraryPreviewBlueprint?.name;
            ShowFeedback("Choose a blueprint with walls");
        }

        private static void SelectLibraryBlueprint(string name, Blueprint bp)
        {
            if (bp == null) return;
            bool pickingMaterial = _libraryMaterialPicker;
            BuildTool returnTool = _materialPickerTool;
            if (pickingMaterial && !BlueprintHasWall(bp))
            {
                BuildToolState.ActiveTool = returnTool;
                ShowFeedback("That blueprint has no walls");
                return;
            }

            BlueprintManager.SetLoadedBlueprint(bp);
            _libraryPreviewBlueprint = bp;
            _libraryPreviewName = name;
            _libraryFocus = Mathf.Clamp(BlueprintLibraryStore.Names.IndexOf(name) - _savedScroll, 0, LibraryMaxRows - 1);

            if (pickingMaterial)
            {
                _libraryMaterialPicker = false;
                BuildToolState.ActiveTool = returnTool;
                _bookPage = _materialPickerReturnPage;
                _pageFocus = 0;
                ShowFeedback($"Material: {name}");
            }
            else
            {
                _bookPage = BookPage.Tools;
                _pageFocus = ToolIndex(BuildToolState.ActiveTool);
                ShowFeedback($"Loaded {name}");
            }
        }

        private static bool BlueprintHasWall(Blueprint bp)
        {
            if (bp == null) return false;
            foreach (var tile in bp.tiles)
                if (tile.hasWall) return true;
            return false;
        }

        private static void DrawBookShapeControls(int x, int y, int w, int s, bool polygon)
        {
            int panelW = w - 2;
            int contentX = x + 4 * s;
            int contentW = panelW - 8;
            var settingsBlock = new Rect(x, y, panelW * s, 55 * s);
            FbTonalPanel(settingsBlock, s, false);
            FbBox(settingsBlock, "fbBox61", 2, s);
            FbBookIcon(new Rect(settingsBlock.xMax - 20 * s, settingsBlock.y + 6 * s, 14 * s, 14 * s),
                polygon ? 11 : 7, 0.10f, s);
            GUI.Label(new Rect(contentX, y + 3 * s, (contentW - 20) * s, 8 * s),
                polygon ? "POLYGON" : "CIRCLE / OVAL", _textAccent);
            y += 12 * s;
            y = BookStepper(contentX, y, s, "WIDTH", BuildToolState.ShapeWidth, 3, 101,
                v => { BuildToolState.ShapeWidth = Mathf.Clamp(v, 3, 101); if (polygon) BuildToolState.ShapeHeight = BuildToolState.ShapeWidth; });
            y = BookStepper(contentX, y, s, polygon ? "SIDES" : "HEIGHT",
                polygon ? BuildToolState.PolygonSides : BuildToolState.ShapeHeight, 3, polygon ? 16 : 101,
                v => { if (polygon) BuildToolState.PolygonSides = Mathf.Clamp(v, 3, 16); else BuildToolState.ShapeHeight = Mathf.Clamp(v, 3, 101); });
            const int shapeButtonGap = 4;
            const int generateW = 12;
            int optionW = contentW - generateW - shapeButtonGap;
            int half = (optionW - shapeButtonGap) / 2;
            if (Button(new Rect(contentX, y, half * s, 10 * s), "HOLLOW", "Toggle hollow shape", s,
                    BuildToolState.Hollow)) BuildToolState.Hollow = !BuildToolState.Hollow;
            if (Button(new Rect(contentX + (half + shapeButtonGap) * s, y, half * s, 10 * s),
                    BuildToolState.Anchor == ShapeAnchor.Cursor ? "CURSOR" : "PLAYER",
                    "Toggle shape anchor", s))
                BuildToolState.Anchor = BuildToolState.Anchor == ShapeAnchor.Cursor ? ShapeAnchor.Player : ShapeAnchor.Cursor;
            if (Button(new Rect(contentX + (contentW - generateW) * s, y, generateW * s, 10 * s), "",
                    "Generate blueprint using copied wall material", s, enabled: HasWallMaterial(),
                    tone: ButtonTone.Confirm, glyph: IconNext))
                if (ShapeBlueprintGenerator.TryGenerate(BuildToolState.ActiveTool, BuildToolState.ShapeWidth,
                        BuildToolState.ShapeHeight, BuildToolState.PolygonSides, BuildToolState.Hollow,
                        BuildToolState.OutlineThickness)) BuildToolState.ActiveTool = BuildTool.Blueprint;
            y += 12 * s;
            DrawMaterialSourceStrip(contentX, y, contentW, s, BookPage.Options,
                polygon ? BuildTool.Polygon : BuildTool.Circle);
        }

        private static void DrawMaterialSourceStrip(int x, int y, int w, int s, BookPage returnPage, BuildTool tool)
        {
            var source = BlueprintManager.ActiveBlueprint;
            bool hasMaterial = BlueprintHasWall(source);
            var strip = new Rect(x, y, w * s, 10 * s);
            FillRect(strip, hasMaterial ? Hex(0xDFC09B) : Hex(0xCF9A84));
            string sourceName = source == null ? "NONE" : source.name.Length > 12 ? source.name.Substring(0, 10) + ".." : source.name;
            GUI.Label(new Rect(x + 3 * s, y + s, 44 * s, 8 * s), "MATERIAL", hasMaterial ? _textDim : _textBlocked);
            GUI.Label(new Rect(x + 43 * s, y + s, (w - 62) * s, 8 * s), sourceName,
                hasMaterial ? _text : _textBlocked);
            if (Button(new Rect(x + (w - 18) * s, y, 18 * s, 10 * s),
                    hasMaterial ? "CHG" : "PICK", "Choose a material blueprint", s,
                    tone: hasMaterial ? ButtonTone.Neutral : ButtonTone.Highlight))
                BeginMaterialPicker(returnPage, tool);
        }

        private static int BookStepper(int x, int y, int s, string label, int value,
            int min, int max, System.Action<int> set)
        {
            GUI.Label(new Rect(x + 2 * s, y, 30 * s, 9 * s), label, _text);
            var slider = new Rect(x + 31 * s, y + s, 16 * s, 7 * s);
            var interactionRect = new Rect(slider.x - s, y, slider.width + 2 * s, 9 * s);
            int controlId = GUIUtility.GetControlID((label + x + y).GetHashCode(), FocusType.Passive, interactionRect);
            var e = Event.current;
            bool hover = interactionRect.Contains(e.mousePosition);
            if (hover && e.type == EventType.Repaint) _hint = $"Drag to change {label.ToLower()}";

            if (hover && e.type == EventType.MouseDown && e.button == 0)
            {
                GUIUtility.hotControl = controlId;
                SetSliderValue(e.mousePosition.x, slider, min, max, set);
                e.Use();
            }
            else if (GUIUtility.hotControl == controlId && e.type == EventType.MouseDrag)
            {
                SetSliderValue(e.mousePosition.x, slider, min, max, set);
                e.Use();
            }
            else if (GUIUtility.hotControl == controlId && e.type == EventType.MouseUp)
            {
                SetSliderValue(e.mousePosition.x, slider, min, max, set);
                GUIUtility.hotControl = 0;
                e.Use();
            }

            float normalized = Mathf.InverseLerp(min, max, value);
            FillRect(new Rect(slider.x, slider.y + 3 * s, slider.width, s), Hex(0x8A4A38));
            FillRect(new Rect(slider.x, slider.y + 3 * s, slider.width * normalized, s), Hex(0xE0B23D));
            FillRect(new Rect(Mathf.Lerp(slider.x, slider.xMax - 4 * s, normalized),
                slider.y + s, 4 * s, 5 * s), Accent);
            if (Button(new Rect(x + 48 * s, y, 11 * s, 9 * s), "-", $"Decrease {label}", s)) set(value - 1);
            GUI.Label(new Rect(x + 60 * s, y, 14 * s, 9 * s), value.ToString(), _textCenter);
            if (Button(new Rect(x + 75 * s, y, 11 * s, 9 * s), "+", $"Increase {label}", s)) set(value + 1);
            return y + 11 * s;
        }

        private static void SetSliderValue(float mouseX, Rect slider, int min, int max, System.Action<int> set)
        {
            float normalized = Mathf.InverseLerp(slider.x, slider.xMax, mouseX);
            set(Mathf.RoundToInt(Mathf.Lerp(min, max, normalized)));
        }

        private static void DrawPageHeading(Rect rect, string label, BookPage page, int s)
            => FbBanner(new Rect(rect.x, rect.y, rect.width, rect.height + 2 * s), label, s);

        private static void DrawSectionLabel(Rect rect, string label, int s)
        {
            GUI.Label(rect, label, _textAccent);
            float lineX = rect.x + Mathf.Min(rect.width - 4 * s, (label.Length * 4 + 4) * s);
            FillRect(new Rect(lineX, rect.y + 4 * s, rect.xMax - lineX, s), DividerCol);
        }

        private static void DrawStatusBar(Rect rect, string label, int color, int s)
        {
            FillRect(rect, color == 0 ? Hex(0xCF8D78)
                : color == 2 ? Hex(0xB9C986) : Hex(0xD8B77B));
            GUI.Label(new Rect(rect.x + 4 * s, rect.y + s, rect.width - 8 * s, rect.height - 2 * s),
                label, color == 0 ? _textBlockedCenter : color == 2 ? _textReadyCenter : _textCenter);
        }

        private static GUIStyle WrappedStyle(GUIStyle source) => new GUIStyle(source)
        {
            wordWrap = true,
            clipping = TextClipping.Clip,
        };

        private static void DrawAtlasIcon(Rect destination, Texture2D texture,
            int sourceX, int sourceY, int sourceW, int sourceH, int atlasW, int atlasH)
        {
            if (texture == null) return;
            var uv = new Rect(
                (float)sourceX / atlasW,
                1f - (float)(sourceY + sourceH) / atlasH,
                (float)sourceW / atlasW,
                (float)sourceH / atlasH);
            GUI.DrawTextureWithTexCoords(destination, texture, uv, true);
        }

        private static void DrawNineSlice(Rect destination, Texture2D texture,
            int sourceX, int sourceY, int sourceW, int sourceH,
            int sourceBorderX, int sourceBorderY, int atlasW, int atlasH, float border)
        {
            if (texture == null) return;
            float middleW = Mathf.Max(0f, destination.width - border * 2f);
            float middleH = Mathf.Max(0f, destination.height - border * 2f);
            int sourceMiddleW = sourceW - sourceBorderX * 2;
            int sourceMiddleH = sourceH - sourceBorderY * 2;

            float[] dx = { destination.x, destination.x + border, destination.x + border + middleW };
            float[] dy = { destination.y, destination.y + border, destination.y + border + middleH };
            float[] dw = { border, middleW, border };
            float[] dh = { border, middleH, border };
            int[] sx = { sourceX, sourceX + sourceBorderX, sourceX + sourceW - sourceBorderX };
            int[] sy = { sourceY, sourceY + sourceBorderY, sourceY + sourceH - sourceBorderY };
            int[] sw = { sourceBorderX, sourceMiddleW, sourceBorderX };
            int[] sh = { sourceBorderY, sourceMiddleH, sourceBorderY };

            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                    if (dw[col] > 0f && dh[row] > 0f)
                    {
                        const float seamOverlap = 0.5f;
                        float left = col > 0 ? seamOverlap : 0f;
                        float top = row > 0 ? seamOverlap : 0f;
                        float right = col < 2 ? seamOverlap : 0f;
                        float bottom = row < 2 ? seamOverlap : 0f;
                        DrawAtlasIcon(new Rect(dx[col] - left, dy[row] - top,
                                dw[col] + left + right, dh[row] + top + bottom), texture,
                            sx[col], sy[row], sw[col], sh[row], atlasW, atlasH);
                    }
        }

        private static string TitleText() => BuildToolState.ActiveTool switch
        {
            BuildTool.Blueprint => "BLUEPRINT",
            BuildTool.Cut => "CUT",
            BuildTool.WallArea => "WALLS",
            BuildTool.Circle => "CIRCLE",
            BuildTool.Polygon => "POLYGON",
            _ => "BUILDER",
        };

        private static string ShortToolName() => BuildToolState.ActiveTool switch
        {
            BuildTool.Blueprint => "BP",
            BuildTool.WallArea => "WALL",
            BuildTool.Circle => "CIRC",
            BuildTool.Polygon => "POLY",
            _ => "CUT",
        };

        private static string ShortPasteMode() => BlueprintManager.PasteMode == BlueprintPasteMode.Replace
            ? "REPLACE" : "MERGE";

        private static string GuideText() => BuildToolState.ActiveTool switch
        {
            BuildTool.Blueprint when BlueprintManager.State == GadgetState.Selecting
                => "Release to capture the area",
            BuildTool.Blueprint when BlueprintManager.ActiveBlueprint == null
                => "Drag in the world to copy",
            BuildTool.Blueprint => "Click in the world to paste",
            BuildTool.Cut => "Drag to clear an area",
            BuildTool.WallArea => "Drag to fill with walls",
            BuildTool.Circle or BuildTool.Polygon when !HasWallMaterial()
                => "Choose a material blueprint",
            BuildTool.Circle or BuildTool.Polygon => "Set size, then Generate",
            _ => "",
        };

        private static string StatusText(Blueprint bp)
        {
            return BlueprintManager.State == GadgetState.Selecting
                ? "Selecting"
                : bp == null ? "Empty"
                : bp.unsupportedObjectCount > 0
                    ? $"{bp.width}x{bp.height} - {bp.unsupportedObjectCount} skipped"
                    : $"{bp.width}x{bp.height} ({bp.tiles.Count})";
        }

        private static string FormatItemName(ObjectID id)
        {
            string raw = id.ToString();
            var sb = new System.Text.StringBuilder(raw.Length + 4);
            for (int i = 0; i < raw.Length; i++)
            {
                if (i > 0 && char.IsUpper(raw[i]) && char.IsLower(raw[i - 1]))
                    sb.Append(' ');
                sb.Append(raw[i]);
            }
            string value = sb.ToString();
            return value.Length <= 18 ? value : value.Substring(0, 16) + "..";
        }

        private static void ToggleTag(Blueprint bp, string tag)
        {
            bp.tags ??= new List<string>();
            for (int i = 0; i < bp.tags.Count; i++)
            {
                if (!string.Equals(bp.tags[i], tag, System.StringComparison.OrdinalIgnoreCase)) continue;
                bp.tags.RemoveAt(i);
                return;
            }
            bp.tags.Add(tag);
        }

        private static string TagSummary(Blueprint bp)
        {
            if (bp?.tags == null || bp.tags.Count == 0) return "UNTAGGED";
            string value = string.Join(",", bp.tags);
            return value.Length <= 20 ? value : value.Substring(0, 18) + "..";
        }

        private static bool HasWallMaterial()
        {
            return BlueprintHasWall(BlueprintManager.ActiveBlueprint);
        }

        private static void RefreshSavedNames()
        {
            if (!BlueprintLibraryStore.Refresh(_tagFilter, _librarySearch, (int)_librarySort)) return;
            int pageCount = Mathf.Max(1, Mathf.CeilToInt(BlueprintLibraryStore.Names.Count / (float)LibraryMaxRows));
            _savedScroll = Mathf.Clamp((_savedScroll / LibraryMaxRows) * LibraryMaxRows,
                0, (pageCount - 1) * LibraryMaxRows);
            if (_libraryPreviewName != null &&
                !BlueprintLibraryStore.Blueprints.ContainsKey(_libraryPreviewName) &&
                BlueprintManager.ActiveBlueprint != _libraryPreviewBlueprint)
            {
                _libraryPreviewName = null;
                _libraryPreviewBlueprint = null;
            }
        }

        private static void EnsureStyles(int s)
        {
            if (_styleScale == s && _text != null) return;
            _styleScale = s;

            int size = 6 * s;

            GUIStyle Make(Color c, TextAnchor anchor)
            {
                var st = new GUIStyle(GUI.skin.label)
                {
                    fontSize = size,
                    alignment = anchor,
                    clipping = TextClipping.Clip,
                    wordWrap = false,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                };
                if (UiAssets.PixelFont != null) st.font = UiAssets.PixelFont;
                st.normal.textColor = c;
                st.hover.textColor = c;
                return st;
            }

            _text          = Make(TextMain, TextAnchor.MiddleLeft);
            _textDim       = Make(TextDim, TextAnchor.MiddleLeft);
            _textAccent    = Make(Accent, TextAnchor.MiddleLeft);
            _textGuide     = Make(GuideCol, TextAnchor.MiddleCenter);
            _textCenter    = Make(TextMain, TextAnchor.MiddleCenter);
            _textDimCenter = Make(TextDim, TextAnchor.MiddleCenter);
            _textReady         = Make(ReadyCol, TextAnchor.MiddleLeft);
            _textBlocked       = Make(BlockedCol, TextAnchor.MiddleLeft);
            _textReadyCenter   = Make(ReadyCol, TextAnchor.MiddleCenter);
            _textBlockedCenter = Make(BlockedCol, TextAnchor.MiddleCenter);
            _textReadyRight    = Make(ReadyCol, TextAnchor.MiddleRight);
            _textBlockedRight  = Make(BlockedCol, TextAnchor.MiddleRight);
            _btnLabel      = Make(TextMain, TextAnchor.MiddleCenter);
            _btnLabelDim   = Make(TextDim, TextAnchor.MiddleCenter);
            _btnLabelLight = Make(Hex(0xFFF6E8), TextAnchor.MiddleCenter);
            _blueprintText          = Make(Hex(0xF7FBFF), TextAnchor.MiddleLeft);
            _blueprintTextCenter    = Make(Hex(0xF7FBFF), TextAnchor.MiddleCenter);
            _blueprintTextDim       = Make(Hex(0xBFD9EC), TextAnchor.MiddleLeft);
            _blueprintTextDimCenter = Make(Hex(0xBFD9EC), TextAnchor.MiddleCenter);
            _heading = new GUIStyle(_textCenter) { fontSize = 8 * s };
            _heading.normal.textColor = Accent;
            _heading.hover.textColor = Accent;

            _fbRowTitle = new GUIStyle(_text) { fontSize = 6 * s };
            _fbRowTitleDim = new GUIStyle(_fbRowTitle);
            _fbRowTitleDim.normal.textColor = Hex(0xA66F56);
            _fbRowTitleDim.hover.textColor = Hex(0xA66F56);
            _fbRowSub = new GUIStyle(_textDim) { fontSize = 5 * s };
            _fbLibrarySub = new GUIStyle(_textDim) { fontSize = 4 * s };
            _fbName = new GUIStyle(_text) { fontSize = 6 * s };
            _fbBody = new GUIStyle(_textDim)
            {
                fontSize = 5 * s,
                wordWrap = true,
                clipping = TextClipping.Clip,
                alignment = TextAnchor.UpperLeft,
            };
            _fbBodyCenter = new GUIStyle(_fbBody) { alignment = TextAnchor.MiddleCenter };
            _fbTiny = new GUIStyle(_textDim) { fontSize = 5 * s };
            _fbTinyCenter = new GUIStyle(_fbTiny) { alignment = TextAnchor.MiddleCenter };
            _fbValueRight = new GUIStyle(_text) { alignment = TextAnchor.MiddleRight };
            _fbCountReady = new GUIStyle(_fbTinyCenter);
            _fbCountReady.normal.textColor = ReadyCol;
            _fbCountReady.hover.textColor = ReadyCol;
            _fbCountBlocked = new GUIStyle(_fbTinyCenter);
            _fbCountBlocked.normal.textColor = BlockedCol;
            _fbCountBlocked.hover.textColor = BlockedCol;
        }

        private static void PreviewLibraryBlueprint(string name, Blueprint bp)
        {
            if (bp == null) return;
            _libraryPreviewBlueprint = bp;
            _libraryPreviewName = name;
            _libraryFocus = Mathf.Clamp(BlueprintLibraryStore.Names.IndexOf(name) - _savedScroll, 0, LibraryMaxRows - 1);
            ShowFeedback($"Previewing {name}");
        }

        private static Color Hex(int rgb) => new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f, 1f);
    }
}

