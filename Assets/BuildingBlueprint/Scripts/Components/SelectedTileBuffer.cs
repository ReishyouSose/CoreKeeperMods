using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Assets.BuildingBlueprint.Scripts.Components
{
    [InternalBufferCapacity(0)]
    public struct SelectedTileBuffer : IBufferElementData
    {
        [GhostField]
        public int2 Position;
        [GhostField]
        public TileCD Tile;
        [GhostField]
        public bool State;
    }
}
