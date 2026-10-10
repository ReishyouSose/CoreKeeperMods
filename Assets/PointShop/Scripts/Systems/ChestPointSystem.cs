using Assets.PointShop.Scripts.Core;
using Interaction;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace Assets.PointShop.Scripts.Systems
{
    [UpdateInGroup(typeof(InteractionSystemGroup))]
    [UpdateAfter(typeof(TriggerUseInteractionSystem))]
    [UpdateBefore(typeof(PerformInteractionSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class SpawnedChestPointSystem : PugSimulationSystemBase
    {
        private FixedString64Bytes byTileLookup;
        protected override void OnCreate()
        {
            RequireForUpdate<SubMapRegistry>();
            byTileLookup = ZoneLookup.ByTileLookup;
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingletonBuffer<GainPointBuffer>(out var gains))
                return;
            var ecb = CreateCommandBuffer();
            var byTile = byTileLookup;
            var tileLookup = CreateTileAccessor();
            var lootLookup = SystemAPI.GetComponentLookup<AddRandomLootCD>(true);
            Entities.ForEach((Entity entity, in DynamicBuffer<TriggerUseInteractionBuffer> triggers, in LocalTransform transform, in GainPointCD gain) =>
            {
                if (triggers.Length == 0)
                    return;
                ecb.AddComponent<HasBeenDiscoveredCD>(entity);
                var player = triggers[0].interactorEntity;
                var zone = gain.Zone;
                if (lootLookup.HasComponent(entity) && ZoneLookup.TryFromLootTable(lootLookup[entity].lootTableID, out var lootZone))
                    zone = lootZone;
                else if (zone == byTile && !ZoneLookup.TryResolveGround(tileLookup, transform.Position, out zone))
                    return;
                gains.Add(new GainPointBuffer
                {
                    Zone = zone,
                    Player = player,
                    Way = ObtainWay.Chest
                });
            })
                .WithName("ChestPointSystem")
                .WithAll<CanBeDiscoveredCD>()
                .WithNone<HasBeenDiscoveredCD, ChangeVariationWhenContainingObjectCD>()
                .WithBurst()
                .Schedule();

            base.OnUpdate();
        }
    }
}
