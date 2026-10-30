using Assets.TerraZenith.Scripts.Component;
using Inventory;
using PlayerEquipment;
using PlayerState;
using Pug.Properties;
using System;
using Unity.Entities;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

namespace Assets.TerraZenith.Scripts.System
{
    /// <summary>
    /// 伤害结算系统。
    /// 改造后职责：调用方（如 ZenitHitSystem）已经完成命中判定并明确 target，
    /// 本系统只负责：校验 → 伤害修正 → 写入 HealthChangeBuffer → 状态/击退/动画/技能统计。
    /// </summary>
    public partial class CustomDamageCalculateSystem : PugSimulationSystemBase
    {
        private NetworkTick currentTick;

        private NetworkTick startServerTick;
        private uint ticksPerSecond;

        // ===== 以下 lookup 仅保留"结算"仍会用到的；命中判定专用的已删除 =====
        private ComponentLookup<LocalTransform> localTransformLookup;
        private ComponentLookup<ObjectDataCD> objectDataLookup;
        private ComponentLookup<EntityPartCD> entityPartLookup;
        private ComponentLookup<FactionCD> factionLookup;
        private ComponentLookup<IndestructibleCD> indestructibleLookup;
        private ComponentLookup<ImmuneToPushBackCD> immuneToPushBackLookup;
        private ComponentLookup<CantBeAttackedCD> cantBeAttackedLookup;
        private ComponentLookup<ExplosionCD> explosionLookup;
        private ComponentLookup<ExplodeStateCD> explodeStateLookup;
        private ComponentLookup<TileCD> tileLookup;
        private ComponentLookup<OwnerReferenceCD> ownerLookup;
        private ComponentLookup<BossCD> bossLookup;
        private ComponentLookup<ObjectCategoryTagsCD> tagsLookup;
        private ComponentLookup<CritterCD> critterLookup;
        private ComponentLookup<MinionCD> minionLookup;
        private ComponentLookup<HealthCD> healthLookup;
        private ComponentLookup<ManaCD> manaLookup;
        private ComponentLookup<MagicBarrierCD> magicBarrierLookup;
        private ComponentLookup<EnemyCD> enemyLookup;
        private ComponentLookup<ImmuneToRangeDamageCD> immuneToRangeDamageLookup;
        private ComponentLookup<RequiresDrillCD> requiresDrillLookup;
        private BufferLookup<HealthChangeBuffer> healthChangeBufferLookup;
        private ComponentLookup<LastDamageTakenTimeCD> lastDamageTakenTimeLookup;
        private ComponentLookup<RandomCD> randomLookup;
        private ComponentLookup<PlayerAttackCD> playerAttackLookup;
        private BufferLookup<SummarizedConditionsBuffer> summarizedConditionsBufferLookup;
        private BufferLookup<SummarizedConditionEffectsBuffer> summarizedConditionEffectsBufferLookup;
        private ComponentLookup<PetCD> petLookup;
        private ComponentLookup<PetOwnerCD> petOwnerLookup;
        private ComponentLookup<CattleCD> cattleLookup;
        private ComponentLookup<IgnoreImmuneZoneCD> ignoreImmuneLookup;
        private ComponentLookup<SnakeMovementStateCD> snakeMovementStateLookup;
        private ComponentLookup<PhysicsVelocity> physicsVelocityAccessor;
        private ComponentLookup<DamageReductionCD> damageReductionLookup;
        private ComponentLookup<DrillCD> drillLookup;
        private TileAccessor tileAccessor;
        private ComponentLookup<ObjectTypeCD> objectTypeLookup;
        private ComponentLookup<DestructibleObjectCD> destructibleLookup;
        private ComponentLookup<ObjectPropertiesCD> propertiesLookup;
        private ComponentLookup<PhaseTransitionStateCD> phaseTransitionStateLookup;
        private ComponentLookup<Simulate> simulateLookup;
        private ComponentLookup<DontCountAsHitForAttackerCD> dontCountAsHitLookup;
        private ComponentLookup<AttackContinuouslyCD> attackContinuouslyLookup;
        private ConditionsTableCD conditionsTableCD;
        private PugDatabase.DatabaseBankCD databaseBank;
        private PhysicsWorld physicsWorld;
        private ComponentLookup<PlayerGhost> playerGhostLookup;
        private ComponentLookup<PlayerStateCD> playerStateLookup;
        private BufferLookup<SummarizedConditionsBuffer> summarizeConiditionsLookup;
        private ComponentLookup<ObjectCategoryTagsCD> objectCategoryTagsLookup;
        private ComponentLookup<GhostInstance> ghostInstanceLookup;
        private BufferLookup<ConditionsBuffer> conditionsBufferLookup;
        private ComponentLookup<UseOffHandStateCD> useOffHandStateLookup;
        private ComponentLookup<AnimationOrientationCD> animationOrientationLookup;
        private ComponentLookup<ImmuneToDamageCD> immuneToDamageLookup;
        private ComponentLookup<DestroyTimerCD> destroyTimerLookup;
        private ComponentLookup<GhostOwner> ghostOwnerLookup;
        private ComponentLookup<BehaviourTagsCD> behaviourTagsLookup;
        private ComponentLookup<PlayerInvincibilityCD> playerInvincibilityLookup;
        private ComponentLookup<PhysicsMass> physicsMassLookup;
        private ComponentLookup<ShieldCD> shieldLookup;
        private BufferLookup<GhostEffectEventBuffer> ghostEffectEventBufferLookup;
        private ComponentLookup<GhostEffectEventBufferPointerCD> ghostEffectEventBufferPointerLookup;
        private BufferLookup<NewConditionsBuffer> newConditionsBufferLookup;
        private BufferLookup<RemoveConditionsBuffer> removeConditionsBufferLookup;
        private ComponentLookup<LastAttackerCD> lastAttackerlookup;
        private ComponentLookup<TookDamageStateCD> tookDamageStateLookup;
        private ComponentLookup<SleepStateCD> sleepStateLookup;
        private BufferLookup<AnimationBuffer> animationBufferLookup;
        private ComponentLookup<AnimationBufferPointer> animationBufferPointerLookup;
        private ComponentLookup<ReduceDurabilityOfAllEquipmentTriggerCD> reduceDurabilityOfAllEquipmentTriggerLookup;
        private ComponentLookup<GodModeCD> godModeLookup;
        private BufferLookup<InventoryChangeBuffer> inventoryChangeBuffer;
        private ComponentLookup<EquipmentCD> equipmentLookup;
        private BufferLookup<DealDamageToEntityBuffer> dealDamageToEntityBuffer;
        private BufferLookup<ContainedObjectsBuffer> containedObjectsBuffer;
        private ComponentLookup<ReceivedPushbackCD> receivedPushbackLookup;
        private ComponentLookup<MoveToPredictedByCombatOrInventoryInteractionCD> moveToPredictedByCombatInteractionLookup;
        private ComponentLookup<MoveToPredictedByPushbackCD> moveToPredictedByPushbackLookup;
        private BufferLookup<NewCombatantsBuffer> newCombatantsBufferLookup;
        private uint tickRate;
        private ServerSeedCD serverSeedCD;
        private WorldInfoCD worldInfo;
        private ComponentLookup<CollectedAndEnabledSoulsMask> soulsMaskLookup;
        private ComponentLookup<MortarProjectileDamageEffectCD> mortarProjectileDamageEffectLookup;
        private ComponentLookup<PiercingProjectileCD> piercingProjectileLookup;
        private ComponentLookup<ProjectileCD> projectileLookup;
        private ComponentLookup<RootCD> rootLookup;
        private ComponentLookup<MineableCD> mineableLookup;
        private ComponentLookup<PlayerGraveCD> playerGraveLookup;
        private ComponentLookup<AttackableWithMeleeCD> attackableWithMeleeLookup;
        private ComponentLookup<MerchantCD> merchantLookup;
        private ComponentLookup<EntityDestroyedCD> entityDestroyedLookup;
        private ComponentLookup<DropAllItemsOnHitCD> dropAllItemsOnHitLookup;
        private ComponentLookup<ProjectileSourceCD> projectileSourceLookup;
        private ComponentLookup<DamageSourceCD> damageSourceLookup;
        private ComponentLookup<EquippedObjectCD> equippedObjectLookup;
        private ComponentLookup<EquipmentSlotCD> equipmentSlotLookup;
        private ComponentLookup<CooldownCD> cooldownLookup;
        private ComponentLookup<HasWeaponDamageCD> hasWeaponDamageLookup;
        private DynamicBuffer<HealthChangeBuffer> healthChangeBufferEntity;
        private DynamicBuffer<InventoryChangeBuffer> inventoryChangeBufferEntity;

