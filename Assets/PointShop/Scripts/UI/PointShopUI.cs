using Assets.PointShop.Scripts.Core;
using CoreLib.Submodule.UserInterface.Interface;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.PointShop.Scripts.UI
{
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
        public UISeparator[] Separators;
        public Transform Content;
        public PugText Header;
        public PugText PointValue;
        public Transform Pool;
        public UIPointCurrency Explore;
        public UIPointCurrency Chest;
        public UIPointCurrency Enemy;
        public UIPointCurrency Boss;
        private UIZoneSlot current;
        private GridLayoutUIComponent layout;
        private UIScrollWindow scroll;
        private List<UIShopSlot> slots;
        private int oldCoin;
        private void Awake()
        {
            Ins = this;
            scroll = GetComponent<UIScrollWindow>();
            ZoneTemplate.gameObject.SetActive(false);
            ShopSlotTemplate.gameObject.SetActive(false);
            PointShopManager.Init();
            var page = ZonePanel.scrollingContent.GetChild(0);
            foreach (var zone in PointShopManager.ZoneSort)
            {
                UIZoneSlot slot = Instantiate(ZoneTemplate, page);
                slot.Zone = zone;
                slot.Icon.sprite = zone.Icon;
                slot.gameObject.SetActive(true);
            }
            ZonePanel.gameObject.SetActive(true);
            layout = Content.GetComponent<GridLayoutUIComponent>();
            ZonePanel.ResetScroll();
            slots = new();
            HideUI();
        }

        private void Start()
        {
            var zoneLayout = ZonePanel.scrollingContent.GetComponentInChildren<LinearLayoutUIComponent>();
            zoneLayout.RenderUIComponent(true);
            OnClickZoneSlot(zoneLayout.transform.GetChild(0).GetComponent<UIZoneSlot>());
        }

        public void HideUI()
        {
            Root.SetActive(false);
        }

        public void ShowUI()
        {
            Manager.ui.TryHideAllInventoryAndCraftingUI();
            Root.SetActive(true);
        }
        public void OnClickZoneSlot(UIZoneSlot zoneSlot)
        {
            if (current)
            {
                current.Selected.gameObject.SetActive(false);
            }
            current = zoneSlot;
            current.Selected.gameObject.SetActive(true);
            var zone = zoneSlot.Zone;
            Header.Render($"ItemCategory/Environment_{zone.name}Biome", false, true);
            AudioManager.Sfx(SfxTableID.inventorySFXCreativeModeCategory, Manager.main.player.transform.position);
            foreach (var slot in slots)
            {
                slot.transform.SetParent(Pool, false);
            }
            foreach (var split in Separators)
            {
                split.transform.SetParent(Pool, false);
            }
            int x = 0, y = 0;
            ObtainWay? shown = null;
            var zoneID = zone.ZoneID;
            var boss = zone.Boss;
            foreach (var offer in PointShopManager.GetShopItems(zone))
            {
                if (shown != offer.Way)
                {
                    if (shown != null && y < Separators.Length)
                    {
                        UISeparator separator = Separators[y++];
                        separator.transform.SetParent(Content, false);
                    }
                    shown = offer.Way;
                }
                UIShopSlot slot;
                if (x >= slots.Count)
                {
                    slot = Instantiate(ShopSlotTemplate, Content);
                    slot.gameObject.SetActive(true);
                    slots.Add(slot);
                    slot.name = x.ToString();
                }
                slot = slots[x++];
                var item = offer.Item;
                slot.SetLimit(zoneID, boss);
                int amount = offer.Amount > 0 ? offer.Amount : item.Amount;
                slot.SetItem(new ObjectData { objectID = item.ObjectID, variation = item.Variation, amount = amount }, offer.Price, offer.Way, zone.name);
                slot.transform.SetParent(Content, false);
            }
            layout.RenderUIComponent(true);
            scroll.ResetScroll();
        }
        private void Update()
        {
            var player = Manager.main.player;
            var buffer = player.world.EntityManager.GetBuffer<PointCurrencyBuffer>(player.entity);
            if (!current)
                return;
            var zone = current.Zone;
            foreach (var currency in buffer)
            {
                if (currency.Zone == zone.name)
                {
                    Explore.UpdateCurrency(currency.Explore);
                    Chest.UpdateCurrency(currency.Chest);
                    Enemy.UpdateCurrency(currency.Enemy);
                    Boss.UpdateCurrency(currency.Boss);
                    break;
                }
            }
            var coin = player.playerInventoryHandler.GetExistingAmountOfObject(PointShop.Coin);
            if (oldCoin == coin)
                return;
            oldCoin = coin;
            PointValue.Render(coin.ToString(), false, true);
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
