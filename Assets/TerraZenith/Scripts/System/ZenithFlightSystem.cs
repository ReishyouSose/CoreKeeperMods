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
            int killTime = timeLeft * extraUpdates;
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
                float2 playerPos = ownerTransform.Position.xz + new float2(0, 0.5f);
                float near = math.clamp(longAxis / 14f, 0, 1);
                ref float timer = ref orbit.Timer;
                float scale = 0.5f;
                float step = math.lerp(2f, 0.7f, near) * dt * scale;
                int sign = orbit.Sign;

                for (int i = 0; i < extraUpdates; i++)
                {
                    timer += step;
                    // 在循环内：计算世界坐标
                    float progress = math.clamp(math.lerp(0, 1, timer / timeLeft), 0, 1);
                    float x = progress * math.PI * 2;
                    // 局部椭圆坐标（原点为椭圆中心，长轴为 X 轴）
                    float2 localPos = new(math.cos(x) * longAxis, math.sin(x) * shortAxis);

                    // 如果保留拉扯感，要加在局部坐标里
                    float lead = 1f - math.clamp(timer / 0.5f, 0f, 1f);
                    float2 leadOffset = lead * new float2(sign * -longAxis * 0.1f, -shortAxis * 0.3f);
                    //localPos += lead;

                    // 局部坐标 -> 旋转
                    float2 worldPos = ZenithBalance.Rotate(localPos, orbit.Rotation);

                    trail.Add(new ZenithTrail { Pos = worldPos, Progress = timer }); // 如果之前是相对，请改成 worldPos - playerPos

                    // 【核心修正】循环外：实体本身要贴在椭圆中心
                    transform.Position = (playerPos + offset).X0Y();
                }

                if (timer >= killTime)
                {
                    destroyed.SetComponentEnabled(entity, true);
                    return;
                }

                // 7. 强行覆盖实体位置，让 ECS 更新到世界中
                //projectile.SetDirection(targetPos);
                transform.Position = (offset + playerPos).X0Y();
            })
                .WithName("ZenithOrbitSystem")
                .WithBurst()
                .Schedule();

            base.OnUpdate();
        }
    }
}