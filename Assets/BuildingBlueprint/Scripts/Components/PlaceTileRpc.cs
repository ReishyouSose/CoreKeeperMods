using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Assets.BuildingBlueprint.Scripts.Components
{
    public struct PlaceTileRpc : IRpcCommand
    {
        public int ID;
        public TileCD Tile;
        public int2 Pos;
        public ObjectID ObjectID;
        public int Variation;
        public Entity Player;
        [DontSerializeForCommand]
        public TickTimer Timer;
    }
}
