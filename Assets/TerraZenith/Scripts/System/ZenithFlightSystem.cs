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

        protected override void OnCreate()
        {
            transformLookup = GetComponentLookup<LocalTransform>();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            var owners = transformLookup;
            float dt = SystemAPI.Time.fixedDeltaTime;
            int extraUpdates = ZenithBalance.UpdateCount;
            int killTime = ZenithBalance.TimeLeft * extraUpdates;
            var ecb = CreateCommandBuffer();
            Entities.ForEach((Entity entity, ref LocalTransform transform, ref ZenithOrbitCD orbit, ref ProjectileCD proj,
                ref DynamicBuffer<ZenithTrail> trail, in OwnerReferenceCD owner) =>
            {
                Entity ownerEntity = owner.owner;
                float longAxis = orbit.LongAxis;
                float shortAxis = orbit.ShortAxis;
                float2 offset = orbit.Offset;
                float near = math.clamp(longAxis / 14f, 0, 1);
                ref float timer = ref orbit.Timer;
                float scale = 1f;
                float step = math.lerp(2f, 0.7f, near) * dt * scale;

                for (int i = 0; i < extraUpdates; i++)
                {
                    timer = math.min(timer + step, killTime);
                    float life = timer / killTime;

                    //在一半的时间内就完成一圈，剩余的时间给拖尾
                    float progress = math.clamp(life / 0.5f, 0f, 1f);

                    if (progress <= 1f)
                    {
                        float x = progress * math.PI * 2f;
                        // 位置：用 x 算椭圆点
                        math.sincos(x, out var sin, out var cos);
                        float2 basePos = new(cos, (shortAxis > 0 ? 1 : -1) * sin);
                        float2 localPos = basePos * new float2(longAxis, math.abs(shortAxis));

                        // lead 拉扯
                        float lead = 1f - math.clamp(progress / 0.5f, 0f, 1f);
                        float2 leadOffset = lead * new float2(-longAxis * 0.1f, shortAxis * 0.3f);
                        localPos += leadOffset;

                        //计算拉扯后应用方向修正
                        float2 worldPos = ZenithBalance.Rotate(localPos, orbit.Rotation);

                        //参数角也需要方向修正
                        trail.Add(new ZenithTrail { Pos = worldPos });
                    }

                    if (timer >= killTime)
                    {
                        ecb.SetComponentEnabled<EntityDestroyedCD>(entity, true);
                        break;
                    }
                }
                if (!owners.TryGetComponent(ownerEntity, out var ownerTransform))
                {
                    transform.Position = proj.prevPrevPos.X0Y();
                    return;
                }
                float2 playerPos = ownerTransform.Position.xz;

                // 7. 强行覆盖实体位置，让 ECS 更新到世界中
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