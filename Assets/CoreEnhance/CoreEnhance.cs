using Assets.CoreEnhance.Scripts.Cores;
using Assets.CoreEnhance.Scripts.Items;
using Assets.CoreEnhance.Scripts.Systems.Automation;
using Assets.CoreEnhance.Scripts.Systems.Infinity;
using Assets.CoreEnhance.Scripts.Systems.Misc;
using Assets.CoreEnhance.Scripts.Systems.Quick;
using CoreLib;
using CoreLib.Submodule.ControlMapping;
using CoreLib.Submodule.Entity;
using PugMod;
using Unity.Entities;
using UnityEngine;

namespace Assets.CoreEnhance
{
    public class CoreEnhance : IMod
    {
        public const string InternalName = "CoreEnhance:";
        public void EarlyInit()
        {
            new EnhanceConfig().Register();
            var authoring = API.Authoring;
            authoring.OnObjectTypeAdded += Authoring_OnObjectTypeAdded;
            ECSManager.BeforeInitialEntityConversion += ECSManager_BeforeInitialEntityConversion;
            CoreLibMod.LoadSubmodule(typeof(ControlMappingModule), typeof(EntityModule));
            ModKeyBind.Load();
        }

        private void ECSManager_BeforeInitialEntityConversion()
        {
            foreach (var database in PugDatabase.entityMonobehaviours)
            {
                if (database.ObjectInfo?.prefabInfo?.authoringRef.TryGet(out var authoring) != true)
                    continue;
                DataModify.AuthoringPreModify(authoring.prefab);
            }
        }

        private void Authoring_OnObjectTypeAdded(Entity entity, GameObject authoringData, EntityManager entityManager)
        {
            AutoDoorSystem.MarkDoor(entity, authoringData, entityManager);
            InfinityArenaSystem.MarkArena(entity, authoringData, entityManager);
            DataModify.AuthoringPostModify(entity, authoringData, entityManager);
            ContainerDisplaySystem.MarkHighLight(entity, authoringData, entityManager);
            SledgeRangeSystem.MarkSledge(entity, authoringData, entityManager);
            InfinityBossScanSystem.MarkCircleMoveBoss(entity, authoringData, entityManager);
            MoveChestClient.AddHoveredChest(entity, authoringData, entityManager);
            ProjectileTimerMultiplierSystem.MarkProjectileTileLeft(entity, authoringData, entityManager);
            //CraftStationRangeSystem.MarkCraftStation(entity, authoringData, entityManager);
            Test(entity, authoringData, entityManager);
        }

        private void Test(Entity entity, GameObject authoringData, EntityManager entityManager)
        {
            //authoringData.LogComponent(ObjectID.ClayWormTrophy);
        }

        public void Init()
        {
            //BurstDisabler.DisableBurstForSystem<PugAutomationFishingSystem>();
        }

        public void ModObjectLoaded(Object obj)
        {
        }

        public void Shutdown()
        {
        }

        public void Update()
        {
            var p = Manager.main.player;
            if (p == null)
                return;
            ModKeyBind.Handle(p);
        }
    }
}
