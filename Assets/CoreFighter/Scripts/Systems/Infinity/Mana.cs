using Assets.CoreFighter.Scripts.Cores;
using Unity.Entities;

namespace Assets.CoreFighter.Scripts.Systems.Infinity
{
    [UpdateInGroup(typeof(ConditionEffectsUpdateSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation, WorldSystemFilterFlags.Default)]
    [UpdateBefore(typeof(ManaSystem))]
    public partial class InfinityManaSystem : PugSimulationSystemBase
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
            if (!FighterConfig.IsEnable(FighterCategory.Mana))
                return;
            Entities.ForEach((ref ManaCD mana) =>
            {
                var max = mana.maxMana;
                if (mana.mana < max)
                {
                    mana.mana = max;
                }
            })
                .WithName("Infinity_Mana")
                .WithBurst()
                .Schedule();
            base.OnUpdate();
        }
    }
}
