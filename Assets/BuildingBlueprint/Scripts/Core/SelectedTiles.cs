using System.Collections.Generic;
using Unity.Mathematics;

namespace Assets.BuildingBlueprint.Scripts.Core
{
    public struct SelectedTiles
    {
        public int2 Position;
        public Dictionary<TileCD, bool> Tiles;
    }
}
