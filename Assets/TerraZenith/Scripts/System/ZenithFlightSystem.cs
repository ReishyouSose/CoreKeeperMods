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
        private ComponentLookup<ContinouslyDamagingProjectileCD> cdLookup;
        private ComponentLookup<ProjectileSetupCD> setupLookup;
        private ComponentLookup<EntityDestroyedCD> destroyLookup;
        private ComponentLookup<ProjectileCD> projLookup;

        protected override void OnCreate()
        {
            tickRate = (uint)PlatformConfiguration.Instance.SessionConfiguration.SimulationTickRate;
            transformLookup = SystemAPI.GetComponentLookup<LocalTransform>();
            cdLookup = SystemAPI.GetComponentLookup<ContinouslyDamagingProjectileCD>();
            setupLookup = SystemAPI.GetComponentLookup<ProjectileSetupCD>();
            destroyLookup = SystemAPI.GetComponentLookup<EntityDestroyedCD>();
            projLookup = SystemAPI.GetComponentLookup<ProjectileCD>();
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var currentTick = networkTime.ServerTick;
            if (networkTime.IsPartialTick)
                return;   // partial tick 不推进

            var transLookup = transformLookup;
            var cdLookup = this.cdLookup;
            var setupLookup = this.setupLookup;
            var projLookup = this.projLookup;
            var destroyLookup = this.destroyLookup;
            uint tickRate = this.tickRate;
            float secondsPerTick = 1f / tickRate;
            var ecb = CreateCommandBuffer();

            int updateCount = ZenithBalance.UpdateCount;
            int killTime = ZenithBalance.TimeLeft * updateCount;
            float sampler = World.Time.fixedDeltaTime;/* 1f / 60f*/

            Entities.ForEach((Entity entity, ref LocalTransform transform, ref ZenithOrbitCD orbit, ref DynamicBuffer<ZenithTrail> trail,
                  ref ProjectileCD proj, in DynamicBuffer<ZenithFakeProjBuffer> fakes, in ProjectileSetupCD setup, in OwnerReferenceCD owner) =>
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

                // 图形类：step = lerp(2, 0.7, near) * DeltaTime
                // ECS：DeltaTime → secondsPerTick * ticksElapsed
                float step = secondsPerTick * ticksElapsed * updateCount * ZenithBalance.NearFix(longAxis);

                ref float timer = ref orbit.Timer;
                float realKillTime = killTime;
                float targetTimer = math.min(timer + step, realKillTime);
                float life = 0;
                int l = trail.Length - 1;
                // 照图形类：每 tick 推进 updateCount 次（dense 时 3 倍）
                if (trail.Length == 0)
                {
                    timer -= sampler;
                }
                while (timer < targetTimer)
                {
                    timer = math.min(timer + sampler, realKillTime);
                    life = timer / realKillTime;

                    float progress = math.clamp(life / 0.5f, 0f, 1f);
                    float x = progress * math.PI * 2f;
                    math.sincos(x, out var sin, out var cos);
                    float2 basePos = new(cos, (shortAxis > 0 ? 1 : -1) * sin);
                    float2 localPos = basePos * new float2(longAxis, math.abs(shortAxis));

                    float lead = 1f - math.clamp(progress / 0.5f, 0f, 1f);
                    float2 leadOffset = lead * new float2(-longAxis * 0.1f, shortAxis * 0.3f);
                    localPos += leadOffset;

                    float2 worldPos = ZenithBalance.Rotate(localPos, orbit.Rotation);
                    //if (life <= 0.5f)
                    trail.Add(new ZenithTrail { Pos = worldPos, CanDamage = life <= 0.5f });
                }
                int k = fakes.Length;
                if (timer >= realKillTime)
                {
                    ecb.SetComponentEnabled<EntityDestroyedCD>(entity, true);
                    /*for (int i = 0; i < k; i++)
                    {
                        ecb.SetComponentEnabled<EntityDestroyedCD>(fakes[i].Entity, true);
                    }*/
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
                /*TickTimer cdTick = default;
                bool anyHit = false;
                for (int i = 0; i < k; i++)
                {
                    var fake = fakes[i].Entity;
                    if (!cdLookup.TryGetComponent(fake, out var acd))
                        continue;
                    var cd = acd.attackCooldown;
                    if (cd.isRunning && !cd.IsTimerElapsed(currentTick))
                    {
                        cdTick = cd;
                        Debug.Log("share cooldown");
                        anyHit = true;
                        break;
                    }
                }*/
                // 在 ZenithFlightSystem 的循环中
                /*int j = 0;
                l = trail.Length - 1;
                for (int i = l; i >= 0; i--)
                {
                    if (j >= k)
                        break;

                    // 当前帧的目标位置（这一段的终点）
                    var trailBuffer = trail[i];
                    float2 partPos = final + trailBuffer.Pos;
                    var fake = fakes[j++].Entity;
                    if (fake == Entity.Null)
                        continue;

                    // 1. 尝试从拖尾点里找“上一段”的位置
                    // 注意：这里的 i 是当前拖尾点的索引，i - 1 就是轨迹上的上一个点
                    float2 prevPartPos = partPos; // 默认跟当前点一样
                    if (i > 0)
                    {
                        prevPartPos = final + trail[i - 1].Pos;
                    }

                    // 2. 激活伤害（如果是刚激活，为了避免第一帧跨屏幕扫，直接强制历史为当前位置）
                    ref var fakeSetup = ref setupLookup.GetRefRW(fake).ValueRW;
                    // 3. 强行填充历史数据
                    ref var fakeProj = ref projLookup.GetRefRW(fake).ValueRW;
                    ref var fakeTrans = ref transLookup.GetRefRW(fake).ValueRW;
                    if (trailBuffer.CanDamage)
                    {
                        if (fakeSetup.damage == 0)
                        {
                            fakeSetup.damage = setup.damage;
                            // 刚刚激活，历史位置和当前位置重合，退化为点检测，避免乱扫
                            fakeProj.prevPrevPos = fakeProj.prevPos = partPos;
                        }
                        else
                        {
                            // 正常情况：用拖尾点填充历史位置，形成连续射线
                            fakeProj.prevPrevPos = fakeProj.prevPos; // 上上帧的历史
                            fakeProj.prevPos = prevPartPos;          // 上帧用【拖尾点的上一个点】填充
                        }
                    }
                    else
                    {
                        if (fakeSetup.damage > 0)
                        {
                            fakeSetup.damage = 0;
                        }
                    }

                    // 4. 将假体的真实物理位置设为这一帧的拖尾点
                    fakeTrans.Position = partPos.X0Y();
                }*/
            })
                .WithName("ZenithOrbitSystem")
                .WithBurst()
                .WithNone<EntityDestroyedCD>()
                .Schedule();

            base.OnUpdate();
        }
    }
}