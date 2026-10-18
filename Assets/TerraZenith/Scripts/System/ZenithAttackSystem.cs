using Assets.TerraZenith.Scripts.Component;
using Inventory;
using PlayerEquipment;
using Pug.Properties;
using Pug.UnityExtensions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

// 天顶剑的服务器逻辑。发射对照玩家挥砍时连续生成 FinalFractal，飞行对照 Projectile.AI_182_FinalFractal。
namespace Assets.TerraZenith.Scripts.System
{
    // 对照 AttackWithEquipmentSystem 里的 RangeWeaponSlot。武器使用改到这里，原版那条路看到空的 projectileID 会自己返回。
    // 两端都在预测里跑。打开 ZenithWeaponTriggerTag 之后，生成系统同一拍把弹幕射出去。
    [UpdateInGroup(typeof(EquipmentAfterUpdateSystemGroup))]
    [UpdateBefore(typeof(AttackWithEquipmentSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation)]
    public partial class ZenithAttackSystem : PugSimulationSystemBase
    {
        private EntityQuery missingTrigger;
        private uint tickRate;
        private ComponentLookup<ZenithWeaponCD> zenithLookup;
        private ComponentLookup<SecondaryUseCD> secondaryLookup;
        private ComponentLookup<CooldownCD> cooldownLookup;
        private ComponentLookup<DurabilityCD> durabilityLookup;
        private ComponentLookup<ConsumesManaCD> consumesManaLookup;
        private ComponentLookup<LevelCD> levelLookup;
        private ComponentLookup<ObjectPropertiesCD> objectPropertiesLookup;
        private ComponentLookup<AOEAttackCD> aoeLookup;
        private ComponentLookup<ManaCD> manaLookup;
        private ComponentLookup<ManaCostForQueuedAttackCD> manaCostLookup;
        private ComponentLookup<PlayerAttackCooldownCD> attackCooldownLookup;
        private ComponentLookup<AnimationOrientationCD> orientationLookup;
        private ComponentLookup<RandomCD> randomLookup;
        private ComponentLookup<GodModeCD> godModeLookup;
        private ComponentLookup<QueueHitTriggerCD> queueHitLookup;
        private ComponentLookup<ReduceDurabilityOfEquippedTriggerCD> reduceDurabilityLookup;
        private ComponentLookup<ZenithWeaponTriggerTag> triggerLookup;
        private BufferLookup<LevelEntitiesBuffer> levelEntitiesLookup;
        private BufferLookup<SummarizedConditionEffectsBuffer> effectBufferLookup;
        private BufferLookup<SummarizedConditionsBuffer> conditionsLookup;
        private BufferLookup<SyncedPlayerSharedCooldownTimersCD> sharedCooldownLookup;
        private BufferLookup<InventoryChangeBuffer> inventoryChangeLookup;

        protected override void OnCreate()
        {
            missingTrigger = GetEntityQuery(
                ComponentType.ReadOnly<ClientInput>(),
                ComponentType.ReadOnly<EquipmentSlotCD>(),
                ComponentType.Exclude<ZenithWeaponTriggerTag>());
            tickRate = (uint)PlatformConfiguration.Instance.SessionConfiguration.SimulationTickRate;
            zenithLookup = GetComponentLookup<ZenithWeaponCD>(true);
            secondaryLookup = GetComponentLookup<SecondaryUseCD>(true);
            cooldownLookup = GetComponentLookup<CooldownCD>(true);
            durabilityLookup = GetComponentLookup<DurabilityCD>(true);
            consumesManaLookup = GetComponentLookup<ConsumesManaCD>(true);
            levelLookup = GetComponentLookup<LevelCD>(true);
            objectPropertiesLookup = GetComponentLookup<ObjectPropertiesCD>(true);
            aoeLookup = GetComponentLookup<AOEAttackCD>(true);
            manaLookup = GetComponentLookup<ManaCD>();
            manaCostLookup = GetComponentLookup<ManaCostForQueuedAttackCD>();
            attackCooldownLookup = GetComponentLookup<PlayerAttackCooldownCD>();
            orientationLookup = GetComponentLookup<AnimationOrientationCD>();
            randomLookup = GetComponentLookup<RandomCD>();
            godModeLookup = GetComponentLookup<GodModeCD>();
            queueHitLookup = GetComponentLookup<QueueHitTriggerCD>();
            reduceDurabilityLookup = GetComponentLookup<ReduceDurabilityOfEquippedTriggerCD>();
            triggerLookup = GetComponentLookup<ZenithWeaponTriggerTag>();
            levelEntitiesLookup = GetBufferLookup<LevelEntitiesBuffer>(true);
            effectBufferLookup = GetBufferLookup<SummarizedConditionEffectsBuffer>(true);
            conditionsLookup = GetBufferLookup<SummarizedConditionsBuffer>(true);
            sharedCooldownLookup = GetBufferLookup<SyncedPlayerSharedCooldownTimersCD>();
            inventoryChangeLookup = GetBufferLookup<InventoryChangeBuffer>();
            RequireForUpdate<PugDatabase.DatabaseBankCD>();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            // 玩家预制件上没有这个开关。两端各自补一个关掉的，不进快照，预测回滚后再缺了也会补上。
            if (!missingTrigger.IsEmptyIgnoreFilter)
            {
                var entities = missingTrigger.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < entities.Length; i++)
                {
                    EntityManager.AddComponent<ZenithWeaponTriggerTag>(entities[i]);
                    EntityManager.SetComponentEnabled<ZenithWeaponTriggerTag>(entities[i], false);
                }
                entities.Dispose();
            }

            zenithLookup.Update(this);
            secondaryLookup.Update(this);
            cooldownLookup.Update(this);
            durabilityLookup.Update(this);
            consumesManaLookup.Update(this);
            levelLookup.Update(this);
            objectPropertiesLookup.Update(this);
            aoeLookup.Update(this);
            manaLookup.Update(this);
            manaCostLookup.Update(this);
            attackCooldownLookup.Update(this);
            orientationLookup.Update(this);
            randomLookup.Update(this);
            godModeLookup.Update(this);
            queueHitLookup.Update(this);
            reduceDurabilityLookup.Update(this);
            triggerLookup.Update(this);
            levelEntitiesLookup.Update(this);
            effectBufferLookup.Update(this);
            conditionsLookup.Update(this);
            sharedCooldownLookup.Update(this);
            inventoryChangeLookup.Update(this);

            var weapons = zenithLookup;
            var secondaryUses = secondaryLookup;
            var cooldowns = cooldownLookup;
            var durabilities = durabilityLookup;
            var manaCosts = consumesManaLookup;
            var levels = levelLookup;
            var properties = objectPropertiesLookup;
            var aoeAttacks = aoeLookup;
            var mana = manaLookup;
            var queuedMana = manaCostLookup;
            var attackCooldowns = attackCooldownLookup;
            var orientations = orientationLookup;
            var randoms = randomLookup;
            var godMode = godModeLookup;
            var queueHits = queueHitLookup;
            var reduceDurability = reduceDurabilityLookup;
            var triggers = triggerLookup;
            var levelEntities = levelEntitiesLookup;
            var effects = effectBufferLookup;
            var conditions = conditionsLookup;
            var sharedCooldowns = sharedCooldownLookup;
            var inventoryChanges = inventoryChangeLookup;
            var database = SystemAPI.GetSingleton<PugDatabase.DatabaseBankCD>();
            var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;
            uint rate = tickRate;
            Entity inventoryEntity = SystemAPI.GetSingletonEntity<InventoryChangeBuffer>();

            Entities
                .WithAll<AttackWithEquipmentTag, Simulate>()
                .ForEach((Entity entity, in EquippedObjectCD equipped, in ClientInput input, in EquipmentSlotCD slot, in PlayerRoutineCD routine) =>
                {
                    if (routine.activeRoutine != PlayerRoutines.Inactive && routine.activeRoutine != PlayerRoutines.Shielding)
                        return;
                    Entity prefab = equipped.equipmentPrefab;
                    if (!weapons.HasComponent(prefab) || equipped.isBroken)
                        return;
                    secondaryUses.TryGetComponent(prefab, out var secondary);
                    if (slot.currentWindup > 0f && slot.currentWindupTier < secondary.cancelAttackIfNotAtWindupTier)
                        return;
                    if (slot.atMaxWindup && aoeAttacks.HasComponent(prefab))
                        return;

                    if (mana.TryGetComponent(entity, out var playerMana) && !playerMana.isUnlimited)
                    {
                        int cost = EquipmentSlot.GetManaCost(entity, prefab, equipped, slot, secondary, manaCosts, levelEntities, levels, properties, conditions);
                        if (cost != 0)
                        {
                            playerMana.mana = math.clamp(playerMana.mana - cost, 0, playerMana.maxMana);
                            playerMana.delay = true;
                            mana.GetRefRW(entity).ValueRW = playerMana;
                        }
                        if (queuedMana.HasComponent(entity))
                            queuedMana.GetRefRW(entity).ValueRW = new ManaCostForQueuedAttackCD { manaCost = cost };
                    }

                    // 对照 RangeWeaponSlot.CalculateCooldown。41、65 是那份攻速条件的下标。
                    float cooldownTime = 0.6f;
                    if (cooldowns.TryGetComponent(prefab, out var cooldown))
                        cooldownTime = cooldown.cooldown;
                    DynamicBuffer<SummarizedConditionEffectsBuffer> effectBuffer = effects[entity];
                    float attackSpeed = math.max(1f + effectBuffer[41].value / 1000f + effectBuffer[65].value / 1000f, 0.1f);
                    cooldownTime /= attackSpeed;

                    var attackCooldown = attackCooldowns.GetRefRW(entity);
                    EquipmentSlot.StartCooldownForItem(in equipped, ref attackCooldown.ValueRW, sharedCooldowns[entity], tick, rate, in database, cooldowns, cooldownTime);
                    orientations.GetRefRW(entity).ValueRW.facingDirection = Direction.FromVector(input.aimDirection, 0f);
                    queueHits.SetComponentEnabled(entity, true);
                    triggers.SetComponentEnabled(entity, true);

                    if (durabilities.HasComponent(prefab))
                    {
                        reduceDurability.SetComponentEnabled(entity, true);
                        reduceDurability.GetRefRW(entity).ValueRW.triggerCounter++;
                    }
                    else
                    {
                        ObjectDataCD objectData = equipped.containedObject.objectData;
                        ref PugDatabase.EntityObjectInfo info = ref PugDatabase.GetEntityObjectInfo(objectData.objectID, database.databaseBankBlob, objectData.variation);
                        float keepChance = EntityUtility.GetConditionValue(ConditionID.ChanceToNotConsumeExplosives, entity, conditions) / 100f;
                        if (info.rarity != Rarity.Legendary && randoms.GetRefRW(entity).ValueRW.Value.NextFloat() > keepChance)
                        {
                            inventoryChanges[inventoryEntity].Add(new InventoryChangeBuffer
                            {
                                inventoryChangeData = Create.ConsumeEntityAt(entity, equipped.equippedSlotIndex, 1, true, godMode.IsComponentEnabled(entity)),
                                playerEntity = entity
                            });
                        }
                    }
                }).WithoutBurst().Run();

            base.OnUpdate();
        }
    }
}