        private EntityQuery networkTimeQuery;
        private EntityQuery physicsWorldQuery;
        private EntityQuery physicsWorldHistoryQuery;
        private EntityQuery worldInfoQuery;
        private bool isFirstTimeFullyPredictingTick;

        protected override void OnCreate()
        {
            Enabled = false;
            RequireForUpdate<NetworkTime>();
            startServerTick.Increment();
            ticksPerSecond = (uint)PlatformConfiguration.Instance.SessionConfiguration.SimulationTickRate;
            RequireForUpdate<HealthChangeBuffer>();

            localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(false);
            objectDataLookup = SystemAPI.GetComponentLookup<ObjectDataCD>(false);
            entityPartLookup = SystemAPI.GetComponentLookup<EntityPartCD>(true);
            factionLookup = SystemAPI.GetComponentLookup<FactionCD>(true);
            indestructibleLookup = SystemAPI.GetComponentLookup<IndestructibleCD>(true);
            tileLookup = SystemAPI.GetComponentLookup<TileCD>(true);
            ownerLookup = SystemAPI.GetComponentLookup<OwnerReferenceCD>(true);
            bossLookup = SystemAPI.GetComponentLookup<BossCD>(true);
            tagsLookup = SystemAPI.GetComponentLookup<ObjectCategoryTagsCD>(true);
            critterLookup = SystemAPI.GetComponentLookup<CritterCD>(true);
            minionLookup = SystemAPI.GetComponentLookup<MinionCD>(true);
            healthLookup = SystemAPI.GetComponentLookup<HealthCD>(false);
            manaLookup = SystemAPI.GetComponentLookup<ManaCD>(false);
            magicBarrierLookup = SystemAPI.GetComponentLookup<MagicBarrierCD>(false);
            enemyLookup = SystemAPI.GetComponentLookup<EnemyCD>(true);
            immuneToRangeDamageLookup = SystemAPI.GetComponentLookup<ImmuneToRangeDamageCD>(true);
            requiresDrillLookup = SystemAPI.GetComponentLookup<RequiresDrillCD>(true);
            immuneToPushBackLookup = SystemAPI.GetComponentLookup<ImmuneToPushBackCD>(true);
            cantBeAttackedLookup = SystemAPI.GetComponentLookup<CantBeAttackedCD>(true);
            explosionLookup = SystemAPI.GetComponentLookup<ExplosionCD>(false);
            explodeStateLookup = SystemAPI.GetComponentLookup<ExplodeStateCD>(false);
            healthChangeBufferLookup = SystemAPI.GetBufferLookup<HealthChangeBuffer>(false);
            summarizedConditionsBufferLookup = SystemAPI.GetBufferLookup<SummarizedConditionsBuffer>(true);
            summarizedConditionEffectsBufferLookup = SystemAPI.GetBufferLookup<SummarizedConditionEffectsBuffer>(true);
            petLookup = SystemAPI.GetComponentLookup<PetCD>(true);
            petOwnerLookup = SystemAPI.GetComponentLookup<PetOwnerCD>(true);
            cattleLookup = SystemAPI.GetComponentLookup<CattleCD>(true);
            ignoreImmuneLookup = SystemAPI.GetComponentLookup<IgnoreImmuneZoneCD>(true);
            snakeMovementStateLookup = SystemAPI.GetComponentLookup<SnakeMovementStateCD>(false);
            physicsVelocityAccessor = SystemAPI.GetComponentLookup<PhysicsVelocity>(false);
            damageReductionLookup = SystemAPI.GetComponentLookup<DamageReductionCD>(true);
            drillLookup = SystemAPI.GetComponentLookup<DrillCD>(true);
            objectTypeLookup = SystemAPI.GetComponentLookup<ObjectTypeCD>(true);
            destructibleLookup = SystemAPI.GetComponentLookup<DestructibleObjectCD>(true);
            propertiesLookup = SystemAPI.GetComponentLookup<ObjectPropertiesCD>(true);
            phaseTransitionStateLookup = SystemAPI.GetComponentLookup<PhaseTransitionStateCD>(true);
            simulateLookup = SystemAPI.GetComponentLookup<Simulate>(true);
            dontCountAsHitLookup = SystemAPI.GetComponentLookup<DontCountAsHitForAttackerCD>(true);
            attackContinuouslyLookup = SystemAPI.GetComponentLookup<AttackContinuouslyCD>(false);
            lastDamageTakenTimeLookup = SystemAPI.GetComponentLookup<LastDamageTakenTimeCD>(false);
            randomLookup = SystemAPI.GetComponentLookup<RandomCD>(false);
            playerAttackLookup = SystemAPI.GetComponentLookup<PlayerAttackCD>(false);


            RequireForUpdate<ConditionsTableCD>();
            RequireForUpdate<PugDatabase.DatabaseBankCD>();

            playerGhostLookup = SystemAPI.GetComponentLookup<PlayerGhost>(true);
            playerStateLookup = SystemAPI.GetComponentLookup<PlayerStateCD>(true);
            summarizeConiditionsLookup = SystemAPI.GetBufferLookup<SummarizedConditionsBuffer>(true);
            objectCategoryTagsLookup = SystemAPI.GetComponentLookup<ObjectCategoryTagsCD>(true);
            ghostInstanceLookup = SystemAPI.GetComponentLookup<GhostInstance>(true);
            conditionsBufferLookup = SystemAPI.GetBufferLookup<ConditionsBuffer>(false);
            useOffHandStateLookup = SystemAPI.GetComponentLookup<UseOffHandStateCD>(true);
            animationOrientationLookup = SystemAPI.GetComponentLookup<AnimationOrientationCD>(false);
            immuneToDamageLookup = SystemAPI.GetComponentLookup<ImmuneToDamageCD>(true);
            destroyTimerLookup = SystemAPI.GetComponentLookup<DestroyTimerCD>(true);
            ghostOwnerLookup = SystemAPI.GetComponentLookup<GhostOwner>(false);
            behaviourTagsLookup = SystemAPI.GetComponentLookup<BehaviourTagsCD>(true);
            playerInvincibilityLookup = SystemAPI.GetComponentLookup<PlayerInvincibilityCD>(true);
            physicsMassLookup = SystemAPI.GetComponentLookup<PhysicsMass>(true);
            shieldLookup = SystemAPI.GetComponentLookup<ShieldCD>(true);
            ghostEffectEventBufferLookup = SystemAPI.GetBufferLookup<GhostEffectEventBuffer>(false);
            ghostEffectEventBufferPointerLookup = SystemAPI.GetComponentLookup<GhostEffectEventBufferPointerCD>(false);
            newConditionsBufferLookup = SystemAPI.GetBufferLookup<NewConditionsBuffer>(false);
            removeConditionsBufferLookup = SystemAPI.GetBufferLookup<RemoveConditionsBuffer>(false);
            lastAttackerlookup = SystemAPI.GetComponentLookup<LastAttackerCD>(false);
            tookDamageStateLookup = SystemAPI.GetComponentLookup<TookDamageStateCD>(true);
            sleepStateLookup = SystemAPI.GetComponentLookup<SleepStateCD>(true);
            animationBufferLookup = SystemAPI.GetBufferLookup<AnimationBuffer>(false);
            animationBufferPointerLookup = SystemAPI.GetComponentLookup<AnimationBufferPointer>(false);
            reduceDurabilityOfAllEquipmentTriggerLookup = SystemAPI.GetComponentLookup<ReduceDurabilityOfAllEquipmentTriggerCD>(false);
            godModeLookup = SystemAPI.GetComponentLookup<GodModeCD>(true);
            inventoryChangeBuffer = SystemAPI.GetBufferLookup<InventoryChangeBuffer>(false);
            equipmentLookup = SystemAPI.GetComponentLookup<EquipmentCD>(true);
            soulsMaskLookup = SystemAPI.GetComponentLookup<CollectedAndEnabledSoulsMask>(true);
            dealDamageToEntityBuffer = SystemAPI.GetBufferLookup<DealDamageToEntityBuffer>(false);
            containedObjectsBuffer = SystemAPI.GetBufferLookup<ContainedObjectsBuffer>(true);
            receivedPushbackLookup = SystemAPI.GetComponentLookup<ReceivedPushbackCD>(false);
            moveToPredictedByCombatInteractionLookup = SystemAPI.GetComponentLookup<MoveToPredictedByCombatOrInventoryInteractionCD>(false);
            moveToPredictedByPushbackLookup = SystemAPI.GetComponentLookup<MoveToPredictedByPushbackCD>(false);
            newCombatantsBufferLookup = SystemAPI.GetBufferLookup<NewCombatantsBuffer>(true);

            RequireForUpdate<ServerSeedCD>();
            RequireForUpdate<WorldInfoCD>();
            RequireForUpdate<InventoryChangeBuffer>();

            networkTimeQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkTime>());
            physicsWorldQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
            physicsWorldHistoryQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldHistorySingleton>());
            worldInfoQuery = EntityManager.CreateEntityQuery(ComponentType.ReadOnly<WorldInfoCD>());

