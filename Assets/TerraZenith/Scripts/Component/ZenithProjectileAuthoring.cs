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
            // 空的幽灵组件必须在烘焙时放上，运行时再加进不去网络快照。参数留到飞行系统第一次看见时由服务器写。
            AddComponentData(new ZenithFlightCD());
            EnsureHasBuffer<ZenithTrail>();
        }
    }

}
