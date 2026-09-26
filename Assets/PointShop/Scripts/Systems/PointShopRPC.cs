using Unity.Entities;
using Unity.NetCode;

namespace Assets.PointShop.Scripts.Systems
{
    public struct PointShopRPC : IRpcCommand
    {
        public Entity Player;
        public ObjectData Item;
        public ObjectID Boss;
        public ObjectID Currency;
        public int Price;
        public bool Scale;
    }
}
