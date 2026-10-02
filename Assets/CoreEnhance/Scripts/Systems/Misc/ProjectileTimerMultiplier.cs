using Assets.CoreEnhance.Scripts.Cores;
using PlayerEquipment;
using Pug.UnityExtensions;
using Unity.Entities;
using UnityEngine;

namespace Assets.CoreEnhance.Scripts.Systems.Misc
{
    public struct OriginTimerCD : IComponentData
    {
        public PlatformDependentValue<float> Timer;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(EquipmentUpdateSystemGroup))]
    [UpdateBefore(typeof(EquipmentUpdateSystem))]
    public partial class ProjectileTimerMultiplierSystem : PugSimulationSystemBase
    {
        private float timer;
        private ComponentLookup<OriginTimerCD> originTimerLookup;
        private ComponentLookup<RangeWeaponCD> rangeWeaponLookup;
        private ComponentLookup<DestroyTimerCD> timerLookup;
        protected override void OnCreate()
        {
            originTimerLookup = SystemAPI.GetComponentLookup<OriginTimerCD>();
            rangeWeaponLookup = SystemAPI.GetComponentLookup<RangeWeaponCD>();
            timerLookup = SystemAPI.GetComponentLookup<DestroyTimerCD>();
            NeedDatabase();
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            if (timer < 1)
            {
                timer += World.Time.DeltaTime;
                return;
            }
            timer = 0;
            var database = this.database;
            var originTimerLookup = this.originTimerLookup;
            var rangeWeaponLookup = this.rangeWeaponLookup;
            var timerLookup = this.timerLookup;
            float multiplier = EnhanceConfig.TryGetValue<float>(EnhanceCategory.ProjectileTimerMultiple, out var value) ? value.Value : 1f;
            uint tickRate = (uint)PlatformConfiguration.Instance.SessionConfiguration.SimulationTickRate;
            Entities.ForEach((DynamicBuffer<ContainedObjectsBuffer> inv, in EquippedObjectCD held) =>
            {
                var index = held.equippedSlotIndex;
                Entity e = PugDatabase.GetPrimaryPrefabEntity(inv[index].objectID, database);
                if (!rangeWeaponLookup.TryGetComponent(e, out var range))
                    return;
                MultProjectileTimeLeft(range.projectileID, database, originTimerLookup, timerLookup, multiplier, tickRate);
                MultProjectileTimeLeft(range.windupProjectileID, database, originTimerLookup, timerLookup, multiplier, tickRate);
            })
                .WithName("ProjectileTimerModify")
                .WithBurst()
                .Schedule();
            base.OnUpdate();
        }
        private static void MultProjectileTimeLeft(ObjectID objID, BlobAssetReference<PugDatabase.PugDatabaseBank> database,
            ComponentLookup<OriginTimerCD> originTimerLookup, ComponentLookup<DestroyTimerCD> timerLookup, float multiplier, uint tickRate)
        {
            Entity e = PugDatabase.GetPrimaryPrefabEntity(objID, database);
            if (!originTimerLookup.TryGetComponent(e, out var origin))
                return;
            ref var timer = ref timerLookup.GetRefRW(e).ValueRW;
            var originTimer = origin.Timer.GetValueForCurrentPlatform();
            var timerTick = timer.timer.targetTicks;
            var newTick = NetworkTimeUtilities.SecondsToTicks(originTimer * multiplier, tickRate);
            if (timerTick != newTick)
            {
                timer.timer = new(newTick);
            }
        }
        public static void MarkProjectileTileLeft(Entity entity, GameObject authoringData, EntityManager manager)
        {
            if (!authoringData.TryGetComponent<ProjectileAuthoring>(out _))
                return;
            if (!authoringData.TryGetComponent<DestroyTimerAuthoring>(out var timer))
                return;
            manager.AddComponentData(entity, new OriginTimerCD()
            {
                Timer = timer.lifetime
            });
        }
    }
}
