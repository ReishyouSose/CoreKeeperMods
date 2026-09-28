using PugMod;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Entities;
using UnityEngine;

public class LootsLookup : IMod
{
    private static Dictionary<ObjectID, DropLootAuthoring> loots;
    private static Dictionary<ObjectID, MerchantAuthoring> shops;
    private static Dictionary<ObjectID, CraftingAuthoring> crafts;
    private static Dictionary<ObjectID, RecipeAuthoring> recipes;
    private static Dictionary<ObjectID, LootTableID> chests;
    private static Dictionary<ObjectID, ChangeVariationWhenContainingObjectAuthoring> lockedChests;
    private static CustomSceneDataBlock[] sceneData;

    public void EarlyInit()
    {
        var authoring = API.Authoring;
        loots = new();
        shops = new();
        crafts = new();
        recipes = new();
        chests = new();
        lockedChests = new();
        authoring.OnObjectTypeAdded += CheckData;
    }

    public void Init()
    {
        if (ScriptableData.TryGetDataBlocks<CustomSceneDataBlock>(out var dataBlocks))
        {
            sceneData = dataBlocks.ToArray();
        }
        else
        {
            Debug.LogError("未找到 CustomSceneDataBlock 数据块");
        }
    }

    private void CheckData(Entity entity, GameObject authoringData, EntityManager entityManager)
    {
        ObjectID id = GetEntityObjectID(authoringData);
        if (authoringData.TryGetComponent<DropLootAuthoring>(out var dropLoot))
            loots[id] = dropLoot;
        if (authoringData.TryGetComponent<MerchantAuthoring>(out var merchant))
            shops[id] = merchant;
        if (authoringData.TryGetComponent<CraftingAuthoring>(out var crafting))
            crafts[id] = crafting;
        if (authoringData.TryGetComponent<RecipeAuthoring>(out var recipe))
            recipes[id] = recipe;
        if (authoringData.TryGetComponent<InventoryAuthoring>(out var inventory)
            && inventory.addLootFromTable != LootTableID.Empty)
            chests[id] = inventory.addLootFromTable;
        if (authoringData.TryGetComponent<ChangeVariationWhenContainingObjectAuthoring>(out var chest))
            lockedChests[id] = chest;
    }

    public void ModObjectLoaded(Object obj)
    {
    }

    public void Shutdown()
    {
    }

