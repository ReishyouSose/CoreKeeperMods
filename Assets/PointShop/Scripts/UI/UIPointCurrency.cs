using Assets.PointShop.Scripts.Core;
using UnityEngine;

namespace Assets.PointShop.Scripts.UI
{
    public class UIPointCurrency : ButtonUIElement
    {
        public ObtainWay Way;
        private uint oldValue;
        public void UpdateCurrency(uint value)
        {
            if (value == oldValue)
                return;
            oldValue = value;
            text.Render(value.ToString(), false, true);
        }
        public override TextAndFormatFields GetHoverTitle()
        {
            return new()
            {
                text = $"Items/PointShop_{Way}Point",
                color = Color.white,
            };
        }
    }
}
