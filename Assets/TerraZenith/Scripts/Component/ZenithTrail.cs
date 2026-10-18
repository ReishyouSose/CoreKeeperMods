using Unity.Entities;
using Unity.Mathematics;

// 天顶剑的服务器逻辑。发射对照玩家挥砍时连续生成 FinalFractal，飞行对照 Projectile.AI_182_FinalFractal。
namespace Assets.TerraZenith.Scripts.Component
{
    // 飞行系统每写出一个位置就压进这个缓冲。不是 GhostComponent，不走网络。
    // 图形只读它，不再自己补弧线。下标 0 是最新点，长度最多 60，对应 TrailCacheLength。
    [InternalBufferCapacity(60)]
    public struct ZenithTrail : IBufferElementData
    {
        public const int Capacity = 60; // TrailCacheLength[933]
        public float3 position; // 模拟世界坐标，和 LocalTransform.Position 同一套
        public float angle;     // 这一拍的剑身角
        public float spin;      // 0 起手，1 转满。后半段是收回玩家身后，用来把刀光收尾变淡
    }
}
