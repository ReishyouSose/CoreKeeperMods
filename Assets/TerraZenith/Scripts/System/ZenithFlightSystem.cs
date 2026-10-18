using Assets.TerraZenith.Scripts.Component;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

// 天顶剑的服务器逻辑。发射对照玩家挥砍时连续生成 FinalFractal，飞行对照 Projectile.AI_182_FinalFractal。
namespace Assets.TerraZenith.Scripts.System
{
    // 轨道。放在弹幕位移之后、弹幕结算之前。
    // 速度、椭圆、剑序号在预测生成时已经写好。两端各自往下积分，不再等快照。
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileMovementSystem))]
    [UpdateBefore(typeof(ProjectileSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial class ZenithFlightSystem : PugSimulationSystemBase
    {
        private ComponentLookup<LocalTransform> transformLookup;
        private ComponentLookup<EntityDestroyedCD> destroyLookup;

        protected override void OnCreate()
        {
            transformLookup = GetComponentLookup<LocalTransform>();
            destroyLookup = GetComponentLookup<EntityDestroyedCD>();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            transformLookup.Update(this);
            destroyLookup.Update(this);

            var owners = transformLookup;
            var destroyed = destroyLookup;
            float dt = SystemAPI.Time.fixedDeltaTime;

            Entities.ForEach((Entity entity, ref ZenithFlightCD flight, ref LocalTransform transform, ref ProjectileCD projectile, ref DynamicBuffer<ZenithTrail> trail, in OwnerReferenceCD owner) =>
            {
                if (flight.initialized == 0)
                    return;

                Entity ownerEntity = owner.owner;
                if (!owners.TryGetComponent(ownerEntity, out var ownerTransform))
                {
                    destroyed.SetComponentEnabled(entity, true);
                    return;
                }

                float2 playerPos = ownerTransform.Position.xz;
                float2 velocity = flight.velocity;
                float ellipse = flight.ellipse;
                float distancePixels = math.length(velocity) * 2f * 16f;
                float closeness = Saturate((900f - distancePixels) / 900f);
                float age = flight.age;
                float2 pos = transform.Position.xz;
                float bladeAngle = 0f;
                bool alive = true;
                float2 playerDelta = flight.hasTrailPlayer != 0 ? playerPos - flight.trailPlayer : float2.zero;
                float height = transform.Position.y;

                // ExtraUpdates 对照泰拉 extraUpdates = 1，也就是每拍多算一次。
                // 预算先乘 SpeedScale，再除以 ExtraUpdates，总路程还是原来那一拍，只是刀光多一个采样。
                // SpeedScale 是推进倍率，觉得快就调小。
                const int ExtraUpdates = 2;
                const float SpeedScale = 0.5f;
                float budget = math.lerp(0.7f, 2f, closeness) * dt * 60f * SpeedScale;
                float step = budget / ExtraUpdates;
                bool firstStep = true;
                for (int i = 0; i < ExtraUpdates && alive; i++)
                {
                    age += step;
                    if (age >= 120f)
                    {
                        alive = false;
                        break;
                    }
                    SampleOrbit(age, velocity, ellipse, closeness, playerPos, out pos, out bladeAngle, out float spin);
                    PushTrail(trail, new float3(pos.x, height, pos.y), bladeAngle, spin, age, playerDelta, firstStep);
                    firstStep = false;
                }

                flight.age = age;
                flight.trailPlayer = playerPos;
                flight.hasTrailPlayer = 1;
                if (!alive)
                {
                    destroyed.SetComponentEnabled(entity, true);
                    return;
                }

                transform.Position = new float3(pos.x, transform.Position.y, pos.y);
                projectile.SetDirection(new float3(math.cos(bladeAngle), 0f, math.sin(bladeAngle)));
            }).Schedule();

            base.OnUpdate();
        }

        // 对照 AI_182。age 是唯一的进度，同一组 velocity、ellipse 在两端算出同一个点。
        private static void SampleOrbit(float age, float2 velocity, float ellipse, float closeness, float2 playerPos, out float2 pos, out float bladeAngle, out float spin)
        {
            spin = Saturate(age / 60f);
            float aimAngle = math.atan2(velocity.y, velocity.x);
            float signX = velocity.x > 0f ? 1f : -1f;
            float sweep = math.PI + signX * spin * (math.PI * 2f);
            float radius = math.length(velocity) + Saturate((spin - 0.5f) / 0.5f) * (40f / 16f);
            if (radius < 60f / 16f)
                radius = 60f / 16f;
            float2 spun = new float2(math.cos(sweep), math.sin(sweep)) * new float2(radius, ellipse * math.lerp(2f, 1f, closeness));
            float2 orbit = playerPos + velocity + Rotate(spun, aimAngle);
            float lead = 1f - Saturate(spin / 0.5f);
            pos = orbit + lead * new float2(signX * -radius * 0.1f, -ellipse * 0.3f);
            bladeAngle = ellipse < 0f ? math.PI - signX * spin * (math.PI * 2f) + aimAngle : sweep + aimAngle;
        }

        // 对照 TrailingMode 4：旧点先整体后移，这一拍的第一步再加玩家地面位移，最新位置写在下标 0。
        private static void PushTrail(DynamicBuffer<ZenithTrail> trail, float3 position, float angle, float spin, float age, float2 playerDelta, bool applyPlayerDelta)
        {
            if (trail.Length < ZenithTrail.Capacity)
                trail.Add(default);

            for (int i = trail.Length - 1; i > 0; i--)
            {
                ZenithTrail point = trail[i - 1];
                if (applyPlayerDelta)
                    point.position += new float3(playerDelta.x, 0f, playerDelta.y);
                trail[i] = point;
            }

            trail[0] = new ZenithTrail { position = position, angle = angle, spin = spin, age = age };
        }

        private static float Saturate(float value)
        {
            return math.saturate(value);
        }

        private static float2 Rotate(float2 value, float angle)
        {
            math.sincos(angle, out float s, out float c);
            return new float2(c * value.x - s * value.y, s * value.x + c * value.y);
        }
    }
}
