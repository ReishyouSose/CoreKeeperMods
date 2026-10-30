using UnityEngine;

namespace Assets.TerraZenith.Scripts.Graphic
{
    public class ZenithFakeProjectile : Projectile
    {
        protected override void Awake()
        {
            base.Awake();
            GetComponentInChildren<SpriteRenderer>().enabled = false;
        }
    }
}
