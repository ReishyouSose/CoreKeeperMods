using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Assets.TerraZenith.Scripts.Component
{
    public struct LocalHitCoolDownCD : IComponentData
    {
        public NativeParallelHashMap<(Entity, Entity), TickTimer> EntityCoolDown;
        public NativeParallelHashMap<(Entity, int2), TickTimer> WallCoolDown;
        public void AddCoolDown(Entity entity, Entity target, uint targetTicks, NetworkTick currentTick)
        {
            TickTimer timer = new(targetTicks);
            timer.Start(currentTick);
            EntityCoolDown.Add((entity, target), timer);
        }
        public void AddCoolDown(Entity entity, int2 pos, uint targetTicks, NetworkTick currentTick)
        {
            TickTimer timer = new(targetTicks);
            timer.Start(currentTick);
            WallCoolDown.Add((entity, pos), timer);
        }
        public bool InCoolDown(Entity entity, Entity target) => EntityCoolDown.ContainsKey((entity, target));
        public bool InCoolDown(Entity entity, int2 pos) => WallCoolDown.ContainsKey((entity, pos));
    }
}
