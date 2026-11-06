using Assets.TerraZenith.Scripts.Component;
using Pug.UnityExtensions;
using System;
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
        private TileAccessor tileAccessor;
        private uint tickRate;
        private static float3 up = math.up() * 0.5f;

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
            if (networkTime.IsPartialTick)
                return;
            var effectEventBufferSingleton = SystemAPI.GetSingletonEntity<EffectEventBuffer>();
            var tileDamageBuffer = SystemAPI.GetSingletonBuffer<TileDamageBuffer>();
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
            tileAccessor.Update(ref CheckedStateRef);
            attackHelper.Update(ref CheckedStateRef, currentTick, this.tickRate);
            var collision = attackHelper.physicsWorld.CollisionWorld;
            Entities.ForEach((Entity e, ref ContinouslyDamagingProjectileCD attackCD, in ProjectileCD proj, in LocalTransform trans,
                 in OwnerReferenceCD owner, in BehaviourTagsCD attackTags, in ProjectileSetupCD setup, in DynamicBuffer<ZenithTrail> trails) =>
            {
                // ===== 1. 频率控制：冷却未到直接跳过 =====
                ref var cd = ref attackCD.attackCooldown;
                if (cd.isRunning && !cd.IsTimerElapsed(currentTick))
                    return;
                cd.Start(currentTick, attackCD.attackEveryXSecond, tickRate);
                // ===== 2. 碰撞世界 =====
                var filter = new CollisionFilter { BelongsTo = uint.MaxValue, CollidesWith = uint.MaxValue };

                // ===== 3. 自己扫一遍，收集命中实体（去重） =====
                var hitEntities = new NativeHashSet<Entity>(16, Allocator.Temp);
                var hitTiles = new NativeHashSet<int2>(32, Allocator.Temp);
                var castHits = new NativeList<ColliderCastHit>(16, Allocator.Temp);
                var hitInfos = new NativeList<ZenithHitInfo>(16, Allocator.Temp);
                int j = 0;
                float3 origin = trans.Position + up;
                int tileDamage = effectLookup[owner.owner][66].value;
                for (int i = trails.Length - 1; i > 0; i--)
                {
                    var trail = trails[i];
                    var preTrail = trails[i - 1];
                    if (!trail.CanDamage)
                    {
                        j++;
                        continue;
                    }
                    var trailPos = trail.Pos;
                    float3 pos = trailPos.X0Y() + origin;
                    var tile = pos.RoundToInt2();
                    if (hitTiles.Add(tile))
                    {
                        tileDamageBuffer.Add(new()
                        {
                            position = tile,
                            damage = tileDamage,
                            causedByEntity = e,
                            dontHitBridges = true,
                            canHitLowColliders = true
                        });
                    }
                    var world = collision;
                    var totar = trailPos - preTrail.Pos;
                    castHits.Clear();
                    bool hitanything = world.SphereCastAll(pos, proj.damageRadius, math.normalizesafe(totar).X0Y(), math.length(totar), ref castHits, filter, QueryInteraction.Default);
                    if (!hitanything)
                    {
                        j++;
                        continue;
                    }
                    //Debug.Log($"hit index {j} count {castHits.Length}");
                    foreach (var hit in castHits)
                    {
                        Entity target = hit.Entity;
                        if (partLookup.TryGetComponent(target, out var part))
                            target = part.mainEntity;
                        if (!enemyLookup.HasComponent(target))
                        {
                            //Debug.Log("no emeny");
                            continue;
                        }
                        if (!healthLookup.TryGetComponent(target, out var health) || health.health <= 0)
                        {
                            //Debug.Log("no health");
                            continue;
                        }
                        if (!transLookup.TryGetComponent(target, out var targetTrans))
                        {
                            //Debug.Log("no transform");
                            continue;
                        }
                        if (hitEntities.Add(target))
                        {
                            hitInfos.Add(new ZenithHitInfo { Pos = targetTrans.Position });
                        }
                    }
                    j++;
                }
                castHits.Dispose();

                // ===== 4. 排序（从左到右、从上到下） =====
                hitInfos.Sort();

                // ===== 5. 逐段构造扫掠体，对每个目标调 Attack =====
                int n = hitInfos.Length;
                float3 center = trans.Position;
                for (int i = 0; i < n; i++)
                {
                    float3 offset = hitInfos[i].Pos - center;

                    var p = new AttackSystem.Helper.Parameters
                    {
                        effectEventBufferSingleton = effectEventBufferSingleton,
                        attacker = e,
                        skipHitsOnEntity = Entity.Null,
                        isRanged = false,
                        attackOffset = offset + up,
                        radius = 0.1f,
                        damage = setup.damage,
                        playerDamage = setup.damage,
                        canOnlyAttackType = CanOnlyAttackType.EnemyAndPlayer,
                        behaviourTags = attackTags,
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