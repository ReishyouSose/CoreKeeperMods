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
            // 武器走原版远程攻击，弹幕 ID 在这里接到天顶剑弹幕上。伤害仍由武器上的近战标记决定。
            if (!authoringData.TryGetComponent<ObjectAuthoring>(out var authoring) || authoring.objectName != ZenithBalance.WeaponName)
                return;
            if (!authoringData.TryGetComponent<RangeWeaponAuthoring>(out _))
                return;
            var range = entityManager.GetComponentData<RangeWeaponCD>(entity);
            range.projectileID = API.Authoring.GetObjectID(ZenithBalance.SwordName);
            entityManager.SetComponentData(entity, range);
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
