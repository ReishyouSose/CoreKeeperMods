using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;

namespace Assets.BuildingBlueprint.Scripts.Core
{
    [HarmonyPatch(typeof(UIMouse))]
    public static class MaterialVariationPatch
    {
        [HarmonyPatch("UpdateHoverText"), HarmonyTranspiler]
        private static List<CodeInstruction> MaterailVariationOverride(IEnumerable<CodeInstruction> codes)
        {
            var list = codes.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                var code = list[i];
                if (code.opcode != OpCodes.Call)
                    continue;
                if (!code.operand.ToString().Contains("HasObject"))
                    continue;
                i -= 3;
                list.RemoveRange(i, 4);
                list.Insert(i, new(OpCodes.Call, AccessTools.Method(typeof(MaterialVariationPatch), nameof(HasObject))));
                for (int j = i; j < list.Count; j++)
                {
                    code = list[j];
                    if (code.opcode != OpCodes.Ldstr)
                        continue;
                    if (!code.operand.ToString().Contains("Items/"))
                        continue;
                    j += 3;
                    list.InsertRange(j, new List<CodeInstruction>()
                    {
                        new (OpCodes.Ldloc_S, 34),
                        new (OpCodes.Ldloc_S, 111),
                        new (OpCodes.Call, AccessTools.Method(typeof(MaterialVariationPatch), nameof(GetNameKey))),
                    });
                    for (int k = j; k < list.Count; k++)
                    {
                        code = list[k];
                        if (code.opcode != OpCodes.Call)
                            continue;
                        if (!code.operand.ToString().Contains("GetObjectInfo"))
                            continue;
                        k -= 3;
                        list.RemoveRange(k, 4);
                        list.Insert(k, new(OpCodes.Call, AccessTools.Method(typeof(MaterialVariationPatch), nameof(GetSmallIcon))));
                        break;
                    }
                    break;
                }
                break;
            }
            return list;
        }
        private static bool HasObject(List<PugDatabase.MaterialInfo> list, int index)
        {
            int variation = Manager.ui.currentSelectedUIElement is IOverrideHoverMaterialVariation vari ? vari.GetOverrideVariation(index) : 0;
            return PugDatabase.HasObject(list[index].objectID, variation);
        }
        private static string GetNameKey(string origin, List<PugDatabase.MaterialInfo> list, int index)
        {
            if (Manager.ui.currentSelectedUIElement is IOverrideHoverMaterialVariation vari)
            {
                return PlayerController.GetObjectName(new()
                {
                    objectData = new()
                    {
                        objectID = list[index].objectID,
                        variation = vari.GetOverrideVariation(index)
                    }
                }, false).text;
            }
            return origin;
        }
        private static ObjectInfo GetSmallIcon(List<PugDatabase.MaterialInfo> list, int index)
        {
            int variation = Manager.ui.currentSelectedUIElement is IOverrideHoverMaterialVariation vari ? vari.GetOverrideVariation(index) : 0;
            return PugDatabase.GetObjectInfo(list[index].objectID, variation);
        }
    }
}
