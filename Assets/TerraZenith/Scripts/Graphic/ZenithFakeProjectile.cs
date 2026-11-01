using UnityEngine;

namespace Assets.TerraZenith.Scripts.Graphic
{
    public class ZenithFakeProjectile : Projectile
    {
        public SpriteRenderer Collider;
        protected override void Awake()
        {
            base.Awake();
            Collider.gameObject.SetActive(false);
        }
    }
}
