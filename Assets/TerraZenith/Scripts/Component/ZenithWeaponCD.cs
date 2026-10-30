using Unity.Entities;

namespace Assets.TerraZenith.Scripts.Component
{
    // 挂在天顶剑武器预制件上。原版远程攻击看到 projectileID 为空会直接返回，武器使用改由天顶剑自己的攻击系统来做。
    public struct ZenithWeaponCD : IComponentData
    {
        public ObjectID projectileID;
        public ObjectID fakeProjID;
    }
}
