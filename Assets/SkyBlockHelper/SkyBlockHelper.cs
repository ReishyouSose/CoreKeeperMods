using CoreLib;
using CoreLib.Submodule.Command;
using PugMod;
using UnityEngine;

namespace Assets.SkyBlockHelper
{
    public class SkyBlockHelper : IMod
    {
        //internal static CustomScenesDataTable sceneData;
        public void EarlyInit()
        {
            CoreLibMod.LoadSubmodule(typeof(CommandModule));
            //sceneData = Resources.Load<CustomScenesDataTable>("Scenes/CustomScenesDataTable");
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