using Assets.BuildingBlueprint.Scripts.Components;
using Assets.BuildingBlueprint.Scripts.Core;
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
        private NativeQueue<PlaceCommandRPC> commands;
        private NativeQueue<PlaceCommandChunkRPC> chunks;
        private NativeQueue<BuildingCacheRPC> caches;
        private EntityArchetype placeArchetype;
        private EntityArchetype commandArchetype;
        private EntityArchetype cacheArchetype;
        private TileWithTilesetToObjectDataMapCD tileSetMap;
        private ComponentLookup<AlwaysDropSpecificVariationCD> dropSpecificVariationLookup;
        private ComponentLookup<GhostInstance> ghostLookup;
        private BufferLookup<ContainedObjectsBuffer> containerLookup;
        private int id = 0;
        private float placeCD;
        const float PlaceCooldownBase = 1f;
        const float PlaceCooldownPerRequest = 0.01f;
        protected override void OnCreate()
        {
            Ins = this;
            commands = new(Allocator.Persistent);
            chunks = new(Allocator.Persistent);
            caches = new(Allocator.Persistent);
            placeArchetype = EntityManager.CreateArchetype(typeof(PlaceCommandRPC), typeof(SendRpcCommandRequest));
            commandArchetype = EntityManager.CreateArchetype(typeof(PlaceCommandChunkRPC), typeof(SendRpcCommandRequest));
            cacheArchetype = EntityManager.CreateArchetype(typeof(BuildingCacheRPC), typeof(SendRpcCommandRequest));
            dropSpecificVariationLookup = SystemAPI.GetComponentLookup<AlwaysDropSpecificVariationCD>();
            ghostLookup = SystemAPI.GetComponentLookup<GhostInstance>(true);
            // Read only: a read-write lookup marks the inventory as changed on every read
            containerLookup = SystemAPI.GetBufferLookup<ContainedObjectsBuffer>(true);
            RequireForUpdate<TileWithTilesetToObjectDataMapCD>();
            NeedDatabase();
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            placeCD = math.max(0, placeCD - World.Time.DeltaTime);
            var commands = this.commands;
            var placeArchetype = this.placeArchetype;
            var ecb = CreateCommandBuffer();
            tileSetMap = SystemAPI.GetSingleton<TileWithTilesetToObjectDataMapCD>();

            var chunks = this.chunks;
            var caches = this.caches;
            var commandArchetype = this.commandArchetype;
            var cacheArchetype = this.cacheArchetype;
            while (commands.TryDequeue(out var placeCommand))
            {
                var e = ecb.CreateEntity(placeArchetype);
                ecb.SetComponent(e, placeCommand);
            }
            while (caches.TryDequeue(out var cache))
            {
                var e = ecb.CreateEntity(cacheArchetype);
                ecb.SetComponent(e, cache);
            }
            while (chunks.TryDequeue(out var command))
            {
                var e = ecb.CreateEntity(commandArchetype);
                ecb.SetComponent(e, command);
            }
            base.OnUpdate();
        }
        public void SendCache(BuildingInfo info)
        {
            if (info.ID > 0)
                return;
            info.ID = ++id;
            var chunks = this.chunks;
            var caches = this.caches;
            int size = PlaceCommandChunkRPC.ChunkSize;
            var commands = info.Commands;
            var commandCount = commands.Count;
            int count = (commandCount + size - 1) / size;
            caches.Enqueue(new() { ID = info.ID, Total = count, Length = commandCount });
            for (int i = 0; i < count; i++)
            {
                int start = i * size;
                int end = math.min(commandCount, start + size);
                PlaceCommandChunkRPC chunk = new()
                {
                    ID = id,
                    Index = i,
                    Commands = new()
                };
                var list = chunk.Commands;
                while (start < end)
                {
                    list.Add(info.Commands[start]);
                    start++;
                }
                chunk.Commands = list;
                chunks.Enqueue(chunk);
            }
        }
        public void Place(BuildingInfo info, int2 origin)
        {
            if (placeCD > 0)
                return;
            var player = Manager.main.player.entity;
            commands.Enqueue(PlaceCommandRPC.Place(player, info.ID, origin));
            placeCD = math.min(3, PlaceCooldownBase + info.Commands.Count * PlaceCooldownPerRequest);
        }
        public void Destory(SelectedEntity entity, Entity player)
        {
            commands.Enqueue(PlaceCommandRPC.DestroyEntity(player, ghostLookup[entity.Entity]));
        }
        public void Destory(TileCD t, int2 pos, Entity player)
        {
            commands.Enqueue(PlaceCommandRPC.DestroyTile(player, t, pos));
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
        // Same test as BuildingPlaceServer uses for tilled soil: any hoe in the inventory will do
        public bool HasAnyHoe(Entity player)
        {
            if (!containerLookup.TryGetBuffer(player, out var inv))
            {
                return false;
            }
            for (int i = 0; i < inv.Length; i++)
            {
                var slot = inv[i];
                if (slot.objectID != ObjectID.None && PugDatabase.GetEntityObjectInfo(slot.objectID, database, slot.variation).objectType == ObjectType.Hoe)
                {
                    return true;
                }
            }
            return false;
        }
    }
}