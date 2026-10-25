using Unity.Mathematics;
using UnityEngine;

namespace Assets.TerraZenith.Scripts.Graphic
{
    public class ZenithStarSparkle : MonoBehaviour
    {
        public SpriteRenderer H_Low;
        public SpriteRenderer H_High;
        public SpriteRenderer V_Low;
        public SpriteRenderer V_High;
        private const float Line = 0.25f;
        private const float Half = 0.125f;
        public void Set(Color color, float scale)
        {
            var low = color;
            low /= 2f;
            low.a = 1;
            var lowS = scale / 2f;
            Set(H_Low, low, new(Line, scale));
            Set(V_Low, low, new(Line, scale));
            Set(H_High, color, new(Half, lowS));
            Set(V_High, color, new(Half, lowS));
        }
        private static void Set(SpriteRenderer sr, Color color, float2 scale)
        {
            sr.color = color;
            sr.transform.localScale = new(scale.x, scale.y, 1);
        }
    }
}
