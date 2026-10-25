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
    public partial class ZenithFlightSystem : PugSimulationSystemBase
    {
        private uint tickRate;
        private ComponentLookup<LocalTransform> transformLookup;

        protected override void OnCreate()
        {
            tickRate = (uint)PlatformConfiguration.Instance.SessionConfiguration.SimulationTickRate;
            transformLookup = GetComponentLookup<LocalTransform>();
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var currentTick = networkTime.ServerTick;
            if (networkTime.IsPartialTick)
                return;   // partial tick 不推进

            var owners = transformLookup;
            uint tickRate = this.tickRate;
            float secondsPerTick = 1f / tickRate;
            var ecb = CreateCommandBuffer();

            int updateCount = ZenithBalance.UpdateCount;
            int killTime = ZenithBalance.TimeLeft * updateCount;
            float sampler = 1f / 60f;

            Entities.ForEach((Entity entity, ref LocalTransform transform, ref ZenithOrbitCD orbit,
                ref ProjectileCD proj, ref DynamicBuffer<ZenithTrail> trail, in OwnerReferenceCD owner) =>
            {
                // tick 差值：本 tick 应推进几次
                if (!orbit.LastUpdateTick.IsValid)
                {
                    orbit.LastUpdateTick = currentTick;
                    return;
                }
                int ticksElapsed = currentTick.TicksSince(orbit.LastUpdateTick);
                if (ticksElapsed == 0)
                    return;
                orbit.LastUpdateTick = currentTick;

                Entity ownerEntity = owner.owner;
                float longAxis = orbit.LongAxis;
                float shortAxis = orbit.ShortAxis;
                float2 offset = orbit.Offset;
                float near = math.clamp(longAxis / 14f, 0, 1);

                // 图形类：step = lerp(2, 0.7, near) * DeltaTime
                // ECS：DeltaTime → secondsPerTick * ticksElapsed
                float step = math.lerp(2f, 0.7f, near) * (secondsPerTick * ticksElapsed) * updateCount * orbit.Scale;

                ref float timer = ref orbit.Timer;
                float targetTimer = math.min(timer + step, killTime);
                float life = 0;
                // 照图形类：每 tick 推进 updateCount 次（dense 时 3 倍）
                while (timer < targetTimer)
                {
                    timer = math.min(timer + sampler, killTime);
                    life = timer / killTime;

                    float progress = math.clamp(life / 0.5f, 0f, 1f);
                    float x = progress * math.PI * 2f;
                    math.sincos(x, out var sin, out var cos);
                    float2 basePos = new(cos, (shortAxis > 0 ? 1 : -1) * sin);
                    float2 localPos = basePos * new float2(longAxis, math.abs(shortAxis));

                    float lead = 1f - math.clamp(progress / 0.5f, 0f, 1f);
                    float2 leadOffset = lead * new float2(-longAxis * 0.1f, shortAxis * 0.3f);
                    localPos += leadOffset;

                    float2 worldPos = ZenithBalance.Rotate(localPos, orbit.Rotation);
                    trail.Add(new ZenithTrail { Pos = worldPos });
                }
                if (timer >= killTime)
                {
                    ecb.SetComponentEnabled<EntityDestroyedCD>(entity, true);
                    return;
                }

                if (!owners.TryGetComponent(ownerEntity, out var ownerTransform))
                {
                    transform.Position = proj.prevPrevPos.X0Y();
                    return;
                }
                float2 playerPos = ownerTransform.Position.xz;
                transform.Position = (offset + playerPos).X0Y();
            })
                .WithName("ZenithOrbitSystem")
                .WithBurst()
                .WithNone<EntityDestroyedCD>()
                .Schedule();

            base.OnUpdate();
        }
    }
}