using Assets.CoreEnhance.Scripts.Cores;
using HarmonyLib;

namespace Assets.CoreEnhance.Scripts.Patchs
{
    [HarmonyPatch(typeof(InventoryUI))]
    public static class InventoryUIPatch
    {
        [HarmonyPatch(nameof(InventoryUI.MAX_ROWS), MethodType.Getter), HarmonyPostfix]
        private static void ExtraInventoryPatch(InventoryUI __instance, ref int __result)
        {
            if (__instance.name == "PlayerInventoryUI")
            {
                int y = EnhanceConfig.TryGetValue<int>(EnhanceCategory.ExtraInventory, out var value) ? value.Value : 0;
                __result += y;
            }
        }
    }
}
