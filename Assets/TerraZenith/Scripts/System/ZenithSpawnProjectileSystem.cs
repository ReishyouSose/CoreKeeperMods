using Assets.TerraZenith.Scripts.Component;
using Assets.TerraZenith.Scripts.Core;
using PlayerEquipment;
using Pug.UnityExtensions;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

// 天顶剑的服务器逻辑。发射对照玩家挥砍时连续生成 FinalFractal，飞行对照 Projectile.AI_182_FinalFractal。
namespace Assets.TerraZenith.Scripts.System
{
    // 对照 RangeWeaponSpawnProjectileSystem。预测里看到 ZenithWeaponTriggerTag 就把剑射出来。
    // 相位和左右从玩家的 RandomCD 里取，瞄准用这一拍的 ClientInput。两端输入相同，飞出来的参数就相同。
    // 回滚重放时只把随机数往前拨，不重复生成，和原版 isFirstTimeFullyPredictingTick 一样。
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [UpdateBefore(typeof(EquipmentLateUpdateSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial class ZenithSpawnProjectileSystem : PugSimulationSystemBase
    {
        private EntityQuery enemies;
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
        private BufferLookup<LevelEntitiesBuffer> levelEntitiesLookup;
        private BufferLookup<SummarizedConditionEffectsBuffer> effectBufferLookup;
        private BufferLookup<SummarizedConditionsBuffer> conditionsLookup;
        private BufferLookup<NearbyEntitiesBufferCD> nearbyLookup;

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
            levelEntitiesLookup = SystemAPI.GetBufferLookup<LevelEntitiesBuffer>(true);
            effectBufferLookup = SystemAPI.GetBufferLookup<SummarizedConditionEffectsBuffer>(true);
            conditionsLookup = SystemAPI.GetBufferLookup<SummarizedConditionsBuffer>(true);
            nearbyLookup = SystemAPI.GetBufferLookup<NearbyEntitiesBufferCD>(true);
            RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            RequireForUpdate<PugDatabase.DatabaseBankCD>();
            RequireForUpdate<ConditionsTableCD>();
            RequireForUpdate<NetworkTime>();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            // 【这里是你要自己薅的：重名捕获】
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
            var enemyLookup = this.enemyLookup;
            var cattleLookup = this.cattleLookup;
            var transLookup = this.transLookup;
            var healthLookup = this.healthLookup;
            var levelEntitiesLookup = this.levelEntitiesLookup;
            var effectLookup = this.effectBufferLookup;
            var conditionsLookup = this.conditionsLookup;
            var nearbyLookup = this.nearbyLookup;
            var database = SystemAPI.GetSingleton<PugDatabase.DatabaseBankCD>();
            var conditionsTable = SystemAPI.GetSingleton<ConditionsTableCD>();
            bool firstTime = SystemAPI.GetSingleton<NetworkTime>().IsFirstTimeFullyPredictingTick;
            var ecb = CreateCommandBuffer();
            int blades = ZenithBalance.Slashs.Length;
            Entities.ForEach((Entity entity, ref RandomCD random, ref ZenithWeaponTriggerTag trigger, in EquippedObjectCD equipped, in ClientInput input,
                in LocalTransform transform, in GhostOwner ghostOwner, in ManaCostForQueuedAttackCD manaCost) =>
            {
                int count = trigger.Count;
                trigger.Count = (count + 1) % 3;
                triggerLookup.SetComponentEnabled(entity, false);
                Entity prefab = equipped.equipmentPrefab;
                if (!zenithLookup.TryGetComponent(prefab, out var weapon))
                    return;
                var projID = weapon.projectileID;
                if (projID == ObjectID.None)
                    return;

                //随机都得拨
                ref var rng = ref random.Value;
                bool vari = count != 0;
                float randomRot = vari ? (rng.NextInt(-15, 15) / 180f) : 0;
                int sign = rng.NextBool() ? 1 : -1;
                float shortAxis = rng.NextInt(8, 49) / 16f * sign;
                float longOffset = rng.NextInt(-16, 17) / 16f;
                int variation = vari ? (rng.NextInt(blades) + 1) : 0;
                var inherited = PugRandom.InheritRngFromEntity(ref random.Value);

                // 如果不是完整的预测帧（比如回滚重放阶段），拨完随机数就退出，防止重复生成实体
                if (!firstTime)
                    return;

                Entity projectilePrefab = PugDatabase.GetPrimaryPrefabEntity(weapon.projectileID, database.databaseBankBlob, 0);
                if (projectilePrefab == Entity.Null)
                    return;

                float2 playerPos = transform.Position.xz /*+ new float2(0, 0.5f)*/;
                float2 target = input.mouseOrJoystickWorldPoint - playerPos;
                // 特定的剑（序号 1、2）会寻找附近的敌人（索敌）
                if (vari && nearbyLookup.TryGetBuffer(entity, out var nearby))
                {
                    float minDis = ZenithBalance.SearchRadiumSqrt;
                    foreach (var enemy in nearby)
                    {
                        var enemyE = enemy.entity;
                        if (!enemyLookup.HasComponent(enemyE))
                            continue;
                        if (cattleLookup.HasComponent(enemyE))
                            continue;
                        if (!healthLookup.TryGetComponent(enemyE, out var health) || health.health <= 0)
                            continue;
                        if (!transLookup.TryGetComponent(enemyE, out var trans))
                            continue;
                        float2 totar = trans.Position.XZ() - playerPos;
                        float dis = math.lengthsq(totar);
                        if (dis > minDis)
                            continue;
                        minDis = dis;
                        target = totar;
                    }
                }
                else
                    ZenithBalance.Rotate(target, randomRot);


                // ==========================================
                // 3. 计算伤害、等级、武器冷却等数据
                // ==========================================
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
                float attackSpeed = math.max(1f + effectBuffer[41].value / 1000f + effectBuffer[65].value / 1000f, 0.1f);
                weaponCooldown /= attackSpeed;

                // ==========================================
                // 4. 在 ECS 中实例化实体并赋予数据
                // ==========================================
                Entity projectile = ecb.Instantiate(projectilePrefab);
                ecb.SetComponent(projectile, LocalTransform.FromPosition(transform.Position));
                float back = 1f;
                float dist = math.length(target);
                dist += ((dist + longOffset < 0) ? -1 : 1) * longOffset;
                if (dist < 1e-4f)
                    target = new float2(1f, 0f);
                float2 dir = math.normalize(target);
                float rot = math.atan2(dir.y, dir.x);

                float halfAxis = (dist + back) * 0.5f; // 半长轴
                if (math.abs(shortAxis) * 4 < halfAxis)
                    shortAxis *= 2;
                var orbit = new ZenithOrbitCD
                {
                    // 【核心修正】Offset 是从“玩家(也就是椭圆短轴端点)”指向“椭圆中心”的向量
                    Offset = dir * ((dist - back) * 0.5f),
                    LongAxis = halfAxis,
                    ShortAxis = shortAxis, // 比如 0.4f，侧向摆动幅度
                    Rotation = rot + math.PI,
                    // Sign = target.x >= 0 ? 1 : -1
                };
                ecb.SetComponent(projectile, orbit);

                if (objectDataLookup.TryGetComponent(projectilePrefab, out var objectData))
                {
                    objectData.variation = variation;
                    ecb.SetComponent(projectile, objectData);
                }

                ecb.SetComponent(projectile, new ProjectileSetupCD
                {
                    damage = damage,
                    directionRadians = rot,
                    isMagic = isMagic
                });

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
                ecb.SetComponent(projectile, new OwnerReferenceCD { owner = entity });
                ecb.SetComponent(projectile, new RandomCD { Value = inherited });

                EntityUtility.InheritAttackData(ecb, entity, projectile, conditionsTable, behaviourLookup, conditionsLookup);
                if (factionLookup.HasComponent(projectilePrefab))
                    EntityUtility.InheritFaction(ecb, entity, projectile, factionLookup);

            })
                .WithName("ZenithWeaponTriggerSystem")
                .WithBurst()
                .WithAll<ZenithWeaponTriggerTag>()
                .Schedule();

            // 清理本次查询分配的 NativeArray，防止内存泄漏
            // enemyTransforms.Dispose();
            // enemyHealth.Dispose();
            base.OnUpdate();
        }
    }
}