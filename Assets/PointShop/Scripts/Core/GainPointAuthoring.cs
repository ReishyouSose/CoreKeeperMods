using Pug.Conversion;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Assets.PointShop.Scripts.Core
{
    public struct GainPointCD : IComponentData
    {
        public FixedString64Bytes Zone;
        public ObtainWay Way;
        public readonly void Deconstruct(out FixedString64Bytes zone, out ObtainWay way)
        {
            zone = Zone;
            way = Way;
        }
    }
    public class GainPointAuthoring : MonoBehaviour
    {
        public string Zone;
        public ObtainWay Way;
    }
    public class GainPointConverter : SingleAuthoringComponentConverter<GainPointAuthoring>
    {
        protected override void Convert(GainPointAuthoring authoring)
        {
            AddComponentData(new GainPointCD { Zone = authoring.Zone, Way = authoring.Way });
        }
    }
}
