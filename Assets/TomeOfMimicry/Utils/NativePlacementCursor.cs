using System;
using HarmonyLib;
using PlayerEquipment;
using Pug.Properties;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TomeOfMimicry
{
    [HarmonyPatch]
    public static class NativePlacementCursor
    {
        private static int2 _cursorTile;
        private static int _cursorTileY;
        private static bool _hasCursor;
        private static PlacementHandler _handler;

        public static void Reset()
        {
            _cursorTile = default;
            _cursorTileY = 0;
            _hasCursor = false;
            _handler = null;
        }

        public static bool TryGetCursorTile(out int2 cursorTile)
        {
            cursorTile = _cursorTile;
            return _hasCursor;
        }

        public static void InvalidateCursor()
        {
            _hasCursor = false;
        }

        private static float _nextCursorLog;

        public static void CachePlacementPosition(in PlacementCD placementCD)
        {
            var best = placementCD.bestPositionToPlaceAt;
            _cursorTile = new int2(best.x, best.z);
            _cursorTileY = best.y;
            _hasCursor = true;

            bool free = TryGetFreeMouseTile(out var freeTile);
            if (free)
                _cursorTile = freeTile;

            if (UnityEngine.Time.realtimeSinceStartup > _nextCursorLog)
            {
                _nextCursorLog = UnityEngine.Time.realtimeSinceStartup + 3f;
                GadgetLog.Trace($"[Tome of Mimicry] cursor: free={free} tile={_cursorTile} clamped=({best.x},{best.z})");
            }
        }

        private static bool TryGetFreeMouseTile(out int2 tile)
        {
            tile = default;
            var mouse = Manager.ui != null ? Manager.ui.mouse : null;
            var player = Manager.main != null ? Manager.main.player : null;
            if (mouse == null || player == null) return false;

            var input = player.inputModule;
            if (input == null || !input.PrefersKeyboardAndMouse()) return false;

            var world = EntityMonoBehaviour.ToWorldFromRender(mouse.GetMouseGameViewPosition());
            tile = new int2(
                UnityEngine.Mathf.FloorToInt(world.x),
                UnityEngine.Mathf.FloorToInt(world.z));
            return true;
        }

        public static void SetHandler(PlacementHandler handler)
        {
            _handler = handler;
        }

        private static float _nextPreviewWarn;

        public static void ApplyBlueprintPreview(bool immediate)
        {
            if (_handler != null && _hasCursor)
            {
                BlueprintPreviewRenderer.Apply(_handler, _cursorTileY, immediate);
            }
            else if (UnityEngine.Time.realtimeSinceStartup > _nextPreviewWarn)
            {
                _nextPreviewWarn = UnityEngine.Time.realtimeSinceStartup + 3f;
                UnityEngine.Debug.LogWarning($"[Tome of Mimicry] preview NOT applied: handler={(_handler != null)} hasCursor={_hasCursor}");
            }
        }

        [HarmonyReversePatch]
        [HarmonyPatch(typeof(PlacementHandler), "FindPlaceablePositionFromMouseOrJoystick")]
        public static bool FindPlaceablePositionFromMouseOrJoystick(
            Entity placementPrefab,
            int width,
            int height,
            NativeHashMap<int3, bool> tilesChecked,
            ref NativeList<PlacementHandler.EntityAndInfoFromPlacement> diggableEntityAndInfos,
            in EquipmentUpdateAspect equipmentUpdateAspect,
            in EquipmentUpdateSharedData equipmentUpdateSharedData,
            in LookupEquipmentUpdateData equipmentUpdateLookupData)
        {
            throw new NotImplementedException("Harmony reverse patch stub");
        }

        [HarmonyPatch(typeof(PlacementHandler), nameof(PlacementHandler.UpdatePlaceIcon))]
        [HarmonyPostfix]
        private static void UpdateBlueprintPlaceIcon(
            PlacementHandler __instance,
            bool immediate,
            in PlacementCD placementCD,
            ObjectDataCD infoAboutObjectToPlace,
            Entity placementPrefab,
            ComponentLookup<DirectionCD> directionLookup,
            ComponentLookup<DirectionBasedOnVariationCD> directionBasedOnVariationLookup,
            ComponentLookup<ObjectPropertiesCD> objectPropertiesLookup,
            PugDatabase.DatabaseBankCD databaseBankCD)
        {
            if (infoAboutObjectToPlace.objectID == BlueprintGadgetItem.ObjectID)
            {
                CachePlacementPosition(in placementCD);
                BlueprintManager.UpdateCursor(_cursorTile);
            }

            if (!BlueprintManager.IsGadgetEquipped)
                return;

            BlueprintPreviewRenderer.Apply(__instance, _cursorTileY, true);
        }
    }
}
