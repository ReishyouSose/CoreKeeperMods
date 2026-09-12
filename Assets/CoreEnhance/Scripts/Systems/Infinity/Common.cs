using Assets.CoreEnhance.Scripts.Cores;
using Unity.Entities;

namespace Assets.CoreEnhance.Scripts.Systems.Infinity
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class InfinityCommonSystem : PugSimulationSystemBase
    {
        private const int ResetTimer = 1;
        private float timer;
        protected override void OnUpdate()
        {
            if (timer < ResetTimer)
            {
                timer += World.Time.DeltaTime;
                return;
            }
            timer = 0;
            Infinity_Boulder();
            base.OnUpdate();
        }
        private void Infinity_Boulder()
        {
            if (!EnhanceConfig.IsEnable(EnhanceCategory.Boulder))//TODO:无限大矿需要测试
                return;
            Entities.ForEach((ref HealthCD heal, in DropsLootWhenDamagedCD dr) =>
            {
                if (heal.health <= 0)
                    return;
                if (heal.health < heal.maxHealth - dr.damageToDealToDropLoot)
                {
                    heal.health = heal.maxHealth;
                }
            })
                .WithName("Infinity_Boulder")
                .WithAll<RequiresDrillCD>()
                .WithAll<DontDropSelfCD>()
                .WithEntityQueryOptions(EntityQueryOptions.IncludeDisabledEntities)
                .WithBurst()
                .Schedule();
        }
    }
}
