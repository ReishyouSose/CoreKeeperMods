using Assets.PointShop.Scripts.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Assets.PointShop.Scripts.Systems
{
    [UpdateInGroup(typeof(RunSimulationSystemGroup))]
    [UpdateAfter(typeof(EventTerminalSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class AlienPointSystem : PugSimulationSystemBase
    {
        private struct MarkAsAlienCD : IComponentData { }
        private static readonly FixedString64Bytes Alien = "Alien";
        private ComponentLookup<PlayerGhost> playerLookup;
        private ComponentLookup<LocalTransform> transLookup;
        private ComponentLookup<GainPointCD> gainLookup;
        private ComponentLookup<MarkAsAlienCD> alienLookup;
        protected override void OnCreate()
        {
            playerLookup = SystemAPI.GetComponentLookup<PlayerGhost>(true);
            transLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            gainLookup = SystemAPI.GetComponentLookup<GainPointCD>();
            alienLookup = SystemAPI.GetComponentLookup<MarkAsAlienCD>(true);
            RequireForUpdate<GainPointBuffer>();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            var ecb = CreateCommandBuffer();
            var gainLookup = this.gainLookup;
            var alienLookup = this.alienLookup;
            var alien = Alien;
            Entities.ForEach((in EnemySpawnerPlatformCD platform) =>
            {
                if (!platform.isCustomSceneSpawner)
                    return;
                var entity = platform.spawnedEntity;
                if (alienLookup.HasComponent(entity))
                    return;
                if (platform.enemyToSpawn is ObjectID.Mimite or ObjectID.OrbitalTurret)
                {
                    var option = gainLookup.GetRefRWOptional(entity);
                    if (!option.IsValid)
                        return;
                    option.ValueRW.Zone = alien;
                    ecb.AddComponent<MarkAsAlienCD>(entity);
                }
            })
                .WithName("AlienPointEnemySpawner")
                .WithBurst()
                .Schedule();

            if (!SystemAPI.TryGetSingletonBuffer<GainPointBuffer>(out var gains))
            {
                base.OnUpdate();
                return;
            }
            var playerLookup = this.playerLookup;
            var transLookup = this.transLookup;
            Entities.ForEach((Entity e, in EventTerminalSystem.EndTerminalEvent end, in EventTerminalCD terminal,
               in ImmunityZoneCD immune, in DynamicBuffer<NearbyEntitiesBufferCD> nearby, in LocalTransform transform) =>
            {
                if (!end.completed)
                    return;
                ecb.AddComponent<PointsGrantedCD>(e);
                foreach (var near in nearby)
                {
                    var entity = near.entity;
                    if (!playerLookup.HasComponent(entity))
                        continue;
                    if (!transLookup.TryGetComponent(entity, out var trans))
                        continue;
                    if (math.distancesq(transform.Position, trans.Position) > immune.radiusSq)
                        continue;
                    gains.Add(new GainPointBuffer
                    {
                        Zone = Alien,
                        Player = near.entity,
                        Way = ObtainWay.Chest
                    });
                }
            })
                .WithName("AlienPointTerminal")
                .WithBurst()
                .WithNone<PointsGrantedCD>()
                .Schedule();
            base.OnUpdate();
        }
    }
}
