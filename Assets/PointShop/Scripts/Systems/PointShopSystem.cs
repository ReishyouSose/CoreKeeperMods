using Assets.PointShop.Scripts.Core;
using Assets.PointShop.Scripts.UI;
using Inventory;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;

namespace Assets.PointShop.Scripts.Systems
{

    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class PointShopClient : PugSimulationSystemBase
    {
        private static PointShopClient ins;
        private NativeQueue<PointShopRPC> sendQueue;
        private NativeQueue<BuyFailureRPC> receiveQueue;
        private EntityArchetype archetype;
        protected override void OnCreate()
        {
            ins = this;
            sendQueue = new NativeQueue<PointShopRPC>(Allocator.Persistent);
            receiveQueue = new NativeQueue<BuyFailureRPC>(Allocator.Persistent);
            archetype = EntityManager.CreateArchetype(typeof(PointShopRPC), typeof(SendRpcCommandRequest));
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            var ecb = CreateCommandBuffer();
            while (sendQueue.TryDequeue(out var rpc))
            {
                Entity e = ecb.CreateEntity(archetype);
                ecb.SetComponent(e, rpc);
            }


            var receive = receiveQueue;
            while (receive.TryDequeue(out var failure))
            {
                var shop = PointShopUI.Ins;
                switch (failure.Reason)
                {
                    case 0:
                        shop.WarnNotDefeat();
                        break;
                    case 1:
                        //shop.CurrentShopSlot.WarnNotEnough();
                        break;
                }
            }
            Entities.ForEach((Entity e, in BuyFailureRPC rpc) =>
            {
                ecb.DestroyEntity(e);
                receive.Enqueue(rpc);
            })
                .WithName("ReceiveBuyFailure")
                .WithBurst()
                .WithAll<ReceiveRpcCommandRequest>()
                .Schedule();

            base.OnUpdate();
        }
        public static void TryBuyItem(Entity player, ObjectDataCD item, ObjectID boss, ObtainWay way, int price, bool scale, string zone)
        {
            ins.sendQueue.Enqueue(new()
            {
                Player = player,
                Item = item,
                Boss = boss,
                Way = way,
                Price = price,
                Scale = scale,
                Zone = zone
            });
        }
    }
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class PointShopServer : PugSimulationSystemBase
    {
        private ComponentLookup<LocalTransform> transLookup;
        private BufferLookup<ContainedObjectsBuffer> containedLookup;
        private BufferLookup<PointCurrencyBuffer> currencyLookup;
        private EntityArchetype archetype;
        protected override void OnCreate()
        {
            NeedDatabase();
            RequireForUpdate<KilledEnemiesBuffer>();
            RequireForUpdate<InventoryChangeBuffer>();
            transLookup = SystemAPI.GetComponentLookup<LocalTransform>();
            containedLookup = SystemAPI.GetBufferLookup<ContainedObjectsBuffer>();
            currencyLookup = SystemAPI.GetBufferLookup<PointCurrencyBuffer>();
            archetype = EntityManager.CreateArchetype(typeof(BuyFailureRPC), typeof(SendRpcCommandRequest));
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingletonBuffer<InventoryChangeBuffer>(out var inv))
                return;
            if (!SystemAPI.TryGetSingletonBuffer<KilledEnemiesBuffer>(out var killeds))
                return;
            var ecb = CreateCommandBuffer();
            var transLookup = this.transLookup;
            var containedLookup = this.containedLookup;
            var currencyLookup = this.currencyLookup;
            var database = this.database;
            var archetype = this.archetype;
            var coin = PointShop.Coin;
            bool creative = WorldInfo.IsWorldModeEnabled(WorldMode.Creative);
            Entities.ForEach((Entity e, in PointShopRPC rpc, in ReceiveRpcCommandRequest receive) =>
            {
                ecb.DestroyEntity(e);
                var player = rpc.Player;
                var boss = rpc.Boss;
                bool usePointCoin = rpc.Way == ObtainWay.Old;
                var price = rpc.Price;
                if (!creative && boss != ObjectID.None)
                {
                    bool defeated = false;
                    foreach (var killed in killeds)
                    {
                        if (killed.objectData.objectID == boss)
                        {
                            defeated = true;
                            break;
                        }
                    }
                    if (!defeated)
                    {
                        SendFailure(ecb, archetype, 0, receive);
                        return;
                    }
                }
                var item = rpc.Item;
                var id = item.objectID;
                var variation = item.variation;
                int amount = item.amount;
                ref var info = ref PugDatabase.GetEntityObjectInfo(id, database, variation);
                if (info.isStackable && rpc.Scale)
                {
                    price *= 10;
                    amount *= 10;
                }
                if (!creative)
                {
                    if (usePointCoin)
                    {
                        var currency = coin;
                        if (!InventoryUtility.HasObject(containedLookup, player, currency, price))
                        {
                            SendFailure(ecb, archetype, 1, receive);
                            return;
                        }
                        inv.Add(new()
                        {
                            inventoryChangeData = Create.ConsumeObjectType(player, currency, price),
                            playerEntity = player
                        });
                    }
                    else
                    {
                        if (!currencyLookup.TryGetBuffer(player, out var currencies))
                            return;
                        bool success = false;
                        for (int i = 0; i < currencies.Length; i++)
                        {
                            var currency = currencies[i];
                            if (currency.Zone != rpc.Zone)
                                continue;
                            switch (rpc.Way)
                            {
                                case ObtainWay.Explore:
                                    if (currency.Explore >= price)
                                    {
                                        currency.Explore -= (uint)price;
                                        success = true;
                                    }
                                    break;
                                case ObtainWay.Chest:
                                    if (currency.Chest >= price)
                                    {
                                        currency.Chest -= (uint)price;
                                        success = true;
                                    }
                                    break;
                                case ObtainWay.Enemy:
                                    if (currency.Enemy >= price)
                                    {
                                        currency.Enemy -= (uint)price;
                                        success = true;
                                    }
                                    break;
                                case ObtainWay.Boss:
                                    if (currency.Boss >= price)
                                    {
                                        currency.Boss -= (uint)price;
                                        success = true;
                                    }
                                    break;
                            }
                            if (success)
                                currencies[i] = currency;
                            break;
                        }
                        if (!success)
                        {
                            SendFailure(ecb, archetype, 1, receive);
                            return;
                        }
                    }
                }
                EntityUtility.CreateAndDropItem(id, variation, amount, transLookup[player].Position, player, database, ecb);
            })
                .WithName("PointShopUpdate")
                .WithBurst()
                .Schedule();
            base.OnUpdate();
        }
        private static void SendFailure(EntityCommandBuffer ecb, EntityArchetype archetype, int reason, ReceiveRpcCommandRequest receive)
        {
            var e = ecb.CreateEntity(archetype);
            ecb.SetComponent(e, new BuyFailureRPC() { Reason = reason });
            ecb.SetComponent(e, new SendRpcCommandRequest() { TargetConnection = receive.SourceConnection });
        }
    }
}
