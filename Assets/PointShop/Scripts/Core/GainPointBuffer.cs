using Unity.Collections;
using Unity.Entities;

namespace Assets.PointShop.Scripts.Core
{
    public struct GainPointBuffer : IBufferElementData
    {
        public FixedString64Bytes Zone;
        public Entity Player;
        public ObtainWay Way;
    }
}