    public void Update()
    {
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.L))
        {
            var obj = Manager.ui.currentSelectedUIElement.GetContainedObject();
            GetItemObtainSources(obj);
        }
    }

    public static ObjectID GetEntityObjectID(GameObject gameObject)
    {
        var entityMonoBehaviorData = gameObject.GetComponent<EntityMonoBehaviourData>();
        var objectAuthoring = gameObject.GetComponent<ObjectAuthoring>();

        if (entityMonoBehaviorData != null)
        {
            return entityMonoBehaviorData.objectInfo.objectID;
        }
        if (objectAuthoring != null)
        {
            return API.Authoring.GetObjectID(objectAuthoring.objectName);
        }

        return 0;
    }
    public static void NewText(string msg)
    {
        Manager.ui.chatWindow.AddInfoText(new string[1] { msg }, ChatWindow.MessageTextType.Sent);
    }
    public static void GetItemObtainSources(ContainedObjectsBuffer obj)
    {
        StringBuilder result = new();
        bool foundAny = false;
        var targetId = obj.objectID;
        // 获取物品名称
        string itemName = I2.Loc.LocalizationManager.GetTranslation("Items/" + targetId);
        if (string.IsNullOrEmpty(itemName))
            itemName = targetId.ToString();

        result.AppendLine($"物品: {itemName}  (ID:{targetId} {(int)targetId} vari:{obj.variation})");
        result.AppendLine("获取方式:");
        result.AppendLine("=".PadRight(40, '='));

        // 1. 查询掉落表（最通用的方式）
        var lootTables = Manager.mod.LootTable;

        // 检查所有掉落表
        foreach (var lootTable in lootTables)
        {
            bool foundInThisTable = false;

            // 检查保底掉落
            foreach (var loot in lootTable.guaranteedLootInfos)
            {
                if (loot.objectID == targetId)
                {
                    result.AppendLine($"[掉落表] {lootTable.id}");
                    result.AppendLine($"  类型: 保底掉落");
                    result.AppendLine($"  几率: {(loot.editorVisualDropChance / 100):P2}");
                    if (loot.onlyDropsInBiome != Biome.None)
                    {
                        result.AppendLine($" 仅限 {loot.onlyDropsInBiome} 环境");
                    }
                    result.AppendLine();
                    foundAny = true;
                    foundInThisTable = true;
                    break;
                }
            }

            // 检查普通掉落
            if (!foundInThisTable)
            {
                foreach (var loot in lootTable.lootInfos)
                {
                    if (loot.objectID == targetId)
                    {
                        result.AppendLine($"[掉落表] {lootTable.id}");
                        result.AppendLine($"  类型: 普通掉落");
                        result.AppendLine($"  几率: {(loot.editorVisualDropChance / 100):P2}");
                        if (loot.onlyDropsInBiome != Biome.None)
                        {
                            result.AppendLine($" 仅限 {loot.onlyDropsInBiome} 环境");
                        }
                        result.AppendLine();
                        foundAny = true;
                        break;
                    }
                }
            }
        }

        // 2. 查询自定义掉落组件
        if (loots != null)
        {
            foreach (var (sourceId, lootInfo) in loots)
            {

                // 自定义掉落
                if (lootInfo.hasCustomLoot)
                {
                    foreach (var loot in lootInfo.customLoot.Values)
                    {
                        if (loot.lootDropID == targetId)
                        {
                            result.AppendLine($"[掉落] {GetObjectName(sourceId)}");
                            result.AppendLine($"  类型: 自定义掉落");
                            result.AppendLine($"  几率: {lootInfo.customLoot.chance:P2}");
                            result.AppendLine();
                            foundAny = true;
                            break;
                        }
                    }
                }

                // 受伤害时掉落
                if (lootInfo.hasLootDropsOnTakingDamage)
                {
                    var loot = lootInfo.lootDropsWhenDamaged.dropsLoot;
                    if (loot == targetId)
                    {
                        result.AppendLine($"[掉落] {GetObjectName(sourceId)}");
                        result.AppendLine($"  类型: 受伤害时掉落");
                        result.AppendLine($"  几率: 100%");
                        result.AppendLine();
                        foundAny = true;
                    }
                }

                // 使用时掉落
                if (lootInfo.hasLootDropsOnUse)
                {
                    foreach (var loot in lootInfo.onUseLootDrops.lootDrops)
                    {
                        if (loot.lootDropID == targetId)
                        {
                            result.AppendLine($"[掉落] {GetObjectName(sourceId)}");
                            result.AppendLine($"  类型: 使用时掉落");
                            result.AppendLine($"  数量: {loot.amount}");
                            result.AppendLine($"  几率: {loot.chance:P2}");
                            result.AppendLine();
                            foundAny = true;
                            break;
                        }
                    }
                }

                // 季节性掉落
                if (lootInfo.hasSeasonalLoot)
                {
                    foreach (var season in lootInfo.seasonalLootDrops.lootDrops)
                    {
                        foreach (var loot in season.lootDrops)
                        {
                            if (loot.lootDropID == targetId)
                            {
                                result.AppendLine($"[掉落] {GetObjectName(sourceId)}");
                                result.AppendLine($"  类型: 季节性掉落");
                                result.AppendLine($"  季节: {season.season}");
                                result.AppendLine($"  几率: {loot.chance:P2}");
                                result.AppendLine();
                                foundAny = true;
                                break;
                            }
                        }
                    }
                }
            }
        }

        // 3. 查询商店
        foreach (var (npcId, shopInfo) in shops)
        {
            foreach (var item in shopInfo.items)
            {
                if (item.objectID == targetId)
                {
                    result.AppendLine($"[商店] {GetObjectName(npcId)}");
                    result.AppendLine($"  类型: 商店出售");
                    if (!string.IsNullOrEmpty(item.requirementToBeAvailable.ToString()))
                        result.AppendLine($"  要求: {item.requirementToBeAvailable}");
                    result.AppendLine();
                    foundAny = true;
                    break;
                }
            }
        }

        // 4. 查询制作配方
        foreach (var (stationId, craftInfo) in crafts)
        {
            foreach (var recipe in craftInfo.canCraftObjects)
            {
                if (recipe.objectID == targetId)
                {
                    result.AppendLine($"[制作] {GetObjectName(stationId)}");
                    result.AppendLine($"  单次制作数量: {recipe.amount}");
                    result.AppendLine();
                    foundAny = true;
                    break;
                }
            }
        }

        //手持合成
        foreach (var (itemID, recipeInfo) in recipes)
        {
            var recipe = recipeInfo.objectToCraft;
            if (recipe.objectID == targetId)
            {
                result.AppendLine($"[合成] {GetObjectName(itemID)}");
                result.AppendLine($"  单次制作数量: {recipe.amount}");
                result.AppendLine();
                foundAny = true;
                break;
            }
        }

        //特殊场景
        foreach (var scene in sceneData)
        {
            foreach (var inv in scene.prefabInventoryOverrides)
            {
                foreach (var item in inv.itemsOverride)
                {
                    if (item.item.objectID == targetId)
                    {
                        result.AppendLine($"[场景] {scene.sceneName}");
                        result.AppendLine();
                        foundAny = true;
                    }
                }
            }
        }

        if (!foundAny)
        {
            result.AppendLine("无渠道");
            Debug.Log(result.ToString());
            NewText("未找到该物品的任何获取方式");
            return;
        }
        NewText("查询成功，已复制到剪贴板");
        string r = result.ToString();
        Debug.Log(r);
        GUIUtility.systemCopyBuffer = r;
    }

    // 辅助方法：获取物品名称
    private static string GetObjectName(ObjectID id)
    {
        string name = I2.Loc.LocalizationManager.GetTranslation("Items/" + id);
        if (!string.IsNullOrEmpty(name))
            return name + $"(id: {id}[{(int)id})]";
        return id.ToString();
    }
}
