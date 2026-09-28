using PugMod;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assets.PointShop.Scripts.Core
{
    public class ShopManager : MonoBehaviour
    {
        public ShopZoneDataBlock[] ZoneSort { get; private set; }
        private Dictionary<int, List<ShopItemDataBlock>> shops;
        public void Awake()
        {
            shops = new();
            Dictionary<string, int> order = new();
            string[] zoneNames =
            {
                "None", "Dirt", "Meadow", "Clay", "LarvaHive", "Stone", "Moss",
                "AbioticFactor", "Nature", "Mold", "Sea", "City", "Desert",
                "Lava", "Oasis", "Crystal", "Alien", "Passage", "Excavation", "Void"
            };
            foreach (var zoneName in zoneNames)
            {
                order.Add(zoneName, order.Count);
            }
            ZoneSort = new ShopZoneDataBlock[order.Count];
            if (ScriptableData.TryGetDataBlocks<ShopZoneDataBlock>(out var zones))
            {
                foreach (var zone in zones)
                {
                    int index = order[zone.name];
                    ZoneSort[index] = zone;
                    zone.ZoneID = index;
                    shops[index] = new();
                }
            }
            if (ScriptableData.TryGetDataBlocks<ShopItemDataBlock>(out var items))
            {
                foreach (var item in items)
                {
                    if (!item.Zone.TryGet(out var zone))
                    {
                        Debug.LogWarning("[" + item.name + "] has no zone assigned.");
                        continue;
                    }
                    /*string header = ScriptableDataEditorUtility.GetDataBlockHeader(item);
                    if (header != zone.name)
                    {
                        Debug.LogWarning("ShopItemDataBlock [" + item.name + "] has a zone mismatch. Expected: " + header + ", Actual: " + zone.name);
                    }*/
                    var itemName = item.name;
                    var zoneName = zone.name;
                    var zoneID = zone.ZoneID;
                    ObjectID id = API.Authoring.GetObjectID(itemName);
                    if (id == ObjectID.None)
                    {
                        if (!itemName.Contains('_'))
                            Debug.LogWarning("[" + itemName + "] has an invalid object ID");
                        continue;
                    }
                    ObjectID currency = item.OverrideCurrency;
                    if (currency == ObjectID.None)
                    {
                        switch (item.Currency)
                        {
                            case CurrencyType.EnvironmentChest:
                                if (zone.EnvironmentChest == ObjectID.None)
                                {
                                    Debug.LogWarning("[" + itemName + "] has an invalid EnvironmentChest for zone [" + zoneName + "]");
                                    continue;
                                }
                                currency = zone.EnvironmentChest;
                                break;
                            case CurrencyType.BossChest:
                                if (zone.BossChest == ObjectID.None)
                                {
                                    Debug.LogWarning("[" + itemName + "] has an invalid BossChest for zone [" + zoneName + "]");
                                    continue;
                                }
                                currency = zone.BossChest;
                                break;
                            case CurrencyType.OverridePointCoin:
                            default:
                                currency = PointShop.Coin;
                                break;
                        }
                    }
                    item.ObjectID = id;
                    item.CurrencyID = currency;
                    shops[zoneID].Add(item);
                }
            }
            foreach (var zone in ZoneSort)
            {
                shops[zone.ZoneID] = shops[zone.ZoneID].OrderBy(x => x.Currency)
                    .ThenBy(x => x.OverrideCurrency)
                    .ThenBy(x => PugDatabase.GetObjectInfo(x.ObjectID).rarity)
                    .ThenBy(x => x.Price)
                    .ThenBy(x => PugDatabase.GetObjectInfo(x.ObjectID).objectType)
                    .ThenBy(x => x.Amount)
                    .ThenBy(x => x.ObjectID)
                    .ToList();
            }
        }
        public List<ShopItemDataBlock> GetShopItems(ShopZoneDataBlock zone) => shops[zone.ZoneID];
    }
}
