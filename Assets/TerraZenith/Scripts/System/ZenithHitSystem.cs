using Assets.TerraZenith.Scripts.Component;
using Pug.UnityExtensions;
using System;                          // IComparable<HitInfo>
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
        private struct HitInfo : IComparable<HitInfo>
        {
            public float3 Pos;
            public Entity Entity;

            // 从左到右、从上到下：x 升序，x 相同则 z 升序
            public readonly int CompareTo(HitInfo other)
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
        private CollisionWorld collision;
        private uint tickRate;
        private static float3 up = new(0, 0.5f, 0);

        protected override void OnCreate()
        {
            Enabled = false;
            factionLookup = SystemAPI.GetComponentLookup<FactionCD>(true);
            behaviourLookup = SystemAPI.GetComponentLookup<BehaviourTagsCD>(true);
            enemyLookup = SystemAPI.GetComponentLookup<EnemyCD>(true);
            transLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            healthLookup = SystemAPI.GetComponentLookup<HealthCD>(true);
            partLookup = SystemAPI.GetComponentLookup<EntityPartCD>(true);
            useLagCompLookup = SystemAPI.GetComponentLookup<UseLagCompensationCD>(true);
            objLookup = SystemAPI.GetComponentLookup<ObjectDataCD>(true);
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
            collision = GetPhysicsWorld().CollisionWorld;
            tickRate = (uint)PlatformConfiguration.Instance.SessionConfiguration.SimulationTickRate;
            attackHelper = new AttackSystem.Helper(ref CheckedStateRef, (int)tickRate);
            tileAccessor = new TileAccessor(ref CheckedStateRef, true);
            base.OnStartRunning();
        }

        protected override void OnUpdate()
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var currentTick = networkTime.ServerTick;
            bool isPartialTick = networkTime.IsPartialTick;

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

            // 更新 Attack Helper 的 lookup + 历史碰撞世界
            tileAccessor.Update(ref CheckedStateRef);
            attackHelper.Update(ref CheckedStateRef, currentTick, this.tickRate);
            var collision = this.collision;
            var up = ZenitHitSystem.up;
            Entities.ForEach(
                (Entity e, ref ContinouslyDamagingProjectileCD attackCD, in ProjectileCD proj, in LocalTransform trans,
                  in BehaviourTagsCD attackTags, in ProjectileSetupCD setup, in DynamicBuffer<ZenithTrail> trails) =>
                {
                    // ===== 1. 频率控制：冷却未到直接跳过 =====
                    ref var cd = ref attackCD.attackCooldown;
                    if (cd.isRunning && !cd.IsTimerElapsed(currentTick))
                        return;
                    cd.Start(currentTick, attackCD.attackEveryXSecond, tickRate);

                    // ===== 2. 双碰撞世界（延迟补偿 + 预测） =====
                    uint delay = 0;
                    if (attackHelper.interpolationDelayLookup.TryGetComponent(e, out var interpDelay))
                        delay += interpDelay.Delay;

                    var physicsHistory = attackHelper.physicsWorldHistory;
                    var physicsWorld = attackHelper.physicsWorld;
                    physicsHistory.GetCollisionWorldFromTick(currentTick, delay, ref physicsWorld, out CollisionWorld worldInterpolated);
                    physicsHistory.GetCollisionWorldFromTick(currentTick, 0U, ref physicsWorld, out CollisionWorld worldPredicted);

                    CollisionFilter filter = new CollisionFilter
                    {
                        BelongsTo = uint.MaxValue,
                        CollidesWith = 1U
                    };
                    // ===== 3. 扫一遍，收集命中实体（经 EntityPartCD 归并 + 去重） =====
                    var hitEntities = new NativeHashSet<Entity>(16, Allocator.Temp);
                    var hitInfos = new NativeList<HitInfo>(16, Allocator.Temp);
                    var castHits = new NativeList<DistanceHit>(16, Allocator.Temp);
                    var projPos = trans.Position;
                    for (int i = 0; i < trails.Length - 1; i++)
                    {
                        float3 p = trails[i].Pos.X0Y() + projPos + up;

                        castHits.Clear();
                        if (!collision.OverlapSphere(p, proj.damageRadius, ref castHits, filter, QueryInteraction.Default))
                            continue;

                        foreach (var hit in castHits)
                        {
                            Entity target = hit.Entity;
                            if (partLookup.TryGetComponent(target, out var part))
                                target = part.mainEntity;
                            if (!enemyLookup.HasComponent(target))
                                continue;
                            if (!healthLookup.HasComponent(target))
                                continue;

                            var h = healthLookup[target];
                            if (h.health <= 0)
                                continue;

                            if (hitEntities.Add(target))
                            {
                                hitInfos.Add(new HitInfo
                                {
                                    Pos = transLookup[target].Position,
                                    Entity = target
                                });
                            }
                        }
                    }
                    castHits.Dispose();

                    // ===== 4. 排序（从左到右、从上到下） =====
                    hitInfos.Sort();
                    int n = hitInfos.Length;
                    // ===== 5. 逐段构造扫掠体，对每个目标调 Attack =====
                    // bool hitAny = false;
                    if (!isPartialTick)
                    {
                        for (int i = 0; i < n; i++)
                        {
                            Entity target = hitInfos[i].Entity;
                            float3 targetPos = hitInfos[i].Pos;

                            float3 offset = targetPos - projPos;
                            var p = new AttackSystem.Helper.Parameters
                            {
                                effectEventBufferSingleton = effectEventBufferSingleton,
                                attacker = e,
                                skipHitsOnEntity = Entity.Null,
                                isRanged = true,
                                attackOffset = offset + up,
                                radius = 0.1f,
                                damage = setup.damage,
                                playerDamage = setup.damage,
                                canOnlyAttackType = CanOnlyAttackType.All,
                                behaviourTags = attackTags,
                                isMagic = setup.isMagic,
                                isPredicted = true,
                                isExecutedBeforePhysics = true,
                                attackTime = 2f / tickRate,
                            };
                            /*if (objLookup.TryGetComponent(target, out var objdata))
                            {
                                Debug.Log(PlayerController.GetObjectName(new ContainedObjectsBuffer() { objectData = objdata }, true));
                            }*/
                            if (attackHelper.Attack(ecb, in p))
                            {
                                // hitAny = true;
                            }
                        }
                    }

                    // ===== 6. 冷却 =====
                    //if (hitAny)

                    // ===== 7. 清理 =====
                    hitEntities.Dispose();
                    hitInfos.Dispose();
                })
            .WithName("ZenitHitSystem")
            .WithBurst()
            .WithAll<GhostInstance>()
            .WithAll<Simulate>()
            .WithNone<EntityDestroyedCD>()
            .Schedule();

            base.OnUpdate();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}