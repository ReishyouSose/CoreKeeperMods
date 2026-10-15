using Assets.CoreEnhance.Scripts.Cores;
using PlayerState;
using Unity.Entities;

namespace Assets.CoreEnhance.Scripts.Systems.Quick
{
    [UpdateInGroup(typeof(PlayerStateSystemGroup))]
    [UpdateAfter(typeof(UpdatePlayerStateSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation)]
    public partial class InstantHookSystem : PugSimulationSystemBase
    {
        protected override void OnUpdate()
        {
            if (!EnhanceConfig.IsEnable(EnhanceCategory.InstantHook))
            {
                base.OnUpdate();
                return;
            }
            Entities.ForEach((ref FishingStateCD fish, in PlayerStateCD playerState) =>
            {
                if (!playerState.HasAnyState(PlayerStateEnum.Fishing))
                    return;
                if (!fish.isSuccessfullyFishing || fish.fishIsNibbling || fish.pullUpTimer.isRunning)
                    return;
                if (!fish.fishBiteTimer.isRunning || fish.fishBiteTimer.targetTicks <= 1)
                    return;
                fish.fishBiteTimer.targetTicks = 1;
            })
                .WithName("Quick_InstantHook")
                .WithBurst()
                .Schedule();
            base.OnUpdate();
        }
    }
}
