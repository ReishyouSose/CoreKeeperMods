using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Assets.TerraZenith.Scripts.Component
{
    public struct ZenithTrail : IBufferElementData
    {
        [GhostField]
        public float2 Pos;
        [GhostField]
        public float2 Dir;
        [GhostField]
        public float Progress;
    }
}
