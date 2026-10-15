using Assets.CoreEnhance.Scripts.Cores;
using Assets.CoreEnhance.Scripts.Helpers;
using Inventory;
using PlayerState;
using Pug.UnityExtensions;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace Assets.CoreEnhance.Scripts.Systems.Automation
{
    public struct AutoFishCD : IComponentData
    {
        public float3 sink;
        public Direction facing;
        public float ratio;
        public bool holding;
        public bool active;
        public bool immune;
        public uint throwSerial;
        public uint refilledSerial;

        public static bool ShouldStop(in ClientInput input)
        {
            if (math.lengthsq(input.movementDirection) > 0.01f)
                return true;
            return input.IsButtonStateSet(CommandInputButtonStateNames.Interact_HeldDown)
                || input.IsButtonStateSet(CommandInputButtonStateNames.Interact_Pressed)
                || input.IsButtonStateSet(CommandInputButtonStateNames.UseOffHand_HeldDown)
                || input.IsButtonStateSet(CommandInputButtonStateNames.UseOffHand_Pressed)
                || input.IsButtonStateSet(CommandInputButtonStateNames.InteractWithObject_Pressed);
        }
    }

    [UpdateInGroup(typeof(PlayerStateSystemGroup))]
    [UpdateAfter(typeof(UpdatePlayerStateSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial class AutoFishCastSystem : PugSimulationSystemBase
    {
        private uint tickRate;
        protected override void OnCreate()
        {
            tickRate = (uint)PlatformConfiguration.Instance.SessionConfiguration.SimulationTickRate;
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            if (!EnhanceConfig.IsEnable(EnhanceCategory.AutoFish))
            {
                Entities.ForEach((ref AutoFishCD cast, ref DynamicBuffer<ConditionsBuffer> conditions) =>
                {
                    if (!cast.active && !cast.immune)
                        return;
                    StopCast(ref cast, ref conditions);
                })
                    .WithName("Automation_AutoFishOff")
                    .WithBurst()
                    .Schedule();
                base.OnUpdate();
                return;
            }
            if (!SystemAPI.TryGetSingleton<NetworkTime>(out var time) || !SystemAPI.TryGetSingleton<ConditionsTableCD>(out var table))
            {
                base.OnUpdate();
                return;
            }
            bool invincible = false;
            if (EnhanceConfig.TryGetValue<bool>(EnhanceCategory.AutoFish, out var invincibleEntry, "Invincible"))
                invincible = invincibleEntry.Value;
            var tick = time.ServerTick;
            var tickRate = this.tickRate;
            Entities.ForEach((ref FishingStateCD fish, ref PlayerAimPositionCD aim, ref AutoFishCD cast, ref DynamicBuffer<ConditionsBuffer> conditions, in DynamicBuffer<SummarizedConditionsBuffer> summarized, in PlayerStateCD playerState, in ClientInput input, in AnimationOrientationCD orientation) =>
            {
                bool fishing = playerState.HasAnyState(PlayerStateEnum.Fishing);
                if (!fishing || AutoFishCD.ShouldStop(input))
                {
                    StopCast(ref cast, ref conditions);
                    return;
                }
                if (cast.active)
                {
                    if (!fish.isSuccessfullyFishing && !fish.castTimer.isRunning && !fish.throwTimer.isRunning)
                    {
                        StopCast(ref cast, ref conditions);
                        return;
                    }
                    fish.targetSinkWorldPosition = cast.sink;
                    aim.position = cast.sink;
                    bool newThrow = false;
                    if (fish.throwTimer.isRunning)
                    {
                        uint serial = fish.throwTimer.startTick.SerializedData;
                        if (serial != cast.throwSerial)
                        {
                            cast.throwSerial = serial;
                            newThrow = true;
                        }
                    }
                    if (newThrow || invincible != cast.immune)
                        CheckImmune(ref cast, ref conditions, summarized, table, tick, tickRate, invincible);
                    return;
                }
                if (fish.throwTimer.isRunning)
                {
                    uint serial = fish.throwTimer.startTick.SerializedData;
                    if (cast.throwSerial == serial)
                        return;
                    cast.sink = fish.targetSinkWorldPosition;
                    cast.facing = orientation.facingDirection;
                    cast.ratio = CastRatio(fish, tick);
                    cast.throwSerial = serial;
                    cast.holding = false;
                    return;
                }
                if (fish.isSuccessfullyFishing && cast.throwSerial != 0 && cast.throwSerial == fish.throwTimer.startTick.SerializedData)
                {
                    cast.active = true;
                    CheckImmune(ref cast, ref conditions, summarized, table, tick, tickRate, invincible);
                }
            })
                .WithName("Automation_Fish")
                .WithBurst()
                .Schedule();
            base.OnUpdate();
        }

        private static float CastRatio(in FishingStateCD fish, NetworkTick tick)
        {
            uint target = fish.castTimer.targetTicks;
            if (target == 0)
                return 1f;
            return math.clamp(fish.castTimer.GetElapsedTicksWithStop(tick) / (float)target, 0f, 1f);
        }

        private static void StopCast(ref AutoFishCD cast, ref DynamicBuffer<ConditionsBuffer> conditions)
        {
            cast.active = false;
            cast.holding = false;
            if (!cast.immune)
                return;
            EntityUtility.RemoveCondition(ConditionID.ImmuneToDamageAfterRespawn, conditions);
            cast.immune = false;
        }

        private static void CheckImmune(ref AutoFishCD cast, ref DynamicBuffer<ConditionsBuffer> conditions,
            in DynamicBuffer<SummarizedConditionsBuffer> summarized, ConditionsTableCD table, NetworkTick tick, uint tickRate, bool invincible)
        {
            if (!invincible || tickRate == 0)
            {
                if (!cast.immune)
                    return;
                EntityUtility.RemoveCondition(ConditionID.ImmuneToDamageAfterRespawn, conditions);
                cast.immune = false;
                return;
            }
            EntityUtility.AddOrRefreshCondition(new ConditionData
            {
                conditionID = ConditionID.ImmuneToDamageAfterRespawn,
                value = 1,
                duration = 60f
            }, conditions, table, tick, tickRate, summarized);
            cast.immune = true;
        }
        internal static void MarkPlayer(Entity entity, GameObject authoringData, EntityManager entityManager)
        {
            if (!authoringData.HasComponent<PlayerAuthoring>())
                return;
            entityManager.AddComponentData(entity, new AutoFishCD());
        }
    }

    [UpdateInGroup(typeof(RunSimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(SendClientInputSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    public partial class AutoFishInputSystem : PugSimulationSystemBase
    {
        protected override void OnUpdate()
        {
            if (!EnhanceConfig.IsEnable(EnhanceCategory.AutoFish))
            {
                base.OnUpdate();
                return;
            }
            var player = Manager.main.player;
            if (player == null)
            {
                base.OnUpdate();
                return;
            }
            /*if (player == null || Manager.ui.isAnyInventoryShowing || Manager.ui.isShowingMap || Manager.menu.IsAnyMenuActive())
            {
                base.OnUpdate();
                return;
            }*/
            if (!SystemAPI.TryGetSingleton<NetworkTime>(out var time))
            {
                base.OnUpdate();
                return;
            }
            var playerEntity = player.entity;
            var tick = time.ServerTick;
            Entities.ForEach((Entity entity, ref ClientInputForLatestFrameCD latest, ref ClientInputData data, ref AutoFishCD cast, in FishingStateCD fish, in FishingMiniGameStateCD mini, in PlayerStateCD state, in ClientInput applied) =>
            {
                if (entity != playerEntity || !cast.active)
                    return;
                if (!state.HasAnyState(PlayerStateEnum.Fishing) || AutoFishCD.ShouldStop(applied))
                    return;
                ClientInput input = latest.clientInput;
                if (AutoFishCD.ShouldStop(input))
                    return;
                ref var holding = ref cast.holding;
                bool needHolding = NeedHolding(fish, mini, cast.ratio, tick);
                input.facingDirection = cast.facing;
                input.SetButtonState(CommandInputButtonStateNames.SecondInteract_HeldDown, needHolding);
                input.SetButtonState(CommandInputButtonStateNames.SecondInteract_Pressed, needHolding && !holding);
                input.SetButtonState(CommandInputButtonStateNames.SecondInteract_Released, !needHolding && holding);
                latest.clientInput = input;
                //按位转换数据，牛逼玩意儿
                data = UnsafeUtility.As<ClientInput, ClientInputData>(ref input);
                holding = needHolding;
            })
                .WithName("Automation_AutoFishInput")
                .WithBurst()
                .Schedule();
            base.OnUpdate();
        }

        static bool NeedHolding(in FishingStateCD fish, in FishingMiniGameStateCD mini, float ratio, NetworkTick tick)
        {
            if (fish.castTimer.isRunning)
            {
                float step = fish.castTimer.targetTicks > 0 ? 2f / fish.castTimer.targetTicks : 0f;
                return fish.castTimer.GetElapsedRatio(tick) + step < ratio;
            }
            if (fish.pullUpTimer.isRunning)
                return true;
            if (mini.isInFishingMiniGame && !mini.miniGameOverTimer.isRunning)
                return !mini.fishIsStruggling;
            return fish.fishIsNibbling;
        }
    }

    [UpdateInGroup(typeof(PlayerStateSystemGroup))]
    [UpdateAfter(typeof(UpdatePlayerStateSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial class AutoFishRefillSystem : PugSimulationSystemBase
    {
        private BufferLookup<InventoryChangeBuffer> invChangeLookup;
        protected override void OnCreate()
        {
            invChangeLookup = SystemAPI.GetBufferLookup<InventoryChangeBuffer>();
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            if (!EnhanceConfig.IsEnable(EnhanceCategory.AutoFish))
            {
                base.OnUpdate();
                return;
            }
            if (!EnhanceConfig.TryGetValue<bool>(EnhanceCategory.AutoFish, out var refill, "Refill") || !refill.Value)
            {
                base.OnUpdate();
                return;
            }
            if (!SystemAPI.TryGetSingletonEntity<InventoryChangeBuffer>(out var changeEntity))
            {
                base.OnUpdate();
                return;
            }
            invChangeLookup.Update(this);
            var changes = invChangeLookup[changeEntity];
            Entities.ForEach((Entity player, ref AutoFishCD cast, in FishingStateCD fish, in EquipmentCD equipment, in DynamicBuffer<ContainedObjectsBuffer> slots) =>
            {
                if (!cast.active)
                    return;
                int off = equipment.offHandIndex;
                if ((uint)off >= (uint)slots.Length)
                    return;
                ContainedObjectsBuffer bait = slots[off];
                if (bait.objectID == ObjectID.None || bait.objectID == ObjectID.BaitOctopusBoss)
                    return;
                uint serial = fish.pullUpTimer.startTick.SerializedData;
                if (serial == 0 || serial == cast.refilledSerial)
                    return;
                cast.refilledSerial = serial;
                changes.Add(new InventoryChangeBuffer
                {
                    playerEntity = player,
                    inventoryChangeData = Create.CreateItem(player, off, bait.objectID, 1, default, bait.variation)
                });
            })
                .WithName("Automation_AutoFishRefill")
                .WithAll<WaitingForConsumedBaitResultCD>()
                .WithBurst()
                .Schedule();
            base.OnUpdate();
        }
    }
}
