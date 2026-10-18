using Assets.CoreFighter.Scripts.Cores;
using Assets.CoreFighter.Scripts.Systems.Equip;
using PugMod;
using Unity.Entities;
using UnityEngine;

namespace Assets.CoreFighter
{
    public class CoreFighter : IMod
    {
        public void EarlyInit()
        {
            new FighterConfig().Register();
            API.Authoring.OnObjectTypeAdded += Authoring_OnObjectTypeAdded;
        }

        private void Authoring_OnObjectTypeAdded(Entity entity, GameObject authoringData, EntityManager entityManager)
        {
            NoRecoilSystem.RecordOriginMoveSpeed(entity, authoringData, entityManager);
            AttackSpeedModifierSystem.RecordOriginATKSpeed(entity, authoringData, entityManager);
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
