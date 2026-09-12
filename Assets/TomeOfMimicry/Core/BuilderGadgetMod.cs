using System;
using CoreLib;
using CoreLib.Submodule.UserInterface;
using CoreLib.Submodule.UserInterface.Component;
using PlayerEquipment;
using Pug.Automation;
using PugMod;
using PugTilemap;
using Rewired;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace TomeOfMimicry
{
    public class BuilderGadgetMod : IMod
    {
        public const string ID = "TomeOfMimicry";
        public const string NAME = "Tome of Mimicry";

        public static LoadedMod ModInfo { get; private set; }

        internal static Player RwPlayer;
        private LoadedMod _modInfo;
        private bool _idLogged;
        private bool _wasInWorld;

        public void EarlyInit()
        {
            CoreLibMod.LoadSubmodule(typeof(UserInterfaceModule));

            foreach (var mod in API.ModLoader.LoadedMods)
            {
                if (mod.Metadata.name == NAME)
                {
                    _modInfo = mod;
                    break;
                }
            }

            if (_modInfo == null)
            {
                Debug.LogError($"[{NAME}] Failed to load: mod metadata not found!");
                return;
            }

            ModInfo = _modInfo;

            API.Authoring.OnObjectTypeAdded += OnObjectTypeAdded;

            Debug.Log($"[{NAME}] EarlyInit complete");
        }

        public void Init()
        {
            API.Client.OnWorldCreated += OnClientWorldCreated;
            BurstDisabler.DisableBurstForSystemAndJobs<EquipmentUpdateSystem>();

            var go = new GameObject("TomeOfMimicry_DebugUI");
            go.AddComponent<DebugUI>();
            UnityEngine.Object.DontDestroyOnLoad(go);

            Debug.Log($"[{NAME}] Init complete");
        }

        private void OnClientWorldCreated()
        {
            if (ReInput.isReady)
                RwPlayer = ReInput.players.GetPlayer(0);
            BlueprintGadgetItem.Reset();
            NativePlacementCursor.Reset();
            BlueprintManager.Shutdown();
            _idLogged = false;
            _wasInWorld = false;
        }

        private void OnObjectTypeAdded(Entity entity, GameObject authoringData, EntityManager em)
        {
            if (authoringData.GetComponent<PlayerController>() == null) return;

            em.AddComponent<BlueprintStateCD>(entity);
            Debug.Log($"[{NAME}] Added BlueprintStateCD to player entity");
        }

        public void Shutdown()
        {
            BlueprintManager.Shutdown();
        }

        public void ModObjectLoaded(UnityEngine.Object obj)
        {
            if (obj is Texture2D tex) { UiAssets.RegisterTexture(tex); return; }
            if (obj is Font font) { UiAssets.RegisterFont(font); return; }

            if (obj is not GameObject go || go.GetComponent<ModUIAuthoring>() == null)
                return;

            UserInterfaceModule.RegisterModUI(go);
        }

        public void Update()
        {
            bool inWorld = DebugUI.IsInWorld;
            if (!inWorld)
            {
                BlueprintManager.IsGadgetEquipped = false;
                if (_wasInWorld)
                {
                    BlueprintManager.Shutdown();
                    NativePlacementCursor.Reset();
                    _idLogged = false;
                }
                _wasInWorld = false;
                return;
            }

            _wasInWorld = true;
            var manager = Manager.main;
            if (manager == null || manager.player == null)
            {
                BlueprintManager.IsGadgetEquipped = false;
                return;
            }
            if (RwPlayer == null)
            {
                if (ReInput.isReady) RwPlayer = ReInput.players.GetPlayer(0);
                return;
            }

            if (BlueprintManager.IsGadgetEquipped)
            {
                if (!BuilderPanelUI.IsEditingText && Input.GetKeyDown(KeyCode.B)) BuilderPanelUI.ToggleOpen();

                bool ctrl  = Input.GetKey(KeyCode.LeftControl)  || Input.GetKey(KeyCode.RightControl);
                bool shift = Input.GetKey(KeyCode.LeftShift)    || Input.GetKey(KeyCode.RightShift);
                if (!BuilderPanelUI.IsEditingText && ctrl && Input.GetKeyDown(KeyCode.U))
                {
                    if (shift)
                    {
                        if (BlueprintManager.CanRedo) { BlueprintManager.RequestRedo(); Debug.Log("[Tome of Mimicry] Redo queued"); }
                        else Debug.Log("[Tome of Mimicry] Nothing to redo.");
                    }
                    else
                    {
                        if (BlueprintManager.CanUndo) { BlueprintManager.RequestUndo(); Debug.Log("[Tome of Mimicry] Undo queued"); }
                        else Debug.Log("[Tome of Mimicry] Nothing to undo.");
                    }
                }
            }

            if (Manager.ui != null && Manager.ui.isAnyInventoryShowing) return;
            if (Manager.menu != null && Manager.menu.IsAnyMenuActive()) return;

            var player = manager.player;
            var equipped = player.playerInventoryHandler.GetContainedObjectData(player.equippedSlotIndex);
            var gadgetID = BlueprintGadgetItem.ObjectID;
            BlueprintManager.IsGadgetEquipped = equipped.objectID == gadgetID;
            if (!BlueprintManager.IsGadgetEquipped)
            {
                BlueprintManager.HandleInput(RwPlayer, player, BlueprintManager.CachedCursorTilePos);
                return;
            }

            if (!_idLogged && BlueprintManager.IsGadgetEquipped)
            {
                _idLogged = true;
                Debug.Log($"[Tome of Mimicry] Gadget equipped — ID: {(int)gadgetID} ({gadgetID})");
            }

            if (!NativePlacementCursor.TryGetCursorTile(out var cursorTilePos))
                return;

            BlueprintManager.UpdateCursor(cursorTilePos);
            BlueprintManager.HandleInput(RwPlayer, player, cursorTilePos);

            ClientCostEstimator.Tick();

            if (Input.GetKeyDown(KeyCode.F7))
            {
                var pp = player.transform.position;
                var renderInt = new Vector3Int(Mathf.FloorToInt(pp.x), 0, Mathf.FloorToInt(pp.z));
                var ecsVec = EntityMonoBehaviour.ToWorldFromRender(renderInt);
                var ecsPos = new int2(ecsVec.x, ecsVec.z);

                var lookup = Manager.multiMap.GetTileLayerLookup();
                var sb = new System.Text.StringBuilder();

                sb.Append($"ecs={ecsPos} | ");
                foreach (TileType tt in System.Enum.GetValues(typeof(TileType)))
                {
                    if (tt == TileType.none) continue;
                    if (lookup.TryGetTileInfo(ecsPos, tt, out TileInfo info))
                        sb.Append($"{tt}(ts={info.tileset}) ");
                }
                Debug.Log($"[Tome of Mimicry] F7 all types at {ecsPos}: {sb}");

                BlueprintManager.TileTestPos = ecsPos;
                BlueprintManager.PendingTileTest = true;
            }

            if (BlueprintManager.PendingCapture)
            {
                BlueprintManager.PendingCapture = false;
                DoCapture();
            }
        }

        private void DoCapture()
        {
            var (min, max) = BlueprintManager.GetSelectionBounds();

            if (max.x - min.x + 1 > 101) { max.x = min.x + 100; Debug.LogWarning("[Tome of Mimicry] Selection wider than 101 — trimmed"); }
            if (max.y - min.y + 1 > 101) { max.y = min.y + 100; Debug.LogWarning("[Tome of Mimicry] Selection taller than 101 — trimmed"); }

            int total = (max.x - min.x + 1) * (max.y - min.y + 1);
            Debug.Log($"[Tome of Mimicry] Capture: sampling {total} positions");

            var blueprint = new Blueprint($"Blueprint_{BlueprintManager.SavedCount + 1}",
                                          max.x - min.x + 1, max.y - min.y + 1);
            blueprint.sourceGameVersion = Application.version;

            bool captureFloors = BlueprintManager.CopyMode == BlueprintCopyMode.Full || BlueprintManager.CopyMode == BlueprintCopyMode.FloorsOnly;
            bool captureWalls  = BlueprintManager.CopyMode == BlueprintCopyMode.Full || BlueprintManager.CopyMode == BlueprintCopyMode.WallsOnly;

            var lookup = Manager.multiMap.GetTileLayerLookup();

            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    var pos = new int2(x, y);
                    var bpTile = new BlueprintTile();
                    bpTile.relativePos = new int3(x - min.x, 0, y - min.y);

                    if (captureFloors)
                    {
                        foreach (var tt in new[] { TileType.bridge, TileType.floor })
                        {
                            if (lookup.TryGetTileInfo(pos, tt, out TileInfo fi))
                            {
                                bpTile.floorTileType = fi.tileset;
                                bpTile.floorTileKind = tt;
                                bpTile.hasFloor = true;
                                break;
                            }
                        }

                        if (lookup.TryGetTileInfo(pos, TileType.rail, out TileInfo railInfo))
                        {
                            bpTile.railTileset = railInfo.tileset;
                            bpTile.hasRail = true;
                        }

                        if (lookup.TryGetTileInfo(pos, TileType.circuitPlate, out TileInfo wireInfo))
                        {
                            bpTile.wireTileset = wireInfo.tileset;
                            bpTile.hasWire = true;
                        }

                        foreach (var tt in BlueprintLayers.Extras)
                        {
                            if (!lookup.TryGetTileInfo(pos, tt, out TileInfo exInfo)) continue;
                            bpTile.extras ??= new System.Collections.Generic.List<int2>();
                            bpTile.extras.Add(new int2((int)tt, exInfo.tileset));
                        }

                        if (bpTile.floorTileKind != TileType.bridge
                            && lookup.TryGetTileInfo(pos, TileType.ground, out TileInfo groundInfo)
                            && IsValidGroundTileset(groundInfo.tileset))
                        {
                            bpTile.groundTileType = groundInfo.tileset;
                            bpTile.hasGround = true;
                        }
                    }

                    if (captureWalls && lookup.TryGetTileInfo(pos, TileType.wall, out TileInfo wallInfo))
                    {
                        bpTile.wallTileType = wallInfo.tileset;
                        bpTile.hasWall = true;
                    }

                    bool hasContent = bpTile.hasFloor || bpTile.hasWall || bpTile.hasWire ||
                                      bpTile.hasRail || bpTile.extras != null;
                    if (BlueprintManager.CopyMode == BlueprintCopyMode.Full && bpTile.hasGround)
                        hasContent = true;
                    if (hasContent)
                    {
                        blueprint.tiles.Add(bpTile);
                        Debug.Log($"[Tome of Mimicry] ({x},{y}): wall={bpTile.hasWall}(ts={bpTile.wallTileType}) floor={bpTile.hasFloor}({bpTile.floorTileKind}/ts={bpTile.floorTileType}) ground={bpTile.hasGround}(ts={bpTile.groundTileType}) rail={bpTile.hasRail}(ts={bpTile.railTileset}) wire={bpTile.hasWire}(ts={bpTile.wireTileset}) extras={(bpTile.extras != null ? bpTile.extras.Count : 0)}");
                    }
                }
            }

            bool captureObjects = BlueprintManager.CopyMode == BlueprintCopyMode.Full ||
                                  BlueprintManager.CopyMode == BlueprintCopyMode.ObjectsOnly;
            if (captureObjects)
                CaptureEntities(blueprint, min, max);

            BlueprintManager.SetCapturedBlueprint(blueprint);
            Debug.Log($"[Tome of Mimicry] Captured '{blueprint.name}' — {blueprint.tiles.Count} tiles, {blueprint.objects.Count} objects, mode: {BlueprintManager.CopyMode}");
        }

        private static void CaptureEntities(Blueprint blueprint, int2 min, int2 max)
        {
            var world = API.Server.World ?? API.Client.World;
            if (world == null)
            {
                Debug.LogWarning("[Tome of Mimicry] No world available — skipping entity capture");
                return;
            }
            Debug.Log($"[Tome of Mimicry] Capturing entities from {(API.Server.World != null ? "server" : "client")} world");

            var em = world.EntityManager;

            PugDatabase.DatabaseBankCD dbBank = default;
            bool hasDb = false;
            try
            {
                using var dbQ = em.CreateEntityQuery(ComponentType.ReadOnly<PugDatabase.DatabaseBankCD>());
                if (dbQ.CalculateEntityCount() > 0)
                {
                    dbBank = dbQ.GetSingleton<PugDatabase.DatabaseBankCD>();
                    hasDb  = true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Tome of Mimicry] DB not ready for entity capture: {ex.Message}");
            }

            var objQ     = em.CreateEntityQuery(
                ComponentType.ReadOnly<ObjectDataCD>(),
                ComponentType.ReadOnly<LocalTransform>());
            var entities = objQ.ToEntityArray(Allocator.Temp);
            objQ.Dispose();

            int count = 0;
            foreach (var entity in entities)
            {
                var objData = em.GetComponentData<ObjectDataCD>(entity);
                if (objData.objectID == ObjectID.None) continue;
                if (objData.objectID == BlueprintGadgetItem.ObjectID) continue;

                var tf  = em.GetComponentData<LocalTransform>(entity);
                int ex2 = Mathf.RoundToInt(tf.Position.x);
                int ez  = Mathf.RoundToInt(tf.Position.z);

                if (ex2 < min.x || ex2 > max.x || ez < min.y || ez > max.y) continue;

                var support = BlueprintSupportPolicy.Classify(entity, objData.objectID, em);
                if (support == BlueprintObjectSupport.Excluded)
                {
                    blueprint.unsupportedObjectCount++;
                    Debug.Log($"[Tome of Mimicry] Object skipped — policy-excluded: {objData.objectID} at ({ex2},{ez})");
                    continue;
                }

                int2 tileSize = new int2(1, 1);
                if (hasDb)
                {
                    try
                    {
                        ref var info = ref PugDatabase.GetEntityObjectInfo(
                            objData.objectID, dbBank.databaseBankBlob, objData.variation);
                        if (info.objectType != ObjectType.PlaceablePrefab)
                        {
                            Debug.Log($"[Tome of Mimicry] Object skipped — not PlaceablePrefab (type={info.objectType}): {objData.objectID} at ({ex2},{ez})");
                            continue;
                        }
                        if (info.tileType != TileType.none)
                        {
                            Debug.Log($"[Tome of Mimicry] Object skipped — tile-based (tileType={info.tileType}): {objData.objectID} at ({ex2},{ez})");
                            continue;
                        }
                        tileSize = new int2(
                            info.prefabTileSize.x > 0 ? info.prefabTileSize.x : 1,
                            info.prefabTileSize.y > 0 ? info.prefabTileSize.y : 1);
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"[Tome of Mimicry] Object skipped — DB lookup failed: {objData.objectID} at ({ex2},{ez}) ({ex.Message})");
                        continue;
                    }
                }

                float3 dir = float3.zero;
                if (em.HasComponent<DirectionCD>(entity))
                    dir = em.GetComponentData<DirectionCD>(entity).direction;

                bool dirByVariation = em.HasComponent<DirectionBasedOnVariationCD>(entity);
                int2 dbvDirection = int2.zero;
                if (dirByVariation)
                    dbvDirection = em.GetComponentData<DirectionBasedOnVariationCD>(entity).direction;

                bool hasPaint = em.HasComponent<PaintableObjectCD>(entity);
                int paintColor = 0;
                if (hasPaint)
                    paintColor = (int)em.GetComponentData<PaintableObjectCD>(entity).color;

                bool hasObjectFilter = em.HasComponent<ObjectFilteringCD>(entity);
                ObjectFilteringCD objectFilter = default;
                if (hasObjectFilter)
                    objectFilter = em.GetComponentData<ObjectFilteringCD>(entity);

                bool hasMoverFilter = em.HasComponent<MoverFilterCD>(entity);
                MoverFilterCD moverFilter = default;
                if (hasMoverFilter)
                    moverFilter = em.GetComponentData<MoverFilterCD>(entity);

                blueprint.objects.Add(new BlueprintObject
                {
                    relativePos    = new int2(ex2 - min.x, ez - min.y),
                    objectId       = objData.objectID,
                    variation      = objData.variation,
                    tileSize       = tileSize,
                    direction      = dir,
                    dirByVariation = dirByVariation,
                    dbvDirection   = dbvDirection,
                    hasPaint       = hasPaint,
                    paintColor     = paintColor,
                    hasObjectFilter      = hasObjectFilter,
                    objectFilterType     = (int)objectFilter.filterType,
                    objectFilterObject   = objectFilter.filterObject,
                    objectFilterVariation = objectFilter.filterVariation,
                    hasMoverFilter       = hasMoverFilter,
                    moverFilterType      = (int)moverFilter.filterType,
                    moverFilterObject    = moverFilter.filterObject,
                    moverFilterVariation = moverFilter.filterVariation,
                    moverFilterCategory  = (int)moverFilter.filterCategory,
                });

                count++;
                Debug.Log($"[Tome of Mimicry] Entity captured: {objData.objectID} var={objData.variation} at ({ex2},{ez}) dir={dir} dbv={dirByVariation}({dbvDirection})");
            }

            entities.Dispose();
            Debug.Log($"[Tome of Mimicry] CaptureEntities: {count} objects found in selection");
        }

        private static bool IsPlacedWallTileset(int tileset) => tileset != 0;

        private static bool IsValidGroundTileset(int tileset) => true;
    }
}
