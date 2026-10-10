using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace Assets.PointShop.Scripts.Core
{
    [InternalBufferCapacity(0)]
    public struct PointCurrencyBuffer : IBufferElementData
    {
        [GhostField]
        public FixedString64Bytes Zone;

        [GhostField]
        public ObjectID BossID;

        [GhostField]
        public uint Explore;

        [GhostField]
        public uint Chest;

        [GhostField]
        public uint Enemy;

        [GhostField]
        public uint Boss;
    }
}
