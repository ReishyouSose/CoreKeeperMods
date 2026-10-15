using Assets.BuildingBlueprint.Scripts.Components;
using Assets.BuildingBlueprint.Scripts.Core;
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
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class BuildingPlaceClient : PugSimulationSystemBase
    {
        internal static BuildingPlaceClient Ins { get; private set; }
        private NativeQueue<PlaceEntityRpc> entities;
        private NativeQueue<PlaceTileRpc> tiles;
        private NativeQueue<StartPlaceRPC> starts;
        private EntityArchetype entityArchetype;
        private EntityArchetype tileArchetype;
        private EntityArchetype startArchetype;
        private TileWithTilesetToObjectDataMapCD tileSetMap;
        private ComponentLookup<AlwaysDropSpecificVariationCD> dropSpecificVariationLookup;
        private BufferLookup<ContainedObjectsBuffer> containerLookup;
        private int id = 0;
        private float placeCD;
        const float PlaceCooldownBase = 1f;
        const float PlaceCooldownPerRequest = 0.01f;
        protected override void OnCreate()
        {
            Ins = this;
            entities = new(Allocator.Persistent);
            tiles = new(Allocator.Persistent);
            starts = new(Allocator.Persistent);
            entityArchetype = EntityManager.CreateArchetype(typeof(PlaceEntityRpc), typeof(SendRpcCommandRequest));
            tileArchetype = EntityManager.CreateArchetype(typeof(PlaceTileRpc), typeof(SendRpcCommandRequest));
            startArchetype = EntityManager.CreateArchetype(typeof(StartPlaceRPC), typeof(SendRpcCommandRequest));
            dropSpecificVariationLookup = SystemAPI.GetComponentLookup<AlwaysDropSpecificVariationCD>();
            containerLookup = SystemAPI.GetBufferLookup<ContainedObjectsBuffer>();
            RequireForUpdate<TileWithTilesetToObjectDataMapCD>();
            NeedDatabase();
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            placeCD = math.max(0, placeCD - World.Time.DeltaTime);
            var starts = this.starts;
            var startArchetype = this.startArchetype;
            var ecb = CreateCommandBuffer();
            tileSetMap = SystemAPI.GetSingleton<TileWithTilesetToObjectDataMapCD>();

            var entities = this.entities;
            var tiles = this.tiles;
            var entityArchetype = this.entityArchetype;
            var tileArchetype = this.tileArchetype;
            while (starts.TryDequeue(out var start))
            {
                var e = ecb.CreateEntity(startArchetype);
                ecb.SetComponent(e, start);
                //Debug.Log($"Send place start, id: {start.ID}");
            }
            while (tiles.TryDequeue(out var tile))
            {
                var e = ecb.CreateEntity(tileArchetype);
                ecb.SetComponent(e, tile);
            }
            while (entities.TryDequeue(out var entity))
            {
                var e = ecb.CreateEntity(entityArchetype);
                ecb.SetComponent(e, entity);
            }
            base.OnUpdate();
        }
        public void Place(BuildingInfo info, int2 origin)
        {
            if (placeCD > 0)
                return;
            var entitiesQueue = entities;
            var tilesQueue = tiles;
            var player = Manager.main.player.entity;
            StartPlaceRPC counter = new() { ID = ++id, Player = player };
            foreach (var tiles in info.TileInfos)
            {
                foreach (var tile in tiles.Tiles)
                {
                    var obj = TileToObject(tile);
                    tilesQueue.Enqueue(new()
                    {
                        ID = id,
                        ObjectID = obj.objectID,
                        Variation = obj.variation,
                        Tile = tile,
                        Pos = tiles.Position + origin,
                        Player = player
                    });
                    switch (TilesInfo.GetPlaceOrder(tile.tileType))
                    {
                        case 0:
                            counter.Count_1++;
                            break;
                        case 1:
                            counter.Count_2++;
                            break;
                        case 2:
                            counter.Count_3++;
                            break;
                        case 3:
                            counter.Count_4++;
                            break;

                    }
                }
            }
            foreach (var entities in info.EntityInfos)
            {
                foreach (var entity in entities.Entities)
                {
                    entitiesQueue.Enqueue(new()
                    {
                        ID = id,
                        ObjectID = entity.ObjectID,
                        Variation = entity.Variation,
                        Pos = entities.Position + origin,
                        Direction = entity.Direction,
                        Color = entity.Color,
                        Player = player,
                    });
                    counter.Count_5++;
                }
            }
            starts.Enqueue(counter);
            int requests = counter.Count_1 + counter.Count_2 + counter.Count_3 + counter.Count_4 + counter.Count_5;
            placeCD = math.min(3, PlaceCooldownBase + requests * PlaceCooldownPerRequest);
        }
        public void Destory(EntityCD entity, Entity player)
        {
            entities.Enqueue(new()
            {
                Entity = entity.Entity,
                Player = player,
            });
        }
        public void Destory(TileCD t, int2 pos, Entity player)
        {
            tiles.Enqueue(new()
            {
                ObjectID = (ObjectID)(-1),
                Tile = t,
                Pos = pos,
                Player = player
            });
        }
        public ObjectDataCD TileToObject(TileCD tile) => MiscHelper.TileToObject(tile, tileSetMap);
        public bool AlwaysDropZero(ObjectID id, int variation)
        {
            return dropSpecificVariationLookup.HasComponent(PugDatabase.GetPrimaryPrefabEntity(id, database, variation));
        }
        public int GetExistObjectAmount(Entity player, ObjectID id, int variation)
        {
            int amount = 0;
            if (!containerLookup.TryGetBuffer(player, out var inv))
            {
                return 0;
            }
            for (int i = 0; i < inv.Length; i++)
            {
                if (inv[i].objectID == id && inv[i].variation == variation)
                {
                    amount += inv[i].amount;
                }
            }

            return amount;
        }
    }
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class BuildingPlaceServer : PugSimulationSystemBase
    {
        private ComponentLookup<PlaceEntityRpc> entityLookup;
        private ComponentLookup<PlaceTileRpc> tileLookup;
        private ComponentLookup<StartPlaceRPC> startLookup;
        private ComponentLookup<AlwaysDropSpecificVariationCD> dropSpecificVariationLookup;
        private ComponentLookup<HealthCD> healthLookup;
        private ComponentLookup<PlayerGhost> playerLookup;
        private ComponentLookup<DurabilityCD> durabilityLookup;
        private ComponentLookup<GodModeCD> godLookup;
        private ComponentLookup<EntityDestroyedCD> destroyedLookup;
        private BufferLookup<PlaceValidateBuffer> placeLookup;
        private BufferLookup<ContainedObjectsBuffer> containedLookup;
        private TileAccessor tileAccessor;
        private uint tickRate;

        protected override void OnCreate()
        {
            NeedDatabase();
            RequireForUpdate<PugDatabase.DatabaseBankCD>();
            RequireForUpdate<TileUpdateBuffer>();
            RequireForUpdate<InventoryChangeBuffer>();
            RequireForUpdate<TileWithTilesetToObjectDataMapCD>();
            RequireForUpdate<NetworkTime>();
            tickRate = (uint)PlatformConfiguration.Instance.SessionConfiguration.SimulationTickRate;
            entityLookup = SystemAPI.GetComponentLookup<PlaceEntityRpc>();
            tileLookup = SystemAPI.GetComponentLookup<PlaceTileRpc>();
            startLookup = SystemAPI.GetComponentLookup<StartPlaceRPC>();
            dropSpecificVariationLookup = SystemAPI.GetComponentLookup<AlwaysDropSpecificVariationCD>();
            healthLookup = SystemAPI.GetComponentLookup<HealthCD>();
            playerLookup = SystemAPI.GetComponentLookup<PlayerGhost>();
            durabilityLookup = SystemAPI.GetComponentLookup<DurabilityCD>();
            godLookup = SystemAPI.GetComponentLookup<GodModeCD>();
            destroyedLookup = SystemAPI.GetComponentLookup<EntityDestroyedCD>();
            placeLookup = SystemAPI.GetBufferLookup<PlaceValidateBuffer>();
            containedLookup = SystemAPI.GetBufferLookup<ContainedObjectsBuffer>();
            base.OnCreate();
        }
        protected override void OnStartRunning()
        {
            tileAccessor = CreateTileAccessor();
            base.OnStartRunning();
        }
        protected override void OnUpdate()
        {
            var entityLookup = this.entityLookup;
            var tileLookup = this.tileLookup;
            var startLookup = this.startLookup;
            var dropSpecificVariationLookup = this.dropSpecificVariationLookup;
            var healthLookup = this.healthLookup;
            var playerLookup = this.playerLookup;
            var durabilityLookup = this.durabilityLookup;
            var godLookup = this.godLookup;
            var destroyedLookup = this.destroyedLookup;
            var placeLookup = this.placeLookup;
            var containedLookup = this.containedLookup;
            var tileAccessor = this.tileAccessor;
            var database = this.database;
            var ecb = CreateCommandBuffer();
            var invChange = SystemAPI.GetSingletonBuffer<InventoryChangeBuffer>();
            var tileChange = SystemAPI.GetSingletonBuffer<TileUpdateBuffer>();
            bool creative = WorldInfo.IsWorldModeEnabled(WorldMode.Creative);
            bool guest = WorldInfo.guestMode;
            var tileSetMap = SystemAPI.GetSingleton<TileWithTilesetToObjectDataMapCD>();
            bool adminOnly = BuildingBlueprint.DestoryPrivileges.Value;
            var tickRate = this.tickRate;
            var currentTick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;
            Entities.ForEach((Entity e) =>
            {
                if (startLookup.TryGetComponent(e, out var start))
                {
                    ecb.DestroyEntity(e);
                    if (placeLookup.TryGetBuffer(start.Player, out var validate))
                    {
                        PlaceValidateBuffer b = new();
                        b.Start(start);
                        if (!b.Finished)
                            validate.Add(b);
                    }
                }
                else if (tileLookup.TryGetComponent(e, out var tile))
                {
                    var playerE = tile.Player;
                    var tileCD = tile.Tile;
                    if ((int)tile.ObjectID != -1)
                    {
                        if (!tile.Timer.isRunning)
                        {
                            tile.Timer.Start(currentTick, 10, tickRate);
                            ecb.SetComponent(e, tile);
                        }
                        else if (tile.Timer.IsTimerElapsed(currentTick))
                        {
                            ecb.DestroyEntity(e);
                            return;
                        }
                        bool collect = false;
                        if (placeLookup.TryGetBuffer(playerE, out var validates))
                        {
                            var id = tile.ID;
                            for (int i = 0; i < validates.Length; i++)
                            {
                                var info = validates[i];
                                if (info.ID != id)
                                    continue;
                                if (!info.Collect(TilesInfo.GetPlaceOrder(tileCD.tileType), currentTick))
                                    continue;
                                collect = true;
                                if (info.Finished)
                                    validates.RemoveAt(i);
                                else
                                    validates[i] = info;
                                break;
                            }
                        }
                        if (!collect)
                            return;
                    }
                    ecb.DestroyEntity(e);
                    var objectID = tile.ObjectID;
                    var tileType = tileCD.tileType;
                    var tileset = tileCD.tileset;
                    var pos = tile.Pos;
                    if ((int)objectID == -1)
                    {
                        if (!playerLookup.TryGetComponent(playerE, out var player))
                            return;
                        if (adminOnly ? player.adminPrivileges <= 0 : guest)
                            return;
                        EntityUtility.RemoveTile(tileset, tileType, pos, tileChange, tileAccessor);
                        switch (tileType)
                        {
                            case TileType.roofHole:
                            case TileType.dugUpGround:
                                return;
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
                                return;
                        }
                        var obj = MiscHelper.TileToObject(tileCD, tileSetMap);
                        obj.amount = 1;
                        EntityUtility.DropNewEntity(ecb, new() { objectData = obj }, pos.ToFloat3(), database, playerE, true);
                        return;
                    }
                    if (objectID is ObjectID.None or ObjectID.Bucket && !tileAccessor.HasType(pos, TileType.ground))
                    {
                        EntityUtility.AddTile(tileset, tileType, pos, creative, tileChange);
                        return;
                    }
                    if (!containedLookup.TryGetBuffer(playerE, out var inv))
                        return;
                    bool dontConsume = godLookup.IsComponentEnabled(playerE);
                    if (objectID is ObjectID.WoodHoe)
                    {
                        for (int i = 0; i < inv.Length; i++)
                        {
                            var slot = inv[i];
                            if (PugDatabase.GetEntityObjectInfo(slot.objectID, database, slot.variation).objectType == ObjectType.Hoe)
                            {
                                dontConsume = true;
                                break;
                            }
                        }
                    }
                    int index = -1;
                    var primary = PugDatabase.GetPrimaryPrefabEntity(objectID, database);
                    if (!dontConsume)
                    {
                        var variation = tile.Variation;
                        var findVari = dropSpecificVariationLookup.HasComponent(primary) ? 0 : variation;
                        for (int i = 0; i < inv.Length; i++)
                        {
                            var slot = inv[i];
                            if (slot.objectID == objectID && slot.variation == findVari)
                            {
                                var amount = slot.amount;
                                if (amount > 0)
                                {
                                    index = i;
                                    break;
                                }
                            }
                        }
                    }
                    if (dontConsume || index > -1)
                    {
                        EntityUtility.AddTile(tileset, tileType, pos, creative, tileChange);
                        if (dontConsume)
                            return;
                        if (durabilityLookup.HasComponent(primary))
                        {
                            var slot = inv[index];
                            slot.objectData.amount--;
                            inv[index] = slot;
                        }
                        else
                        {
                            invChange.Add(new()
                            {
                                inventoryChangeData = Create.ConsumeEntityAt(playerE, index, 1, true, false)
                            });
                        }
                    }
                }
                else if (entityLookup.TryGetComponent(e, out var entity))
                {
                    var playerE = entity.Player;
                    if (entity.Entity == Entity.Null)
                    {
                        if (!entity.Timer.isRunning)
                        {
                            entity.Timer.Start(currentTick, 10, tickRate);
                            ecb.SetComponent(e, entity);
                        }
                        else if (entity.Timer.IsTimerElapsed(currentTick))
                        {
                            ecb.DestroyEntity(e);
                            return;
                        }
                        bool collect = false;
                        if (placeLookup.TryGetBuffer(playerE, out var validates))
                        {
                            var id = entity.ID;
                            for (int i = 0; i < validates.Length; i++)
                            {
                                var info = validates[i];
                                if (info.ID != id)
                                    continue;
                                if (!info.Collect(4, currentTick))
                                    continue;
                                collect = true;
                                if (info.Finished)
                                    validates.RemoveAt(i);
                                else
                                    validates[i] = info;
                                break;
                            }
                        }
                        if (!collect)
                            return;
                    }
                    ecb.DestroyEntity(e);
                    var targetE = entity.Entity;
                    if (targetE != Entity.Null)
                    {
                        if (!playerLookup.TryGetComponent(playerE, out var player))
                            return;
                        if (adminOnly ? player.adminPrivileges <= 0 : guest)
                            return;
                        if (healthLookup.TryGetComponent(targetE, out var health))
                        {
                            var h = health;
                            h.health = 0;
                            ecb.SetComponent(targetE, h);
                        }
                        else
                            destroyedLookup.SetComponentEnabled(targetE, true);
                        return;
                    }
                    if (!containedLookup.TryGetBuffer(playerE, out var inv))
                        return;
                    var objectID = entity.ObjectID;
                    var primary = PugDatabase.GetPrimaryPrefabEntity(objectID, database);
                    var variation = entity.Variation;
                    var findVari = dropSpecificVariationLookup.HasComponent(primary) ? 0 : variation;
                    int index = -1;
                    bool dontConsume = godLookup.IsComponentEnabled(playerE);
                    if (!dontConsume)
                    {
                        for (int i = 0; i < inv.Length; i++)
                        {
                            var slot = inv[i];
                            if (slot.objectID == objectID && slot.variation == findVari)
                            {
                                var amount = slot.amount;
                                if (amount > 0)
                                {
                                    index = i;
                                    break;
                                }
                            }
                        }
                    }
                    if (dontConsume || index > -1)
                    {
                        var create = EntityUtility.CreateEntity(ecb, entity.Pos.ToFloat3(), objectID, 1, database, variation);
                        if (!entity.Direction.Equals(int2.zero))
                        {
                            ecb.SetComponent(create, new DirectionCD() { direction = entity.Direction.ToFloat3() });
                        }
                        if (entity.Color != PaintableColor.Unpainted)
                        {
                            ecb.SetComponent(create, new PaintableObjectCD() { color = entity.Color });
                        }
                        if (dontConsume)
                            return;
                        if (durabilityLookup.HasComponent(primary))
                        {
                            var slot = inv[index];
                            slot.objectData.amount--;
                            inv[index] = slot;
                        }
                        else
                        {
                            invChange.Add(new()
                            {
                                inventoryChangeData = Create.ConsumeEntityAt(playerE, index, 1, true, false)
                            });
                        }
                    }
                }
            })
                .WithName("PlaceBuildingSystem")
                .WithAll<ReceiveRpcCommandRequest>()
                .WithAny<PlaceEntityRpc, PlaceTileRpc, StartPlaceRPC>()
                .WithBurst()
                .Schedule();
            base.OnUpdate();
        }


    }
}
