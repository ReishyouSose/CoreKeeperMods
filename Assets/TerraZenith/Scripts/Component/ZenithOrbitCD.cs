using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

// 天顶剑的服务器逻辑。发射对照玩家挥砍时连续生成 FinalFractal，飞行对照 Projectile.AI_182_FinalFractal。
namespace Assets.TerraZenith.Scripts.Component
{
    public struct ZenithOrbitCD : IComponentData
    {
        public float2 Offset;
        public float LongAxis;
        public float ShortAxis;
        public float Rotation;
        public float Timer;
    }
}
