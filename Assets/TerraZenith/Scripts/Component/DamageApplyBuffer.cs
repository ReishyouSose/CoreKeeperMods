using Unity.Entities;
using Unity.Mathematics;

namespace Assets.TerraZenith.Scripts.Component
{
    public struct DamageApplyBuffer : IBufferElementData
    {
        public Entity attacker;            // 攻击者（用于归属、技能统计、击退来源）
        public Entity target;              // ★ 已确认命中的目标（新增，核心）
        public float3 hitPosition;         // ★ 命中点（特效/击退方向用，新增）
        public float damage;               // 对目标造成的伤害
        public DamageEffectType damageEffectType; // 伤害类型（元素/物理等）
        public bool isMagic;               // 是否法术（技能统计）
        public bool isRanged;              // 是否远程（技能统计）
        public bool isPredicted;           // 预测侧
        public float manaCost;             // 法力消耗（若有）
        public float pushback;             // 击退力度
        public bool reversePushback;       // 反向击退
        public bool triggerAnimationOnClientHit; // 客户端受击动画
        public float3 castDirection;       // 施法/击退方向
        public Entity effectEventBufferSingleton; // 特效事件缓冲单例
        public BehaviourTagsCD behaviourTags; // 若结算内仍需 CanAttack 校验
        public bool performAttackEvenIfZeroDamage;
        public bool isExplosive;
    }
}
