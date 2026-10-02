using System;

namespace Assets.PointShop.Scripts.Core
{
    public class ShopItemDataBlock : ScriptableDataBlock
    {
        public DataBlockRef<ShopZoneDataBlock> Zone;
        public int Variation;
        public int Amount;
        public CurrencyType Currency;
        public int Price;
        public ObjectID OverrideCurrency;
        public DataBlockRef<ShopItemCategoryDataBlock> Category;
        public DataBlockRef<ShopItemLevelDataBlock> Level;
        public int ExplorePrice;
        public int ChestPrice;
        public int EnemyPrice;
        public int BossPrice;

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
