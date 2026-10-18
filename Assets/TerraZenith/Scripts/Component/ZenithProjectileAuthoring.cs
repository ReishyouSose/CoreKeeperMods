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
            // 烘焙时放上，预测生成才能直接改参数。这组参数不进快照，两端各写各的。
            AddComponentData(new ZenithFlightCD());
            EnsureHasBuffer<ZenithTrail>();
        }
    }

}
