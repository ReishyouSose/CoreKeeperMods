using Assets.TerraZenith.Scripts.Component;
using Inventory;
using PlayerEquipment;
using Pug.Properties;
using Pug.UnityExtensions;
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
        private ComponentLookup<AttackWithEquipmentTag> attackTagLookup;
        private BufferLookup<LevelEntitiesBuffer> levelEntitiesLookup;
        private BufferLookup<SummarizedConditionEffectsBuffer> effectBufferLookup;
        private BufferLookup<SummarizedConditionsBuffer> conditionsLookup;
        private BufferLookup<SyncedPlayerSharedCooldownTimersCD> sharedCooldownLookup;
        private BufferLookup<InventoryChangeBuffer> inventoryChangeLookup;

        protected override void OnCreate()
        {
            tickRate = (uint)PlatformConfiguration.Instance.SessionConfiguration.SimulationTickRate;
            zenithLookup = SystemAPI.GetComponentLookup<ZenithWeaponCD>(true);
            secondaryLookup = SystemAPI.GetComponentLookup<SecondaryUseCD>(true);
            cooldownLookup = SystemAPI.GetComponentLookup<CooldownCD>(true);
            durabilityLookup = SystemAPI.GetComponentLookup<DurabilityCD>(true);
            consumesManaLookup = SystemAPI.GetComponentLookup<ConsumesManaCD>(true);
            levelLookup = SystemAPI.GetComponentLookup<LevelCD>(true);
            objectPropertiesLookup = SystemAPI.GetComponentLookup<ObjectPropertiesCD>(true);
            aoeLookup = SystemAPI.GetComponentLookup<AOEAttackCD>(true);
            manaLookup = SystemAPI.GetComponentLookup<ManaCD>();
            manaCostLookup = SystemAPI.GetComponentLookup<ManaCostForQueuedAttackCD>();
            attackCooldownLookup = SystemAPI.GetComponentLookup<PlayerAttackCooldownCD>();
            orientationLookup = SystemAPI.GetComponentLookup<AnimationOrientationCD>();
            randomLookup = SystemAPI.GetComponentLookup<RandomCD>();
            godModeLookup = SystemAPI.GetComponentLookup<GodModeCD>();
            queueHitLookup = SystemAPI.GetComponentLookup<QueueHitTriggerCD>();
            reduceDurabilityLookup = SystemAPI.GetComponentLookup<ReduceDurabilityOfEquippedTriggerCD>();
            triggerLookup = SystemAPI.GetComponentLookup<ZenithWeaponTriggerTag>();
            attackTagLookup = SystemAPI.GetComponentLookup<AttackWithEquipmentTag>();
            levelEntitiesLookup = SystemAPI.GetBufferLookup<LevelEntitiesBuffer>(true);
            effectBufferLookup = SystemAPI.GetBufferLookup<SummarizedConditionEffectsBuffer>(true);
            conditionsLookup = SystemAPI.GetBufferLookup<SummarizedConditionsBuffer>(true);
            sharedCooldownLookup = SystemAPI.GetBufferLookup<SyncedPlayerSharedCooldownTimersCD>();
            inventoryChangeLookup = SystemAPI.GetBufferLookup<InventoryChangeBuffer>();
            RequireForUpdate<PugDatabase.DatabaseBankCD>();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            var zenithLookup = this.zenithLookup;
            var secondaryLookup = this.secondaryLookup;
            var cooldownLookup = this.cooldownLookup;
            var durabilityLookup = this.durabilityLookup;
            var consumesManaLookup = this.consumesManaLookup;
            var levelLookup = this.levelLookup;
            var propertiesLookup = objectPropertiesLookup;
            var aoeLookup = this.aoeLookup;
            var manaLookup = this.manaLookup;
            var manaCostLookup = this.manaCostLookup;
            var attackCooldownLookup = this.attackCooldownLookup;
            var orientationLookup = this.orientationLookup;
            var randomLookup = this.randomLookup;
            var godModeLookup = this.godModeLookup;
            var queueHitLookup = this.queueHitLookup;
            var reduceDurabilityLookup = this.reduceDurabilityLookup;
            var triggerLookup = this.triggerLookup;
            var attackTagLookup = this.attackTagLookup;
            var levelEntitiesLookup = this.levelEntitiesLookup;
            var effectsLookup = effectBufferLookup;
            var conditionsLookup = this.conditionsLookup;
            var sharedCooldownLookup = this.sharedCooldownLookup;
            var inventoryChanges = inventoryChangeLookup;
            var database = SystemAPI.GetSingleton<PugDatabase.DatabaseBankCD>();
            var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;
            uint tickRate = this.tickRate;
            Entity inventoryEntity = SystemAPI.GetSingletonEntity<InventoryChangeBuffer>();

            Entities.ForEach((Entity entity, in EquippedObjectCD equipped, in ClientInput input, in EquipmentSlotCD slot, in PlayerRoutineCD routine) =>
            {
                if (routine.activeRoutine != PlayerRoutines.Inactive && routine.activeRoutine != PlayerRoutines.Shielding)
                    return;
                Entity prefab = equipped.equipmentPrefab;
                if (!zenithLookup.HasComponent(prefab) || equipped.isBroken)
                    return;
                secondaryLookup.TryGetComponent(prefab, out var secondary);
                if (slot.currentWindup > 0f && slot.currentWindupTier < secondary.cancelAttackIfNotAtWindupTier)
                    return;
                if (slot.atMaxWindup && aoeLookup.HasComponent(prefab))
                    return;
                if (manaLookup.TryGetComponent(entity, out var playerMana) && !playerMana.isUnlimited)
                {
                    int cost = EquipmentSlot.GetManaCost(entity, prefab, equipped, slot, secondary, consumesManaLookup, levelEntitiesLookup, levelLookup, propertiesLookup, conditionsLookup);
                    if (cost != 0)
                    {
                        playerMana.mana = math.clamp(playerMana.mana - cost, 0, playerMana.maxMana);
                        playerMana.delay = true;
                        manaLookup.GetRefRW(entity).ValueRW = playerMana;
                    }
                    if (manaCostLookup.HasComponent(entity))
                        manaCostLookup.GetRefRW(entity).ValueRW = new ManaCostForQueuedAttackCD { manaCost = cost };
                }

                // 对照 RangeWeaponSlot.CalculateCooldown。41、65 是那份攻速条件的下标。
                float cooldownTime = 0.6f;
                if (cooldownLookup.TryGetComponent(prefab, out var cooldown))
                    cooldownTime = cooldown.cooldown;
                DynamicBuffer<SummarizedConditionEffectsBuffer> effectBuffer = effectsLookup[entity];
                float attackSpeed = math.max(1f + effectBuffer[41].value / 1000f + effectBuffer[65].value / 1000f, 0.1f);
                cooldownTime /= attackSpeed;

                var attackCooldown = attackCooldownLookup.GetRefRW(entity);
                EquipmentSlot.StartCooldownForItem(in equipped, ref attackCooldown.ValueRW, sharedCooldownLookup[entity], tick, tickRate, in database, cooldownLookup, cooldownTime);
                orientationLookup.GetRefRW(entity).ValueRW.facingDirection = Direction.FromVector(input.aimDirection, 0f);
                queueHitLookup.SetComponentEnabled(entity, true);
                triggerLookup.SetComponentEnabled(entity, true);

                if (durabilityLookup.HasComponent(prefab))
                {
                    reduceDurabilityLookup.SetComponentEnabled(entity, true);
                    reduceDurabilityLookup.GetRefRW(entity).ValueRW.triggerCounter++;
                }
                else
                {
                    ObjectDataCD objectData = equipped.containedObject.objectData;
                    ref PugDatabase.EntityObjectInfo info = ref PugDatabase.GetEntityObjectInfo(objectData.objectID, database.databaseBankBlob, objectData.variation);
                    float keepChance = EntityUtility.GetConditionValue(ConditionID.ChanceToNotConsumeExplosives, entity, conditionsLookup) / 100f;
                    if (info.rarity != Rarity.Legendary && randomLookup.GetRefRW(entity).ValueRW.Value.NextFloat() > keepChance)
                    {
                        inventoryChanges[inventoryEntity].Add(new InventoryChangeBuffer
                        {
                            inventoryChangeData = Create.ConsumeEntityAt(entity, equipped.equippedSlotIndex, 1, true, godModeLookup.IsComponentEnabled(entity)),
                            playerEntity = entity
                        });
                    }
                }
                attackTagLookup.SetComponentEnabled(entity, false);
            })
                .WithName("ZenithAttackSystem")
                .WithAll<AttackWithEquipmentTag, Simulate>()
                .WithBurst()
                .Schedule();

            base.OnUpdate();
        }
    }
}
