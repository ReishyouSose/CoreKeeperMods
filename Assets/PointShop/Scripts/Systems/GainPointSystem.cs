using Assets.PointShop.Scripts.Core;
using Unity.Entities;
using Unity.NetCode;

namespace Assets.PointShop.Scripts.Systems
{
    [UpdateInGroup(typeof(RunSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class GainPointSystem : PugSimulationSystemBase
    {
        private BufferLookup<PointCurrencyBuffer> currencyLookup;
        private ComponentLookup<PlayerGhost> playerLookup;
        private EntityArchetype archetype;
        protected override void OnCreate()
        {
            EntityManager.CreateSingletonBuffer<GainPointBuffer>("GainPointBuffer");
            RequireForUpdate<GainPointBuffer>();
            currencyLookup = SystemAPI.GetBufferLookup<PointCurrencyBuffer>();
            playerLookup = SystemAPI.GetComponentLookup<PlayerGhost>();
            archetype = EntityManager.CreateArchetype(typeof(PointGainReportRPC), typeof(SendRpcCommandRequest));
            base.OnCreate();
        }
        protected override void OnUpdate()
        {
            if (!SystemAPI.TryGetSingletonBuffer<GainPointBuffer>(out var buffer))
                return;
            int multiplier = PointShop.PointMultiplier.Value;
            var currencyLookup = this.currencyLookup;
            var playerLookup = this.playerLookup;
            var ecb = CreateCommandBuffer();
            var archetype = this.archetype;
            foreach (var gain in buffer)
            {
                var player = gain.Player;
                if (!playerLookup.TryGetComponent(player, out var playerGhost))
                    continue;
                if (!currencyLookup.TryGetBuffer(gain.Player, out var currencies))
                    continue;
                var zone = gain.Zone;
                for (int i = 0; i < currencies.Length; i++)
                {
                    var currency = currencies[i];
                    if (currency.Zone != gain.Zone)
                        continue;
                    int explore = 0;
                    int chest = 0;
                    int enemy = 0;
                    int boss = 0;
                    switch (gain.Way)
                    {
                        case ObtainWay.Explore:
                            explore = 20;
                            break;
                        case ObtainWay.Chest:
                            explore = 15;
                            chest = 10;
                            break;
                        case ObtainWay.Enemy:
                            explore = 10;
                            enemy = 1;
                            break;
                        case ObtainWay.Boss:
                            explore = 20;
                            enemy = 5;
                            boss = 1;
                            break;
                        case ObtainWay.Wall:
                            explore = 1;
                            break;
                        case ObtainWay.Animal:
                            enemy = 1;
                            break;
                    }
                    currency.Explore += (uint)(explore * multiplier);
                    currency.Chest += (uint)(chest * multiplier);
                    currency.Enemy += (uint)(enemy * multiplier);
                    currency.Boss += (uint)(boss * multiplier);
                    currencies[i] = currency;
                    Entity e = ecb.CreateEntity(archetype);
                    ecb.SetComponent(e, new PointGainReportRPC() { Zone = zone, Way = gain.Way });
                    ecb.SetComponent(e, new SendRpcCommandRequest() { TargetConnection = playerGhost.connection });
                    break;
                }
            }
            buffer.Clear();
            base.OnUpdate();
        }
    }
}
