using System;

namespace Assets.PointShop.Scripts
{
    public class ShopItemDataBlock : ScriptableDataBlock
    {
        public DataBlockRef<ShopZoneDataBlock> Zone;
        public int Variation;
        public int Amount;
        public CurrencyType Currency;
        public int Price;
        public DataBlockRef<ShopItemCategoryDataBlock> Category;
        public DataBlockRef<ShopItemLevelDataBlock> Level;

        [NonSerialized]
        public ObjectID ObjectID;

        [NonSerialized]
        public ObjectID CurrencyID;
        public void OnValidate()
        {
            if (Amount < 1)
                Amount = 1;
            if (Price < 1)
                Price = 1;
        }
    }
}
