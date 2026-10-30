using Unity.Entities;
using Unity.NetCode;

namespace Assets.TerraZenith.Scripts.Component
{
    public struct ZenithFakeProjBuffer : IBufferElementData
    {
        [GhostField]
        public Entity Entity;
    }
}
