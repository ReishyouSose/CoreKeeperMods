using Assets.PointShop.Scripts.Systems;
using CoreLib;
using CoreLib.Data.Configuration;
using CoreLib.Submodule.ControlMapping;
using CoreLib.Submodule.UserInterface;
using CoreLib.Util.Extension;
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
        public static ConfigEntry<bool> ShowSwitch { get; private set; }
        public void EarlyInit()
        {
            CoreLibMod.LoadSubmodule(typeof(UserInterfaceModule), typeof(ControlMappingModule));
            ControlMappingModule.AddKeyboardBind(Open, KeyboardKeyCode.P, ModifierKey.Control);
            API.Authoring.OnObjectTypeAdded += DropPointSystem.AddPointDrop;
            ConfigFile file = new(nameof(PointShop) + "/Config.cfg", true);
            ShowSwitch = file.Bind("General", nameof(ShowSwitch), true, null, new(ConfigAccessLevel.Client));
        }

        public void Init()
        {
            Coin = API.Authoring.GetObjectID("PointShop_Currency");
            Debug.Log("On init: coin ID is" + Coin);
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
