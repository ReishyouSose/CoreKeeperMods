using System.Collections.Generic;
using PugMod;
using PugTilemap;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace TomeOfMimicry
{
    public static class ClientCostEstimator
    {
        private static float _nextUpdate;

        public static void Tick()
        {
            if (Time.realtimeSinceStartup < _nextUpdate) return;
            _nextUpdate = Time.realtimeSinceStartup + 0.2f;

            var bp = BlueprintManager.ActiveBlueprint;
            if (bp == null || BlueprintManager.State != GadgetState.HoldingBlueprint)
            {
                TileCostTracker.Clear();
                return;
            }

            var world = API.Client.World;
            var player = Manager.main?.player;
            if (world == null || player == null) return;
            var em = world.EntityManager;

            bool godMode = false;
            using (var worldInfoQ = em.CreateEntityQuery(ComponentType.ReadOnly<WorldInfoCD>()))
                if (worldInfoQ.CalculateEntityCount() > 0)
                    godMode = worldInfoQ.GetSingleton<WorldInfoCD>().IsWorldModeEnabled(WorldMode.Creative);

            bool hasMap;
            TileWithTilesetToObjectDataMapCD tileMap;
            using (var mapQ = em.CreateEntityQuery(ComponentType.ReadOnly<TileWithTilesetToObjectDataMapCD>()))
            {
                hasMap = mapQ.CalculateEntityCount() > 0;
                tileMap = hasMap ? mapQ.GetSingleton<TileWithTilesetToObjectDataMapCD>() : default;
            }
            if (!hasMap)
            {
                TileCostTracker.Publish(null, null, godMode, true);
                return;
            }

            var lookup = Manager.multiMap.GetTileLayerLookup();
            var origin = BlueprintManager.PasteOrigin;
            var offset = BlueprintManager.GetCursorOffset();
            int rot = BlueprintManager.BlueprintRotation;
            bool flip = BlueprintManager.BlueprintFlipped;
            var mode = BlueprintManager.PasteMode;

            var needed = new Dictionary<ObjectID, int>();
            int placeableTiles = 0;
            foreach (var tile in bp.tiles)
            {
                var rel = new int2(tile.relativePos.x, tile.relativePos.z);
                var rotated = BlueprintManager.GetRotatedRelPos(bp, rot, flip, rel);
                var pos = new int2(origin.x + offset.x + rotated.x, origin.y + offset.y + rotated.y);

                if (mode == BlueprintPasteMode.MergeEmpty &&
                    (lookup.TryGetTileInfo(pos, TileType.wall, out _) ||
                     lookup.TryGetTileInfo(pos, TileType.floor, out _) ||
                     lookup.TryGetTileInfo(pos, TileType.bridge, out _) ||
                     lookup.TryGetTileInfo(pos, TileType.rail, out _) ||
                     lookup.TryGetTileInfo(pos, TileType.circuitPlate, out _)))
                    continue;

                placeableTiles++;
                AddTileCost(tile, tileMap, needed);
            }

            foreach (var obj in bp.objects)
                Add(needed, obj.objectId);

            Dictionary<ObjectID, int> available = null;
            if (!godMode && em.Exists(player.entity) && em.HasBuffer<ContainedObjectsBuffer>(player.entity))
            {
                available = new Dictionary<ObjectID, int>();
                var inv = em.GetBuffer<ContainedObjectsBuffer>(player.entity, true);
                for (int i = 0; i < inv.Length; i++)
                {
                    var slot = inv[i];
                    if (slot.objectData.objectID == ObjectID.None || slot.objectData.amount <= 0) continue;
                    available.TryGetValue(slot.objectData.objectID, out int existing);
                    available[slot.objectData.objectID] = existing + slot.objectData.amount;
                }
            }

            bool hasPlaceableContent = placeableTiles > 0 || bp.objects.Count > 0;
            TileCostTracker.Publish(needed, available, godMode, true, hasPlaceableContent);
        }

        private static void AddTileCost(
            BlueprintTile tile,
            TileWithTilesetToObjectDataMapCD tileMap,
            Dictionary<ObjectID, int> cost)
        {
            if (tile.hasWall)
                Add(cost, PugDatabase.TryGetTileItemInfo(TileType.wall, (Tileset)tile.wallTileType, tileMap).objectID);
            if (tile.hasFloor)
                Add(cost, PugDatabase.TryGetTileItemInfo(tile.floorTileKind, (Tileset)tile.floorTileType, tileMap).objectID);
            if (tile.hasWire)
                Add(cost, PugDatabase.TryGetTileItemInfo(TileType.circuitPlate, (Tileset)tile.wireTileset, tileMap).objectID);
            if (tile.hasRail)
                Add(cost, PugDatabase.TryGetTileItemInfo(TileType.rail, (Tileset)tile.railTileset, tileMap).objectID);
            if (tile.extras != null)
                foreach (var ex in tile.extras)
                    Add(cost, PugDatabase.TryGetTileItemInfo((TileType)ex.x, (Tileset)ex.y, tileMap).objectID);
        }

        private static void Add(Dictionary<ObjectID, int> d, ObjectID id)
        {
            if (id == ObjectID.None) return;
            d.TryGetValue(id, out int existing);
            d[id] = existing + 1;
        }
    }
}
