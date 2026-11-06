using Pug.Sprite;
using Pug.UnityExtensions;
using Unity.Mathematics;
using UnityEngine;

namespace Assets.TerraZenith.Scripts.Graphic
{
    public class ZenithDust : MonoBehaviour
    {
        public SpriteObject SR;
        private float3 vel;
        private float3 pos;
        private float scale;
        private void Update()
        {
            pos += vel * Time.deltaTime;
            vel *= 0.95f;
            scale *= 0.98f;
            transform.position = new(pos.x, transform.parent.position.y, pos.z + 0.5f);
            transform.localScale = new(scale, scale, 1f);
            SR.emissiveColor.a = scale;
            if (scale <= 0.01f)
            {
                gameObject.SetActive(false);
            }
        }
        public void Init(float3 pos, float2 vel, Color color)
        {
            this.pos = pos;
            this.vel = vel.X0Y();
            scale = 1;
            transform.position = this.pos;
            var c = color;
            c.a = 1f;
            SR.emissiveColor = c;
        }
    }
}
