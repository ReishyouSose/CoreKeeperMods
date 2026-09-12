using HarmonyLib;
using PlayerEquipment;
using Unity.Collections;
using Unity.Mathematics;

namespace TomeOfMimicry
{
    [HarmonyPatch]
    public static class PlaceObjectSlot_Patch
    {
        [HarmonyPatch(typeof(EquipmentSlot), nameof(EquipmentSlot.AttackWithItem))]
        [HarmonyPrefix]
        public static bool BlockGadgetFallbackAttack()
            => !BlueprintManager.IsGadgetEquipped;

        [HarmonyPatch(typeof(PlaceObjectSlot), nameof(PlaceObjectSlot.OnEquip))]
        [HarmonyPostfix]
        public static void CapturePlacementHandler(PlaceObjectSlot __instance)
        {
            NativePlacementCursor.SetHandler(__instance.placementHandler);
        }

        [HarmonyPatch(typeof(PlaceObjectSlot), "PlaceItem")]
        [HarmonyPrefix]
        public static bool BlockGadgetPlaceItem(in EquipmentUpdateAspect equipmentUpdateAspect)
        {
            var objectId = equipmentUpdateAspect.equippedObjectCD.ValueRO.containedObject.objectData.objectID;
            return objectId == ObjectID.None || objectId != BlueprintGadgetItem.ObjectID;
        }

        [HarmonyPatch(typeof(PlaceObjectSlot), nameof(PlaceObjectSlot.UpdateEquipment))]
        [HarmonyPrefix]
        public static bool UpdateGadgetCursor(
            in EquipmentUpdateAspect equipmentUpdateAspect,
            in EquipmentUpdateSharedData equipmentUpdateSharedData,
            in LookupEquipmentUpdateData equipmentUpdateLookupData,
            ref bool __result)
        {
            var objectId = equipmentUpdateAspect.equippedObjectCD.ValueRO.containedObject.objectData.objectID;
            if (objectId == ObjectID.None || objectId != BlueprintGadgetItem.ObjectID)
                return true;

            if (equipmentUpdateSharedData.isServer)
            {
                __result = true;
                return false;
            }

            NativePlacementCursor.InvalidateCursor();
            var placements = new NativeList<PlacementHandler.EntityAndInfoFromPlacement>(Allocator.Temp);
            var checkedTiles = new NativeHashMap<int3, bool>(32, Allocator.Temp);

            try
            {
                bool foundMousePosition = NativePlacementCursor.FindPlaceablePositionFromMouseOrJoystick(
                    equipmentUpdateAspect.equippedObjectCD.ValueRO.equipmentPrefab,
                    1,
                    1,
                    checkedTiles,
                    ref placements,
                    in equipmentUpdateAspect,
                    in equipmentUpdateSharedData,
                    in equipmentUpdateLookupData);

                NativePlacementCursor.CachePlacementPosition(in equipmentUpdateAspect.placementCD.ValueRO);
                if (NativePlacementCursor.TryGetCursorTile(out var cursorTile))
                    BlueprintManager.UpdateCursor(cursorTile);
                NativePlacementCursor.ApplyBlueprintPreview(true);

                equipmentUpdateAspect.placementCD.ValueRW.canPlaceObject = false;
            }
            finally
            {
                checkedTiles.Dispose();
                placements.Dispose();
            }

            __result = true;
            return false;
        }
    }
}