            mortarProjectileDamageEffectLookup = SystemAPI.GetComponentLookup<MortarProjectileDamageEffectCD>(true);
            piercingProjectileLookup = SystemAPI.GetComponentLookup<PiercingProjectileCD>(false);
            projectileLookup = SystemAPI.GetComponentLookup<ProjectileCD>(false);
            rootLookup = SystemAPI.GetComponentLookup<RootCD>(true);
            mineableLookup = SystemAPI.GetComponentLookup<MineableCD>(true);
            playerGraveLookup = SystemAPI.GetComponentLookup<PlayerGraveCD>(true);
            attackableWithMeleeLookup = SystemAPI.GetComponentLookup<AttackableWithMeleeCD>(true);
            merchantLookup = SystemAPI.GetComponentLookup<MerchantCD>(true);
            entityDestroyedLookup = SystemAPI.GetComponentLookup<EntityDestroyedCD>(true);
            dropAllItemsOnHitLookup = SystemAPI.GetComponentLookup<DropAllItemsOnHitCD>(true);
            projectileSourceLookup = SystemAPI.GetComponentLookup<ProjectileSourceCD>(true);
            damageSourceLookup = SystemAPI.GetComponentLookup<DamageSourceCD>(true);
            equippedObjectLookup = SystemAPI.GetComponentLookup<EquippedObjectCD>(true);
            equipmentSlotLookup = SystemAPI.GetComponentLookup<EquipmentSlotCD>(true);
            cooldownLookup = SystemAPI.GetComponentLookup<CooldownCD>(true);
            hasWeaponDamageLookup = SystemAPI.GetComponentLookup<HasWeaponDamageCD>(true);

