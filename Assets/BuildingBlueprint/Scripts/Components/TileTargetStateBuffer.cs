using Unity.Entities;
using Unity.NetCode;

namespace Assets.BuildingBlueprint.Scripts.Components
{
    [InternalBufferCapacity(0)]
    public struct TileTargetStateBuffer : IBufferElementData
    {
        [GhostField]
        public bool State;
    }
}
