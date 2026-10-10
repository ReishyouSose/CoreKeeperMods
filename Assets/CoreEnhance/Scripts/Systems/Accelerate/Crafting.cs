using Assets.CoreEnhance.Scripts.Cores;
using CoreLib.Data.Configuration;
using Inventory;
using Pug.Automation;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

namespace Assets.CoreEnhance.Scripts.Systems.Accelerate
{
    [UpdateBefore(typeof(PugTimerSystem))]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class AccelerateCraftingSystem : PugSimulationSystemBase
    {
        private ComponentLookup<CattleCD> cattleLookup;
        private ComponentLookup<FishingCD> fishingLookup;
        private ComponentLookup<BigEntityRefCD> bigLookup;
        private ComponentLookup<CraftingCD> craftingLookup;
        private ComponentLookup<CrafterForSlotCD> slotLookup;
        private BufferLookup<CraftingTimerSlotBuffer> timerLookup;
        protected override void OnCreate()
        {
            cattleLookup = SystemAPI.GetComponentLookup<CattleCD>();
            fishingLookup = SystemAPI.GetComponentLookup<FishingCD>();
            bigLookup = SystemAPI.GetComponentLookup<BigEntityRefCD>();
            craftingLookup = SystemAPI.GetComponentLookup<CraftingCD>();
            slotLookup = SystemAPI.GetComponentLookup<CrafterForSlotCD>();
            timerLookup = SystemAPI.GetBufferLookup<CraftingTimerSlotBuffer>();
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            if (!EnhanceConfig.TryGetValues(EnhanceCategory.Crafting, out var values))
                return;
            bool animals = (values["Animals"] as ConfigEntry<bool>).Value;
            bool fishingNets = (values["FishingNet"] as ConfigEntry<bool>).Value;
            var bigLookup = this.bigLookup;
            var slotLookup = this.slotLookup;
            var timerLookup = this.timerLookup;
            var cattleLookup = this.cattleLookup;
            var fishingLookup = this.fishingLookup;
            var craftingLookup = this.craftingLookup;
            Entities.ForEach((ref PugTimerCD timer, in PugTimerRefCD timerRef) =>
            {
                var entity = timerRef.entity;
                if (entity == Entity.Null)
                    return;
                if (!bigLookup.TryGetComponent(entity, out var big))
                    return;
                if (!slotLookup.TryGetComponent(entity, out var slot))
                    return;
                var bigEntity = big.Value;
                int index = slot.slotIndex;
                if (!craftingLookup.HasComponent(bigEntity))
                    return;
                if (animals && cattleLookup.HasComponent(bigEntity))//TODO:动物需要测试
                    return;
                if (fishingNets && fishingLookup.HasComponent(bigEntity))
                    return;
                if (!timerLookup.TryGetBuffer(bigEntity, out var craftTimerSlot))
                    return;
                ref var craftTimer = ref craftTimerSlot.ElementAt(index);
                if (craftTimer.timeLeftToCraft > 1)
                {
                    craftTimer.timeLeftToCraft = 1f;
                    timer.timer = 1;
                }
            })
                .WithName("Accelerate_Crafting")
                .WithBurst()
                .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabledEntities)
                .Schedule();
            Dependency.Complete();
            base.OnUpdate();
        }
    }

    [UpdateInGroup(typeof(PugAutomationFinishCraftingSystemGroup))]
    [UpdateBefore(typeof(PugAutomationCraftingSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class AccelerateFinishAllSystem : PugSimulationSystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<CraftingTimerTriggerCD>();
            RequireForUpdate<PugDatabase.DatabaseBankCD>();
            RequireForUpdate<SkillTalentsTableCD>();
            RequireForUpdate<UpgradeCostsTableCD>();
            RequireForUpdate<InventoryAuxDataSystemDataCD>();
            RequireForUpdate<NetworkTime>();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            if (!EnhanceConfig.TryGetValues(EnhanceCategory.Crafting, out var values))
                return;
            if (values["FinishAll"] is not ConfigEntry<bool> finishAll || !finishAll.Value)
                return;
            var ecb = CreateCommandBuffer();
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var shared = new InventoryHandlerShared(ref CheckedStateRef,
                SystemAPI.GetSingleton<PugDatabase.DatabaseBankCD>(),
                SystemAPI.GetSingleton<SkillTalentsTableCD>(),
                SystemAPI.GetSingleton<UpgradeCostsTableCD>(),
                SystemAPI.GetSingleton<InventoryAuxDataSystemDataCD>());
            shared.Update(ref CheckedStateRef, ecb, networkTime);

            var bigLookup = SystemAPI.GetComponentLookup<BigEntityRefCD>(true);
            var slotLookup = SystemAPI.GetComponentLookup<CrafterForSlotCD>(true);
            var objectLookup = SystemAPI.GetComponentLookup<ObjectDataCD>(true);
            var autoLookup = SystemAPI.GetComponentLookup<PugAutomationCD>(true);
            var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
            var timerLookup = SystemAPI.GetBufferLookup<CraftingTimerSlotBuffer>();
            var recipeStateLookup = SystemAPI.GetBufferLookup<CraftingByRecipeSlotBuffer>();
            var canCraftLookup = SystemAPI.GetBufferLookup<CanCraftObjectsBuffer>(true);

            foreach (var timerRef in SystemAPI.Query<RefRO<PugTimerRefCD>>().WithAll<CraftingTimerTriggerCD>())
            {
                var slot = timerRef.ValueRO.entity;
                if (!bigLookup.TryGetComponent(slot, out var bigRef) || !slotLookup.TryGetComponent(slot, out var slotCD))
                    continue;
                var big = bigRef.Value;
                if (!objectLookup.TryGetComponent(big, out var objectData) || !IsBatchCrafter(objectData.objectID))
                    continue;
                if (autoLookup.TryGetComponent(big, out var automation) && !automation.isActive)
                    continue;
                if (!timerLookup.TryGetBuffer(big, out var timers) || !recipeStateLookup.TryGetBuffer(big, out var recipeState))
                    continue;
                int index = slotCD.slotIndex;
                if ((uint)index >= (uint)timers.Length || (uint)index >= (uint)recipeState.Length)
                    continue;
                if (timers[index].timeLeftToCraft > 1f)
                    continue;
                if (!canCraftLookup.TryGetBuffer(big, out var recipes))
                    continue;
                int recipeIndex = recipeState[index].currentlyCraftingIndex;
                if ((uint)recipeIndex >= (uint)recipes.Length)
                    continue;

                var recipe = recipes[recipeIndex];
                if (!transformLookup.TryGetComponent(big, out var transform))
                    continue;
                var inventories = new NativeList<Entity>(1, Allocator.Temp);
                inventories.Add(big);
                int guard = 0;
                while (guard++ < 4096 && InventoryUtility.CanCraft(in shared, big, objectData, inventories, recipe, 1f))
                {
                    if (!InventoryUtility.Craft(in shared, big, objectData, inventories, recipe, 0, transform.Position, false))
                        break;
                }
                inventories.Dispose();

                timers.ElementAt(index).timeLeftToCraft = 0f;
                recipeState.ElementAt(index).currentlyCraftingIndex = -1;
            }
            base.OnUpdate();
        }

        private static bool IsBatchCrafter(ObjectID id)
        {
            switch (id)
            {
                case ObjectID.Furnace:
                case ObjectID.SmelterKiln:
                case ObjectID.FuryForge:
                case ObjectID.GlassSmelter:
                case ObjectID.CookingPot:
                    return true;
                default:
                    return false;
            }
        }
    }
}