            base.OnCreate();
        }

        protected override void OnStartRunning()
        {
            tileAccessor = CreateTileAccessor();
            physicsWorld = GetPhysicsWorld();
            base.OnStartRunning();
        }

        protected override void OnUpdate()
        {
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            currentTick = networkTime.ServerTick;
            isFirstTimeFullyPredictingTick = networkTime.IsFirstTimeFullyPredictingTick;

            healthChangeBufferEntity = SystemAPI.GetSingletonBuffer<HealthChangeBuffer>();
            conditionsTableCD = SystemAPI.GetSingleton<ConditionsTableCD>();
            databaseBank = SystemAPI.GetSingleton<PugDatabase.DatabaseBankCD>();
            serverSeedCD = SystemAPI.GetSingleton<ServerSeedCD>();
            worldInfo = SystemAPI.GetSingleton<WorldInfoCD>();
            inventoryChangeBufferEntity = SystemAPI.GetSingletonBuffer<InventoryChangeBuffer>();

            base.OnUpdate();
        }

        // ================================================================
        // ★ 改造点：新入口。调用方已确认 target 被命中，这里只做结算。
        // ================================================================
        private bool ApplyDamageToTarget(EntityCommandBuffer ecb, DamageApplyBuffer p)
        {
            // ---------- 1. 基础校验（target 必须存在且可受伤） ----------
            Entity target = p.target;
            if (target == Entity.Null)
                return false;
            if (!healthLookup.HasComponent(target))
                return false;
            if (entityDestroyedLookup.HasComponent(target))
                return false;
            if (indestructibleLookup.HasComponent(target))
                return false;
            if (immuneToDamageLookup.HasComponent(target))
                return false;
            if (godModeLookup.HasComponent(target))
                return false;
            if (playerInvincibilityLookup.HasComponent(target))
                return false;
            if (cantBeAttackedLookup.HasComponent(target) && !ignoreImmuneLookup.HasComponent(p.attacker))
                return false;

            // ---------- 2. 阵营合法性（兜底；若调用方已筛可再删） ----------
            FactionCD attackerFaction = factionLookup.HasComponent(p.attacker) ? factionLookup[p.attacker] : default;
            FactionCD targetFaction = factionLookup.HasComponent(target) ? factionLookup[target] : default;
            if (!attackerFaction.CanAttack(targetFaction, worldInfo))
                return false;

            // ---------- 3. 取归属信息（技能统计用） ----------
            EntityUtility.OwnerInfo ownerInfo = EntityUtility.GetOwnerInfo(
                entityPartLookup, ownerLookup, summarizeConiditionsLookup, playerGhostLookup,
                petLookup, minionLookup, bossLookup, healthLookup, enemyLookup, p.attacker);

            // ---------- 4. 伤害修正（护甲/减伤/元素/状态） ----------
            // 保持原实现：原 AttackInternal 中 CheckForHit 内部对单目标计算 finalDamage 的逻辑，
            // 请抽取为独立方法 CalculateFinalDamage(target, p, ownerInfo)。
            float finalDamage = CalculateFinalDamage(target, p, ownerInfo);
            if (finalDamage <= 0f && !p.performAttackEvenIfZeroDamage)
                return false;

            // ---------- 5. 写入 HealthChangeBuffer（核心结算） ----------
            // ★ 保持你原 CheckForHit 内写 buffer 的方式；这里给出语义等价写法。
            var hcBuffer = healthChangeBufferEntity;
            hcBuffer.Add(new HealthChangeBuffer
            {
                // 字段按你项目实际定义填：
                // entity = target,
                // amount = -finalDamage,
                // damageEffectType = p.damageEffectType,
                // attacker = p.attacker,
                // hitPosition = p.hitPosition,
                // isPredicted = p.isPredicted,
            });

            // ---------- 6. 状态 / 效果（保持原实现） ----------
            // 原 CheckForHit 里对 newConditionsBuffer / removeConditionsBuffer 的写入在此保留。

            // ---------- 7. 击退（保持原实现） ----------
            if (p.pushback > 0f && !immuneToPushBackLookup.HasComponent(target))
            {
                // 原逻辑：根据 castDirection / reversePushback 计算方向，写 physicsVelocity / MoveToPredictedByPushbackCD
                ApplyPushback(target, p);
            }

            // ---------- 8. 受击动画 / 特效（保持原实现） ----------
            if (p.triggerAnimationOnClientHit && animationBufferPointerLookup.HasComponent(target))
            {
                // 原逻辑：写 animationBuffer / AnimationBufferPointer
                TriggerHitAnimation(target, p);
            }
            // 特效事件用 p.hitPosition 触发：
            if (p.effectEventBufferSingleton != Entity.Null &&
                ghostEffectEventBufferPointerLookup.HasComponent(target))
            {
                // 原逻辑：写 GhostEffectEventBuffer
            }

            // ---------- 9. 技能统计（与原命中后统计一致） ----------
            Entity minionOwner = ownerInfo.minionOwner;
            Entity playerOwner = ownerInfo.playerOwner;
            if (minionOwner != Entity.Null)
            {
                if (playerOwner != Entity.Null)
                    PlayerController.AddSkill(playerOwner, SkillID.Summoning, 1f, ecb, isServer);
            }
            else if (playerOwner != Entity.Null && !p.isExplosive)
            {
                PlayerController.AddCombatSkillByCooldown(playerOwner, p.attacker, p.isMagic, p.isRanged,
                    ecb, isServer, equipmentSlotLookup, equippedObjectLookup, projectileSourceLookup,
                    damageSourceLookup, cooldownLookup, hasWeaponDamageLookup,
                    summarizedConditionEffectsBufferLookup, databaseBank);
            }
            else if (playerOwner != Entity.Null && p.isExplosive)
            {
                PlayerController.AddSkill(playerOwner, SkillID.Explosives, 1f, ecb, isServer);
            }

            return true; // 返回"已完成结算"
        }

        // ================================================================
        // ★ 改造点：原 AttackInternal 外部保留的公开入口（供 ZenitHitSystem 调用）
        // ================================================================
        public bool AttackInternal(EntityCommandBuffer ecb, DamageApplyBuffer p)
        {
            return ApplyDamageToTarget(ecb, p);
        }

        // ================================================================
        // 以下为原实现中"单目标伤害计算/击退/动画"的抽取桩，
        // 内容请从原 CheckForHit 内部对应分支搬过来（此处不重复贴，避免与你的实际字段冲突）。
        // ================================================================
        private float CalculateFinalDamage(Entity target, DamageApplyBuffer p, EntityUtility.OwnerInfo ownerInfo)
        {
            // 保持原实现：减伤、元素抗性、护盾、暴击、状态加成等。
            throw new NotImplementedException("从原 CheckForHit 内搬入单目标伤害计算逻辑");
        }

        private void ApplyPushback(Entity target, DamageApplyBuffer p)
        {
            // 保持原实现。
        }

        private void TriggerHitAnimation(Entity target, DamageApplyBuffer p)
        {
            // 保持原实现。
        }
    }
}