using Assets.PointShop.Scripts.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace Assets.PointShop.Scripts.Systems
{
    public struct PointShopRPC : IRpcCommand
    {
        public Entity Player;
        public ObjectData Item;
        public ObjectID Boss;
        public ObtainWay Way;
        public int Price;
        public bool Scale;
        public FixedString64Bytes Zone;
    }
}
