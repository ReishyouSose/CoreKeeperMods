using Assets.CoreEnhance.Scripts.Cores;
using Assets.CoreEnhance.Scripts.Helpers;
using Interaction;
using Inventory;
using Outlines.Systems;
using PlayerEquipment;
using Pug.UnityExtensions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace Assets.CoreEnhance.Scripts.Systems.Quick
{
    public struct MoveChestContentRPC : IRpcCommand
    {
        public Entity MoveFrom;
        public Entity MoveTo;
    }
    public struct HoverChestCD : IComponentData
    {
        public bool Hovered;
        public float lockTime;
    }

    [GhostComponent]
    public struct MoveChestBlockInputCD : IComponentData
    {
        [GhostField]
        public bool block;
    }
    public struct MoveChestBlockInputRPC : IRpcCommand
    {
        public bool Block;
        public Entity Player;
    }

    [UpdateAfter(typeof(VisualOutlineDisplaySystem))]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation, WorldSystemFilterFlags.Default)]
    public partial class MoveChestClient : PugSimulationSystemBase
    {
        private NativeList<Entity> selected;
        private EntityArchetype moveArchetype;
        private EntityArchetype blockArchetype;
        private ComponentLookup<DirectionCD> directionLookup;
        private float timer;
        private bool held;
        protected override void OnCreate()
        {
            NeedDatabase();
            directionLookup = SystemAPI.GetComponentLookup<DirectionCD>();
            selected = new NativeList<Entity>(1, Allocator.Persistent)
            {
                Entity.Null
            };
            moveArchetype = EntityManager.CreateArchetype(typeof(SendRpcCommandRequest), typeof(MoveChestContentRPC));
            blockArchetype = EntityManager.CreateArchetype(typeof(MoveChestBlockInputRPC), typeof(SendRpcCommandRequest));
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            var player = Manager.main.player;
            if (player == null)
            {
                base.OnUpdate();
                return;
            }
            var ecb = CreateCommandBuffer();
            bool enabled = EnhanceConfig.IsEnable(EnhanceCategory.MoveChestContent);
            bool press = enabled && player.inputModule.rewiredPlayer.GetButton(ModKeyBind.MoveChest);
            if (press != held)
            {
                held = press;
                var blockSend = ecb.CreateEntity(blockArchetype);
                ecb.SetComponent(blockSend, new MoveChestBlockInputRPC { Block = press, Player = player.entity });
            }
            if (!enabled)
            {
                base.OnUpdate();
                return;
            }
            var selected = this.selected;
            if (press)
                timer = 1.6f;
            if (timer <= 0)
            {
                base.OnUpdate();
                return;
            }
            var delta = World.Time.DeltaTime;
            timer -= delta;
            var mouse = MiscHelper.MouseWorld.ToFloat2();
            var database = this.database;
            bool click = Input.GetMouseButtonDown(0);
            var moveArchetype = this.moveArchetype;
            var directionLookup = this.directionLookup;
            if (selected[0] != Entity.Null && !EntityManager.Exists(selected[0]))
                selected[0] = Entity.Null;
            Entities.ForEach((Entity entity, ref HoverChestCD hover, in ObjectDataCD objData,
                in LocalTransform trans, in InteractableCD interactable, in InteractableObjectReferenceCD interact) =>
            {
                if (hover.lockTime > 0)
                {
                    hover.lockTime -= delta;
                    GraphicEntityHelper.UpdateOutline(interact, Color.red);
                    return;
                }
                ref bool hovered = ref hover.Hovered;
                if (selected[0] == entity)
                    GraphicEntityHelper.UpdateOutline(interact, Color.yellow);
                else if (hovered)
                    GraphicEntityHelper.UpdateOutline(interact, Color.cyan);
                if (!press)
                {
                    if (hovered)
                        hovered = false;
                    return;
                }
                directionLookup.TryGetComponent(entity, out var direction);
                var rect = GraphicEntityHelper.GetInteractableRect(trans, interactable, Direction.FromVector(direction.direction).id);
                var first = selected[0];
                if (rect.Contains(mouse))
                {
                    if (click)
                    {
                        if (first == Entity.Null)
                            selected[0] = entity;
                        else if (first == entity)
                            selected[0] = Entity.Null;
                        else
                        {
                            var send = ecb.CreateEntity(moveArchetype);
                            ecb.SetComponent(send, new MoveChestContentRPC() { MoveFrom = first, MoveTo = entity });
                            hover.lockTime = 1f;
                            selected[0] = Entity.Null;
                        }
                    }
                    if (!hovered)
                        hovered = true;
                }
                else if (hovered)
                    hovered = false;
            })
                .WithName("ChooseChest")
                .WithoutBurst()
                .Schedule();
            base.OnUpdate();
        }
        internal static void AddHoveredChest(Entity e, GameObject authoringData, EntityManager manager)
        {
            if (authoringData.HasComponent<LocalInteractableAuthoring>() && authoringData.HasComponent<InventoryAuthoring>()
                && authoringData.HasComponent<MineableAuthoring>() && authoringData.HasComponent<DamageReductionAuthoring>()
                && authoringData.HasComponent<DescriptionAuthoring>())
            {
                manager.AddComponent<HoverChestCD>(e);
            }
        }
        internal static void MarkPlayer(Entity entity, GameObject authoringData, EntityManager manager)
        {
            if (!authoringData.HasComponent<PlayerAuthoring>())
                return;
            manager.AddComponentData(entity, new MoveChestBlockInputCD());
        }
    }

    [UpdateInGroup(typeof(EquipmentUpdateSystemGroup))]
    [UpdateBefore(typeof(EquipmentUpdateSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial class MoveChestBlockInputSystem : PugSimulationSystemBase
    {
        private ComponentLookup<MoveChestBlockInputCD> blockLookup;
        protected override void OnCreate()
        {
            blockLookup = SystemAPI.GetComponentLookup<MoveChestBlockInputCD>();
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            var ecb = CreateCommandBuffer();
            var blocks = blockLookup;
            Entities.ForEach((Entity e, in MoveChestBlockInputRPC rpc) =>
            {
                ecb.DestroyEntity(e);
                var option = blocks.GetRefRWOptional(rpc.Player);
                if (option.IsValid)
                {
                    option.ValueRW.block = rpc.Block;
                }
            })
                .WithName("MoveChestBlockInputRPC")
                .WithAll<ReceiveRpcCommandRequest>()
                .WithBurst()
                .Schedule();

            Entities.ForEach((ref ClientInput input, in MoveChestBlockInputCD block) =>
            {
                if (!block.block)
                    return;
                input.SetButtonState(CommandInputButtonStateNames.Interact_HeldDown, false);
                input.SetButtonState(CommandInputButtonStateNames.SecondInteract_HeldDown, false);
            })
                .WithName("BlockInputWhenMoveChest")
                .WithBurst()
                .Schedule();
            base.OnUpdate();
        }
    }

    [UpdateInGroup(typeof(InventorySystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation, WorldSystemFilterFlags.Default)]
    public partial class MoveChestServer : PugSimulationSystemBase
    {
        private BufferLookup<ContainedObjectsBuffer> containerLookup;
        private BufferLookup<InventoryBuffer> invLookup;
        private ComponentLookup<LocalTransform> transLookup;
        private const int MaxStack = 9999;

        // 用于记录槽位容量信息的结构
        private struct SlotCapacity
        {
            public int slotIndex;
            public ObjectID objectID;
            public int variation;
            public int remainingCapacity;
        }

        protected override void OnCreate()
        {
            NeedDatabase();
            containerLookup = SystemAPI.GetBufferLookup<ContainedObjectsBuffer>();
            invLookup = SystemAPI.GetBufferLookup<InventoryBuffer>();
            transLookup = SystemAPI.GetComponentLookup<LocalTransform>();
            base.OnCreate();
        }

        protected override void OnUpdate()
        {
            if (!EnhanceConfig.IsEnable(EnhanceCategory.MoveChestContent))
                return;
            var ecb = CreateCommandBuffer();
            var containerLookup = this.containerLookup;
            var invLookup = this.invLookup;
            var transLookup = this.transLookup;
            var database = this.database;

            Entities.ForEach((Entity e, in MoveChestContentRPC rpc) =>
            {
                ecb.DestroyEntity(e);

                var source = rpc.MoveFrom;
                var target = rpc.MoveTo;
                float3 sourcePosition = transLookup[source].Position;

                if (!invLookup.HasBuffer(source) || !invLookup.HasBuffer(target) ||
                    !containerLookup.HasBuffer(source) || !containerLookup.HasBuffer(target))
                    return;

                var sourceInv = invLookup[source];
                var targetInv = invLookup[target];
                var sourceContainer = containerLookup[source];
                var targetContainer = containerLookup[target];

                // 阶段1: 收集目标容器中未满的堆叠槽位
                var slotCapacities = new NativeList<SlotCapacity>(targetContainer.Length, Allocator.Temp);
                int emptySlotCount = 0;

                foreach (var inv in targetInv)
                {
                    for (int i = inv.startIndex; i < inv.startIndex + inv.size; i++)
                    {
                        var item = targetContainer[i];
                        if (item.objectID == ObjectID.None)
                        {
                            emptySlotCount++;
                        }
                        else
                        {
                            if (PugDatabase.GetEntityObjectInfo(item.objectID, database, item.variation).isStackable && item.amount < MaxStack)
                            {
                                slotCapacities.Add(new SlotCapacity
                                {
                                    slotIndex = i,
                                    objectID = item.objectID,
                                    variation = item.variation,
                                    remainingCapacity = MaxStack - item.amount
                                });
                            }
                        }
                    }
                }

                // 阶段2: 遍历源容器，转移物品
                foreach (var inv in sourceInv)
                {
                    for (int i = inv.startIndex; i < inv.startIndex + inv.size; i++)
                    {
                        var sourceItem = sourceContainer[i];
                        if (sourceItem.objectID == ObjectID.None)
                            continue;

                        int remainingAmount = sourceItem.amount;

                        if (PugDatabase.GetEntityObjectInfo(sourceItem.objectID, database, 0).isStackable)
                        {
                            // 先尝试放入匹配的未满槽位
                            for (int capIdx = 0; capIdx < slotCapacities.Length && remainingAmount > 0; capIdx++)
                            {
                                var capacity = slotCapacities[capIdx];
                                if (capacity.objectID == sourceItem.objectID && capacity.variation == sourceItem.variation)
                                {
                                    int moveAmount = math.min(remainingAmount, capacity.remainingCapacity);

                                    // 创建要转移的物品
                                    var movedItem = sourceItem;
                                    movedItem.objectData.amount = moveAmount;

                                    // 添加到目标容器
                                    EntityUtility.DropNewEntity(ecb, movedItem, sourcePosition, database, target, true);

                                    remainingAmount -= moveAmount;

                                    // 更新容量信息
                                    if (moveAmount == capacity.remainingCapacity)
                                    {
                                        slotCapacities.RemoveAt(capIdx);
                                        capIdx--; // 调整索引
                                    }
                                    else
                                    {
                                        capacity.remainingCapacity -= moveAmount;
                                        slotCapacities[capIdx] = capacity;
                                    }
                                }
                            }

                            // 剩余物品放入空槽位
                            while (remainingAmount > 0 && emptySlotCount > 0)
                            {
                                int moveAmount = math.min(remainingAmount, MaxStack);

                                var movedItem = sourceItem;
                                movedItem.objectData.amount = moveAmount;

                                EntityUtility.DropNewEntity(ecb, movedItem, sourcePosition, database, target, true);

                                remainingAmount -= moveAmount;
                                emptySlotCount--;

                                // 如果没填满一个完整的堆叠，记录剩余容量供后续使用
                                if (remainingAmount == 0 && moveAmount < MaxStack)
                                {
                                    slotCapacities.Add(new SlotCapacity
                                    {
                                        slotIndex = -1, // 新槽位，索引不重要
                                        objectID = sourceItem.objectID,
                                        variation = sourceItem.variation,
                                        remainingCapacity = MaxStack - moveAmount
                                    });
                                }
                            }

                            // 更新源槽位
                            if (remainingAmount <= 0)
                            {
                                sourceContainer[i] = default;
                            }
                            else if (remainingAmount < sourceItem.amount)
                            {
                                sourceItem.objectData.amount = remainingAmount;
                                sourceContainer[i] = sourceItem;
                            }
                        }
                        else // 非堆叠物品
                        {
                            if (emptySlotCount > 0)
                            {
                                // 直接转移整个物品
                                EntityUtility.DropNewEntity(ecb, sourceItem, sourcePosition, database, target, true);
                                sourceContainer[i] = default;
                                emptySlotCount--;
                            }
                        }
                    }
                }

                slotCapacities.Dispose();
            })
                .WithName("MoveChestContent")
                .WithBurst()
                .Schedule();

            base.OnUpdate();
        }
    }
}