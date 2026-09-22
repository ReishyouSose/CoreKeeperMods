using Assets.CoreEnhance.Scripts.Components;
using Assets.CoreEnhance.Scripts.Cores;
using Assets.CoreEnhance.Scripts.Patchs;
using CoreLib.Data.Configuration;
using PlayerState;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Assets.CoreEnhance.Scripts.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class AccelerateCommonSystem : PugSimulationSystemBase
    {
        private int tickRate;
        protected override void OnCreate()
        {
            tickRate = PlatformConfiguration.Instance.SessionConfiguration.SimulationTickRate;
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            var ecb = CreateCommandBuffer();
            var utick = (uint)tickRate;
            Accelerate_Merchant();
            Accelerate_Casting(utick);
            Accelerate_Portal(ecb);
            base.OnUpdate();
        }
        private void Accelerate_Merchant()
        {
            if (!EnhanceConfig.TryGetValue(EnhanceCategory.Merchant, out ConfigEntry<int> value))
                return;
            int timeLimit = value.Value;
            if (timeLimit == 0)
            {
                Entities.ForEach((DynamicBuffer<ContainedObjectsBuffer> inventoryBuffer,
                    ref MerchantCD merchantCD, ref ObjectDataCD objectData) =>
                {
                    int count = 0;
                    foreach (var item in inventoryBuffer)
                    {
                        if (item.objectID != ObjectID.None)
                        {
                            count++;
                        }
                    }
                    if (count != merchantCD.previousAmountOfItems)
                    {
                        objectData.amount = 0;
                    }
                })
                    .WithName("Accelerate_Merchant_Immediate")
                    .WithAll<StateInfoCD>()
                    .WithBurst()
                    .Schedule();
            }
            else
            {
                int time = math.max(180, timeLimit);
                Entities.ForEach((ref ObjectDataCD objectData) =>
                {
                    if (objectData.amount > time)
                    {
                        objectData.amount = time;
                    }
                })
                    .WithName("Accelerate_Merchant_Reduce")
                    .WithAll<MerchantCD>()
                    .WithAll<StateInfoCD>()
                    .WithBurst()
                    .Schedule();
            }
        }
        private void Accelerate_Casting(uint tickRate)
        {
            if (!EnhanceConfig.IsEnable(EnhanceCategory.Casting))
                return;
            Entities.ForEach((ref CastingStateCD casting) =>
            {
                casting.castTimer.SetTargetTicks(0, tickRate);
            })
                .WithName("Accelerate_Casting")
                .WithBurst()
                .Schedule();
        }
        private void Accelerate_Portal(EntityCommandBuffer ecb)
        {
            if (!EnhanceConfig.IsEnable(EnhanceCategory.Portal))
                return;
            Entities.ForEach((Entity e, ref ObjectDataCD objData) =>
            {
                objData.amount = 1200;
                ecb.AddComponent<ProcessedTagCD>(e);
            })
                .WithName("Accelerate_Portal")
                .WithAll<PortalCD>()
                .WithNone<WayPointCD>()
                .WithNone<ProcessedTagCD>()
                .WithBurst()
                .Schedule();

            Entities.ForEach((Entity e, ref ObjectDataCD objData, in WayPointCD wayPoint, in DistanceToPlayerCD dis) =>
            {
                float minDis = dis.minDistanceSq;
                if (minDis > 0 && minDis <= wayPoint.distanceToActivateSQ)
                {
                    objData.amount = 600;
                    ecb.AddComponent<ProcessedTagCD>(e);
                }
            })
                .WithName("Accelerate_WayPoint")
                .WithNone<ProcessedTagCD>()
                .WithBurst()
                .Schedule();
        }
    }
}
