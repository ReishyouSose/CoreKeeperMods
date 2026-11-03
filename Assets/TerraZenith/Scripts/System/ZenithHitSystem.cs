using Assets.TerraZenith.Scripts.Component;
using Pug.UnityExtensions;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace Assets.TerraZenith.Scripts.System
{
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(ZenithFlightSystem))]
    [UpdateBefore(typeof(ProjectileSystem))]   // 关键：同帧结算
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial class ZenitHitSystem : PugSimulationSystemBase
    {
        private struct ZenithHitInfo : IComparable<ZenithHitInfo>
        {
            public float3 Pos;

            // 从左到右、从上到下：x 升序，x 相同则 z 升序
            public readonly int CompareTo(ZenithHitInfo other)
            {
                if (Pos.x < other.Pos.x)
                    return -1;
                if (Pos.x > other.Pos.x)
                    return 1;
                if (Pos.z < other.Pos.z)
                    return -1;
                if (Pos.z > other.Pos.z)
                    return 1;
                return 0;
            }
        }
        private ComponentLookup<FactionCD> factionLookup;
        private ComponentLookup<BehaviourTagsCD> behaviourLookup;
        private ComponentLookup<EnemyCD> enemyLookup;
        private ComponentLookup<LocalTransform> transLookup;
        private ComponentLookup<HealthCD> healthLookup;
        private ComponentLookup<EntityPartCD> partLookup;
        private ComponentLookup<UseLagCompensationCD> useLagCompLookup;
        private ComponentLookup<ObjectDataCD> objLookup;
        private BufferLookup<SummarizedConditionsBuffer> conditionsLookup;
        private BufferLookup<SummarizedConditionEffectsBuffer> effectBufferLookup;

        private PhysicsWorldHistorySingleton physicsHistory;
        private AttackSystem.Helper attackHelper;
        private TileAccessor tileAccessor;
        private uint tickRate;
        private static float3 up = math.up() * 0.5f;
        private CollisionWorld collision;

        protected override void OnCreate()
        {
            factionLookup = SystemAPI.GetComponentLookup<FactionCD>(true);
            behaviourLookup = SystemAPI.GetComponentLookup<BehaviourTagsCD>(true);
            enemyLookup = SystemAPI.GetComponentLookup<EnemyCD>(true);
            transLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            healthLookup = SystemAPI.GetComponentLookup<HealthCD>(true);
            partLookup = SystemAPI.GetComponentLookup<EntityPartCD>(true);
            objLookup = SystemAPI.GetComponentLookup<ObjectDataCD>(true);
            useLagCompLookup = SystemAPI.GetComponentLookup<UseLagCompensationCD>(true);
            conditionsLookup = SystemAPI.GetBufferLookup<SummarizedConditionsBuffer>(true);
            effectBufferLookup = SystemAPI.GetBufferLookup<SummarizedConditionEffectsBuffer>(true);
            tickRate = (uint)PlatformConfiguration.Instance.SessionConfiguration.SimulationTickRate;
            RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            RequireForUpdate<PhysicsWorldSingleton>();
            RequireForUpdate<PhysicsWorldHistorySingleton>();
            RequireForUpdate<PugDatabase.DatabaseBankCD>();
            RequireForUpdate<ConditionsTableCD>();
            RequireForUpdate<NetworkTime>();
            RequireForUpdate<EffectEventBuffer>();
            RequireForUpdate<WorldInfoCD>();
            base.OnCreate();
        }

        protected override void OnStartRunning()
        {
            tickRate = (uint)PlatformConfiguration.Instance.SessionConfiguration.SimulationTickRate;
            attackHelper = new AttackSystem.Helper(ref CheckedStateRef, (int)tickRate);
            tileAccessor = new TileAccessor(ref CheckedStateRef, true);
            collision = GetPhysicsWorld().CollisionWorld;
            base.OnStartRunning();
        }
        protected override void OnUpdate()
        {

            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var currentTick = networkTime.ServerTick;
            if (networkTime.IsPartialTick)
                return;


            var database = SystemAPI.GetSingleton<PugDatabase.DatabaseBankCD>();
            var conditionsTable = SystemAPI.GetSingleton<ConditionsTableCD>();
            var worldInfo = SystemAPI.GetSingleton<WorldInfoCD>();
            var effectEventBufferSingleton = SystemAPI.GetSingletonEntity<EffectEventBuffer>();
            var ecb = CreateCommandBuffer();

            // 捕获到 job 的局部变量
            var factionLookup = this.factionLookup;
            var enemyLookup = this.enemyLookup;
            var transLookup = this.transLookup;
            var healthLookup = this.healthLookup;
            var partLookup = this.partLookup;
            var useLagCompLookup = this.useLagCompLookup;
            var objLookup = this.objLookup;
            var conditionsLookup = this.conditionsLookup;
            var effectBufferLookup = this.effectBufferLookup;
            var attackHelper = this.attackHelper;
            var tileAccessor = this.tileAccessor;
            uint tickRate = this.tickRate;
            var up = ZenitHitSystem.up;
            // 更新 Attack Helper 的 lookup + 历史碰撞世界
            tileAccessor.Update(ref CheckedStateRef);
            attackHelper.Update(ref CheckedStateRef, currentTick, this.tickRate);
            var collision = this.collision;
            Entities.ForEach((Entity e, ref ContinouslyDamagingProjectileCD attackCD, in ProjectileCD proj, in LocalTransform trans,
                 in OwnerReferenceCD owner, in BehaviourTagsCD attackTags, in ProjectileSetupCD setup, in DynamicBuffer<ZenithTrail> trails) =>
            {
                // ===== 1. 频率控制：冷却未到直接跳过 =====
                ref var cd = ref attackCD.attackCooldown;
                if (cd.isRunning && !cd.IsTimerElapsed(currentTick))
                    return;

                // ===== 2. 双碰撞世界（延迟补偿 + 预测） =====
                /*uint delay = 0;
                if (attackHelper.interpolationDelayLookup.TryGetComponent(e, out var interpDelay))
                    delay += interpDelay.Delay;
                var physicsHistory = attackHelper.physicsWorldHistory;
                var physicsWorld = attackHelper.physicsWorld;
                physicsHistory.GetCollisionWorldFromTick(currentTick, delay, ref physicsWorld, out CollisionWorld worldInterpolated);
                physicsHistory.GetCollisionWorldFromTick(currentTick, 0U, ref physicsWorld, out CollisionWorld worldPredicted);

                // PvP 决定碰撞层
                uint collidesWith = worldInfo.pvpEnabled ? 1u : 1u;   // 按你的武器 BeamWeaponCD 取，这里用 1u 占位*/
                var filter = new CollisionFilter { BelongsTo = uint.MaxValue, CollidesWith = uint.MaxValue };

                // ===== 3. 自己扫一遍，收集命中实体（去重） =====
                var hitEntities = new NativeHashSet<Entity>(16, Allocator.Temp);
                var castHits = new NativeList<ColliderCastHit>(16, Allocator.Temp);
                var hitInfos = new NativeList<ZenithHitInfo>(16, Allocator.Temp);
                int j = 0;
                float3 origin = trans.Position + up;
                for (int i = trails.Length - 1; i > 0; i--)
                {
                    if (j >= 33)
                        break;
                    var trail = trails[i];
                    var preTrail = trails[i - 1];
                    if (!trail.CanDamage)
                    {
                        j++;
                        continue;
                    }
                    var trailPos = trail.Pos;
                    float3 pos = trailPos.X0Y() + origin;

                    // 两套世界各扫一次
                    //for (int w = 0; w < 2; w++)
                    {
                        /*bool useLag = w == 0;
                        var world = useLag ? worldInterpolated : worldPredicted;*/
                        var world = collision;

                        castHits.Clear();
                        bool hitanything = world.SphereCastAll(pos, 0.5f, (preTrail.Pos - trailPos).X0Y() + up, 0, ref castHits, filter, QueryInteraction.Default);
                        if (!hitanything)
                        {
                            j++;
                            continue;
                        }
                        Debug.Log($"hit index {j} count {castHits.Length}");
                        foreach (var hit in castHits)
                        {
                            // 延迟补偿匹配
                            /*if (useLag != useLagCompLookup.HasComponent(hit.Entity))
                                continue;*/
                            Entity target = hit.Entity;
                            if (partLookup.TryGetComponent(target, out var part))
                                target = part.mainEntity;
                            if (!enemyLookup.HasComponent(target))
                            {
                                Debug.Log("no emeny");
                                continue;
                            }
                            if (!healthLookup.TryGetComponent(target, out var health) || health.health <= 0)
                            {
                                Debug.Log("no health");
                                continue;
                            }
                            if (!transLookup.TryGetComponent(target, out var targetTrans))
                            {
                                Debug.Log("no transform");
                                continue;
                            }
                            if (hitEntities.Add(target))
                            {
                                hitInfos.Add(new ZenithHitInfo { Pos = targetTrans.Position });
                                if (objLookup.TryGetComponent(target, out var objData))
                                {
                                    Debug.Log("hit " + objData.objectID);
                                }
                            }
                        }
                    }
                    j++;
                }
                castHits.Dispose();

                // ===== 4. 排序（从左到右、从上到下） =====
                hitInfos.Sort();

                // ===== 5. 逐段构造扫掠体，对每个目标调 Attack =====
                bool hitAny = false;
                int n = hitInfos.Length;
                float3 center = trans.Position;
                for (int i = 0; i < n; i++)
                {
                    float3 offset = hitInfos[i].Pos - center;
                    /*float dist = math.length(dir);
                    if (dist < 1e-5f)
                        continue;
                    dir /= dist;*/

                    // 路径上是否有墙：每 0.2 格步进
                    /*float3 cur = prevPos;
                    int steps = (int)(dist / 0.2f);
                    for (int s = 0; s < steps; s++)
                    {
                        cur += dir * 0.2f;
                        if (tileAccessor.GetTop(cur.RoundToInt2()).tileType.IsWallTile())
                        {
                            dist = math.length(cur - prevPos);
                            break;
                        }
                    }
                    if (dist < 1e-3f)
                        break;*/

                    var p = new AttackSystem.Helper.Parameters
                    {
                        effectEventBufferSingleton = effectEventBufferSingleton,
                        attacker = e,
                        skipHitsOnEntity = Entity.Null,
                        isRanged = false,
                        attackOffset = offset + up,
                        radius = 0.1f,
                        /*castDirection = dir,
                        castDistance = dist,*/
                        damage = setup.damage,
                        playerDamage = setup.damage,
                        canOnlyAttackType = CanOnlyAttackType.EnemyAndPlayer,
                        behaviourTags = attackTags,
                        isMagic = setup.isMagic,
                        isPredicted = true,
                        //isExecutedBeforePhysics = true,
                        attackTime = 2f / tickRate,
                    };

                    if (attackHelper.Attack(ecb, in p))
                    {
                        hitAny = true;
                    }
                }

                // ===== 6. 冷却 =====
                if (hitAny)
                    cd.Start(currentTick, attackCD.attackEveryXSecond, tickRate);

                // ===== 7. 清理 =====
                hitEntities.Dispose();
            })
                .WithName("ZenitHitSystem")
                .WithBurst()
                .WithAll<GhostInstance>()
                .WithAll<Simulate>()
                .WithNone<EntityDestroyedCD>()
                .Run();
            base.OnUpdate();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}