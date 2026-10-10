using Assets.PointShop.Scripts.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

namespace Assets.PointShop.Scripts.Systems
{
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateAfter(typeof(DropLootSystem))]
    public partial class DropLootPointSystem : PugSimulationSystemBase
    {
        private ComponentLookup<BossCD> bossLookup;
        private ComponentLookup<EnemyCD> enemyLookup;
        private ComponentLookup<CattleCD> cattleLookup;
        private ComponentLookup<MineableCD> mineableLookup;
        private ComponentLookup<DiggableCD> diggableLookup;
        private ComponentLookup<DropsLootFromLootTableCD> lootLookup;
        private ComponentLookup<PointsGrantedCD> grantedLookup;
        private ComponentLookup<GainPointCD> zoneLookup;
        private ComponentLookup<LocalTransform> transformLookup;
        private ComponentLookup<CanBeDiscoveredCD> discoverableLookup;
        private ComponentLookup<HasBeenDiscoveredCD> discoveredLookup;
        private ComponentLookup<IsBeingTransformedCD> transformedLookup;
        private BufferLookup<ContainedObjectsBuffer> inventoryLookup;
        private BufferLookup<PointCurrencyBuffer> currencyLookup;
        private readonly FixedString64Bytes byTileLookup = ZoneLookup.ByTileLookup;
        protected override void OnCreate()
        {
            bossLookup = SystemAPI.GetComponentLookup<BossCD>(true);
            enemyLookup = SystemAPI.GetComponentLookup<EnemyCD>(true);
            cattleLookup = SystemAPI.GetComponentLookup<CattleCD>(true);
            mineableLookup = SystemAPI.GetComponentLookup<MineableCD>(true);
            diggableLookup = SystemAPI.GetComponentLookup<DiggableCD>(true);
            lootLookup = SystemAPI.GetComponentLookup<DropsLootFromLootTableCD>(true);
            grantedLookup = SystemAPI.GetComponentLookup<PointsGrantedCD>(true);
            zoneLookup = SystemAPI.GetComponentLookup<GainPointCD>(true);
            transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            discoverableLookup = SystemAPI.GetComponentLookup<CanBeDiscoveredCD>(true);
            discoveredLookup = SystemAPI.GetComponentLookup<HasBeenDiscoveredCD>(true);
            transformedLookup = SystemAPI.GetComponentLookup<IsBeingTransformedCD>(true);
            inventoryLookup = SystemAPI.GetBufferLookup<ContainedObjectsBuffer>(true);
            currencyLookup = SystemAPI.GetBufferLookup<PointCurrencyBuffer>();
            RequireForUpdate<GainPointBuffer>();
            RequireForUpdate<NetworkTime>();
            RequireForUpdate<SubMapRegistry>();
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingleton<NetworkTime>(out var networkTime) || !networkTime.IsFirstTimeFullyPredictingTick)
                return;
            if (!SystemAPI.TryGetSingletonBuffer<GainPointBuffer>(out var gainBuffer))
                return;
            var ecb = CreateCommandBuffer();
            var bossLookup = this.bossLookup;
            var enemyLookup = this.enemyLookup;
            var cattleLookup = this.cattleLookup;
            var mineableLookup = this.mineableLookup;
            var diggableLookup = this.diggableLookup;
            var lootLookup = this.lootLookup;
            var grantedLookup = this.grantedLookup;
            var discoverableLookup = this.discoverableLookup;
            var discoveredLookup = this.discoveredLookup;
            var transformedLookup = this.transformedLookup;
            var inventoryLookup = this.inventoryLookup;
            var zoneLookup = this.zoneLookup;
            var transformLookup = this.transformLookup;
            var currencyLookup = this.currencyLookup;
            var byTileLookup = this.byTileLookup;
            var tileLookup = CreateTileAccessor();
            Entities.ForEach((Entity entity, in GainPointCD gain, in KilledByPlayerCD killed) =>
            {
                ecb.AddComponent<PointsGrantedCD>(entity);
                var zone = gain.Zone;
                if (lootLookup.TryGetComponent(entity,out var loot) && ZoneLookup.TryFromLootTable(loot.lootTableID, out var lootZone))
                    zone = lootZone;
                else if (zone == byTileLookup && transformLookup.TryGetComponent(entity, out var trans))
                {
                    if (!ZoneLookup.TryResolveGround(tileLookup, trans.Position, out zone))
                        return;
                }
                if (gain.Way == ObtainWay.Chest)
                {
                    if (!discoverableLookup.HasComponent(entity) || discoveredLookup.HasComponent(entity))
                        return;
                }
                if (gain.Way == ObtainWay.Wall && discoveredLookup.HasComponent(entity))
                    return;
                gainBuffer.Add(new()
                {
                    Player = killed.playerEntity,
                    Way = gain.Way,
                    Zone = zone,
                });
            })
                .WithName("DropLootPointSystem")
                .WithBurst()
                .WithNone<PointsGrantedCD>()
                .Schedule();
            base.OnUpdate();
        }
    }
}
