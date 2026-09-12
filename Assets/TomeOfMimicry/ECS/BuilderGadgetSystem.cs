using System.Collections.Generic;
using Pug.Automation;
using PugMod;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace TomeOfMimicry
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class BuilderGadgetSystem : SystemBase
    {
        public struct PasteRequest
        {
            public Blueprint Blueprint;
            public Entity Player;
            public Entity SourceConnection;
            public int2 Origin;
            public int Rotation;
            public bool Flipped;
            public BlueprintPasteMode PasteMode;
        }

        private static readonly Queue<PasteRequest> s_pendingPastes = new();

        public static void EnqueuePaste(PasteRequest request)
        {
            if (request.Blueprint != null)
                s_pendingPastes.Enqueue(request);
        }

        public struct CutRequest
        {
            public Entity Player;
            public Entity SourceConnection;
            public int2 Min;
            public int2 Max;
        }

        private static readonly Queue<CutRequest> s_pendingCuts = new();

        public static void EnqueueCut(CutRequest request)
        {
            s_pendingCuts.Enqueue(request);
        }

        private sealed class PendingSpawn
        {
            public UndoManager.PasteRecord Record;
            public Entity Player;
            public int FramesRemaining;
        }

        private const int EntitySpawnDelayFrames = 5;
        private static readonly List<PendingSpawn> s_pendingSpawns = new();
        private BufferLookup<ContainedObjectsBuffer> _invLookup;

        private TileAccessor _tileAccessor;
        private bool _tileAccessorReady;
        private float _nextStackPrune;

        protected override void OnCreate()
        {
            RequireForUpdate<TileUpdateBuffer>();
            s_pendingSpawns.Clear();
            s_pendingPastes.Clear();
            s_pendingCuts.Clear();
            _invLookup = GetBufferLookup<ContainedObjectsBuffer>(false);
        }

        private void SendFeedback(Entity connection, int command)
        {
            if (connection == Entity.Null || !EntityManager.Exists(connection)) return;
            var e = EntityManager.CreateEntity();
            EntityManager.AddComponentData(e, new BlueprintPasteRPC
            {
                protocol = BlueprintPasteRPC.CurrentProtocol,
                command = command,
            });
            EntityManager.AddComponentData(e, new SendRpcCommandRequest { TargetConnection = connection });
        }

        protected override void OnUpdate()
        {

            var tileBuffer = SystemAPI.GetSingletonBuffer<TileUpdateBuffer>();

            var worldInfo = SystemAPI.GetSingleton<WorldInfoCD>();
            bool godMode  = worldInfo.IsWorldModeEnabled(WorldMode.Creative);

            bool hasTileMap = SystemAPI.TryGetSingleton<TileWithTilesetToObjectDataMapCD>(
                                  out var tileMap);
            bool hasDbBank  = SystemAPI.TryGetSingleton<PugDatabase.DatabaseBankCD>(
                                  out var dbBank);
            bool dbReady    = hasTileMap && hasDbBank;

            Entity playerEntity = Entity.Null;
            foreach (var (_, entity) in SystemAPI.Query<RefRO<ClientInput>>()
                         .WithAll<ContainedObjectsBuffer>().WithEntityAccess())
            {
                playerEntity = entity;
                break;
            }

            _invLookup.Update(this);

            if (!_tileAccessorReady)
            {
                try
                {
                    _tileAccessor = new TileAccessor(this);
                    _tileAccessorReady = true;
                    Debug.Log("[Tome of Mimicry] TileAccessor initialised");
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[Tome of Mimicry] TileAccessor not ready: {ex.Message}");
                    return;
                }
            }
            _tileAccessor.Update(this);

            if (World.Time.ElapsedTime > _nextStackPrune)
            {
                _nextStackPrune = (float)World.Time.ElapsedTime + 30f;
                UndoManager.PruneInvalid(e => EntityManager.Exists(e));
            }

            ProcessPendingSpawns(dbReady, dbBank, godMode);

            if (BlueprintManager.PendingTileTest)
            {
                BlueprintManager.PendingTileTest = false;
                var tp = BlueprintManager.TileTestPos;
                tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = tp, tile = new TileCD { tileType = TileType.wall } });
                tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = tp, tile = new TileCD { tileType = TileType.floor } });
                tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add,    position = tp, tile = new TileCD { tileType = TileType.floor, tileset = 14 } });
                Debug.Log($"[Tome of Mimicry] F7 ECS: queued floor/14 at {tp}");
            }

            if (UndoManager.TryDequeueRequest(out var undoRequest))
            {
                var reqPlayer = undoRequest.player != Entity.Null ? undoRequest.player : playerEntity;

                if (!undoRequest.redo)
                {
                    if (UndoManager.TryPopUndo(reqPlayer, out var undoRecord))
                    {
                        RestoreSnapshots(undoRecord.Before, tileBuffer, godMode, _tileAccessor);

                        Dictionary<ObjectID, int> objectRefunds = null;
                        try
                        {
                            if (!godMode && !undoRecord.IsCut && undoRecord.EntityPastes.Count > 0)
                            {
                                objectRefunds = new Dictionary<ObjectID, int>();
                                if (!undoRecord.EntitiesSpawned)
                                {
                                    foreach (var ep in undoRecord.EntityPastes)
                                        AddCost(objectRefunds, ep.objectId, 1);
                                }
                                else
                                {
                                    foreach (var e in undoRecord.CreatedEntities)
                                        if (EntityManager.Exists(e) && EntityManager.HasComponent<ObjectDataCD>(e))
                                            AddCost(objectRefunds,
                                                EntityManager.GetComponentData<ObjectDataCD>(e).objectID, 1);
                                }
                            }

                            undoRecord.Cancelled = true;
                            foreach (var e in undoRecord.CreatedEntities)
                                if (EntityManager.Exists(e))
                                    EntityManager.DestroyEntity(e);
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"[Tome of Mimicry] Undo entity teardown failed (continuing): {ex}");
                        }
                        undoRecord.CreatedEntities.Clear();

                        if (!godMode && reqPlayer != Entity.Null
                            && _invLookup.TryGetBuffer(reqPlayer, out var invUndo))
                        {
                            if (undoRecord.IsCut)
                            {
                                if (undoRecord.TileCost != null && undoRecord.TileCost.Count > 0)
                                {
                                    DeductItems(invUndo, undoRecord.TileCost);
                                    Debug.Log($"[Tome of Mimicry] Undo cut reclaimed {undoRecord.TileCost.Count} item type(s)");
                                }
                            }
                            else
                            {
                                if (undoRecord.TileCost != null && undoRecord.TileCost.Count > 0)
                                    GiveItems(invUndo, undoRecord.TileCost);
                                if (objectRefunds != null && objectRefunds.Count > 0)
                                    GiveItems(invUndo, objectRefunds);
                                Debug.Log($"[Tome of Mimicry] Undo refunded tiles + {(objectRefunds != null ? objectRefunds.Count : 0)} object type(s)");
                            }
                        }
                    }
                    else
                    {
                        Debug.Log($"[Tome of Mimicry] Nothing to undo for {reqPlayer}.");
                        SendFeedback(undoRequest.connection, 6);
                    }
                    return;
                }

                if (UndoManager.TryPopRedo(reqPlayer, out var redoRecord))
                {
                    Dictionary<ObjectID, int> redoCharge = null;
                    if (!redoRecord.IsCut)
                    {
                        redoCharge = redoRecord.TileCost != null
                            ? new Dictionary<ObjectID, int>(redoRecord.TileCost)
                            : new Dictionary<ObjectID, int>();
                        foreach (var ep in redoRecord.EntityPastes)
                            AddCost(redoCharge, ep.objectId, 1);
                    }

                    if (!godMode && redoCharge != null && redoCharge.Count > 0
                        && reqPlayer != Entity.Null)
                    {
                        var have = GetInventory(reqPlayer);
                        foreach (var kv in redoCharge)
                        {
                            int count = have != null && have.TryGetValue(kv.Key, out int a) ? a : 0;
                            if (count < kv.Value)
                            {
                                Debug.LogWarning(
                                    $"[Tome of Mimicry] Redo blocked: need {kv.Value}x {kv.Key}, have {count}");
                                BlueprintManager.PasteFailed = true;
                                UndoManager.PushRedoBack(reqPlayer, redoRecord);
                                SendFeedback(undoRequest.connection, 5);
                                return;
                            }
                        }
                    }

                    RestoreSnapshots(redoRecord.After, tileBuffer, godMode, _tileAccessor);

                    redoRecord.Cancelled = false;
                    redoRecord.EntitiesSpawned = false;
                    if (redoRecord.EntityPastes.Count > 0)
                        s_pendingSpawns.Add(new PendingSpawn
                        {
                            Record = redoRecord,
                            Player = reqPlayer,
                            FramesRemaining = EntitySpawnDelayFrames,
                        });

                    if (!godMode && reqPlayer != Entity.Null
                        && _invLookup.TryGetBuffer(reqPlayer, out var invRedo))
                    {
                        if (redoRecord.IsCut)
                        {
                            if (redoRecord.TileCost != null && redoRecord.TileCost.Count > 0)
                            {
                                GiveItems(invRedo, redoRecord.TileCost);
                                Debug.Log($"[Tome of Mimicry] Redo cut re-refunded {redoRecord.TileCost.Count} item type(s)");
                            }
                        }
                        else if (redoCharge != null && redoCharge.Count > 0)
                        {
                            DeductItems(invRedo, redoCharge);
                            Debug.Log($"[Tome of Mimicry] Redo re-deducted {redoCharge.Count} item type(s)");
                        }
                    }
                }
                else
                {
                    Debug.Log($"[Tome of Mimicry] Nothing to redo for {reqPlayer}.");
                    SendFeedback(undoRequest.connection, 7);
                }
                return;
            }

            if (s_pendingCuts.Count > 0)
            {
                ProcessCut(s_pendingCuts.Dequeue(), tileBuffer, godMode, dbReady, tileMap, playerEntity);
                return;
            }

            var activeBp = BlueprintManager.ActiveBlueprint;

            bool hasQueuedPaste = s_pendingPastes.Count > 0;
            PasteRequest queuedPaste = default;
            if (hasQueuedPaste)
                queuedPaste = s_pendingPastes.Dequeue();

            if ((!BlueprintManager.PendingPaste || activeBp == null) && !hasQueuedPaste)
                return;

            if (BlueprintManager.PendingPaste)
                BlueprintManager.PendingPaste = false;

            var bp     = hasQueuedPaste ? queuedPaste.Blueprint : activeBp;
            var origin = hasQueuedPaste ? queuedPaste.Origin : BlueprintManager.PasteOrigin;
            int pasteRot = hasQueuedPaste ? queuedPaste.Rotation : BlueprintManager.BlueprintRotation;
            bool pasteFlip = hasQueuedPaste ? queuedPaste.Flipped : BlueprintManager.BlueprintFlipped;
            var offset = hasQueuedPaste
                ? BlueprintManager.GetCursorOffset(bp, pasteRot)
                : BlueprintManager.GetCursorOffset();
            var mode   = hasQueuedPaste ? queuedPaste.PasteMode : BlueprintManager.PasteMode;
            var pastePlayer = hasQueuedPaste ? queuedPaste.Player : playerEntity;

            if (!godMode && dbReady)
            {
                var needed = ComputeCost(bp, tileMap, dbBank, _tileAccessor, origin, offset,
                                         mode, pasteRot, pasteFlip);
                var have   = GetInventory(pastePlayer);

                foreach (var kv in needed)
                {
                    int count = have != null && have.TryGetValue(kv.Key, out int a) ? a : 0;
                    if (count < kv.Value)
                    {
                        Debug.LogWarning($"[Tome of Mimicry] Paste blocked: need {kv.Value}x {kv.Key}, have {count}");
                        BlueprintManager.PasteFailed = true;
                        if (hasQueuedPaste)
                            SendFeedback(queuedPaste.SourceConnection, 4);
                        return;
                    }
                }
            }

            var record = new UndoManager.PasteRecord();
            int placed = 0, placedCells = 0, skipped = 0, unloaded = 0;

            var positions   = new int2[bp.tiles.Count];
            var include     = new bool[bp.tiles.Count];
            var beforeSnaps = new UndoManager.TileSnapshot[bp.tiles.Count];

            for (int i = 0; i < bp.tiles.Count; i++)
            {
                var tile    = bp.tiles[i];
                var relPos  = new int2(tile.relativePos.x, tile.relativePos.z);
                var rotated = BlueprintManager.GetRotatedRelPos(bp, pasteRot, pasteFlip, relPos);
                var pos     = new int2(origin.x + offset.x + rotated.x,
                                       origin.y + offset.y + rotated.y);
                positions[i] = pos;

                if (!_tileAccessor.IsInitialized(pos))
                {
                    unloaded++;
                    include[i] = false;
                    continue;
                }

                if (mode == BlueprintPasteMode.MergeEmpty &&
                    (_tileAccessor.HasType(pos, TileType.wall) ||
                     _tileAccessor.HasType(pos, TileType.floor) ||
                     _tileAccessor.HasType(pos, TileType.bridge) ||
                     _tileAccessor.HasType(pos, TileType.rail) ||
                     _tileAccessor.HasType(pos, TileType.circuitPlate)))
                {
                    skipped++;
                    include[i] = false;
                }
                else
                {
                    include[i] = true;
                    placedCells++;
                }
            }

            for (int i = 0; i < bp.tiles.Count; i++)
            {
                if (!include[i]) continue;
                var tile = bp.tiles[i];
                var pos  = positions[i];
                bool placesBridge = tile.hasFloor && tile.floorTileKind == TileType.bridge;

                var snap = SnapshotPos(pos, _tileAccessor);
                record.Before.Add(snap);
                beforeSnaps[i] = snap;

                if (mode == BlueprintPasteMode.Replace)
                {
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.wall } });
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.floor } });
                    if (!placesBridge && tile.hasGround)
                        tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.ground } });
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.rail } });
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.bridge } });
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.circuitPlate } });
                    foreach (var extraType in BlueprintLayers.Extras)
                        tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = extraType } });
                }

                if (tile.hasGround && !placesBridge)
                {
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add, position = pos, tile = new TileCD { tileType = TileType.ground, tileset = tile.groundTileType } });
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.pit } });
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.water } });
                    placed++;
                }

                if (placesBridge)
                {
                    bool overWater = beforeSnaps[i].hadWater;
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.floor } });
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.ground } });
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.water } });
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.pit } });
                    if (overWater)
                        tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add, position = pos, tile = new TileCD { tileType = TileType.water, tileset = 0 } });
                    else
                        tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add, position = pos, tile = new TileCD { tileType = TileType.pit, tileset = 0 } });
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add, position = pos, tile = new TileCD { tileType = TileType.bridge, tileset = tile.floorTileType } });
                    placed++;
                }
            }

            for (int i = 0; i < bp.tiles.Count; i++)
            {
                if (!include[i]) continue;
                var tile = bp.tiles[i];
                var pos  = positions[i];

                if (tile.hasWall)
                {
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add, position = pos, tile = new TileCD { tileType = TileType.wall, tileset = tile.wallTileType } });
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.roofHole } });
                    placed++;
                }

                if (tile.hasFloor && tile.floorTileKind != TileType.bridge)
                {
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add, position = pos, tile = new TileCD { tileType = tile.floorTileKind, tileset = tile.floorTileType } });
                    placed++;
                }

                if (tile.hasRail)
                {
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add, position = pos, tile = new TileCD { tileType = TileType.rail, tileset = tile.railTileset } });
                    placed++;
                }

                if (tile.hasWire)
                {
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add, position = pos, tile = new TileCD { tileType = TileType.circuitPlate, tileset = tile.wireTileset } });
                    placed++;
                }

                if (tile.extras != null)
                {
                    foreach (var ex in tile.extras)
                    {
                        tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = (TileType)ex.x } });
                        tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add, position = pos, tile = new TileCD { tileType = (TileType)ex.x, tileset = ex.y } });
                        placed++;
                    }
                }

                var before = beforeSnaps[i];
                var after  = new UndoManager.TileSnapshot { pos = pos };
                after.hadWall       = tile.hasWall;
                after.wallTileset   = tile.hasWall   ? tile.wallTileType  : 0;
                after.hadFloor      = tile.hasFloor;
                after.floorKind     = tile.hasFloor  ? tile.floorTileKind : TileType.floor;
                after.floorTileset  = tile.hasFloor  ? tile.floorTileType : 0;
                bool pastedBridge   = tile.hasFloor && tile.floorTileKind == TileType.bridge;
                bool afterHasGround = !pastedBridge && (tile.hasGround || before.hadGround);
                after.hadGround     = afterHasGround;
                after.groundTileset = !afterHasGround ? 0
                    : tile.hasGround ? tile.groundTileType : before.groundTileset;
                after.hadBridge     = pastedBridge;
                after.bridgeTileset = pastedBridge ? tile.floorTileType : 0;
                if (pastedBridge)
                {
                    after.hadWater = before.hadWater;
                    after.hadPit   = !before.hadWater;
                }
                else
                {
                    after.hadWater = before.hadWater && !tile.hasGround;
                    after.hadPit   = before.hadPit   && !tile.hasGround;
                }
                after.hadRail      = tile.hasRail;
                after.railTileset  = tile.hasRail ? tile.railTileset : 0;
                after.hadWire      = tile.hasWire;
                after.wireTileset  = tile.hasWire ? tile.wireTileset : 0;
                List<int2> afterExtras = null;
                if (mode != BlueprintPasteMode.Replace && before.extras != null)
                    afterExtras = new List<int2>(before.extras);
                if (tile.extras != null)
                {
                    afterExtras ??= new List<int2>();
                    foreach (var ex in tile.extras)
                    {
                        bool replaced = false;
                        for (int k = 0; k < afterExtras.Count; k++)
                            if (afterExtras[k].x == ex.x) { afterExtras[k] = ex; replaced = true; break; }
                        if (!replaced) afterExtras.Add(ex);
                    }
                }
                after.extras = afterExtras;
                record.After.Add(after);
            }

            var centreOrigin = new int2(origin.x + offset.x, origin.y + offset.y);

            int objPlaced = 0;
            if (bp.objects.Count > 0 && dbReady)
            {
                int  rot  = pasteRot;
                bool flip = pasteFlip;

                foreach (var obj in bp.objects)
                {
                    var relPos = new int2(obj.relativePos.x, obj.relativePos.y);

                    int W = obj.tileSize.x, H = obj.tileSize.y;
                    bool baseX = (rot == 180 || rot == 270);
                    bool baseZ = (rot == 90  || rot == 180);
                    bool addX  = flip ? !baseZ : baseX;
                    bool addZ  = flip ? baseX  : baseZ;
                    var  anchorRelPos = new int2(relPos.x + (addX ? W - 1 : 0),
                                                relPos.y + (addZ ? H - 1 : 0));

                    var rotated = BlueprintManager.GetRotatedRelPos(bp, rot, flip, anchorRelPos);
                    var wpos    = new int2(origin.x + offset.x + rotated.x,
                                          origin.y + offset.y + rotated.y);

                    int  pasteVariation = obj.variation;
                    int2 newDbvDir      = obj.dbvDirection;
                    if (obj.dirByVariation && (rot != 0 || flip) && math.any(obj.dbvDirection != int2.zero))
                    {
                        var rotatedDir = RotateDirection2(obj.dbvDirection, rot, flip);
                        var dirPlain   = DirectionBasedOnVariationCD.GetDirectionFromVariation(obj.variation);
                        var dirZeroNo  = DirectionBasedOnVariationCD.GetDirectionFromVariation(obj.variation, true);
                        if (math.all(dirPlain == obj.dbvDirection))
                        {
                            pasteVariation = DirectionBasedOnVariationCD.GetVariationFromDirection(rotatedDir, false);
                            newDbvDir = rotatedDir;
                        }
                        else if (math.all(dirZeroNo == obj.dbvDirection))
                        {
                            pasteVariation = DirectionBasedOnVariationCD.GetVariationFromDirection(rotatedDir, true);
                            newDbvDir = rotatedDir;
                        }
                    }

                    float3 rotDir = float3.zero;
                    if (math.any(obj.direction != float3.zero))
                        rotDir = RotateDirection(obj.direction, rot, flip);

                    record.EntityPastes.Add(new UndoManager.EntityPasteInfo
                    {
                        wpos           = wpos,
                        objectId       = obj.objectId,
                        variation      = pasteVariation,
                        direction      = rotDir,
                        dirByVariation = obj.dirByVariation,
                        dbvDirection   = newDbvDir,
                        hasPaint       = obj.hasPaint,
                        paintColor     = obj.paintColor,
                        hasObjectFilter       = obj.hasObjectFilter,
                        objectFilterType      = obj.objectFilterType,
                        objectFilterObject    = obj.objectFilterObject,
                        objectFilterVariation = obj.objectFilterVariation,
                        hasMoverFilter        = obj.hasMoverFilter,
                        moverFilterType       = obj.moverFilterType,
                        moverFilterObject     = obj.moverFilterObject,
                        moverFilterVariation  = obj.moverFilterVariation,
                        moverFilterCategory   = obj.moverFilterCategory,
                    });

                    objPlaced++;
                }

                s_pendingSpawns.Add(new PendingSpawn
                {
                    Record = record,
                    Player = pastePlayer,
                    FramesRemaining = EntitySpawnDelayFrames,
                });
            }

            Debug.Log($"[Tome of Mimicry] Placed {placedCells} cells ({placed} layer writes), {objPlaced} objects, skipped {skipped} " +
                      $"(mode: {mode}) cursor={origin} centreOrigin={centreOrigin} " +
                      $"rot={pasteRot}°");
            if (unloaded > 0)
                Debug.LogWarning($"[Tome of Mimicry] {unloaded} cell(s) skipped — no server submap loaded there");

            if (placed == 0 && objPlaced == 0)
            {
                BlueprintManager.PasteFailed = true;
                if (hasQueuedPaste)
                    SendFeedback(queuedPaste.SourceConnection, 4);
            }

            if (placed > 0 || objPlaced > 0)
            {
                if (!godMode && dbReady && pastePlayer != Entity.Null
                             && _invLookup.TryGetBuffer(pastePlayer, out var inv))
                {
                    var cost = ComputeActualCost(bp, tileMap, dbBank, include);
                    record.TileCost = cost;
                    var charge = new Dictionary<ObjectID, int>(cost);
                    foreach (var ep in record.EntityPastes)
                        AddCost(charge, ep.objectId, 1);
                    DeductItems(inv, charge);
                    Debug.Log($"[Tome of Mimicry] Deducted resources for {placedCells} cell(s) + {record.EntityPastes.Count} object(s)");
                }

                UndoManager.PushPaste(pastePlayer != Entity.Null ? pastePlayer : playerEntity, record);
            }

            if (BlueprintManager.State == GadgetState.Idle)
                BlueprintManager.ClearActiveBlueprint();
        }


        private void ProcessPendingSpawns(bool dbReady, PugDatabase.DatabaseBankCD dbBank, bool godMode)
        {
            if (s_pendingSpawns.Count == 0 || !dbReady) return;

            for (int i = s_pendingSpawns.Count - 1; i >= 0; i--)
            {
                var pending = s_pendingSpawns[i];
                if (pending.Record.Cancelled)
                {
                    s_pendingSpawns.RemoveAt(i);
                    continue;
                }
                if (--pending.FramesRemaining > 0) continue;

                s_pendingSpawns.RemoveAt(i);
                SpawnRecordEntities(pending.Record, dbBank, pending.Player, godMode);
            }
        }

        private void SpawnRecordEntities(
            UndoManager.PasteRecord record,
            PugDatabase.DatabaseBankCD dbBank,
            Entity player,
            bool godMode)
        {
            if (record.EntityPastes.Count == 0) return;

            var preExisting = CollectMatchingEntities(record.EntityPastes);

            Dictionary<ObjectID, int> skippedRefunds = null;
            if (preExisting.Count > 0)
            {
                for (int i = record.EntityPastes.Count - 1; i >= 0; i--)
                {
                    var ep = record.EntityPastes[i];
                    if (!HasMatchingEntity(preExisting, ep)) continue;

                    record.EntityPastes.RemoveAt(i);
                    if (!godMode)
                    {
                        skippedRefunds ??= new Dictionary<ObjectID, int>();
                        AddCost(skippedRefunds, ep.objectId, 1);
                    }
                }
            }

            if (skippedRefunds != null && player != Entity.Null
                && _invLookup.TryGetBuffer(player, out var inv))
            {
                GiveItems(inv, skippedRefunds);
                Debug.Log($"[Tome of Mimicry] Skipped duplicate object(s), refunded {skippedRefunds.Count} type(s)");
            }

            if (record.EntityPastes.Count > 0)
            {
                var ecb = new EntityCommandBuffer(Allocator.Temp);
                foreach (var ep in record.EntityPastes)
                {
                    var ent = EntityUtility.CreateEntity(
                        ecb, ep.objectId, 1, dbBank.databaseBankBlob, ep.variation);
                    ecb.SetComponent(ent, LocalTransform.FromPosition(
                        new float3(ep.wpos.x, 0f, ep.wpos.y)));
                    if (math.any(ep.direction != float3.zero))
                        ecb.SetComponent(ent, new DirectionCD { direction = ep.direction });
                    if (ep.hasPaint)
                        ecb.SetComponent(ent, new PaintableObjectCD { color = (PaintableColor)ep.paintColor });
                    if (ep.hasObjectFilter)
                    {
                        ecb.SetComponent(ent, new ObjectFilteringCD
                        {
                            filterType = (FilterType)ep.objectFilterType,
                            filterObject = ep.objectFilterObject,
                            filterVariation = ep.objectFilterVariation,
                        });
                    }
                    if (ep.hasMoverFilter)
                    {
                        ecb.SetComponent(ent, new MoverFilterCD
                        {
                            filterType = (FilterType)ep.moverFilterType,
                            filterObject = ep.moverFilterObject,
                            filterVariation = ep.moverFilterVariation,
                            filterCategory = (ObjectCategoryTag)ep.moverFilterCategory,
                        });
                    }
                }
                ecb.Playback(EntityManager);
                ecb.Dispose();
            }

            record.CreatedEntities.Clear();
            FindCreatedEntities(record.EntityPastes, record.CreatedEntities, preExisting);
            record.EntitiesSpawned = true;
            Debug.Log($"[Tome of Mimicry] Spawned {record.EntityPastes.Count} deferred object(s)");
        }

        private bool HasMatchingEntity(HashSet<Entity> candidates, UndoManager.EntityPasteInfo ep)
        {
            foreach (var e in candidates)
            {
                if (!EntityManager.Exists(e)) continue;
                if (EntityManager.GetComponentData<ObjectDataCD>(e).objectID != ep.objectId) continue;
                var lt = EntityManager.GetComponentData<LocalTransform>(e);
                if (Mathf.RoundToInt(lt.Position.x) == ep.wpos.x &&
                    Mathf.RoundToInt(lt.Position.z) == ep.wpos.y)
                    return true;
            }
            return false;
        }


        private void ProcessCut(
            CutRequest cut,
            DynamicBuffer<TileUpdateBuffer> tileBuffer,
            bool godMode,
            bool dbReady,
            TileWithTilesetToObjectDataMapCD tileMap,
            Entity playerEntity)
        {
            var record = new UndoManager.PasteRecord { IsCut = true };
            var refunds = new Dictionary<ObjectID, int>();
            int unloadedCells = 0;

            for (int x = cut.Min.x; x <= cut.Max.x; x++)
            {
                for (int y = cut.Min.y; y <= cut.Max.y; y++)
                {
                    var pos = new int2(x, y);

                    if (!_tileAccessor.IsInitialized(pos))
                    {
                        unloadedCells++;
                        continue;
                    }

                    var snap = SnapshotPos(pos, _tileAccessor);

                    bool needsGround = !snap.hadGround && !snap.hadWater && !snap.hadPit;
                    bool hasLayers = snap.hadWall || snap.hadFloor || snap.hadBridge ||
                                     snap.hadRail || snap.hadWire || snap.extras != null;
                    if (!hasLayers && !needsGround)
                        continue;

                    record.Before.Add(snap);

                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.wall } });
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.floor } });
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.rail } });
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.bridge } });
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.circuitPlate } });
                    foreach (var extraType in BlueprintLayers.Extras)
                        tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = extraType } });

                    if (needsGround)
                        tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add, position = pos, tile = new TileCD { tileType = TileType.ground, tileset = 0 } });

                    var after = new UndoManager.TileSnapshot { pos = pos };
                    after.hadGround = snap.hadGround || needsGround;
                    after.groundTileset = snap.hadGround ? snap.groundTileset : 0;
                    after.hadWater = snap.hadWater;
                    after.hadPit = snap.hadPit;
                    record.After.Add(after);

                    if (dbReady && !godMode)
                        AddSnapshotRefund(snap, tileMap, refunds);
                }
            }

            if (unloadedCells > 0)
                Debug.LogWarning($"[Tome of Mimicry] Cut: {unloadedCells} cell(s) skipped — no server submap loaded there");

            if (record.Before.Count == 0)
            {
                Debug.Log($"[Tome of Mimicry] Cut {cut.Min}->{cut.Max}: nothing to clear");
                SendFeedback(cut.SourceConnection, 4);
                return;
            }

            var cutPlayer = cut.Player != Entity.Null ? cut.Player : playerEntity;

            if (!godMode && refunds.Count > 0 && cutPlayer != Entity.Null
                && _invLookup.TryGetBuffer(cutPlayer, out var inv))
            {
                GiveItems(inv, refunds);
                record.TileCost = refunds;
            }

            UndoManager.PushPaste(cutPlayer, record);
            Debug.Log($"[Tome of Mimicry] Cut {cut.Min}->{cut.Max}: cleared {record.Before.Count} cell(s), refunded {refunds.Count} item type(s)");
        }

        private static void AddSnapshotRefund(
            UndoManager.TileSnapshot s,
            TileWithTilesetToObjectDataMapCD tileMap,
            Dictionary<ObjectID, int> refunds)
        {
            if (s.hadWall)
            {
                var item = PugDatabase.TryGetTileItemInfo(TileType.wall, (Tileset)s.wallTileset, tileMap);
                if (item.objectID != ObjectID.None) AddCost(refunds, item.objectID, 1);
            }
            if (s.hadFloor && s.floorKind != TileType.bridge)
            {
                var item = PugDatabase.TryGetTileItemInfo(s.floorKind, (Tileset)s.floorTileset, tileMap);
                if (item.objectID != ObjectID.None) AddCost(refunds, item.objectID, 1);
            }
            if (s.hadBridge)
            {
                var item = PugDatabase.TryGetTileItemInfo(TileType.bridge, (Tileset)s.bridgeTileset, tileMap);
                if (item.objectID != ObjectID.None) AddCost(refunds, item.objectID, 1);
            }
            if (s.hadRail)
            {
                var item = PugDatabase.TryGetTileItemInfo(TileType.rail, (Tileset)s.railTileset, tileMap);
                if (item.objectID != ObjectID.None) AddCost(refunds, item.objectID, 1);
            }
            if (s.hadWire)
            {
                var item = PugDatabase.TryGetTileItemInfo(TileType.circuitPlate, (Tileset)s.wireTileset, tileMap);
                if (item.objectID != ObjectID.None) AddCost(refunds, item.objectID, 1);
            }
            if (s.extras != null)
            {
                foreach (var ex in s.extras)
                {
                    var item = PugDatabase.TryGetTileItemInfo((TileType)ex.x, (Tileset)ex.y, tileMap);
                    if (item.objectID != ObjectID.None) AddCost(refunds, item.objectID, 1);
                }
            }
        }


        private static Dictionary<ObjectID, int> ComputeCost(
            Blueprint bp,
            TileWithTilesetToObjectDataMapCD tileMap,
            PugDatabase.DatabaseBankCD dbBank,
            TileAccessor tiles,
            int2 origin, int2 offset,
            BlueprintPasteMode mode,
            int rotation,
            bool flipped)
        {
            var cost = new Dictionary<ObjectID, int>();

            for (int i = 0; i < bp.tiles.Count; i++)
            {
                var tile    = bp.tiles[i];
                var relPos  = new int2(tile.relativePos.x, tile.relativePos.z);
                var rotated = BlueprintManager.GetRotatedRelPos(bp, rotation, flipped, relPos);
                var pos     = new int2(origin.x + offset.x + rotated.x,
                                       origin.y + offset.y + rotated.y);

                if (mode == BlueprintPasteMode.MergeEmpty &&
                    (tiles.HasType(pos, TileType.wall) ||
                     tiles.HasType(pos, TileType.floor) ||
                     tiles.HasType(pos, TileType.bridge) ||
                     tiles.HasType(pos, TileType.rail) ||
                     tiles.HasType(pos, TileType.circuitPlate)))
                    continue;

                AddTileCost(tile, tileMap, cost);
            }

            foreach (var obj in bp.objects)
                AddCost(cost, obj.objectId, 1);

            return cost;
        }

        private static Dictionary<ObjectID, int> ComputeActualCost(
            Blueprint bp,
            TileWithTilesetToObjectDataMapCD tileMap,
            PugDatabase.DatabaseBankCD dbBank,
            bool[] include)
        {
            var cost = new Dictionary<ObjectID, int>();
            for (int i = 0; i < bp.tiles.Count; i++)
            {
                if (!include[i]) continue;
                AddTileCost(bp.tiles[i], tileMap, cost);
            }
            return cost;
        }

        private static void AddTileCost(
            BlueprintTile tile,
            TileWithTilesetToObjectDataMapCD tileMap,
            Dictionary<ObjectID, int> cost)
        {
            if (tile.hasWall)
            {
                var item = PugDatabase.TryGetTileItemInfo(TileType.wall,
                               (Tileset)tile.wallTileType, tileMap);
                if (item.objectID != ObjectID.None)
                    AddCost(cost, item.objectID, 1);
            }

            if (tile.hasFloor)
            {
                var item = PugDatabase.TryGetTileItemInfo(tile.floorTileKind,
                               (Tileset)tile.floorTileType, tileMap);
                if (item.objectID != ObjectID.None)
                    AddCost(cost, item.objectID, 1);
            }

            if (tile.hasWire)
            {
                var item = PugDatabase.TryGetTileItemInfo(TileType.circuitPlate,
                               (Tileset)tile.wireTileset, tileMap);
                if (item.objectID != ObjectID.None)
                    AddCost(cost, item.objectID, 1);
            }

            if (tile.hasRail)
            {
                var item = PugDatabase.TryGetTileItemInfo(TileType.rail,
                               (Tileset)tile.railTileset, tileMap);
                if (item.objectID != ObjectID.None)
                    AddCost(cost, item.objectID, 1);
            }

            if (tile.extras != null)
            {
                foreach (var ex in tile.extras)
                {
                    var item = PugDatabase.TryGetTileItemInfo((TileType)ex.x,
                                   (Tileset)ex.y, tileMap);
                    if (item.objectID != ObjectID.None)
                        AddCost(cost, item.objectID, 1);
                }
            }
        }

        private static void AddCost(Dictionary<ObjectID, int> d, ObjectID id, int amount)
        {
            d.TryGetValue(id, out int existing);
            d[id] = existing + amount;
        }


        private Dictionary<ObjectID, int> GetInventory(Entity player)
        {
            if (player == Entity.Null) return null;
            if (!_invLookup.TryGetBuffer(player, out var inv)) return null;

            var result = new Dictionary<ObjectID, int>();
            for (int i = 0; i < inv.Length; i++)
            {
                var slot = inv[i];
                if (slot.objectData.objectID == ObjectID.None) continue;
                if (slot.objectData.amount   <= 0)             continue;

                result.TryGetValue(slot.objectData.objectID, out int existing);
                result[slot.objectData.objectID] = existing + slot.objectData.amount;
            }
            return result;
        }

        private static void DeductItems(
            DynamicBuffer<ContainedObjectsBuffer> inv,
            Dictionary<ObjectID, int> cost)
        {
            foreach (var kv in cost)
            {
                int remaining = kv.Value;
                for (int i = 0; i < inv.Length && remaining > 0; i++)
                {
                    ref var slot = ref inv.ElementAt(i);
                    if (slot.objectData.objectID != kv.Key) continue;
                    if (slot.objectData.amount   <= 0)      continue;

                    int consume           = math.min(remaining, slot.objectData.amount);
                    slot.objectData.amount -= consume;
                    remaining             -= consume;
                }

                if (remaining > 0)
                    Debug.LogWarning($"[Tome of Mimicry] DeductItems: ran out of {kv.Key} " +
                                     $"(still needed {remaining})");
            }
        }

        private static void GiveItems(
            DynamicBuffer<ContainedObjectsBuffer> inv,
            Dictionary<ObjectID, int> items)
        {
            if (items == null) return;
            foreach (var kv in items)
            {
                int remaining = kv.Value;

                for (int i = 0; i < inv.Length && remaining > 0; i++)
                {
                    ref var slot = ref inv.ElementAt(i);
                    if (slot.objectData.objectID != kv.Key) continue;
                    int add = math.min(remaining, 999 - slot.objectData.amount);
                    if (add <= 0) continue;
                    slot.objectData.amount += add;
                    remaining -= add;
                }

                for (int i = 0; i < inv.Length && remaining > 0; i++)
                {
                    ref var slot = ref inv.ElementAt(i);
                    if (slot.objectData.objectID != ObjectID.None) continue;
                    int add = math.min(remaining, 999);
                    slot.objectData.objectID  = kv.Key;
                    slot.objectData.amount    = add;
                    slot.objectData.variation = 0;
                    remaining -= add;
                }

                if (remaining > 0)
                    Debug.LogWarning($"[Tome of Mimicry] GiveItems: inventory full, lost {remaining}x {kv.Key}");
            }
        }


        private static UndoManager.TileSnapshot SnapshotPos(int2 pos, TileAccessor tiles)
        {
            var s = new UndoManager.TileSnapshot { pos = pos };

            var present = tiles.Get(pos, Allocator.Temp);
            for (int i = 0; i < present.Length; i++)
            {
                var tile = present[i];
                switch (tile.tileType)
                {
                    case TileType.wall:
                        s.hadWall = true; s.wallTileset = tile.tileset;
                        break;
                    case TileType.floor:
                        s.hadFloor = true; s.floorKind = TileType.floor; s.floorTileset = tile.tileset;
                        break;
                    case TileType.bridge:
                        s.hadBridge = true; s.bridgeTileset = tile.tileset;
                        if (!s.hadFloor)
                        { s.hadFloor = true; s.floorKind = TileType.bridge; s.floorTileset = tile.tileset; }
                        break;
                    case TileType.ground:
                        s.hadGround = true; s.groundTileset = tile.tileset;
                        break;
                    case TileType.rail:
                        s.hadRail = true; s.railTileset = tile.tileset;
                        break;
                    case TileType.circuitPlate:
                        s.hadWire = true; s.wireTileset = tile.tileset;
                        break;
                    case TileType.water:
                        s.hadWater = true;
                        break;
                    case TileType.pit:
                        s.hadPit = true;
                        break;
                    default:
                        if (BlueprintLayers.IsExtra(tile.tileType))
                        {
                            s.extras ??= new List<int2>();
                            s.extras.Add(new int2((int)tile.tileType, tile.tileset));
                        }
                        break;
                }
            }
            present.Dispose();

            return s;
        }


        private static void RestoreSnapshots(
            List<UndoManager.TileSnapshot> snapshots,
            DynamicBuffer<TileUpdateBuffer> tileBuffer,
            bool godMode,
            TileAccessor tiles)
        {
            int unloaded = 0;
            foreach (var s in snapshots)
            {
                var pos = s.pos;

                if (!tiles.IsInitialized(pos))
                {
                    unloaded++;
                    continue;
                }

                tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.wall } });
                tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.floor } });
                tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.bridge } });
                tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.rail } });
                tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.circuitPlate } });
                foreach (var extraType in BlueprintLayers.Extras)
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = extraType } });

                if (s.hadWater || s.hadPit || s.hadGround)
                {
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.ground } });
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.water } });
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Remove, position = pos, tile = new TileCD { tileType = TileType.pit } });

                    if (s.hadWater)
                        tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add, position = pos, tile = new TileCD { tileType = TileType.water, tileset = 0 } });
                    else if (s.hadPit)
                        tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add, position = pos, tile = new TileCD { tileType = TileType.pit, tileset = 0 } });
                    else
                        tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add, position = pos, tile = new TileCD { tileType = TileType.ground, tileset = s.groundTileset } });
                }

                if (s.hadBridge)
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add, position = pos, tile = new TileCD { tileType = TileType.bridge, tileset = s.bridgeTileset } });
                else if (s.hadFloor && s.floorKind == TileType.bridge)
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add, position = pos, tile = new TileCD { tileType = TileType.bridge, tileset = s.floorTileset } });
            }

            foreach (var s in snapshots)
            {
                var pos = s.pos;
                if (!tiles.IsInitialized(pos)) continue;
                if (s.hadWall)
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add, position = pos, tile = new TileCD { tileType = TileType.wall, tileset = s.wallTileset } });
                if (s.hadFloor && s.floorKind != TileType.bridge)
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add, position = pos, tile = new TileCD { tileType = s.floorKind, tileset = s.floorTileset } });
                if (s.hadRail)
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add, position = pos, tile = new TileCD { tileType = TileType.rail, tileset = s.railTileset } });
                if (s.hadWire)
                    tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add, position = pos, tile = new TileCD { tileType = TileType.circuitPlate, tileset = s.wireTileset } });
                if (s.extras != null)
                    foreach (var ex in s.extras)
                        tileBuffer.Add(new TileUpdateBuffer { command = TileUpdateBuffer.Command.Add, position = pos, tile = new TileCD { tileType = (TileType)ex.x, tileset = ex.y } });
            }

            if (unloaded > 0)
                Debug.LogWarning($"[Tome of Mimicry] Undo/redo: {unloaded} cell(s) skipped — no server submap loaded there");
        }

        private HashSet<Entity> CollectMatchingEntities(
            System.Collections.Generic.List<UndoManager.EntityPasteInfo> pastes)
        {
            var set = new HashSet<Entity>();
            if (pastes.Count == 0) return set;

            var q   = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ObjectDataCD>(),
                ComponentType.ReadOnly<LocalTransform>());
            var all = q.ToEntityArray(Allocator.Temp);
            q.Dispose();

            foreach (var ep in pastes)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    var e  = all[i];
                    var od = EntityManager.GetComponentData<ObjectDataCD>(e);
                    if (od.objectID != ep.objectId) continue;
                    var lt = EntityManager.GetComponentData<LocalTransform>(e);
                    if (Mathf.RoundToInt(lt.Position.x) == ep.wpos.x &&
                        Mathf.RoundToInt(lt.Position.z) == ep.wpos.y)
                        set.Add(e);
                }
            }

            all.Dispose();
            return set;
        }

        private void FindCreatedEntities(
            System.Collections.Generic.List<UndoManager.EntityPasteInfo> pastes,
            System.Collections.Generic.List<Unity.Entities.Entity> outHandles,
            HashSet<Entity> exclude = null)
        {
            if (pastes.Count == 0) return;

            var q    = EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<ObjectDataCD>(),
                ComponentType.ReadOnly<LocalTransform>());
            var all  = q.ToEntityArray(Allocator.Temp);
            q.Dispose();

            foreach (var ep in pastes)
            {
                for (int i = 0; i < all.Length; i++)
                {
                    var e  = all[i];
                    if (exclude != null && exclude.Contains(e)) continue;
                    var od = EntityManager.GetComponentData<ObjectDataCD>(e);
                    if (od.objectID != ep.objectId) continue;
                    var lt = EntityManager.GetComponentData<LocalTransform>(e);
                    int ex = Mathf.RoundToInt(lt.Position.x);
                    int ez = Mathf.RoundToInt(lt.Position.z);
                    if (ex == ep.wpos.x && ez == ep.wpos.y)
                    {
                        outHandles.Add(e);

                        if (ep.dirByVariation && math.any(ep.dbvDirection != int2.zero) &&
                            EntityManager.HasComponent<DirectionBasedOnVariationCD>(e))
                        {
                            var dbv = EntityManager.GetComponentData<DirectionBasedOnVariationCD>(e);
                            dbv.direction = ep.dbvDirection;
                            EntityManager.SetComponentData(e, dbv);
                        }
                        break;
                    }
                }
            }

            all.Dispose();
        }

        private static float3 RotateDirection(float3 dir, int degrees, bool flipped)
        {
            float3 r = degrees switch
            {
                90  => new float3(-dir.z, dir.y,  dir.x),
                180 => new float3(-dir.x, dir.y, -dir.z),
                270 => new float3( dir.z, dir.y, -dir.x),
                _   => dir,
            };
            if (flipped) r.x = -r.x;
            return r;
        }

        private static int2 RotateDirection2(int2 dir, int degrees, bool flipped)
        {
            int2 r = degrees switch
            {
                90  => new int2(-dir.y,  dir.x),
                180 => new int2(-dir.x, -dir.y),
                270 => new int2( dir.y, -dir.x),
                _   => dir,
            };
            if (flipped) r.x = -r.x;
            return r;
        }
    }
}

