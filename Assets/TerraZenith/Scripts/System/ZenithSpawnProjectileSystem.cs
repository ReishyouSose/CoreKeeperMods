using Assets.TerraZenith.Scripts.Component;
using Assets.TerraZenith.Scripts.Core;
using PlayerEquipment;
using Pug.UnityExtensions;
using Unity.Collections;
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
        private BufferLookup<LevelEntitiesBuffer> levelEntitiesLookup;
        private BufferLookup<SummarizedConditionEffectsBuffer> effectBufferLookup;
        private BufferLookup<SummarizedConditionsBuffer> conditionsLookup;

        protected override void OnCreate()
        {
            enemies = GetEntityQuery(ComponentType.ReadOnly<EnemyCD>(), ComponentType.ReadOnly<LocalTransform>(), ComponentType.ReadOnly<HealthCD>());
            zenithLookup = GetComponentLookup<ZenithWeaponCD>(true);
            triggerLookup = GetComponentLookup<ZenithWeaponTriggerTag>();
            weaponDamageLookup = GetComponentLookup<HasWeaponDamageCD>(true);
            durabilityLookup = GetComponentLookup<DurabilityCD>(true);
            levelLookup = GetComponentLookup<LevelCD>(true);
            levelDamageLookup = GetComponentLookup<WeaponDamageCD>(true);
            cooldownLookup = GetComponentLookup<CooldownCD>(true);
            objectDataLookup = GetComponentLookup<ObjectDataCD>(true);
            factionLookup = GetComponentLookup<FactionCD>(true);
            behaviourLookup = GetComponentLookup<BehaviourTagsCD>(true);
            slotLookup = GetComponentLookup<EquipmentSlotCD>(true);
            levelEntitiesLookup = GetBufferLookup<LevelEntitiesBuffer>(true);
            effectBufferLookup = GetBufferLookup<SummarizedConditionEffectsBuffer>(true);
            conditionsLookup = GetBufferLookup<SummarizedConditionsBuffer>(true);
            RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
            RequireForUpdate<PugDatabase.DatabaseBankCD>();
            RequireForUpdate<ConditionsTableCD>();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            zenithLookup.Update(this);
            triggerLookup.Update(this);
            weaponDamageLookup.Update(this);
            durabilityLookup.Update(this);
            levelLookup.Update(this);
            levelDamageLookup.Update(this);
            cooldownLookup.Update(this);
            objectDataLookup.Update(this);
            factionLookup.Update(this);
            behaviourLookup.Update(this);
            slotLookup.Update(this);
            levelEntitiesLookup.Update(this);
            effectBufferLookup.Update(this);
            conditionsLookup.Update(this);

            var weapons = zenithLookup;
            var triggers = triggerLookup;
            var weaponDamages = weaponDamageLookup;
            var durabilities = durabilityLookup;
            var levels = levelLookup;
            var levelDamages = levelDamageLookup;
            var cooldowns = cooldownLookup;
            var objectDatas = objectDataLookup;
            var factions = factionLookup;
            var behaviours = behaviourLookup;
            var slots = slotLookup;
            var levelEntities = levelEntitiesLookup;
            var effects = effectBufferLookup;
            var conditions = conditionsLookup;
            var database = SystemAPI.GetSingleton<PugDatabase.DatabaseBankCD>();
            var conditionsTable = SystemAPI.GetSingleton<ConditionsTableCD>();
            bool firstTime = SystemAPI.GetSingleton<NetworkTime>().IsFirstTimeFullyPredictingTick;
            var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);
            var enemyTransforms = enemies.ToComponentDataArray<LocalTransform>(Allocator.Temp);
            var enemyHealth = enemies.ToComponentDataArray<HealthCD>(Allocator.Temp);

            Entities
                .WithAll<ZenithWeaponTriggerTag, Simulate>()
                .ForEach((Entity entity, ref RandomCD random, in EquippedObjectCD equipped, in ClientInput input, in LocalTransform transform, in GhostOwner ghostOwner, in ManaCostForQueuedAttackCD manaCost) =>
                {
                    triggers.SetComponentEnabled(entity, false);
                    Entity prefab = equipped.equipmentPrefab;
                    if (!weapons.TryGetComponent(prefab, out var weapon) || weapon.projectileID == ObjectID.None)
                        return;

                    // 重放和第一次预测拨同样多次随机数，玩家的 RandomCD 才不会岔开。
                    int index = 0;
                    int phaseByte = random.Value.NextInt(0, 256);
                    int side = random.Value.NextInt(0, 2);
                    if (ZenithBalance.ShotsPerSwing > 1)
                        index = random.Value.NextInt(1, ZenithBalance.SwordTypes);
                    int packed = ZenithBalance.PackVariation(index, phaseByte, side);
                    var inherited = PugRandom.InheritRngFromEntity(ref random.Value);
                    if (!firstTime)
                        return;

                    Entity projectilePrefab = PugDatabase.GetPrimaryPrefabEntity(weapon.projectileID, database.databaseBankBlob, 0);
                    if (projectilePrefab == Entity.Null)
                        return;

                    ZenithBalance.UnpackVariation(packed, out index, out float phase, out _);
                    float2 playerPos = transform.Position.xz;
                    float2 aim = ZenithBalance.ClampAim(playerPos, input.mouseOrJoystickWorldPoint);
                    float2 vector = aim - playerPos;
                    if (index == 1 || index == 2)
                    {
                        bool homed = TryHome(aim, enemyTransforms, enemyHealth, out float2 home);
                        if (homed)
                            vector = home - playerPos;
                        if (index == 2 || !homed)
                            vector += new float2(math.cos(phase), math.sin(phase)) * (150f / 16f);
                    }

                    float2 velocity = vector * 0.5f;
                    float ellipse = math.lerp(-100f, 101f, phase / (math.PI * 2f)) / 16f;
                    ObjectDataCD held = equipped.containedObject.objectData;
                    int damage = 10;
                    bool reinforced = false;
                    if (durabilities.TryGetComponent(prefab, out var durability))
                        reinforced = durability.IsReinforced(held.amount);
                    Entity levelEntity = EntityUtility.GetLevelEntity(prefab, held, levelEntities, levels);
                    if (levelEntity != Entity.Null && levelDamages.TryGetComponent(levelEntity, out var levelDamage))
                        damage = levelDamage.GetDamage(reinforced);
                    bool isMagic = weaponDamages.TryGetComponent(prefab, out var marker) && marker.isMagic;
                    int weaponLevel = held.variation > 0 ? held.variation : 1;
                    if (held.variation <= 0 && levels.TryGetComponent(prefab, out var level))
                        weaponLevel = level.level;
                    float weaponCooldown = 0.6f;
                    if (cooldowns.TryGetComponent(prefab, out var cooldown))
                        weaponCooldown = cooldown.cooldown;
                    DynamicBuffer<SummarizedConditionEffectsBuffer> effectBuffer = effects[entity];
                    float attackSpeed = math.max(1f + effectBuffer[41].value / 1000f + effectBuffer[65].value / 1000f, 0.1f);
                    weaponCooldown /= attackSpeed;

                    Entity projectile = ecb.Instantiate(projectilePrefab);
                    ecb.SetComponent(projectile, LocalTransform.FromPosition(transform.Position));
                    ecb.SetComponent(projectile, new ZenithFlightCD
                    {
                        velocity = velocity,
                        ellipse = ellipse,
                        variation = packed,
                        initialized = 1
                    });
                    if (objectDatas.TryGetComponent(projectilePrefab, out var objectData))
                    {
                        objectData.variation = packed;
                        ecb.SetComponent(projectile, objectData);
                    }
                    ecb.SetComponent(projectile, new ProjectileSetupCD
                    {
                        damage = damage,
                        directionRadians = math.atan2(velocity.y, velocity.x),
                        isMagic = isMagic
                    });
                    slots.TryGetComponent(entity, out var slot);
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
                    EntityUtility.InheritAttackData(ecb, entity, projectile, conditionsTable, behaviours, conditions);
                    if (factions.HasComponent(projectilePrefab))
                        EntityUtility.InheritFaction(ecb, entity, projectile, factions);
                }).WithoutBurst().Run();

            enemyTransforms.Dispose();
            enemyHealth.Dispose();
            base.OnUpdate();
        }

        // 准星周围 25 格内最近的活着的敌人。对照 GetZenithTarget。序号 0 不走这里。
        private static bool TryHome(float2 aim, NativeArray<LocalTransform> transforms, NativeArray<HealthCD> healths, out float2 home)
        {
            float best = ZenithBalance.HomeRadius * ZenithBalance.HomeRadius;
            home = aim;
            bool found = false;
            for (int i = 0; i < transforms.Length; i++)
            {
                if (healths[i].health <= 0)
                    continue;
                float2 point = transforms[i].Position.xz;
                float distance = math.lengthsq(point - aim);
                if (distance >= best)
                    continue;
                best = distance;
                home = point;
                found = true;
            }
            return found;
        }
    }
}
