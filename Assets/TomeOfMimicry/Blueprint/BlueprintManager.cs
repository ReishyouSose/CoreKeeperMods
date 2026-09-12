using Rewired;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace TomeOfMimicry
{
    public enum GadgetState
    {
        Idle,
        Selecting,
        HoldingBlueprint
    }

    public static class BlueprintManager
    {
        public static GadgetState State { get; private set; } = GadgetState.Idle;
        public static Blueprint ActiveBlueprint { get; private set; }
        public static BlueprintCopyMode CopyMode { get; set; } = BlueprintCopyMode.Full;
        public static BlueprintPasteMode PasteMode { get; set; } = BlueprintPasteMode.MergeEmpty;

        public static bool IsGadgetEquipped;
        public static bool PendingCapture;
        public static bool PendingPaste;
        public static bool PendingTileTest;
        public static bool PasteFailed;
        public static string PasteFailureMessage;

        public static int2 CachedCursorTilePos { get; private set; }
        public static int2 PasteOrigin;
        public static int2 TileTestPos;

        public static int BlueprintRotation { get; private set; }
        public static bool BlueprintFlipped { get; private set; }

        private static int2 _selectionStart;
        private static int2 _selectionEnd;
        private static bool _selectionStartedWithBlueprint;
        private static bool _selectionDragged;
        private static readonly List<Blueprint> _savedBlueprints = new();

        private static int _undoable;
        private static int _redoable;

        public static int SavedCount => _savedBlueprints.Count;

        public static bool CanUndo => _undoable > 0;
        public static bool CanRedo => _redoable > 0;

        public static void RequestUndo()
        {
            if (_undoable <= 0)
            {
                Debug.Log("[Tome of Mimicry] Nothing to undo.");
                return;
            }

            if (ClientBlueprintPasteSystem.TrySendCommand(1))
            {
                _undoable--;
                _redoable++;
                Debug.Log("[Tome of Mimicry] Sent undo request to server");
            }
        }

        public static void HandleServerFeedback(int command)
        {
            switch (command)
            {
                case 4:
                    _undoable = math.max(0, _undoable - 1);
                    PasteFailed = true;
                    Debug.Log("[Tome of Mimicry] Server rejected the last operation");
                    break;
                case 5:
                    _undoable = math.max(0, _undoable - 1);
                    _redoable++;
                    PasteFailed = true;
                    Debug.Log("[Tome of Mimicry] Redo blocked: not enough materials");
                    break;
                case 6:
                    _redoable = math.max(0, _redoable - 1);
                    break;
                case 7:
                    _undoable = math.max(0, _undoable - 1);
                    break;
            }
        }

        public static void RequestRedo()
        {
            if (_redoable <= 0)
            {
                Debug.Log("[Tome of Mimicry] Nothing to redo.");
                return;
            }

            if (ClientBlueprintPasteSystem.TrySendCommand(2))
            {
                _redoable--;
                _undoable++;
                Debug.Log("[Tome of Mimicry] Sent redo request to server");
            }
        }

        public static void UpdateCursor(int2 cursorPos)
        {
            CachedCursorTilePos = BuildToolState.UsesShapeAnchor
                ? BuildToolState.ResolveAnchor(cursorPos)
                : cursorPos;
            if (State == GadgetState.HoldingBlueprint)
                PasteOrigin = CachedCursorTilePos;
        }

        public static void HandleInput(Player rwPlayer, PlayerController player, int2 cursorPos)
        {
            if (!IsGadgetEquipped)
            {
                if (State != GadgetState.Idle)
                    Cancel();
                return;
            }

            bool overUI = BuilderPanelUI.IsMouseOver;
            bool selectDown = Input.GetMouseButtonDown(1) && !overUI;
            bool selectHeld = Input.GetMouseButton(1) && !overUI;
            bool selectUp = Input.GetMouseButtonUp(1);
            bool cancelDown = Input.GetMouseButtonDown(2) && !overUI;

            bool shortcutsEnabled = !BuilderPanelUI.IsOpen && !BuilderPanelUI.IsMouseOver;
            if (shortcutsEnabled && Input.GetKeyDown(KeyCode.F5))
                CycleCopyMode();
            if (shortcutsEnabled && Input.GetKeyDown(KeyCode.F6))
                CyclePasteMode();
            if (shortcutsEnabled && Input.GetKeyDown(KeyCode.R) && State == GadgetState.HoldingBlueprint)
                CycleRotation();
            if (shortcutsEnabled && Input.GetKeyDown(KeyCode.X) && State == GadgetState.HoldingBlueprint)
                ToggleFlip();
            if (Input.GetKeyDown(KeyCode.Escape) && State != GadgetState.Idle)
                Cancel();

            switch (State)
            {
                case GadgetState.Idle:
                    if (selectDown && !BuilderPanelUI.IsMouseOver)
                        BeginSelection(cursorPos, false);
                    break;

                case GadgetState.Selecting:
                    if (selectHeld && !BuilderPanelUI.IsMouseOver)
                    {
                        _selectionEnd = cursorPos;
                        _selectionDragged |= !cursorPos.Equals(_selectionStart);
                    }

                    if (selectUp)
                    {
                        _selectionEnd = cursorPos;
                        _selectionDragged |= !cursorPos.Equals(_selectionStart);

                        if (_selectionStartedWithBlueprint && !_selectionDragged)
                        {
                            State = GadgetState.HoldingBlueprint;
                            PasteBlueprint(BuildToolState.UsesShapeAnchor
                                ? BuildToolState.ResolveAnchor(cursorPos)
                                : cursorPos);
                        }
                        else if (BuildToolState.ActiveTool == BuildTool.Cut)
                        {
                            var (min, max) = GetSelectionBounds();
                            RequestCut(min, max);
                            State = GadgetState.Idle;
                        }
                        else if (BuildToolState.ActiveTool == BuildTool.WallArea)
                        {
                            var (min, max) = GetSelectionBounds();
                            FillWalls(min, max);
                            State = GadgetState.Idle;
                        }
                        else
                        {
                            PendingCapture = true;
                            var (min, max) = GetSelectionBounds();
                            Debug.Log($"[Tome of Mimicry] Selection complete min={min} max={max}");
                            State = GadgetState.HoldingBlueprint;
                        }
                    }

                    if (cancelDown)
                        Cancel();
                    break;

                case GadgetState.HoldingBlueprint:
                    if (selectDown && !BuilderPanelUI.IsMouseOver)
                    {
                        BeginSelection(cursorPos, true);
                        break;
                    }

                    if (cancelDown)
                        Cancel();
                    break;
            }
        }

        private static void BeginSelection(int2 cursorPos, bool startedWithBlueprint)
        {
            _selectionStart = cursorPos;
            _selectionEnd = cursorPos;
            _selectionStartedWithBlueprint = startedWithBlueprint;
            _selectionDragged = false;
            PendingCapture = false;
            State = GadgetState.Selecting;
            Debug.Log(startedWithBlueprint
                ? $"[Tome of Mimicry] Right-click armed paste at {cursorPos}; drag to reselect"
                : $"[Tome of Mimicry] Selection started at {cursorPos}");
        }

        public static (int2 min, int2 max) GetSelectionBounds()
        {
            return (math.min(_selectionStart, _selectionEnd), math.max(_selectionStart, _selectionEnd));
        }

        public static void ClearActiveBlueprint()
        {
            ActiveBlueprint = null;
        }

        public static void SetCapturedBlueprint(Blueprint blueprint)
        {
            ActivateBlueprint(blueprint);
            _savedBlueprints.Add(blueprint);
        }

        public static void SetLoadedBlueprint(Blueprint blueprint)
        {
            ActivateBlueprint(blueprint);
            BuildToolState.ActiveTool = BuildTool.Blueprint;
            Debug.Log($"[Tome of Mimicry] Activated loaded blueprint '{blueprint.name}' for placement");
        }

        public static void SetGeneratedBlueprint(Blueprint blueprint)
        {
            ActivateBlueprint(blueprint);
        }

        private static void ActivateBlueprint(Blueprint blueprint)
        {
            if (blueprint == null)
                return;

            ActiveBlueprint = blueprint;
            PendingCapture = false;
            BlueprintRotation = 0;
            BlueprintFlipped = false;
            State = GadgetState.HoldingBlueprint;
            PasteOrigin = CachedCursorTilePos;
        }

        public static int2 GetRotatedRelPos(int2 relPos)
        {
            return GetRotatedRelPos(ActiveBlueprint, BlueprintRotation, BlueprintFlipped, relPos);
        }

        internal static int2 GetRotatedRelPos(Blueprint bp, int rotation, bool flipped, int2 relPos)
        {
            if (bp == null)
                return relPos;

            int width = bp.width;
            int height = bp.height;
            int rx = relPos.x;
            int rz = relPos.y;

            int2 rotated = rotation switch
            {
                90 => new int2(height - 1 - rz, rx),
                180 => new int2(width - 1 - rx, height - 1 - rz),
                270 => new int2(rz, width - 1 - rx),
                _ => new int2(rx, rz),
            };

            if (flipped)
            {
                int rotatedWidth = rotation == 90 || rotation == 270 ? height : width;
                rotated = new int2(rotatedWidth - 1 - rotated.x, rotated.y);
            }

            return rotated;
        }

        public static int2 GetCursorOffset()
        {
            return GetCursorOffset(ActiveBlueprint, BlueprintRotation);
        }

        internal static int2 GetCursorOffset(Blueprint bp, int rotation)
        {
            var size = GetRotatedSize(bp, rotation);
            return new int2(-((size.x - 1) / 2), -((size.y - 1) / 2));
        }

        public static int2 GetRotatedSize()
        {
            return GetRotatedSize(ActiveBlueprint, BlueprintRotation);
        }

        internal static int2 GetRotatedSize(Blueprint bp, int rotation)
        {
            if (bp == null)
                return default;

            return rotation == 90 || rotation == 270
                ? new int2(bp.height, bp.width)
                : new int2(bp.width, bp.height);
        }

        public static void RequestCut(int2 min, int2 max)
        {
            long area = (long)(max.x - min.x + 1) * (max.y - min.y + 1);
            if (area > 101 * 101)
            {
                Debug.LogWarning($"[Tome of Mimicry] Cut too large ({area} cells, max {101 * 101})");
                return;
            }

            string payload = $"{min.x};{min.y};{max.x};{max.y}";
            if (ClientBlueprintPasteSystem.TrySendCommand(3, payload))
            {
                _undoable++;
                _redoable = 0;
                Debug.Log($"[Tome of Mimicry] Sent cut request {min}->{max}");
            }
        }

        public static void FillWalls(int2 min, int2 max)
        {
            if (!ShapeBlueprintGenerator.TryGetWallMaterial(out int tileset))
            {
                Debug.Log("[Tome of Mimicry] Wall tool needs a material — capture or load a blueprint containing walls first.");
                return;
            }

            int w = max.x - min.x + 1;
            int h = max.y - min.y + 1;
            if ((long)w * h > 101 * 101)
            {
                Debug.LogWarning($"[Tome of Mimicry] Wall fill too large ({(long)w * h} cells, max {101 * 101})");
                return;
            }

            var bp = new Blueprint($"WallFill_{w}x{h}", w, h);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    bp.tiles.Add(new BlueprintTile
                    {
                        relativePos = new int3(x, 0, y),
                        hasWall = true,
                        wallTileType = tileset,
                        floorTileKind = PugTilemap.TileType.none,
                    });

            var offset = GetCursorOffset(bp, 0);
            var origin = new int2(min.x - offset.x, min.y - offset.y);

            if (ClientBlueprintPasteSystem.QueuePaste(bp, origin, 0, false, PasteMode))
            {
                _undoable++;
                _redoable = 0;
                Debug.Log($"[Tome of Mimicry] Queued wall fill {min}->{max} (tileset {tileset}, mode {PasteMode})");
            }
        }

        private static void PasteBlueprint(int2 origin)
        {
            if (ActiveBlueprint == null)
                return;

            if (!ClientBlueprintPasteSystem.QueuePaste(
                    ActiveBlueprint, origin, BlueprintRotation, BlueprintFlipped, PasteMode))
            {
                PasteFailed = true;
                Debug.LogWarning("[Tome of Mimicry] Paste was not queued.");
                return;
            }
            else
            {
                _undoable++;
                _redoable = 0;
            }

            Debug.Log($"[Tome of Mimicry] Queued paste '{ActiveBlueprint.name}' at {origin}, paste mode: {PasteMode}");
        }

        public static void Cancel()
        {
            State = GadgetState.Idle;
            if (!PendingPaste)
                ClearActiveBlueprint();
            PendingCapture = false;
            Debug.Log("[Tome of Mimicry] Cancelled");
        }

        public static void Shutdown()
        {
            State = GadgetState.Idle;
            ActiveBlueprint = null;
            PendingCapture = false;
            PendingPaste = false;
            BlueprintRotation = 0;
            BlueprintFlipped = false;
            PasteFailed = false;
            PasteFailureMessage = null;
            _undoable = 0;
            _redoable = 0;
            TileCostTracker.Clear();
            UndoManager.Clear();
        }

        public static void ToggleFlip()
        {
            BlueprintFlipped = !BlueprintFlipped;
            Debug.Log($"[Tome of Mimicry] Blueprint flip: {BlueprintFlipped}");
        }

        public static void SetRotation(int degrees)
        {
            BlueprintRotation = ((degrees % 360) + 360) % 360;
            Debug.Log($"[Tome of Mimicry] Blueprint rotation: {BlueprintRotation}");
        }

        private static void CycleRotation()
        {
            BlueprintRotation = (BlueprintRotation + 90) % 360;
            Debug.Log($"[Tome of Mimicry] Blueprint rotation: {BlueprintRotation}");
        }

        private static void CycleCopyMode()
        {
            CopyMode = CopyMode switch
            {
                BlueprintCopyMode.Full => BlueprintCopyMode.FloorsOnly,
                BlueprintCopyMode.FloorsOnly => BlueprintCopyMode.WallsOnly,
                BlueprintCopyMode.WallsOnly => BlueprintCopyMode.ObjectsOnly,
                BlueprintCopyMode.ObjectsOnly => BlueprintCopyMode.Full,
                _ => BlueprintCopyMode.Full
            };
            Debug.Log($"[Tome of Mimicry] Copy mode: {CopyMode}");
        }

        private static void CyclePasteMode()
        {
            PasteMode = PasteMode switch
            {
                BlueprintPasteMode.Replace => BlueprintPasteMode.MergeEmpty,
                BlueprintPasteMode.MergeEmpty => BlueprintPasteMode.Replace,
                _ => BlueprintPasteMode.MergeEmpty
            };
            Debug.Log($"[Tome of Mimicry] Paste mode: {PasteMode}");
        }
    }
}
