using Unity.Collections;
using Unity.NetCode;

namespace Assets.BuildingBlueprint.Scripts.Components
{
    public struct PlaceCommandChunkRPC : IRpcCommand
    {
        public const int ChunkSize = 20;
        public int ID;
        public int Index;
        public FixedList512Bytes<PlaceCommand> Commands;
    }
}
