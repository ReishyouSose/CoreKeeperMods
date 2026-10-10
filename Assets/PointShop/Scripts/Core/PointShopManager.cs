using CoreLib.Util.Extension;
using PugMod;
using PugTilemap;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using static Assets.PointShop.Scripts.Core.ZoneLookup;

namespace Assets.PointShop.Scripts.Core
{

    public static class PointShopManager
    {
        public static ShopZoneDataBlock[] ZoneSort { get; private set; }
        public static Dictionary<FixedString64Bytes, List<ShopOffer>> shops;
        private static Dictionary<ObjectID, DropLootAuthoring> loots;
        private static Dictionary<ObjectID, InventoryAuthoring> chests;
        private static Dictionary<ObjectID, ChangeVariationWhenContainingObjectAuthoring> changeChests;
        private static Dictionary<ObjectID, GainPointCD> gain;
        internal static Dictionary<ObjectID, Dictionary<FixedString64Bytes, float[]>> lootChance;
        internal static void EarlyInit()
        {
            loots = new();
            chests = new();
            changeChests = new();
            gain = new();
            API.Authoring.OnObjectTypeAdded += (entity, authoring, manager) =>
            {
                if (AnalyzeAuthoring(authoring, out var way, out var zone))
                {
                    GainPointCD _gain = new() { Way = way, Zone = zone };
                    gain.TryAdd(authoring.GetEntityObjectID(), _gain);
                    manager.AddComponentData(entity, _gain);
                    if (way == ObtainWay.Chest)
                        manager.AddComponent<CanBeDiscoveredCD>(entity);
                }
            };
        }
        internal static bool TryGetChances(ObjectID item, string zone, out float[] chances)
        {
            chances = null;
            return lootChance != null
                && lootChance.TryGetValue(item, out var byZone)
                && byZone.TryGetValue(zone, out chances);
        }
        internal static void Init()
        {
            SortZones();
            AnalyzeLoot();
            InitShopItem();
        }
        public static List<ShopOffer> GetShopItems(ShopZoneDataBlock zone)
        {
            if (zone != null && shops != null && shops.TryGetValue(zone.name, out var offers))
                return offers;
            return new();
        }
        private static void SortZones()
        {
            Dictionary<string, int> order = new();
            string[] zoneNames =
            {
                "None", "Dirt", "Meadow", "Clay", "LarvaHive", "Stone",
                "Moss", "Nature", "Mold", "Sea", "City", "Desert", "Lava",
                "Crystal", "Alien", "Oasis", "Passage", "Excavation", "Void"
            };
            foreach (var zoneName in zoneNames)
                order.Add(zoneName, order.Count);
            ZoneSort = new ShopZoneDataBlock[order.Count];
            if (!ScriptableData.TryGetDataBlocks<ShopZoneDataBlock>(out var zones))
                return;
            foreach (var zone in zones)
            {
                int index = order[zone.name];
                ZoneSort[index] = zone;
                zone.ZoneID = index;
            }
        }
        /// <summary>
        /// 想要注册时获取ObjectID必须在烘焙后，所以无法使用ECSManager.Before
        /// </summary>
        /// <param name="authoring"></param>
        /// <param name="way"></param>
        /// <param name="zone"></param>
        /// <returns></returns>
        private static bool AnalyzeAuthoring(GameObject authoring, out ObtainWay way, out FixedString64Bytes zone)
        {
            way = default;
            zone = default;
            var objID = authoring.GetEntityObjectID();
            if (authoring.TryGetComponent<CustomScenePrefabAuthoring>(out _))
                return false;
            if (authoring.TryGetComponent<CattleAuthoring>(out _))
            {
                way = ObtainWay.Animal;
                zone = Meadow;
                return true;
            }
            if (authoring.TryGetComponent<TileAuthoring>(out var tile) && tile.tileType == TileType.wall)
            {
                way = ObtainWay.Wall;
                return TryFromTileset(tile.tileset, out zone);
            }
            bool hasObjZone = TryFromObject(objID, out var idZone);
            if (hasObjZone)
                zone = idZone;
            if (authoring.TryGetComponent<InventoryAuthoring>(out var inv) && authoring.TryGetComponent<PlaceableObjectAuthoring>(out _))
            {
                if (authoring.TryGetComponent<ChangeVariationWhenContainingObjectAuthoring>(out var change)
                    && change.reinstantiateToNewObjectId != ObjectID.None)
                    changeChests.TryAdd(objID, change);
                else if (hasObjZone && inv.autoTransferEnabled)
                {
                    chests.TryAdd(objID, inv);
                    way = ObtainWay.Chest;
                    return true;
                }
                return false;
            }
            if (!authoring.TryGetComponent<DropLootAuthoring>(out var loot))
                return false;
            if (!hasObjZone)
            {
                if (loot.hasLootTable && TryFromLootTable(loot.lootTableID, out var lootZone))
                    zone = lootZone;
                else if (authoring.TryGetComponent<AreaLevelAuthoring>(out var area) && TryFromAreaLevel(area.areaLevel, out var areaZone))
                    zone = areaZone;
                else
                    return false;
            }
            if (authoring.TryGetComponent<MineableAuthoring>(out _) || authoring.TryGetComponent<DiggableAuthoring>(out _))
                way = ObtainWay.Explore;
            else if (authoring.TryGetComponent<BossAuthoring>(out _))
                way = ObtainWay.Boss;
            else if (authoring.TryGetComponent<EnemyAuthoring>(out _))
                way = ObtainWay.Enemy;
            else
                return false;
            if (!loots.TryAdd(objID, loot))
            {
                Debug.LogWarning($"[{objID}] has an another loot at vari {authoring.GetEntityVariation()}");
            }
            return true;
        }
        private static void AnalyzeLoot()
        {
            lootChance = new();
            static void Add(ObjectID id, FixedString64Bytes zone, ObtainWay way, float chance)
            {
                if (!lootChance.TryGetValue(id, out var value))
                    lootChance[id] = value = new();
                if (!value.TryGetValue(zone, out var chances))
                    value[zone] = chances = new float[4];
                chances[(int)way] += chance;
            }
            var lootTables = Manager.mod.LootTable;
            Dictionary<LootTableID, LootTable> map = lootTables.ToDictionary(x => x.id);
            var seenTables = new HashSet<LootTableID>();
            void AddByLoot(LootTableID lootID, GainPointCD gain, bool mustFromLootTable = false)
            {
                if (lootID != LootTableID.Empty && seenTables.Add(lootID))
                {
                    var lt = map[lootID];
                    var (zone, way) = gain;
                    if (TryFromLootTable(lootID, out var lootZone))
                        zone = lootZone;
                    else if (mustFromLootTable)
                        return;
                    foreach (var info in lt.lootInfos)
                    {
                        Add(info.objectID, zone, way, info.editorVisualDropChance / 100f);
                    }
                }
            }
            GainPointCD fromChest = new() { Way = ObtainWay.Chest };
            foreach (var (id, change) in changeChests)
                AddByLoot(change.addLootFromTableToNewObject, fromChest, true);
            foreach (var (id, chest) in chests)
                AddByLoot(chest.addLootFromTable, gain[id]);
            foreach (var (id, loot) in loots)
            {
                if (loot.hasLootTable)
                    AddByLoot(loot.lootTableID, gain[id]);
            }
            if (ScriptableData.TryGetDataBlocks<CustomSceneDataBlock>(out var scenes))
            {
                foreach (var scene in scenes)
                {
                    var objs = scene.authObjects;
                    int count = objs.Count;
                    var inv = scene.prefabInventoryOverrides;
                    var invCount = inv.Count;
                    bool end = false;
                    for (int i = 0; i < count; i++)
                    {
                        if (i >= invCount)
                        {
                            end = true;
                            break;
                        }
                        var ivr = inv[i];
                        if (ivr.hasLootTableOverride)
                            AddByLoot(ivr.lootTableOverride, fromChest, true);
                    }
                    if (end)
                        continue;
                }
            }
        }
        private static void InitShopItem()
        {
            shops = new();
            if (!ScriptableData.TryGetDataBlocks<ShopItemDataBlock>(out var dataBlocks))
                return;
            static void Add(ShopItemDataBlock item, FixedString64Bytes zone, ObtainWay way, int price, int amount)
            {
                if (!shops.TryGetValue(zone, out var items))
                    shops[zone] = items = new();
                items.Add(new() { Item = item, Price = price, Way = way, Amount = amount });
            }
            static ObtainWay CatalogWay(CurrencyType currency)
            {
                switch (currency)
                {
                    case CurrencyType.EnvironmentChest:
                        return ObtainWay.Chest;
                    case CurrencyType.BossChest:
                        return ObtainWay.Boss;
                    default:
                        return ObtainWay.Old;
                }
            }
            foreach (var item in dataBlocks)
            {
                var itemName = item.name;
                ObjectID id = API.Authoring.GetObjectID(itemName);
                if (id == ObjectID.None)
                {
                    if (!itemName.Contains('_'))
                        Debug.LogWarning("[" + itemName + "] has an invalid object ID");
                    continue;
                }
                item.ObjectID = id;
                bool hasAuthoredZone = item.Zone.TryGet(out var authoredZone);
                var dataZone = authoredZone.name;
                if (hasAuthoredZone && (dataZone == "None" || dataZone == "Meadow"))
                {
                    Add(item, dataZone, CatalogWay(item.Currency), item.Price, item.Amount);
                    continue;
                }
                if (!lootChance.TryGetValue(id, out var chanceByZone))
                {
                    if (!hasAuthoredZone)
                    {
                        Debug.LogWarning("[" + itemName + "] no loot and no zone dataBlock ref");
                        continue;
                    }
                    Add(item, dataZone, ObtainWay.Explore, item.Price, item.Amount);
                }
                else
                {
                    foreach (var (zone, chances) in chanceByZone)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            var chance = chances[i];
                            if (chance <= 0)
                                continue;
                            ObtainWay way = (ObtainWay)i;
                            int price;
                            int amount;
                            if (chance > 1f)
                            {
                                price = 1;
                                amount = (int)MathF.Round(chance, MidpointRounding.AwayFromZero);
                                if (amount < 1)
                                    amount = 1;
                            }
                            else
                            {
                                price = (int)MathF.Round(1f / chance);
                                if (price < 1)
                                    price = 1;
                                amount = item.Amount;
                            }
                            Add(item, zone, way, price, amount);
                        }
                    }
                }
            }
            foreach (var items in shops.Values)
            {
                items.Sort((a, b) =>
                {
                    int cmp = a.Way.CompareTo(b.Way);
                    if (cmp != 0)
                        return cmp;
                    var infoA = PugDatabase.GetObjectInfo(a.Item.ObjectID);
                    var infoB = PugDatabase.GetObjectInfo(b.Item.ObjectID);
                    cmp = infoA.rarity.CompareTo(infoB.rarity);
                    if (cmp != 0)
                        return cmp;
                    cmp = a.Price.CompareTo(b.Price);
                    if (cmp != 0)
                        return cmp;
                    cmp = infoA.objectType.CompareTo(infoB.objectType);
                    if (cmp != 0)
                        return cmp;
                    cmp = a.Amount.CompareTo(b.Amount);
                    if (cmp != 0)
                        return cmp;
                    return a.Item.ObjectID.CompareTo(b.Item.ObjectID);
                });
            }
        }
    }
}
