using Pug.Sprite;
using Pug.UnityExtensions;
using Unity.Mathematics;
using UnityEngine;

namespace Assets.TerraZenith.Scripts.Graphic
{
    public class ZenithDust : MonoBehaviour
    {
        public SpriteObject SR;
        public float3 Vel;
        public float3 Pos;
        public float Scale;
        private void Update()
        {
            Pos += Vel * Time.deltaTime;
            Vel *= 0.95f;
            Scale *= 0.98f;
            transform.position = new(Pos.x, transform.parent.position.y, Pos.z + 0.5f);
            transform.localScale = new(Scale, Scale, 1f);
            //var c = SR.color;
            SR.color.a = Scale;
            if (Scale <= 0.01f)
            {
                gameObject.SetActive(false);
            }
        }
        public void Init(float3 pos, float2 vel)
        {
            Pos = pos;
            Vel = vel.X0Y();
            Scale = 1;
            transform.position = Pos;
            //transform.localScale = Vector3.one * Scale;
            var c = SR.color;
            SR.emissiveColor = new Color(c.r, c.g, c.b, 1f);
        }
    }
}
