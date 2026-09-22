using Assets.CoreEnhance.Scripts.Components;
using Assets.CoreEnhance.Scripts.Cores;
using Unity.Entities;

namespace Assets.CoreEnhance.Scripts.Systems.Accelerate
{
    [UpdateBefore(typeof(AddSkillValueSystem))]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    public partial class AccelerateLevelSystem : PugSimulationSystemBase
    {
        protected override void OnUpdate()
        {
            if (!EnhanceConfig.TryGetValue<int>(EnhanceCategory.Level, out var value))
                return;
            int multipler = value.Value;
            var ecb = CreateCommandBuffer();
            Entities.ForEach((Entity e, ref AddSkillValueCD skill) =>
            {
                skill.amount *= multipler;
                ecb.AddComponent<ProcessedTagCD>(e);
            })
                .WithName("Accelerate_Level")
                .WithBurst()
                .WithNone<ProcessedTagCD>()
                .Schedule();
            base.OnUpdate();
        }
    }
}
