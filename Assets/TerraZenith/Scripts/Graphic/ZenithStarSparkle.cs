using UnityEngine;

namespace Assets.TerraZenith.Scripts.Graphic
{
    public class ZenithStarSparkle : MonoBehaviour
    {
        public SpriteRenderer H_Low;
        public SpriteRenderer H_High;
        public SpriteRenderer V_Low;
        public SpriteRenderer V_High;
        public void Set(Color color)
        {
            var low = color;
            low /= 2f;
            low.a = 1;
            Set(H_Low, low);
            Set(V_Low, low);
            Set(H_High, color);
            Set(V_High, color);
        }
        private static void Set(SpriteRenderer sr, Color color)
        {
            sr.color = color;
        }
    }
}
