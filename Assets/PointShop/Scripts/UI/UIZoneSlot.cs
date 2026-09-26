using Assets.PointShop.Scripts.Core;
using Assets.PointShop.Scripts.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.PointShop.Scripts.UI
{
    public class UIZoneSlot : ButtonUIElement
    {
        private static readonly WaitForSeconds time = new(0.1f);

        [HideInInspector]
        public ShopZoneDataBlock Zone;

        [HideInInspector]
        public Transform Page;

        public GameObject WarnBorder;
        public SpriteRenderer Icon;
        public SpriteRenderer Selected;
        public override void OnLeftClicked(bool mod1, bool mod2)
        {
            base.OnLeftClicked(mod1, mod2);
            PointShopUI.Ins.OnClickZoneSlot(this);
        }
        public override TextAndFormatFields GetHoverTitle()
        {
            return new()
            {
                text = $"ItemCategory/Environment_{Zone.name}Biome",
                color = Color.white,
            };
        }
        public override List<TextAndFormatFields> GetHoverStats(bool previewReinforced)
        {
            if (Zone.name != "None")
            {
                return new List<TextAndFormatFields>()
                {
                    new()
                    {
                        text = "PointShop/NeedDefeat",
                        color = Color.cyan,
                    },
                    new()
                    {
                        text = "Names/" + (Zone.Boss == ObjectID.BossLarva ? "LarvaBoss" : Zone.Boss),
                        color = Color.cyan,
                    }
                };
            }
            return null;
        }
        public void WarnNotDefeat()
        {
            StartCoroutine(NotDefeat());
        }
        private IEnumerator NotDefeat()
        {
            WarnBorder.SetActive(true);
            yield return time;
            WarnBorder.SetActive(false);
            yield return time;
            WarnBorder.SetActive(true);
            yield return time;
            WarnBorder.SetActive(false);
            yield return time;
            WarnBorder.SetActive(true);
            yield return time;
            WarnBorder.SetActive(false);
            yield return time;
        }
        protected override void OnDisable()
        {
            base.OnDisable();
            WarnBorder.SetActive(false);
        }
    }
}
