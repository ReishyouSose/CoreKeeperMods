using Assets.BuildingBlueprint.Scripts.Components;
using Assets.BuildingBlueprint.Scripts.Systems;
using Assets.BuildingBlueprint.Scripts.UI;
using CoreLib;
using CoreLib.Data.Configuration;
using CoreLib.Submodule.ControlMapping;
using CoreLib.Submodule.UserInterface;
using PugMod;
using UnityEngine;

namespace Assets.BuildingBlueprint
{
    public class BuildingBlueprint : IMod
    {
        internal const string Key = "BuildingBlueprint_";
        internal const string Menu = Key + "Menu";
        internal const string OpenUI = Key + "OpenUI";
        internal static ConfigEntry<bool> DestoryPrivileges;
        internal static ConfigEntry<bool> ClearSelected;
        internal static ConfigEntry<bool> ExitPlaceMode;
        public void EarlyInit()
        {
            CoreLibMod.LoadSubmodule(typeof(UserInterfaceModule));
            CoreLibMod.LoadSubmodule(typeof(ControlMappingModule));
            int category = ControlMappingModule.AddNewCategory(Key[..^1]);
            ControlMappingModule.AddKeyboardBind(OpenUI, Rewired.KeyboardKeyCode.B, categoryId: category);
            API.Authoring.OnObjectTypeAdded += Authoring_OnObjectTypeAdded;
            var file = new ConfigFile("BuildingBlueprint/Config.cfg", true);
            DestoryPrivileges = file.Bind("General", nameof(DestoryPrivileges), true, scope: new(ConfigAccessLevel.Admin));
            ClearSelected = file.Bind("OnUIOpen", nameof(ClearSelected), true, scope: new(ConfigAccessLevel.Client));
            ExitPlaceMode = file.Bind("OnUIOpen", nameof(ExitPlaceMode), true, scope: new(ConfigAccessLevel.Client));
        }

        private void Authoring_OnObjectTypeAdded(Unity.Entities.Entity entity, GameObject authoringData, Unity.Entities.EntityManager entityManager)
        {
            BuildingSelectServer.AddSelectComponent(entity, authoringData, entityManager);
            BuildingSelectServer.MarkPlaceable(entity, authoringData, entityManager);
        }

        public void Init()
        {
        }

        public void ModObjectLoaded(Object obj)
        {
            if (obj is GameObject go)
            {
                UserInterfaceModule.RegisterModUI(go);
            }
        }

        public void Shutdown()
        {
        }

        public void Update()
        {
            var player = Manager.main.player;
            if (!player)
                return;
            if (Manager.input.textInputIsActive)
                return;
            if (player.inputModule.rewiredPlayer.GetButtonDown(OpenUI))
            {
                var ui = BlueprintUI.Ins;
                if (!ui.Root.activeSelf)
                    ui.ShowUI();
                else
                    ui.HideUI();
            }
        }
    }
}
