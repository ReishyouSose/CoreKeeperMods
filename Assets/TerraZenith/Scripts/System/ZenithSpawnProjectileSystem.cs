using Assets.TerraZenith.Scripts.Component;
using Assets.TerraZenith.Scripts.Core;
using PlayerEquipment;
using Pug.UnityExtensions;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace Assets.TerraZenith.Scripts.System
{
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(EquipmentLateUpdateSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial class ZenithSpawnProjectileSystem : PugSimulationSystemBase
    {
        private ComponentLookup<ZenithWeaponCD> zenithLookup;
        private ComponentLookup<ZenithWeaponTriggerTag> triggerLookup;
        private ComponentLookup<HasWeaponDamageCD> weaponDamageLookup;
        private ComponentLookup<DurabilityCD> durabilityLookup;
        private ComponentLookup<LevelCD> levelLookup;
        private ComponentLookup<WeaponDamageCD> levelDamageLookup;
        private ComponentLookup<CooldownCD> cooldownLookup;
        private ComponentLookup<ObjectDataCD> objectDataLookup;
        private ComponentLookup<FactionCD> factionLookup;
        private ComponentLookup<BehaviourTagsCD> behaviourLookup;
        private ComponentLookup<EquipmentSlotCD> slotLookup;
        private ComponentLookup<EnemyCD> enemyLookup;
        private ComponentLookup<CattleCD> cattleLookup;
        private ComponentLookup<LocalTransform> transLookup;
        private ComponentLookup<HealthCD> healthLookup;
        private ComponentLookup<ImmuneToDamageCD> immuneLookup;
        private ComponentLookup<DisablePhysicsCD> noPhysicsLookup;
        private BufferLookup<LevelEntitiesBuffer> levelEntitiesLookup;
        private BufferLookup<SummarizedConditionEffectsBuffer> effectLookup;
        private BufferLookup<SummarizedConditionsBuffer> conditionsLookup;
        private EntityQuery EnemyQueue;
        private EntityQuery PlayerQueue;
        private PlayerAttackLookups lookups;
        private PlayerAttackShared shared;

        protected override void OnCreate()
        {
            zenithLookup = SystemAPI.GetComponentLookup<ZenithWeaponCD>(true);
            triggerLookup = SystemAPI.GetComponentLookup<ZenithWeaponTriggerTag>();
            weaponDamageLookup = SystemAPI.GetComponentLookup<HasWeaponDamageCD>(true);
            durabilityLookup = SystemAPI.GetComponentLookup<DurabilityCD>(true);
            levelLookup = SystemAPI.GetComponentLookup<LevelCD>(true);
            levelDamageLookup = SystemAPI.GetComponentLookup<WeaponDamageCD>(true);
            cooldownLookup = SystemAPI.GetComponentLookup<CooldownCD>(true);
            objectDataLookup = SystemAPI.GetComponentLookup<ObjectDataCD>(true);
            factionLookup = SystemAPI.GetComponentLookup<FactionCD>(true);
            behaviourLookup = SystemAPI.GetComponentLookup<BehaviourTagsCD>(true);
            slotLookup = SystemAPI.GetComponentLookup<EquipmentSlotCD>(true);
            enemyLookup = SystemAPI.GetComponentLookup<EnemyCD>(true);
            cattleLookup = SystemAPI.GetComponentLookup<CattleCD>(true);
            transLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            healthLookup = SystemAPI.GetComponentLookup<HealthCD>(true);
            immuneLookup = SystemAPI.GetComponentLookup<ImmuneToDamageCD>(true);
            noPhysicsLookup = SystemAPI.GetComponentLookup<DisablePhysicsCD>(true);
            levelEntitiesLookup = SystemAPI.GetBufferLookup<LevelEntitiesBuffer>(true);
            effectLookup = SystemAPI.GetBufferLookup<SummarizedConditionEffectsBuffer>(true);
            conditionsLookup = SystemAPI.GetBufferLookup<SummarizedConditionsBuffer>(true);
            EnemyQueue = EntityManager.CreateEntityQuery(new EntityQueryDesc()
            {
                All = new ComponentType[] { typeof(EnemyCD), typeof(HealthCD), typeof(LocalTransform) },
                None = new ComponentType[] { typeof(CattleCD), typeof(EntityDestroyedCD) }
            });
            PlayerQueue = EntityManager.CreateEntityQuery(typeof(PlayerGhost));
            lookups = new(ref CheckedStateRef);
            shared = new(ref CheckedStateRef);
            RequireForUpdate<PugDatabase.DatabaseBankCD>();
            RequireForUpdate<ConditionsTableCD>();
            RequireForUpdate<NetworkTime>();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            var zenithLookup = this.zenithLookup;
            var triggerLookup = this.triggerLookup;
            var weaponDamageLookup = this.weaponDamageLookup;
            var durabilityLookup = this.durabilityLookup;
            var levelLookup = this.levelLookup;
            var levelDamageLookup = this.levelDamageLookup;
            var cooldownLookup = this.cooldownLookup;
            var objectDataLookup = this.objectDataLookup;
            var factionLookup = this.factionLookup;
            var behaviourLookup = this.behaviourLookup;
            var slotLookup = this.slotLookup;
            var transLookup = this.transLookup;
            var healthLookup = this.healthLookup;
            var immuneLookup = this.immuneLookup;
            var noPhysicsLookup = this.noPhysicsLookup;
            var levelEntitiesLookup = this.levelEntitiesLookup;
            var effectLookup = this.effectLookup;
            var conditionsLookup = this.conditionsLookup;
            var database = SystemAPI.GetSingleton<PugDatabase.DatabaseBankCD>();
            var conditionsTable = SystemAPI.GetSingleton<ConditionsTableCD>();
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            bool firstTime = networkTime.IsFirstTimeFullyPredictingTick;
            var currentTick = networkTime.ServerTick;
            var ecb = CreateCommandBuffer();
            int blades = ZenithBalance.Slashs.Length;
            float searchSQ = ZenithBalance.SearchRadiumSqrt;
            float jitterRadius = ZenithBalance.JitterRadius;
            var shortAxisRandom = ZenithBalance.ShortAxisRandom;
            var terraFx = ZenithBalance.Terra_PxFix;
            var minLongAxis = ZenithBalance.MinLongAxis;
            lookups.Update(ref CheckedStateRef);
            var players = PlayerQueue.ToEntityListAsync(Allocator.TempJob, out var jobWait);
            shared.Update(ref CheckedStateRef, ecb, players);
            var enemies = EnemyQueue.ToEntityArray(Allocator.TempJob);
            var job = Entities.ForEach((Entity entity, ref RandomCD random, ref ZenithWeaponTriggerTag trigger,
                in EquippedObjectCD equipped, in ClientInput input, in LocalTransform transform,
                in GhostOwner ghostOwner, in ManaCostForQueuedAttackCD manaCost) =>
            {
                int count = trigger.Count;
                trigger.Count = (count + 1) % 3;
                triggerLookup.SetComponentEnabled(entity, false);
                Entity prefab = equipped.equipmentPrefab;
                if (!zenithLookup.TryGetComponent(prefab, out var weapon))
                    return;

                // 随机都得拨
                ref var rng = ref random.Value;
                bool vari = count != 0;
                float shortAxis = rng.NextInt(-shortAxisRandom, shortAxisRandom + 1) * terraFx;
                int variation = vari ? (rng.NextInt(blades) + 1) : 0;
                var inherited = PugRandom.InheritRngFromEntity(ref random.Value);
                if (!firstTime)
                    return;

                Entity projectilePrefab = PugDatabase.GetPrimaryPrefabEntity(weapon.projectileID, database.databaseBankBlob, 0);
                if (projectilePrefab == Entity.Null)
                    return;

                float2 playerPos = transform.Position.xz;
                var mouse = input.mouseOrJoystickWorldPoint;
                float2 target = mouse - playerPos;

                if (vari)
                {
                    bool shouldJitter = true;
                    if (ZenithBalance.FindTarget(enemies, mouse, searchSQ, true, healthLookup, transLookup,
                        noPhysicsLookup, immuneLookup, effectLookup, out _, out float2 targetPos, out _))
                    {
                        target = targetPos - playerPos;
                        shouldJitter = count == 2;
                    }

                    // 原版：第三刀必抖，第二刀没找到才抖
                    if (shouldJitter)
                        target += ZenithBalance.NextVector2Circular(ref rng, jitterRadius);
                }

                // 伤害/等级/冷却
                ObjectDataCD held = equipped.containedObject.objectData;
                int damage = 10;
                bool reinforced = false;
                if (durabilityLookup.TryGetComponent(prefab, out var durability))
                    reinforced = durability.IsReinforced(held.amount);

                Entity levelEntity = EntityUtility.GetLevelEntity(prefab, held, levelEntitiesLookup, levelLookup);
                if (levelEntity != Entity.Null && levelDamageLookup.TryGetComponent(levelEntity, out var levelDamage))
                    damage = levelDamage.GetDamage(reinforced);

                bool isMagic = weaponDamageLookup.TryGetComponent(prefab, out var marker) && marker.isMagic;

                int weaponLevel = held.variation > 0 ? held.variation : 1;
                if (held.variation <= 0 && levelLookup.TryGetComponent(prefab, out var level))
                    weaponLevel = level.level;

                float weaponCooldown = 0.6f;
                if (cooldownLookup.TryGetComponent(prefab, out var cooldown))
                    weaponCooldown = cooldown.cooldown;

                DynamicBuffer<SummarizedConditionEffectsBuffer> effectBuffer = effectLookup[entity];
                float attackSpeed = math.max(1f + effectBuffer[(int)ConditionEffect.MeleeAttackSpeed].value / 1000f
                    + effectBuffer[(int)ConditionEffect.AttackSpeed].value / 1000f, 0.1f);
                weaponCooldown /= attackSpeed;

                // 轨道参数（严格对齐原版 velocity = vector42 / 2）t
                target *= 0.5f;
                float dist = math.length(target);
                float rot = math.atan2(target.y, target.x);

                Entity projectile = ecb.Instantiate(projectilePrefab);
                var orbit = new ZenithOrbitCD
                {
                    Offset = target,
                    LongAxis = math.max(minLongAxis, dist),
                    ShortAxis = shortAxis,
                    // 发射端 +PI；CalculateOrbit 的 x 从 0 起步，两者抵消原版的 PI 起始
                    Rotation = rot + math.PI,
                    LastUpdateTick = currentTick,
                    TileDamage = input.IsButtonStateSet(CommandInputButtonStateNames.SecondInteract_HeldDown)
                        ? (int)math.round(effectBuffer[(int)ConditionEffect.Mining].value
                            * math.max(1f + effectBuffer[(int)ConditionEffect.MiningPercentage].value / 100f, 0)) : 0,
                };
                ecb.SetComponent(projectile, orbit);
                ecb.SetComponent(projectile, new ProjectileSetupCD
                {
                    damage = damage,
                    directionRadians = rot,
                    isMagic = isMagic
                });

                if (objectDataLookup.TryGetComponent(projectilePrefab, out var objectData))
                {
                    objectData.variation = variation;
                    ecb.SetComponent(projectile, objectData);
                }

                // 实体停在“玩家中心 + offset”（椭圆中心）
                LocalTransform p = LocalTransform.Identity;
                p.Position = (playerPos + target).X0Y();
                ecb.SetComponent(projectile, p);
                ecb.SetComponent(projectile, new OwnerReferenceCD { owner = entity });

                slotLookup.TryGetComponent(entity, out var slot);
                ecb.SetComponent(projectile, new ProjectileSourceCD
                {
                    shotFromReinforcedWeapon = reinforced,
                    weaponLevel = weaponLevel,
                    sequenceExplosionTotalExplosions = -1,
                    weaponCooldown = weaponCooldown,
                    manaCost = manaCost.manaCost,
                    isSecondaryAttack = slot.currentWindupTier > 0
                });

                ecb.SetComponent(projectile, ghostOwner);
                ecb.SetComponent(projectile, new RandomCD { Value = inherited });

                EntityUtility.InheritAttackData(ecb, entity, projectile, conditionsTable, behaviourLookup, conditionsLookup);
                if (factionLookup.HasComponent(projectilePrefab))
                    EntityUtility.InheritFaction(ecb, entity, projectile, factionLookup);
            })
                .WithName("ZenithWeaponTriggerSystem")
                .WithBurst()
                .WithAll<ZenithWeaponTriggerTag>()
                .WithDisposeOnCompletion(enemies)
                .ScheduleParallel(JobHandle.CombineDependencies(Dependency, jobWait));
            base.OnUpdate();
        }
    }
}