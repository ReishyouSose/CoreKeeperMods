using Assets.TerraZenith.Scripts.Component;
using Assets.TerraZenith.Scripts.Core;
using PugMod;
using Unity.Entities;
using UnityEngine;

namespace Assets.TerraZenith
{
    public class TerraZenith : IMod
    {
        public void EarlyInit()
        {
            API.Authoring.OnObjectTypeAdded += Authoring_OnObjectTypeAdded;
        }

        private void Authoring_OnObjectTypeAdded(Entity entity, GameObject authoringData, EntityManager entityManager)
        {
            if (authoringData.TryGetComponent<MeleeWeaponAuthoring>(out _)
                && authoringData.TryGetComponent<EntityMonoBehaviourData>(out var vanilla)
                && vanilla.ObjectInfo.objectType == ObjectType.MeleeWeapon)
            {
                ZenithBalance.Blades.Add(vanilla.ObjectInfo.objectID);
            }
            if (authoringData.TryGetComponent(out PlayerAuthoring _))
            {
                entityManager.AddComponent<ZenithWeaponTriggerTag>(entity);
                entityManager.SetComponentEnabled<ZenithWeaponTriggerTag>(entity, false);
            }
            // projectileID 留空，原版 RangeWeaponSlot.AttackWithItem 看到 None 会直接返回。
            // 冷却、朝向和弹幕改由天顶剑的预测攻击系统来做，伤害仍是武器上的近战标记。
            if (!authoringData.TryGetComponent<ObjectAuthoring>(out var authoring) || authoring.objectName != ZenithBalance.WeaponName)
                return;
            if (!authoringData.TryGetComponent<RangeWeaponAuthoring>(out _))
                return;
            entityManager.AddComponentData(entity, new ZenithWeaponCD
            {
                projectileID = API.Authoring.GetObjectID(ZenithBalance.SwordName)
            });
        }

        public void Init()
        {
        }

        public void ModObjectLoaded(Object obj)
        {
        }

        public void Shutdown()
        {
        }

        public void Update()
        {
        }
    }
}
