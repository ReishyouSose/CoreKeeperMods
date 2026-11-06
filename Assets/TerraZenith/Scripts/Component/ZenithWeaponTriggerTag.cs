using Unity.Entities;
using Unity.NetCode;

namespace Assets.TerraZenith.Scripts.Component
{
    [GhostComponent]
    public struct ZenithWeaponTriggerTag : IComponentData, IEnableableComponent
    {
        [GhostField]
        public int Count;
    }
}
