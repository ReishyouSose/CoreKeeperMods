using CoreLib.Submodule.UserInterface.Interface;
using UnityEngine;

namespace Assets.PointShop.Scripts.UI
{
    public class ShopSwitchUI : MonoBehaviour, IModUI
    {
        public GameObject Switch;
        public GameObject Root => Switch;

        public bool ShowWithPlayerInventory => false;

        public bool ShouldPlayerCraftingShow => false;

        public void HideUI()
        {
            Root.SetActive(false);
        }

        public void ShowUI()
        {
            Root.SetActive(true);
        }
        private void Update()
        {
            bool active = Switch.activeSelf;
            if (Manager.main.player == null)
            {
                if (active)
                    HideUI();
                return;
            }
            if (!PointShop.ShowSwitch.Value)
            {
                if (active)
                    HideUI();
                return;
            }
            var ui = Manager.ui;
            if (active && ui.isAnyInventoryShowing)
                HideUI();
            else if (!active && !ui.isAnyInventoryShowing)
                ShowUI();
        }
        private void LateUpdate()
        {
            Switch.transform.localScale = Manager.ui.CalcGameplayUITargetScaleMultiplier();
        }
    }
}
