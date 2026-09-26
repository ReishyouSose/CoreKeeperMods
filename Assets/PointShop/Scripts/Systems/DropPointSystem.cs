using Assets.PointShop.Scripts.Systems;
using CoreLib.Util.Extension;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace Assets.PointShop.Scripts.Systems
{

    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    [UpdateAfter(typeof(DropLootSystem))]
    public partial class DropPointSystem : PugSimulationSystemBase
    {
        protected override void OnCreate()
        {
            NeedDatabase();
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            var ecb = CreateCommandBuffer();
            var database = this.database;
            var coin = PointShop.Coin;
            Entities.ForEach((Entity e, in DropPointCD drop, in LocalTransform local) =>
            {
                ecb.RemoveComponent<DropPointCD>(e);
                EntityUtility.CreateAndDropItem(coin, 0, drop.Value, local.Position, Entity.Null, database, ecb);
            })
                .WithName("DropPoint")
                .WithAll<StartDroppingLootCD>()
                .WithNone<DontDropLootCD>()
                .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabledEntities)
                .WithBurst()
                .Schedule();
            base.OnUpdate();
        }
        public static void AddPointDrop(Entity entity, GameObject authoringData, EntityManager entityManager)
        {
            int value = authoringData.GetEntityObjectID()
            switch
            {
                ObjectID.SlimeBoss => 1,
                ObjectID.BossLarva => 2,
                ObjectID.KingSlime => 3,
                ObjectID.LarvaHiveBoss => 3,
                ObjectID.ShamanBoss => 4,
                ObjectID.BirdBoss => 10,
                ObjectID.PoisonSlimeBoss => 8,
                ObjectID.OctopusBoss => 20,
                ObjectID.SlipperySlimeBoss => 12,
                ObjectID.ScarabBoss => 30,
                ObjectID.LavaSlimeBoss => 40,
                ObjectID.SnakeBossSegment => 50,
                ObjectID.HydraBossNature => 40,
                ObjectID.HydraBossSea => 60,
                ObjectID.HydraBossDesert => 80,
                ObjectID.CoreBoss => 100,
                ObjectID.GiantCicadaBoss => 90,
                ObjectID.WallBoss => 200,
                ObjectID.RobotBoss => 120,
                ObjectID.HydraBossVoid => 150,
                _ => 0
            };
            if (value <= 0)
                return;
            entityManager.AddComponentData(entity, new DropPointCD() { Value = value });
        }
    }
}
