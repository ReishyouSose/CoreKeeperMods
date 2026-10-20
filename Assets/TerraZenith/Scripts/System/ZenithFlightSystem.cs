using Assets.TerraZenith.Scripts.Component;
using Assets.TerraZenith.Scripts.Core;
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
        private ComponentLookup<LocalTransform> transformLookup;
        private ComponentLookup<EntityDestroyedCD> destroyLookup;
        private ComponentLookup<MovementSpeedCD> moveLookup;

        protected override void OnCreate()
        {
            transformLookup = GetComponentLookup<LocalTransform>();
            destroyLookup = GetComponentLookup<EntityDestroyedCD>();
            moveLookup = SystemAPI.GetComponentLookup<MovementSpeedCD>();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            var owners = transformLookup;
            var destroyed = destroyLookup;
            var moveLookup = this.moveLookup;
            float dt = SystemAPI.Time.fixedDeltaTime;
            int extraUpdates = ZenithBalance.ExtraUpdates;
            int timeLeft = ZenithBalance.TimeLeft;
            var ecb = CreateCommandBuffer();
            Entities.ForEach((Entity entity, ref LocalTransform transform, /*ref ProjectileCD projectile,*/ ref ZenithOrbitCD orbit,
                ref DynamicBuffer<ZenithTrail> trail, in OwnerReferenceCD owner) =>
            {
                /*if (moveLookup.HasComponent(entity))
                    ecb.RemoveComponent<MovementSpeedCD>(entity);*/
                Entity ownerEntity = owner.owner;
                if (!owners.TryGetComponent(ownerEntity, out var ownerTransform))
                {
                    destroyed.SetComponentEnabled(entity, true);
                    return;
                }

                float longAxis = orbit.LongAxis;
                float shortAxis = orbit.ShortAxis;
                float2 offset = orbit.Offset;
                float2 playerPos = ownerTransform.Position.xz;
                float near = math.clamp(longAxis / 14f, 0, 1);
                ref float timer = ref orbit.Timer;
                float speedScale = 1f;
                float budget = math.lerp(2f, 0.7f, near) * dt * speedScale;
                float step = budget / extraUpdates;
                float2 targetPos = float2.zero;
                bool alive = true;

                for (int i = 0; i < extraUpdates && alive; i++)
                {
                    timer += step;
                    if (timer >= timeLeft)
                    {
                        alive = false;
                        break;
                    }
                    float x = timer * math.PI;
                    float2 basePos = new(math.cos(x) * ((timer > 1 ? 1.5f : 0) + longAxis), math.sin(x) * shortAxis);
                    basePos = Rotate(basePos, orbit.Rotation + math.PI);

                    // 拉扯计算
                    float signX = math.sign(offset.x);
                    float lead = 1f - math.clamp(timer / 0.5f, 0f, 1f);
                    float2 leadOffset = lead * new float2(signX * -longAxis * 0.1f, -shortAxis * 0.3f);
                    float2 finalPos = basePos + leadOffset;
                    targetPos = finalPos;
                    trail.Add(new ZenithTrail()
                    {
                        Pos = finalPos,
                        Timer = timer
                    });
                }

                if (!alive)
                {
                    //destroyed.SetComponentEnabled(entity, true);
                    return;
                }

                // 7. 强行覆盖实体位置，让 ECS 更新到世界中
                //projectile.SetDirection(targetPos);
                targetPos += offset + playerPos;
                transform.Position = new float3(targetPos.x, transform.Position.y, targetPos.y);
            })
                .WithName("ZenithOrbitSystem")
                .WithBurst()
                .Schedule();

            base.OnUpdate();
        }

        private static float2 Rotate(float2 value, float angle)
        {
            math.sincos(angle, out float s, out float c);
            return new float2(c * value.x - s * value.y, s * value.x + c * value.y);
        }
    }
}