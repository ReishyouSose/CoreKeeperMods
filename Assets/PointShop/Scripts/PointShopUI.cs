using CoreLib.Submodule.UserInterface.Interface;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.PointShop.Scripts
{
    [RequireComponent(typeof(ShopManager))]
    [RequireComponent(typeof(UIScrollWindow))]
    public class PointShopUI : UIelement, IModUI, IScrollable
    {
        internal static PointShopUI Ins { get; private set; }
        public GameObject Root => gameObject;

        public bool ShowWithPlayerInventory => true;

        public bool ShouldPlayerCraftingShow => false;
        public UIScrollWindow ZonePanel;
        public UIZoneSlot ZoneTemplate;
        public UIShopSlot ShopSlotTemplate;
        public Transform EmptryPage;
        public Transform PageContainer;
        public PugText Header;
        public PugText PointValue;
        private ShopManager manager;
        private UIZoneSlot current;
        private GridLayoutUIComponent layout;
        private UIScrollWindow scroll;
        private void Awake()
        {
            Ins = this;
            manager = GetComponent<ShopManager>();
            scroll = GetComponent<UIScrollWindow>();
            ZoneTemplate.gameObject.SetActive(false);
            ShopSlotTemplate.gameObject.SetActive(false);
            EmptryPage.gameObject.SetActive(false);
            var page = ZonePanel.scrollingContent.GetChild(0);
            manager.Awake();
            foreach (var zone in manager.ZoneSort)
            {
                UIZoneSlot slot = Instantiate(ZoneTemplate, page);
                slot.Zone = zone;
                slot.Icon.sprite = zone.Icon;
                slot.gameObject.SetActive(true);
                slot.Page = RegisterShop(zone, manager.GetShopItems(zone));
            }
            ZonePanel.gameObject.SetActive(true);
            HideUI();
        }

        public void HideUI()
        {
            Root.SetActive(false);
        }

        public void ShowUI()
        {
            Manager.ui.TryHideAllInventoryAndCraftingUI();
            Root.SetActive(true);
            var layout = ZonePanel.scrollingContent.GetComponentInChildren<LinearLayoutUIComponent>();
            layout.RenderUIComponent(true);
            ZonePanel.ResetScroll();
            if (!current)
            {
                OnClickZoneSlot(layout.transform.GetChild(0).GetComponent<UIZoneSlot>());
            }
        }
        private Transform RegisterShop(ShopZoneDataBlock zone, List<ShopItemDataBlock> items)
        {
            var page = Instantiate(EmptryPage, PageContainer);
            page.gameObject.SetActive(false);
            var contents = page.GetChild(0);
            //items = items.OrderBy(x => PugDatabase.GetObjectInfo(x.Item.objectID).rarity).ThenBy(x => x.Item.objectID).ToList();
            var boss = zone.Boss;
            var zoneID = zone.ZoneID;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                UIShopSlot slot = Instantiate(ShopSlotTemplate, contents);
                slot.SetLimit(zoneID, boss);
                slot.SetItem(new ObjectData { objectID = item.ObjectID, variation = item.Variation, amount = item.Amount }, item.Price, PointShop.Coin);
                slot.gameObject.SetActive(true);
            }
            return page;
        }
        public void OnClickZoneSlot(UIZoneSlot slot)
        {
            if (current)
            {
                current.Selected.gameObject.SetActive(false);
                current.Page.gameObject.SetActive(false);
            }
            current = slot;
            current.Selected.gameObject.SetActive(true);
            var page = current.Page;
            page.gameObject.SetActive(true);
            scroll.scrollingContent = page;
            layout = page.GetComponentInChildren<GridLayoutUIComponent>();
            layout.RenderUIComponent(true);
            Header.Render($"ItemCategory/Environment_{slot.Zone.name}Biome", false, true);
            AudioManager.Sfx(SfxTableID.inventorySFXCreativeModeCategory, Manager.main.player.transform.position);
            scroll.ResetScroll();
        }
        private void Update()
        {
            PointValue.Render(Manager.main.player.playerInventoryHandler.GetExistingAmountOfObject(PointShop.Coin).ToString(), false, true);
        }

        public void UpdateContainingElements(float _)
        {
            if (!layout)
                return;
            Vector3 center = scroll.transform.position + (Vector3)scroll.windowLocalCenter;
            Rect rect = new(center.x - scroll.windowWidth / 2f, center.y - scroll.windowHeight / 2f, scroll.windowWidth, scroll.windowHeight);
            foreach (Transform trans in layout.transform)
            {
                if (trans.TryGetComponent<UIComponentMonoBehaviour>(out var ui) && trans.TryGetComponent<BoxCollider>(out var box))
                {
                    float width = ui.GetUIComponentRenderWidth();
                    float height = ui.GetUIComponentRenderHeight();

                    // 根据 Pivot 计算左下角
                    float left = trans.position.x;
                    float bottom = trans.position.y;

                    if (ui.GetUIComponentPivotPosition() == UIComponentMonoBehaviour.PivotPosition.TopLeft)
                        bottom -= height;
                    else // MiddleLeft
                        bottom -= height / 2f;

                    box.enabled = new Rect(left, bottom, width, height).Overlaps(rect);
                }
            }
        }

        public bool IsBottomElementSelected() => layout ? layout.IsBottomElemntSelected() : false;

        public bool IsTopElementSelected() => layout ? layout.IsTopElementSelected() : false;

        public float GetCurrentWindowHeight()
        {
            if (layout)
                return layout.GetUIComponentRenderHeight();
            return 0;
        }
        public void WarnNotDefeat()
        {
            current.WarnNotDefeat();
        }
    }
}
