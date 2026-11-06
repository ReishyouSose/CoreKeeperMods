using Unity.Entities;

namespace Assets.TerraZenith.Scripts.Component
{
    public struct ZenithWeaponCD : IComponentData
    {
        public ObjectID projectileID;
    }
}
