using Unity.Entities;
using Unity.Mathematics;

namespace TomeOfMimicry
{
    public struct BlueprintStateCD : IComponentData
    {
        public GadgetState state;
        public int3 selectionStart;
        public int3 selectionEnd;
        public bool isDragging;
    }
}
