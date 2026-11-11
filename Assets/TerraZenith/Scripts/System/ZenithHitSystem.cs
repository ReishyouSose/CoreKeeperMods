using Assets.TerraZenith.Scripts.Component;
using Assets.TerraZenith.Scripts.Core;
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
        private ComponentLookup<EnemyCD> enemyLookup;
        private ComponentLookup<LocalTransform> transLookup;
        private ComponentLookup<HealthCD> healthLookup;
        private ComponentLookup<EntityPartCD> partLookup;
        private BufferLookup<SummarizedConditionEffectsBuffer> effectLookup;

        private AttackSystem.Helper attackHelper;
        private uint tickRate;
        private static float3 up = math.up() * 0.5f;
        private TileAccessor tileAccessor;
        private NativeArray<int> trailTargets;

        protected override void OnCreate()
        {
            enemyLookup = SystemAPI.GetComponentLookup<EnemyCD>(true);
            transLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            healthLookup = SystemAPI.GetComponentLookup<HealthCD>(true);
            partLookup = SystemAPI.GetComponentLookup<EntityPartCD>(true);
            effectLookup = SystemAPI.GetBufferLookup<SummarizedConditionEffectsBuffer>(true);
            tickRate = (uint)PlatformConfiguration.Instance.SessionConfiguration.SimulationTickRate;
            RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            RequireForUpdate<PhysicsWorldSingleton>();
            RequireForUpdate<PhysicsWorldHistorySingleton>();
            RequireForUpdate<PugDatabase.DatabaseBankCD>();
            RequireForUpdate<ConditionsTableCD>();
            RequireForUpdate<NetworkTime>();
            RequireForUpdate<EffectEventBuffer>();
            RequireForUpdate<WorldInfoCD>();
            RequireForUpdate<LocalHitCoolDownCD>();
            Debug.LogWarning("cd exist " + SystemAPI.TryGetSingleton<LocalHitCoolDownCD>(out _));
            trailTargets = new(4, Allocator.Persistent);
            int j = 0;
            int count = ZenithBalance.TrailCount;
            for (int i = 0; i < tickRate; i++)
            {
                float phase = i / (float)tickRate * count;
                if (phase >= j)
                {
                    trailTargets[j] = i;
                    j++;
                }
            }
            base.OnCreate();
        }

        protected override void OnStartRunning()
        {
            tickRate = (uint)PlatformConfiguration.Instance.SessionConfiguration.SimulationTickRate;
            attackHelper = new AttackSystem.Helper(ref CheckedStateRef, (int)tickRate);
            tileAccessor = CreateTileAccessor();
            base.OnStartRunning();
        }
        protected override void OnUpdate()
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var currentTick = networkTime.ServerTick;
            if (networkTime.IsPartialTick)
                return;
            var effectEventBufferSingleton = SystemAPI.GetSingletonEntity<EffectEventBuffer>();
            var tileDamageBuffer = SystemAPI.GetSingletonBuffer<TileDamageBuffer>();
            var cooldown = SystemAPI.GetSingleton<LocalHitCoolDownCD>();
            var ecb = CreateCommandBuffer();

            // 捕获到 job 的局部变量
            var enemyLookup = this.enemyLookup;
            var transLookup = this.transLookup;
            var healthLookup = this.healthLookup;
            var partLookup = this.partLookup;
            var attackHelper = this.attackHelper;
            var tileAccessor = this.tileAccessor;
            uint tickRate = this.tickRate;
            var effectLookup = this.effectLookup;
            var up = ZenitHitSystem.up;
            // 更新 Attack Helper 的 lookup + 历史碰撞世界
            attackHelper.Update(ref CheckedStateRef, currentTick, this.tickRate);
            var collision = attackHelper.physicsWorld.CollisionWorld;
            var manager = EntityManager;
            var trailTargets = this.trailTargets;
            var hitbox = ZenithBalance.HitBox;
            var width = hitbox.x;
            var height = hitbox.y;
            Entities.ForEach((Entity e, ref ContinouslyDamagingProjectileCD attackCD, in ProjectileSetupCD setup, in ZenithOrbitCD orbit,
                in ProjectileCD proj, in LocalTransform trans, in BehaviourTagsCD behaviourTags, in DynamicBuffer<ZenithTrail> trails) =>
            {
                // ===== 1. 频率控制：冷却未到直接跳过/另外把原版冷却设为最大值来跳过穿透弹幕的逻辑 =====
                ref var cd = ref attackCD.attackCooldown;
                if (!cd.isRunning)
                {
                    cd.Start(currentTick, uint.MaxValue);
                    //Debug.Log("Set inf cd");
                }
                // ===== 2. 碰撞世界 =====

                // ===== 3. 自己扫一遍，收集命中实体（去重） =====
                var damageFix = ZenithBalance.GetCDFix(attackCD.attackEveryXSecond, tickRate, out uint targetTicks);
                var hitEntities = new NativeHashSet<Entity>(16, Allocator.Temp);
                var hitTiles = new NativeHashSet<int2>(32, Allocator.Temp);
                var castHits = new NativeList<ColliderCastHit>(16, Allocator.Temp);
                var hitInfos = new NativeList<ZenithHitInfo>(16, Allocator.Temp);
                float3 origin = trans.Position + up;
                var tileDamage = orbit.TileDamage;
                bool hitTile = tileDamage > 0;
                var collidesWith = 1 << 3 | 1 << 4;//EnemyTrigger和EnemyCollider
                if (hitTile)
                    collidesWith |= 1 << 0;//DefaultCollider
                var filter = new CollisionFilter { BelongsTo = uint.MaxValue, CollidesWith = (uint)collidesWith };
                var trailLength = trails.Length;
                var lifeRatio = orbit.LifeRatio;
                //Debug.Log("lifeRatio: " + lifeRatio);
                for (int i = 0; i < trailTargets.Length; i++)
                {
                    int j = trailLength - 1 - trailTargets[i];
                    if (j - 2 < 0)
                        continue;
                    var trail = trails[j];
                    var perTrail = trails[j - 2];
                    var delta = lifeRatio - trail.LifeRatio;
                    //Debug.Log("lifeDelta: " + delta);
                    if (delta < 0 || delta > 0.5f)
                        continue;
                    var trailPos = trail.Pos;
                    var rot = trail.Rot;
                    float3 pos = trailPos.X0Y() + origin;
                    var dir = (rot * width).X0Y();
                    if (hitTile)
                    {
                        for (int k = -1; k <= 1; k++)
                        {
                            var tilePos = (pos + dir * k).RoundToInt2();

                            var tile = tileAccessor.GetTopDamageableTile(tilePos);
                            if (tile.tileType == PugTilemap.TileType.wall)
                            {
                                if (cooldown.InCoolDown(e, tilePos))
                                {
                                    //Debug.Log($"pos {tilePos} is cd");
                                }
                                else if (hitTiles.Add(tilePos))
                                {
                                    tileDamageBuffer.Add(new()
                                    {
                                        position = tilePos,
                                        damage = tileDamage,
                                        causedByEntity = e,
                                        dontHitBridges = true,
                                        canHitLowColliders = true,
                                    });
                                    cooldown.AddCoolDown(e, tilePos, targetTicks, currentTick);
                                }
                            }
                        }
                    }
                    float2 prePos = perTrail.Pos;
                    float3 castStart = prePos.X0Y() + origin;
                    var totar = trailPos - prePos;
                    dir = math.normalize(totar).X0Y();

                    castHits.Clear();
                    bool hitAny = collision.SphereCastAll(castStart, width, dir, math.length(totar), ref castHits, filter);
                    //Debug.Log("hit any " + hitAny);
                    foreach (var hit in castHits)
                    {
                        Entity target = hit.Entity;
                        if (partLookup.TryGetComponent(target, out var part))
                            target = part.mainEntity;
                        if (!healthLookup.TryGetComponent(target, out var health) || health.health <= 0)
                        {
                            //Debug.Log("no health");
                            continue;
                        }
                        if (!hitTile && !enemyLookup.HasComponent(target))
                        {
                            //Debug.Log("no emeny");
                            continue;
                        }
                        if (!transLookup.TryGetComponent(target, out var targetTrans))
                        {
                            //Debug.Log("no transform");
                            continue;
                        }
                        if (cooldown.InCoolDown(e, target))
                        {
                            //Debug.Log($"target {target} is cd");
                        }
                        else if (hitEntities.Add(target))
                        {
                            hitInfos.Add(new ZenithHitInfo { Pos = targetTrans.Position });
                            cooldown.AddCoolDown(e, target, targetTicks, currentTick);
                        }
                    }
                }
                hitTiles.Dispose();
                castHits.Dispose();

                // ===== 4. 排序（从左到右、从上到下） =====
                hitInfos.Sort();

                // ===== 5. 逐段构造扫掠体，对每个目标调 Attack =====
                int n = hitInfos.Length;
                float3 center = trans.Position;
                var attackType = hitTile ? CanOnlyAttackType.All : CanOnlyAttackType.EnemyAndPlayer;
                var damage = (int)math.round(setup.damage * damageFix);
                for (int i = 0; i < n; i++)
                {
                    float3 offset = hitInfos[i].Pos - center;

                    var p = new AttackSystem.Helper.Parameters
                    {
                        effectEventBufferSingleton = effectEventBufferSingleton,
                        attacker = e,
                        skipHitsOnEntity = Entity.Null,
                        isRanged = false,
                        attackOffset = offset,
                        radius = 0.1f,
                        damage = damage,
                        playerDamage = damage,
                        canOnlyAttackType = attackType,
                        behaviourTags = behaviourTags,
                        isMagic = setup.isMagic,
                        isPredicted = true,
                        attackTime = 2f / tickRate,
                    };

                    attackHelper.Attack(ecb, in p);
                }

                // ===== 6. 冷却 =====

                // ===== 7. 清理 =====
                hitEntities.Dispose();
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