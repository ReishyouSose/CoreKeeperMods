using Unity.Entities;
using Unity.NetCode;

namespace Assets.TerraZenith.Scripts.Component
{
    [GhostComponent]
    public struct DropZenithCD : IComponentData, IQueryTypeParameter
    {
        [GhostField]
        public int AdditiveChance;
    }
}
