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
        public void Set(Color color, float lifeFade, float lifeRatio)
        {
            float pulseRatio = math.clamp((lifeRatio - 0.125f) / 0.25f, 0f, 1f);
            float factor = math.sin(pulseRatio * math.PI);
            float fix = lifeFade * 0.5f * factor;
            Color core = Color.white * fix;
            Color shine = color * fix;
            factor = math.lerp(1f, 2f, factor);
            float half = fix * 0.625f;
            transform.localScale = new(factor, factor, 1);
            Set(H_High, core, new(Half, half));
            Set(V_High, core, new(Half, half));
            Set(H_Low, shine, new(Line, fix));
            Set(V_Low, shine, new(Line, fix));
        }
        private static void Set(SpriteRenderer sr, Color color, float2 scale)
        {
            sr.color = color;
            sr.transform.localScale = new(scale.x, scale.y, 1);
        }
    }
}
