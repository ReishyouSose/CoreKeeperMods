using System.Collections.Generic;
using UnityEngine;

public static class AoaNoneObjectInfoRepair
{
    private static readonly List<ObjectDataCD> StaleKeys = new List<ObjectDataCD>();
    private static int _lastCount = -1;
    public static void Tick()
    {
        Dictionary<ObjectDataCD, ObjectInfo> objects = PugDatabase.objectsByType;
        if (objects == null || objects.Count == _lastCount)
            return;
        Repair(objects);
        _lastCount = objects.Count;
    }
    private static void Repair(Dictionary<ObjectDataCD, ObjectInfo> objects)
    {
        StaleKeys.Clear();
        foreach (var pair in objects)
            if (pair.Key.objectID == ObjectID.None && IsModObject(pair.Value))
                StaleKeys.Add(pair.Key);
        foreach (var key in StaleKeys)
        {
            ObjectInfo vanilla = FindVanillaNoneInfo(key.variation);
            if (vanilla != null)
                objects[key] = vanilla;
            else
                objects.Remove(key);
        }
        StaleKeys.Clear();
    }
    private static bool IsModObject(ObjectInfo info)
    {
        GameObject authoring = info?.prefabInfo?.authoring;
        return authoring != null && authoring.TryGetComponent(out ObjectAuthoring _);
    }
    private static ObjectInfo FindVanillaNoneInfo(int variation)
    {
        var blocks = ScriptableData.GetDataBlocks<EntityAuthoringDataBlock>();
        if (blocks == null)
            return null;
        foreach (var block in blocks)
        {
            if (block == null || block.prefab == null)
                continue;
            var data = block.prefab.GetComponent<IEntityMonoBehaviourData>();
            if (data == null || data is ObjectAuthoring)
                continue;
            ObjectInfo info = data.ObjectInfo;
            if (info != null && !info.isCustomScenePrefab && info.objectID == ObjectID.None && info.variation == variation)
                return info;
        }
        return null;
    }
}