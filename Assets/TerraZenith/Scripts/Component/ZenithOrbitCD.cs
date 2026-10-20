using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

// 天顶剑的服务器逻辑。发射对照玩家挥砍时连续生成 FinalFractal，飞行对照 Projectile.AI_182_FinalFractal。
namespace Assets.TerraZenith.Scripts.Component
{
    [GhostComponent]
    public struct ZenithOrbitCD : IComponentData
    {
        [GhostField]
        public float2 Offset;
        [GhostField]
        public float LongAxis;
        [GhostField]
        public float ShortAxis;
        [GhostField]
        public float Rotation;
        [GhostField]
        public float Timer;
        [GhostField]
        public int Sign;
    }
}
