using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace TomeOfMimicry
{
    public struct BlueprintPasteRPC : IRpcCommand
    {
        public const int CurrentProtocol = 2;

        public int protocol;
        public int command;
        public Entity player;
        public int requestId;
        public int chunkIndex;
        public int totalChunks;
        public int originX;
        public int originY;
        public int rotation;
        public int flipped;
        public int pasteMode;
        public FixedString512Bytes payload;
    }
}
