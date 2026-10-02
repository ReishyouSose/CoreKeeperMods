using PugMod;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Assets.PointShop.Scripts.Core
{
    public enum ObtainWay
    {
        Explore,
        Chest,
        Enemy,
        Boss,
    }
    public class LootChanceCalculator
    {

        // 掉落来源 -> 掉落组件
        private static Dictionary<ObjectID, DropLootAuthoring> loots;
        // 箱子ID -> 掉落表ID
        private static Dictionary<ObjectID, LootTableID> chests;
        // 锁箱子ID -> 开锁组件
        private static Dictionary<ObjectID, ChangeVariationWhenContainingObjectAuthoring> lockedChests;
        // Boss 集合
        private static HashSet<ObjectID> bosses;

        // 物品 -> float[4]，对应 ObtainWay
        internal static Dictionary<ObjectID, float[]> lootChance;

        // LootTableID -> LootTable 的运行时缓存
        private static Dictionary<LootTableID, LootTable> lootTableCache;

        public static void EarlyInit()
        {
            var authoring = API.Authoring;
            loots = new();
            chests = new();
            lockedChests = new();
            bosses = new();
            lootChance = new();
            lootTableCache = new();
            authoring.OnObjectTypeAdded += CheckData;
        }

        public static void Init()
        {
            BuildLootTableCache();
            CalculateAllLootChance();
        }

        private static void CheckData(Entity entity, GameObject authoringData, EntityManager entityManager)
        {
            ObjectID id = GetEntityObjectID(authoringData);

            if (authoringData.TryGetComponent<BossAuthoring>(out var boss))
                bosses.Add(id);

            if (authoringData.TryGetComponent<DropLootAuthoring>(out var dropLoot))
                loots[id] = dropLoot;

            if (authoringData.TryGetComponent<InventoryAuthoring>(out var inventory)
                && inventory.addLootFromTable != LootTableID.Empty)
                chests[id] = inventory.addLootFromTable;

            if (authoringData.TryGetComponent<ChangeVariationWhenContainingObjectAuthoring>(out var lockedChest))
                lockedChests[id] = lockedChest;
        }

        public static ObjectID GetEntityObjectID(GameObject gameObject)
        {
            var entityMonoBehaviorData = gameObject.GetComponent<EntityMonoBehaviourData>();
            var objectAuthoring = gameObject.GetComponent<ObjectAuthoring>();

            if (entityMonoBehaviorData != null)
                return entityMonoBehaviorData.objectInfo.objectID;
            if (objectAuthoring != null)
                return API.Authoring.GetObjectID(objectAuthoring.objectName);
            return 0;
        }

        // ---------- 掉落表缓存 ----------

        private static void BuildLootTableCache()
        {
            lootTableCache.Clear();
            var lootTables = Manager.mod.LootTable;
            if (lootTables == null)
                return;
            foreach (var table in lootTables)
            {
                if (table == null)
                    continue;
                lootTableCache[table.id] = table;
            }
        }

        private static LootTable GetLootTable(LootTableID id)
        {
            if (lootTableCache != null && lootTableCache.TryGetValue(id, out var table))
                return table;
            return null;
        }

        // ---------- 分类 ----------

        private static ObtainWay GetObtainWay(ObjectID sourceId)
        {
            if (bosses != null && bosses.Contains(sourceId))
                return ObtainWay.Boss;

            var info = PugDatabase.GetObjectInfo(sourceId);
            if (info != null && info.objectType == ObjectType.Creature)
                return ObtainWay.Enemy;

            return ObtainWay.Explore;
        }

        // ---------- 累加辅助 ----------

        private static void AddChance(ObjectID item, ObtainWay way, float chance)
        {
            if (chance <= 0f)
                return;
            if (!lootChance.TryGetValue(item, out var arr))
            {
                arr = new float[4];
                lootChance[item] = arr;
            }
            arr[(int)way] += chance;
        }

        // 把一张掉落表的内容累加到某个途径上（只遍历 lootInfos，同表同ID多条目累加）
        private static void AddLootTable(LootTableID tableId, ObtainWay way)
        {
            var table = GetLootTable(tableId);
            if (table == null)
                return;

            foreach (var loot in table.lootInfos)
            {
                AddChance(loot.objectID, way, loot.editorVisualDropChance / 100f);
            }
        }

        // ---------- 主计算 ----------

        private static void CalculateAllLootChance()
        {
            lootChance.Clear();
            BuildLootTableCache();

            // 1. 扫描到的掉落来源（Explore / Enemy / Boss）
            if (loots != null)
            {
                foreach (var (sourceId, dropLoot) in loots)
                {
                    if (dropLoot == null)
                        continue;
                    var way = GetObtainWay(sourceId);

                    // A. 掉落表
                    if (dropLoot.hasLootTable)
                        AddLootTable(dropLoot.lootTableID, way);

                    // B. 自定义掉落
                    if (dropLoot.hasCustomLoot && dropLoot.customLoot != null)
                    {
                        foreach (var loot in dropLoot.customLoot.Values)
                        {
                            AddChance(loot.lootDropID, way, dropLoot.customLoot.chance);
                        }
                    }

                    // C. 受伤害时掉落
                    if (dropLoot.hasLootDropsOnTakingDamage)
                    {
                        AddChance(dropLoot.lootDropsWhenDamaged.dropsLoot, way, 1f);
                    }

                    // D. 使用时掉落
                    if (dropLoot.hasLootDropsOnUse)
                    {
                        foreach (var loot in dropLoot.onUseLootDrops.lootDrops)
                        {
                            AddChance(loot.lootDropID, way, loot.chance);
                        }
                    }

                    // E. 季节性掉落
                    if (dropLoot.hasSeasonalLoot && dropLoot.seasonalLootDrops != null)
                    {
                        foreach (var season in dropLoot.seasonalLootDrops.lootDrops)
                        {
                            if (season.lootDrops == null)
                                continue;
                            foreach (var loot in season.lootDrops)
                            {
                                AddChance(loot.lootDropID, way, loot.chance);
                            }
                        }
                    }
                }
            }

            // 2. 箱子
            if (chests != null)
            {
                foreach (var (chestId, tableId) in chests)
                {
                    AddLootTable(tableId, ObtainWay.Chest);
                }
            }

            // 3. 锁箱子（选法1：直接用 addLootFromTableToNewObject）
            if (lockedChests != null)
            {
                foreach (var (lockedId, change) in lockedChests)
                {
                    if (change == null)
                        continue;
                    if (change.addLootFromTableToNewObject == LootTableID.Empty)
                        continue;
                    AddLootTable(change.addLootFromTableToNewObject, ObtainWay.Chest);
                }
            }
        }
    }
}