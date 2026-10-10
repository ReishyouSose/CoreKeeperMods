using Assets.PointShop.Scripts.Core;
using CoreLib;
using CoreLib.Data.Configuration;
using CoreLib.Submodule.ControlMapping;
using CoreLib.Submodule.UserInterface;
using PugMod;
using Rewired;
using UnityEngine;

namespace Assets.PointShop
{
    public class PointShop : IMod
    {
        private const string UIName = "PointShop_PointShopMenu";
        private const string Open = "PointShop_Open";
        public static ObjectID Coin { get; private set; }
        public static ObjectID ExplorePoint { get; private set; }
        public static ObjectID ChestPoint { get; private set; }
        public static ObjectID EnemyPoint { get; private set; }
        public static ObjectID BossPoint { get; private set; }
        public static ConfigEntry<bool> ShowSwitch { get; private set; }
        public static ConfigEntry<int> PointMultiplier { get; private set; }
        public void EarlyInit()
        {
            CoreLibMod.LoadSubmodule(typeof(UserInterfaceModule), typeof(ControlMappingModule));
            ControlMappingModule.AddKeyboardBind(Open, KeyboardKeyCode.P, ModifierKey.Control);
            PointShopManager.EarlyInit();
            API.Authoring.OnObjectTypeAdded += Authoring_OnObjectTypeAdded;
            ConfigFile file = new(nameof(PointShop) + "/Config.cfg", true);
            ShowSwitch = file.Bind("General", nameof(ShowSwitch), true, null, new(ConfigAccessLevel.Client));
            PointMultiplier = file.Bind("General", nameof(PointMultiplier), 1, new(string.Empty, new AcceptableValueRange<int>(1, 3)), new(ConfigAccessLevel.Client));
        }

        private void Authoring_OnObjectTypeAdded(Unity.Entities.Entity entity, GameObject authoringData, Unity.Entities.EntityManager entityManager)
        {
            if (!authoringData.TryGetComponent<PlayerAuthoring>(out _))
                return;
            if (!ScriptableData.TryGetDataBlocks<ShopZoneDataBlock>(out var zones))
                return;
            var buffer = entityManager.AddBuffer<PointCurrencyBuffer>(entity);
            foreach (var zone in zones)
            {
                buffer.Add(new() { Zone = zone.name, BossID = zone.Boss });
            }
        }

        public void Init()
        {
            var api = API.Authoring;
            Coin = api.GetObjectID("PointShop_Currency");
            ExplorePoint = api.GetObjectID("PointShop_ExplorePoint");
            ChestPoint = api.GetObjectID("PointShop_ChestPoint");
            EnemyPoint = api.GetObjectID("PointShop_EnemyPoint");
            BossPoint = api.GetObjectID("PointShop_BossPoint");
        }

        public void ModObjectLoaded(Object obj)
        {
            if (obj is GameObject gameObj)
            {
                UserInterfaceModule.RegisterModUI(gameObj);
            }
        }

        public void Shutdown()
        {
        }

        public void Update()
        {
            var player = Manager.main.player;
            if (player == null)
                return;
            var p = player.inputModule.rewiredPlayer;
            if (p.GetButtonDown(Open))
                OpenShop();
        }
        public static void OpenShop()
        {
            UserInterfaceModule.OpenModUI(UIName);
        }
    }
}
