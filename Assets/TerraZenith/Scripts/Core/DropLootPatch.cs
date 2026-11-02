using HarmonyLib;
using PugMod;
using UnityEngine;

namespace Assets.TerraZenith.Scripts.Core
{
    [HarmonyPatch(typeof(LootTableConverter))]
    public static class DropLootPatch
    {
        [HarmonyPatch(nameof(LootTableConverter.Convert))]
        private static void Prefix(LootTableConverter __instance)
        {
            Debug.Log(API.Authoring.GetObjectID(ZenithBalance.RecipeName));
        }
    }
}
