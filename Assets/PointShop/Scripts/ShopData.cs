using PugMod;
using System.Collections.Generic;

namespace Assets.PointShop.Scripts
{
    public struct ShopData
    {
        public Zone Zone;
        public ObjectID Boss;
        public List<ShopItem> Items;
    }
    public struct ShopItem
    {
        public ObjectData Item;
        public ObjectID Currency;
        public int Price;
        public ShopItem(ObjectData objData, int price = 1, ObjectID currency = ObjectID.None)
        {
            Item = objData;
            Price = price;
            Currency = currency;

        }
        public ShopItem(ObjectID id, int amount)
        {
            Item = new()
            {
                objectID = id,
                amount = amount,
            };
            Price = 1;
            Currency = ObjectID.None;
        }
        public ShopItem(ObjectID id, int price = 1, ObjectID currency = ObjectID.None)
        {
            Item = new()
            {
                objectID = id,
                amount = 1,
            };
            Price = price;
            Currency = currency;
        }
        public ShopItem(string id, int price = 1, ObjectID currency = ObjectID.None)
        {
            Item = new()
            {
                //objectID = API.Authoring.GetObjectID(id),
                amount = 1,
            };
            Price = price;
            Currency = currency;
        }
        public static implicit operator ShopItem(ObjectID id) => new(id, 1);
        public static implicit operator ShopItem(ObjectData obj) => new(obj);
        public static implicit operator ShopItem((ObjectID id, int price) value) => new(value.id, value.price, ObjectID.None);
    }
}