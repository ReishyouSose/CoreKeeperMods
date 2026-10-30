using Pug.Conversion;
using UnityEngine;

namespace Assets.TerraZenith.Scripts.Component
{
    public class ZenithProjectileAuthoring : MonoBehaviour
    {
    }
    public class ZenithProjectileConverter : SingleAuthoringComponentConverter<ZenithProjectileAuthoring>
    {
        protected override void Convert(ZenithProjectileAuthoring authoring)
        {
            AddComponentData(new ZenithOrbitCD());
            EnsureHasBuffer<ZenithTrail>();
            EnsureHasBuffer<ZenithFakeProjBuffer>();
        }
    }

}
