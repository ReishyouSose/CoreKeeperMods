using Unity.Entities;

namespace Assets.TerraZenith.Scripts.Component
{
    // 玩家身上的本地开关，对照 RangedWeaponSpawnProjectileTriggerTag。
    // 不进幽灵快照。攻击系统在预测里打开，生成系统同一拍关掉并射出弹幕。
    public struct ZenithWeaponTriggerTag : IComponentData, IEnableableComponent
    {
    }
}
