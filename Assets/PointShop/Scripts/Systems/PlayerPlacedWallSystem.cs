using Assets.PointShop.Scripts.Core;
using Unity.Entities;

namespace Assets.PointShop.Scripts.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(DestroyEntityIfPlacementNotValidSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class PlayerPlacedWallSystem : PugSimulationSystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = CreateCommandBuffer();
            var discoverableLookup = SystemAPI.GetComponentLookup<CanBeDiscoveredCD>(true);
            Entities.ForEach((Entity entity, in GainPointCD gain) =>
            {
                if (gain.Way == ObtainWay.Wall)
                {
                    ecb.AddComponent<HasBeenDiscoveredCD>(entity);
                    return;
                }
                if (gain.Way == ObtainWay.Chest && discoverableLookup.HasComponent(entity))
                    ecb.RemoveComponent<CanBeDiscoveredCD>(entity);
            })
                .WithName("PlayerPlacedWall")
                .WithAll<DestroyEntityIfPlacementNotValidCD>()
                .WithNone<HasBeenDiscoveredCD>()
                .WithBurst()
                .Schedule();
            base.OnUpdate();
        }
    }
}
