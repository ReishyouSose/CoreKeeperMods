using Assets.CoreFighter.Scripts.Cores;
using Unity.Entities;
using Unity.NetCode;

namespace Assets.CoreFighter.Scripts.Systems.Infinity
{
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation, WorldSystemFilterFlags.Default)]
    [UpdateAfter(typeof(HungerAndRunningSystem))]
    public partial class InfinityHungerSystem : PugSimulationSystemBase
    {
        private float timer;
        protected override void OnUpdate()
        {
            if (timer > 0)
            {
                timer -= SystemAPI.Time.DeltaTime;
                return;
            }
            timer = 0.2f;
            if (!FighterConfig.IsEnable(FighterCategory.Hunger))
                return;
            Entities.ForEach((ref HungerCD hunger) =>
            {
                if (hunger.hunger < HungerCD.MAX_HUNGER)
                {
                    hunger.hunger = HungerCD.MAX_HUNGER;
                }
            })
                .WithName("Infinity_Hunger")
                .WithBurst()
                .Schedule();
            base.OnUpdate();
        }
    }
}
