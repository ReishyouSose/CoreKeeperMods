using CoreLib.Submodule.UserInterface.Interface;
using System.Linq;
using UnityEngine;

namespace Assets.PointShop.Scripts
{
    [RequireComponent(typeof(ShopInfo))]
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

        private UIZoneSlot current;
        private ShopInfo info;
        private GridLayoutUIComponent layout;
        private UIScrollWindow scroll;
        private void Awake()
        {
            Ins = this;
            info = GetComponent<ShopInfo>();
            scroll = GetComponent<UIScrollWindow>();
            ZoneTemplate.gameObject.SetActive(false);
            ShopSlotTemplate.gameObject.SetActive(false);
            EmptryPage.gameObject.SetActive(false);
            info.Init();
            int max = (int)Zone.MAX;
            var page = ZonePanel.scrollingContent.GetChild(0);
            for (int i = 0; i < max; i++)
            {
                Zone zone = (Zone)i;
                UIZoneSlot slot = Instantiate(ZoneTemplate, page);
                slot.Zone = zone;
                slot.Boss = info.GetBoss(zone);
                slot.Icon.sprite = SelectZoneIcon(zone);
                slot.gameObject.SetActive(true);
                slot.Page = RegisterShop(zone);
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
        private Transform RegisterShop(Zone zone)
        {
            var page = Instantiate(EmptryPage, PageContainer);
            page.gameObject.SetActive(false);
            var contents = page.GetChild(0);
            var items = info.GetShop(zone);
            items = items.OrderBy(x => PugDatabase.GetObjectInfo(x.Item.objectID).rarity).ThenBy(x => x.Item.objectID).ToList();
            var boss = info.GetBoss(zone);
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.Item.objectID == ObjectID.None)
                    continue;
                UIShopSlot slot = Instantiate(ShopSlotTemplate, contents);
                slot.Zone = zone;
                slot.Boss = boss;
                slot.SetItem(item.Item, item.Price, item.Currency);
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
            Header.Render($"ItemCategory/Environment_{slot.Zone}Biome", false, true);
            AudioManager.Sfx(SfxTableID.inventorySFXCreativeModeCategory, Manager.main.player.transform.position);
            scroll.ResetScroll();
        }
        private void Update()
        {
            PointValue.Render(Manager.main.player.playerInventoryHandler.GetExistingAmountOfObject(PointShop.Coin).ToString(), false, true);
        }
        private static Sprite SelectZoneIcon(Zone zone)
        {
            ObjectID id = zone switch
            {
                Zone.Dirt => ObjectID.WallDirtBlock,
                Zone.Clay => ObjectID.WallClayBlock,
                Zone.LarvaHive => ObjectID.WallHiveBlock,
                Zone.Stone => ObjectID.WallStoneBlock,
                Zone.Nature => ObjectID.WallGrassBlock,
                Zone.Mold => ObjectID.WallMoldBlock,
                Zone.Sea => ObjectID.WallLimestoneBlock,
                Zone.City => ObjectID.WallCityBlock,
                Zone.Desert => ObjectID.WallDesertBlock,
                Zone.Lava => ObjectID.WallLavaBlock,
                Zone.Crystal => ObjectID.WallCrystalBlock,
                Zone.Oasis => ObjectID.WallOasisBlock,
                Zone.Alien => ObjectID.WallAlienBlock,
                Zone.Passage => ObjectID.WallPassageBlock,
                Zone.Excavation => ObjectID.WallExcavationBlock,
                _ => ObjectID.WallObsidianBlock,
            };
            ObjectInfo info = PugDatabase.GetObjectInfo(id);
            return info.smallIcon;
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
