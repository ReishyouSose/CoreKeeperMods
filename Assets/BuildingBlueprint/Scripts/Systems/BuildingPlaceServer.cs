using Assets.BuildingBlueprint.Scripts.Components;
using Assets.BuildingBlueprint.Scripts.Core;
using Assets.BuildingBlueprint.Scripts.Enums;
using Inventory;
using Pug.UnityExtensions;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Assets.BuildingBlueprint.Scripts.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class BuildingPlaceServer : PugSimulationSystemBase
    {
        private ComponentLookup<AlwaysDropSpecificVariationCD> dropSpecificVariationLookup;
        private ComponentLookup<HealthCD> healthLookup;
        private ComponentLookup<PlayerGhost> playerLookup;
        private ComponentLookup<DurabilityCD> durabilityLookup;
        private ComponentLookup<GodModeCD> godLookup;
        private ComponentLookup<EntityDestroyedCD> destroyedLookup;
        private BufferLookup<ContainedObjectsBuffer> containedLookup;
        private TileAccessor tileAccessor;
        private NativeParallelHashMap<BuildingCache.Key, BuildingCache> caches;

        protected override void OnCreate()
        {
            NeedDatabase();
            RequireForUpdate<PugDatabase.DatabaseBankCD>();
            RequireForUpdate<TileUpdateBuffer>();
            RequireForUpdate<InventoryChangeBuffer>();
            RequireForUpdate<TileWithTilesetToObjectDataMapCD>();
            RequireForUpdate<SpawnedGhostEntityMap>();
            dropSpecificVariationLookup = SystemAPI.GetComponentLookup<AlwaysDropSpecificVariationCD>();
            healthLookup = SystemAPI.GetComponentLookup<HealthCD>();
            playerLookup = SystemAPI.GetComponentLookup<PlayerGhost>();
            durabilityLookup = SystemAPI.GetComponentLookup<DurabilityCD>();
            godLookup = SystemAPI.GetComponentLookup<GodModeCD>();
            destroyedLookup = SystemAPI.GetComponentLookup<EntityDestroyedCD>();
            containedLookup = SystemAPI.GetBufferLookup<ContainedObjectsBuffer>();
            caches = new(16, Allocator.Persistent);
            base.OnCreate();
        }
        protected override void OnStartRunning()
        {
            tileAccessor = CreateTileAccessor();
            base.OnStartRunning();
        }
        protected override void OnUpdate()
        {
            var dropSpecificVariationLookup = this.dropSpecificVariationLookup;
            var healthLookup = this.healthLookup;
            var playerLookup = this.playerLookup;
            var durabilityLookup = this.durabilityLookup;
            var godLookup = this.godLookup;
            var destroyedLookup = this.destroyedLookup;
            var containedLookup = this.containedLookup;
            var tileAccessor = this.tileAccessor;
            var database = this.database;
            var ecb = CreateCommandBuffer();
            var invChange = SystemAPI.GetSingletonBuffer<InventoryChangeBuffer>();
            var tileChange = SystemAPI.GetSingletonBuffer<TileUpdateBuffer>();
            var ghostMap = SystemAPI.GetSingleton<SpawnedGhostEntityMap>();
            bool creative = WorldInfo.IsWorldModeEnabled(WorldMode.Creative);
            bool guest = WorldInfo.guestMode;
            var tileSetMap = SystemAPI.GetSingleton<TileWithTilesetToObjectDataMapCD>();
            bool adminOnly = BuildingBlueprint.DestoryPrivileges.Value;
            var caches = this.caches;
            Entities.ForEach((Entity e, in BuildingCacheRPC rpc, in ReceiveRpcCommandRequest req) =>
            {
                ecb.DestroyEntity(e);
                var key = (req.SourceConnection, rpc.ID);
                if (caches.TryGetValue(key, out var cache))
                {
                    cache.Total = rpc.Total;
                    cache.Commands.Length = rpc.Length;
                    cache.Check();
                    caches[key] = cache;
                }
                else
                {
                    cache = rpc;
                    caches.Add(key, cache);
                }
            })
                .WithName("CollectBuildingCache")
                .WithBurst()
                .Schedule();

            Entities.ForEach((Entity e, in PlaceCommandChunkRPC rpc, in ReceiveRpcCommandRequest req) =>
            {
                var key = (req.SourceConnection, rpc.ID);
                var commands = rpc.Commands;
                int start = rpc.Index * PlaceCommandChunkRPC.ChunkSize;
                int end = start + commands.Length;
                if (!caches.TryGetValue(key, out var cache))
                {
                    cache = new()
                    {
                        ID = rpc.ID,
                        Commands = new(end, Allocator.Persistent),
                    };
                    caches.Add(key, cache);
                }
                if (cache.Commands.Length < end)
                {
                    cache.Commands.Length = end;
                }
                var list = cache.Commands;
                int j = 0;
                for (int i = start; i < end; i++)
                {
                    list[i] = commands[j++];
                }
                cache.Count++;
                cache.Check();
                caches[key] = cache;
            })
                .WithName("CollectCommandChunk")
                .WithBurst()
                .Schedule();

            Entities.ForEach((Entity e, in PlaceCommandRPC rpc, in ReceiveRpcCommandRequest req) =>
            {
                ecb.DestroyEntity(e);
                var playerE = rpc.Player;
                if (!playerLookup.TryGetComponent(playerE, out var player))
                    return;
                bool admin = player.adminPrivileges > 0;
                if (!admin && guest)
                    return;
                bool god = godLookup.IsComponentEnabled(playerE);
                Entity entity = Entity.Null;
                TileCD tile;
                int tileset;
                TileType tileType;
                int2 pos;
                ObjectID objectID;
                int variation;
                DynamicBuffer<ContainedObjectsBuffer> containers = default;
                switch (rpc.Op)
                {
                    case PlaceOperator.Place:
                        rpc.Place(out int id, out int2 origin);
                        if (!caches.TryGetValue((req.SourceConnection, id), out var cache))
                        {
                            break;
                        }
                        if (!cache.Completed)
                        {
                            break;
                        }
                        foreach (var command in cache.Commands)
                        {
                            switch (command.Op)
                            {
                                case PlaceOperator.Tile:
                                    var tileCommand = IPlaceCommand.ReadCommand<TileCommand>(command);
                                    tile = tileCommand.TileCD;
                                    tileset = tile.tileset;
                                    tileType = tile.tileType;
                                    pos = tileCommand.Pos;
                                    var objData = MiscHelper.TileToObject(tile, tileSetMap);
                                    objectID = objData.objectID;
                                    if ((objectID is ObjectID.None or ObjectID.Bucket) && !tileAccessor.HasType(pos, TileType.ground))
                                    {
                                        EntityUtility.AddTile(tileset, tileType, pos, creative, tileChange);
                                        break;
                                    }
                                    // 找不到材料就不放置；返回 true 时才执行实际放置
                                    if (!TryTakeForPlace(playerE, objectID, objData.variation, god, dropSpecificVariationLookup,
                                        durabilityLookup, containedLookup, database, invChange))
                                        break;
                                    EntityUtility.AddTile(tileset, tileType, pos, creative, tileChange);
                                    break;
                                case PlaceOperator.Entity:
                                    entity = Entity.Null;
                                    containers = default;
                                    var entityCommand = IPlaceCommand.ReadCommand<EntityCommand>(command);
                                    objectID = entityCommand.ObjectID;
                                    variation = entityCommand.Variation;
                                    // 找不到材料就不放置；返回 true 时才执行实际放置
                                    if (!TryTakeForPlace(playerE, objectID, entityCommand.Variation, god, dropSpecificVariationLookup,
                                        durabilityLookup, containedLookup, database, invChange))
                                        break;
                                    entity = EntityUtility.CreateEntity(ecb, entityCommand.Pos.ToFloat3(), objectID, 1,
                                        database, out var prefab, variation);
                                    if (entity != Entity.Null && containedLookup.TryGetBuffer(prefab, out var prefabContainers))
                                    {
                                        containers = ecb.SetBuffer<ContainedObjectsBuffer>(entity);
                                        for (int i = 0; i < prefabContainers.Length; i++)
                                            containers.Add(default);
                                    }
                                    break;
                                case PlaceOperator.Direction:
                                    if (entity == Entity.Null)
                                        break;
                                    var dir = IPlaceCommand.ReadCommand<DirectionCommand>(command);
                                    if (!dir.Direction.Equals(int2.zero))
                                    {
                                        ecb.SetComponent(entity, new DirectionCD() { direction = dir.Direction.ToFloat3() });
                                    }
                                    break;
                                case PlaceOperator.Paint:
                                    if (entity == Entity.Null)
                                        break;
                                    var paint = IPlaceCommand.ReadCommand<PaintCommand>(command);
                                    if (paint.Color != PaintableColor.Unpainted)
                                    {
                                        ecb.SetComponent(entity, new PaintableObjectCD() { color = paint.Color });
                                    }
                                    break;
                                case PlaceOperator.ObjFilter:
                                    if (entity == Entity.Null)
                                        break;
                                    var filter = IPlaceCommand.ReadCommand<ObjFilterCommand>(command);
                                    // Only what the game itself sets (InventoryUtility.AddFilter): a whitelist of one item.
                                    // SetComponent does nothing on an object without a filter, so a filter in the file
                                    // never adds one to, say, a chest. (AddComponent would, and it would not set the value
                                    // on objects that already have one, like robot arms.)
                                    if (filter.Type == FilterType.Whitelist && filter.Object != ObjectID.None)
                                    {
                                        ecb.SetComponent(entity, new ObjectFilteringCD()
                                        {
                                            filterType = FilterType.Whitelist,
                                            filterObject = filter.Object,
                                            filterVariation = filter.Variation
                                        });
                                    }
                                    break;
                                case PlaceOperator.Container:
                                    if (entity == Entity.Null || !containers.IsCreated)
                                        break;
                                    var container = command.ReadAs<ContainerCommand>();
                                    objectID = container.ObjectID;
                                    var index = container.Index;
                                    if (objectID != ObjectID.None && index >= 0 && index < containers.Length)
                                    {
                                        variation = container.Variation;
                                        if (!TryTakeForPlace(playerE, objectID, variation, god, dropSpecificVariationLookup,
                                            durabilityLookup, containedLookup, database, invChange))
                                            break;
                                        ref var info = ref PugDatabase.GetEntityObjectInfo(objectID, database, variation);
                                        containers[index] = new()
                                        {
                                            objectData = new()
                                            {
                                                objectID = container.ObjectID,
                                                variation = variation,
                                                amount = info.isStackable ? 1 : info.initialAmount,
                                            }
                                        };
                                    }
                                    break;
                            }
                        }
                        break;
                    case PlaceOperator.DestroyEntity:
                        rpc.DestroyEntity(out int ghostID, out var tick);
                        entity = ghostMap.Value[new SpawnedGhost(ghostID, tick)];
                        if (entity != Entity.Null)
                        {
                            if (adminOnly && !admin)
                                break;
                            if (healthLookup.TryGetComponent(entity, out var health))
                            {
                                var h = health;
                                h.health = 0;
                                ecb.SetComponent(entity, h);
                            }
                            else
                                destroyedLookup.SetComponentEnabled(entity, true);
                            break;
                        }
                        break;
                    case PlaceOperator.DestroyTile:
                        rpc.DestroyTile(out tile, out pos);
                        tileType = tile.tileType;
                        tileset = tile.tileset;
                        if (adminOnly && !admin)
                            break;
                        EntityUtility.RemoveTile(tileset, tileType, pos, tileChange, tileAccessor);
                        switch (tileType)
                        {
                            case TileType.roofHole:
                            case TileType.dugUpGround:
                                break;
                            case TileType.water:
                                tileChange.Add(new TileUpdateBuffer
                                {
                                    command = TileUpdateBuffer.Command.Add,
                                    position = pos,
                                    tile = new TileCD
                                    {
                                        tileType = TileType.pit
                                    }
                                });
                                break;
                        }
                        var obj = MiscHelper.TileToObject(tile, tileSetMap);
                        obj.amount = 1;
                        EntityUtility.DropNewEntity(ecb, new() { objectData = obj }, pos.ToFloat3(), database, playerE, true);
                        break;
                }
            })
                .WithName("PlaceBuildingSystem")
                .WithAll<ReceiveRpcCommandRequest>()
                .WithBurst()
                .Schedule();
            base.OnUpdate();
        }

        protected override void OnDestroy()
        {
            foreach (var entry in caches)
            {
                entry.Value.Commands.Dispose();
            }
            caches.Dispose();
        }
        public static bool TryTakeForPlace(Entity player, ObjectID objectID, int variation, bool free,
            in ComponentLookup<AlwaysDropSpecificVariationCD> dropSpecificVariationLookup, in ComponentLookup<DurabilityCD> durabilityLookup,
            in BufferLookup<ContainedObjectsBuffer> containedLookup, BlobAssetReference<PugDatabase.PugDatabaseBank> database,
            DynamicBuffer<InventoryChangeBuffer> invChange)
        {
            if (!containedLookup.TryGetBuffer(player, out var containers))
                return false;
            // 铺土：背包里有任意锄头即可，且不消耗
            if (!free && objectID == ObjectID.WoodHoe)
            {
                for (int i = 0; i < containers.Length; i++)
                {
                    var slot = containers[i];
                    if (PugDatabase.GetEntityObjectInfo(slot.objectID, database, slot.variation).objectType == ObjectType.Hoe)
                    {
                        free = true;
                        break;
                    }
                }
            }

            if (free)
                return true;

            Entity primary = PugDatabase.GetPrimaryPrefabEntity(objectID, database);
            int findVariation = dropSpecificVariationLookup.HasComponent(primary) ? 0 : variation;

            for (int i = 0; i < containers.Length; i++)
            {
                var slot = containers[i];
                if (slot.objectID == objectID && slot.variation == findVariation && slot.amount > 0)
                {
                    if (durabilityLookup.HasComponent(primary))
                    {
                        slot.objectData.amount--;
                        containers[i] = slot;
                    }
                    else
                    {
                        invChange.Add(new()
                        {
                            inventoryChangeData = Create.ConsumeEntityAt(player, i, 1, true, false)
                        });
                    }
                    return true;
                }
            }
            return false;
        }
    }
}