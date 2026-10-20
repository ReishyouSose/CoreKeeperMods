using Unity.Entities;
using Unity.Mathematics;

namespace Assets.TerraZenith.Scripts.Component
{
    public struct ZenithTrail : IBufferElementData
    {
        //[GhostField]
        public float2 Pos;
        //[GhostField]
        public float Timer;
    }
}
