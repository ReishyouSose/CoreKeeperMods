using Assets.TerraZenith.Scripts.Component;
using Assets.TerraZenith.Scripts.System;
using Pug.UnityExtensions;
using PugTilemap;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

namespace Assets.TerraZenith.Scripts.System
{
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateAfter(typeof(ZenithFlightSystem))]
    [UpdateBefore(typeof(PlayerAttackSystem))]   // 关键：同帧结算
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial class ZenitHitSystem : PugSimulationSystemBase
    {
        private ComponentLookup<FactionCD> factionLookup;
        private ComponentLookup<BehaviourTagsCD> behaviourLookup;
        private ComponentLookup<EnemyCD> enemyLookup;
        private ComponentLookup<LocalTransform> transLookup;
        private ComponentLookup<HealthCD> healthLookup;
        private ComponentLookup<EntityPartCD> partLookup;
        private ComponentLookup<UseLagCompensationCD> useLagCompLookup;
        private BufferLookup<SummarizedConditionsBuffer> conditionsLookup;
        private BufferLookup<SummarizedConditionEffectsBuffer> effectBufferLookup;

        private PhysicsWorldHistorySingleton physicsHistory;
        private AttackSystem.Helper attackHelper;
        private TileAccessor tileAccessor;
        private uint tickRate;

        protected override void OnCreate()
        {
            factionLookup = SystemAPI.GetComponentLookup<FactionCD>(true);
            behaviourLookup = SystemAPI.GetComponentLookup<BehaviourTagsCD>(true);
            enemyLookup = SystemAPI.GetComponentLookup<EnemyCD>(true);
            transLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            healthLookup = SystemAPI.GetComponentLookup<HealthCD>(true);
            partLookup = SystemAPI.GetComponentLookup<EntityPartCD>(true);
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
            var conditionsLookup = this.conditionsLookup;
            var effectBufferLookup = this.effectBufferLookup;
            var attackHelper = this.attackHelper;
            var tileAccessor = this.tileAccessor;
            uint tickRate = this.tickRate;
            // 更新 Attack Helper 的 lookup + 历史碰撞世界
            tileAccessor.Update(ref CheckedStateRef);
            attackHelper.Update(ref CheckedStateRef, currentTick, this.tickRate);

            Entities.ForEach((Entity e, ref ContinouslyDamagingProjectileCD attackCD, in ProjectileCD proj, in LocalTransform trans,
                 in OwnerReferenceCD owner, in BehaviourTagsCD attackTags, in ProjectileSetupCD setup, in DynamicBuffer<ZenithTrail> trails) =>
            {
                // ===== 1. 频率控制：冷却未到直接跳过 =====
                ref var cd = ref attackCD.attackCooldown;
                if (cd.isRunning && !cd.IsTimerElapsed(currentTick))
                    return;

                // ===== 2. 双碰撞世界（延迟补偿 + 预测） =====
                uint delay = 0;
                if (attackHelper.interpolationDelayLookup.TryGetComponent(e, out var interpDelay))
                    delay += interpDelay.Delay;
                var physicsHistory = attackHelper.physicsWorldHistory;
                var physicsWorld = attackHelper.physicsWorld;
                physicsHistory.GetCollisionWorldFromTick(currentTick, delay, ref physicsWorld, out CollisionWorld worldInterpolated);
                physicsHistory.GetCollisionWorldFromTick(currentTick, 0U, ref physicsWorld, out CollisionWorld worldPredicted);

                // PvP 决定碰撞层
                uint collidesWith = worldInfo.pvpEnabled ? 1u : 1u;   // 按你的武器 BeamWeaponCD 取，这里用 1u 占位
                var filter = new CollisionFilter { BelongsTo = uint.MaxValue, CollidesWith = collidesWith };

                // ===== 3. 自己扫一遍，收集命中实体（去重） =====
                var hitEntities = new NativeHashSet<Entity>(16, Allocator.Temp);
                var hitPositions = new NativeList<float3>(16, Allocator.Temp);
                var rayHits = new NativeList<RaycastHit>(16, Allocator.Temp);
                var castHits = new NativeList<ColliderCastHit>(16, Allocator.Temp);

                for (int i = 0; i < trails.Length - 1; i++)
                {
                    float3 a = trails[i].Pos.X0Y() + trans.Position;
                    float3 b = trails[i + 1].Pos.X0Y() + trans.Position;
                    float3 dir = b - a;
                    float dist = math.length(dir);
                    if (dist < 1e-5f)
                        continue;
                    dir /= dist;

                    // 两套世界各扫一次
                    for (int w = 0; w < 2; w++)
                    {
                        bool useLag = w == 0;
                        var world = useLag ? worldInterpolated : worldPredicted;

                        castHits.Clear();
                        if (!world.SphereCastAll(a, proj.damageRadius, dir, dist,
                            ref castHits, filter, QueryInteraction.Default))
                            continue;

                        foreach (var hit in castHits)
                        {
                            // 延迟补偿匹配
                            if (useLag != useLagCompLookup.HasComponent(hit.Entity))
                                continue;

                            Entity target = hit.Entity;
                            if (partLookup.TryGetComponent(target, out var part))
                                target = part.mainEntity;

                            if (target == e || target == owner.owner)
                                continue;
                            if (!enemyLookup.HasComponent(target))
                                continue;
                            if (!healthLookup.HasComponent(target))
                                continue;

                            var h = healthLookup[target];
                            if (h.health <= 0)
                                continue;

                            if (hitEntities.Add(target))
                            {
                                hitPositions.Add(transLookup[target].Position);
                            }
                        }
                    }
                }
                castHits.Dispose();

                // ===== 4. 排序（从左到右、从上到下） =====
                // 用 (x 升序, z 升序) 排序。实体少时插入排序足够，避免额外分配。
                int n = hitPositions.Length;
                var sortedEntities = new NativeArray<Entity>(n, Allocator.Temp);
                var sortedPositions = new NativeArray<float3>(n, Allocator.Temp);

                // 把 HashSet 展开到数组
                int idx = 0;
                foreach (var ent in hitEntities)
                {
                    sortedEntities[idx] = ent;
                    sortedPositions[idx] = transLookup[ent].Position;
                    idx++;
                }

                // 简单插入排序：先 x 再 z
                for (int i = 1; i < n; i++)
                {
                    Entity keyE = sortedEntities[i];
                    float3 keyP = sortedPositions[i];
                    int j = i - 1;
                    while (j >= 0 &&
                        (sortedPositions[j].x > keyP.x ||
                        (math.abs(sortedPositions[j].x - keyP.x) < 1e-4f && sortedPositions[j].z > keyP.z)))
                    {
                        sortedEntities[j + 1] = sortedEntities[j];
                        sortedPositions[j + 1] = sortedPositions[j];
                        j--;
                    }
                    sortedEntities[j + 1] = keyE;
                    sortedPositions[j + 1] = keyP;
                }

                // ===== 5. 逐段构造扫掠体，对每个目标调 Attack =====
                bool hitAny = false;
                if (n > 0 && !isPartialTick)   // partial tick 不结算
                {
                    float3 playerPos = trans.Position;
                    float3 prevPos = playerPos;   // 第一段从玩家出发

                    for (int i = 0; i < n; i++)
                    {
                        float3 targetPos = sortedPositions[i];
                        float3 dir = targetPos - prevPos;
                        float dist = math.length(dir);
                        if (dist < 1e-5f)
                            continue;
                        dir /= dist;

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
                            attacker = owner.owner,
                            skipHitsOnEntity = Entity.Null,
                            isRanged = true,
                            attackOffset = prevPos - playerPos + 0.5f * math.up(),
                            radius = 0.1f,
                            castDirection = dir,
                            castDistance = dist,
                            damage = setup.damage,
                            playerDamage = setup.damage,
                            canOnlyAttackType = CanOnlyAttackType.All,
                            behaviourTags = attackTags,
                            isMagic = setup.isMagic,
                            isPredicted = true,
                            isExecutedBeforePhysics = true,
                            attackTime = 2f / tickRate,
                        };

                        if (attackHelper.Attack(ecb, in p))
                            hitAny = true;

                        prevPos = targetPos;
                    }
                }

                // ===== 6. 冷却 =====
                if (hitAny)
                    cd.Start(currentTick, attackCD.attackEveryXSecond, tickRate);

                // ===== 7. 清理 =====
                hitEntities.Dispose();
                hitPositions.Dispose();
                rayHits.Dispose();
                sortedEntities.Dispose();
                sortedPositions.Dispose();
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