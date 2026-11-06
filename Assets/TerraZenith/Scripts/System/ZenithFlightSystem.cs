using Assets.TerraZenith.Scripts.Component;
using Assets.TerraZenith.Scripts.Core;
using Pug.UnityExtensions;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace Assets.TerraZenith.Scripts.System
{
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileMovementSystem))]
    [UpdateBefore(typeof(ProjectileSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial class ZenithHitSystem : PugSimulationSystemBase
    {
        private float tickRate;
        private ComponentLookup<LocalTransform> transformLookup;
        private ComponentLookup<ExtraUpdateCD> exupdateLookup;

        protected override void OnCreate()
        {
            tickRate = 1f / PlatformConfiguration.Instance.SessionConfiguration.SimulationTickRate;
            transformLookup = SystemAPI.GetComponentLookup<LocalTransform>();
            exupdateLookup = SystemAPI.GetComponentLookup<ExtraUpdateCD>();
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var currentTick = networkTime.ServerTick;
            if (networkTime.IsPartialTick)
                return;   // partial tick 不推进

            var transLookup = transformLookup;
            var exupdateLookup = this.exupdateLookup;
            var tickRate = this.tickRate;
            var ecb = CreateCommandBuffer();
            Entities.ForEach((Entity entity, ref LocalTransform transform, ref LifeProgressCD killProgress, ref ZenithOrbitCD orbit,
                ref DynamicBuffer<ZenithTrail> trail, ref ProjectileCD proj, in OwnerReferenceCD owner) =>
            {
                int ticksElapsed = currentTick.TicksSince(orbit.LastUpdateTick);
                if (ticksElapsed == 0)
                    return;
                orbit.LastUpdateTick = currentTick;

                Entity ownerEntity = owner.owner;
                float longAxis = orbit.LongAxis;
                float2 offset = orbit.Offset;
                int updateCount = 1;
                if (exupdateLookup.TryGetComponent(entity, out var extraUpdate))
                    updateCount = extraUpdate.TotalUpdate;
                int totalUpdates = updateCount;
                float step = ticksElapsed * tickRate * ZenithBalance.NearFix(longAxis);
                var totalProgress = killProgress.Max;
                while (updateCount-- > 0)
                {
                    ref float progress = ref killProgress.Current;
                    progress += step;
                    ZenithBalance.CalculateOrbit(orbit, progress, totalProgress, totalUpdates, out float lifeRatio, out float ellipse, out _, out float2 relativePos);
                    trail.Add(new ZenithTrail { Pos = relativePos, CanDamage = ellipse < 1 });
                    if (lifeRatio >= 1)
                    {
                        ecb.SetComponentEnabled<EntityDestroyedCD>(entity, true);
                        return;
                    }

                    if (!transLookup.TryGetComponent(ownerEntity, out var ownerTransform))
                    {
                        transform.Position = proj.prevPrevPos.X0Y();
                        return;
                    }
                    float2 playerPos = ownerTransform.Position.xz;
                    float2 final = offset + playerPos;
                    transform.Position = final.X0Y();
                }
            })
                .WithName("ZenithOrbitSystem")
                .WithBurst()
                .WithNone<EntityDestroyedCD>()
                .Schedule();

            base.OnUpdate();
        }
    }
}