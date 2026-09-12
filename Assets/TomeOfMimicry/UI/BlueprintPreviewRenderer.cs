using Unity.Mathematics;
using UnityEngine;

namespace TomeOfMimicry
{
    public static class BlueprintPreviewRenderer
    {
        public static void Apply(PlacementHandler handler, int worldY, bool immediate)
        {
            if (handler == null)
                return;

            switch (BlueprintManager.State)
            {
                case GadgetState.Selecting:
                    ShowSelectionGrid(handler, worldY, immediate);
                    break;

                case GadgetState.HoldingBlueprint:
                    ShowBlueprintFootprint(handler, worldY, immediate);
                    break;

                default:
                    handler.placeableIcon.SetSize(1, 1);
                    break;
            }
        }

        private static void ShowSelectionGrid(PlacementHandler handler, int worldY, bool immediate)
        {
            var (min, max) = BlueprintManager.GetSelectionBounds();
            var size = max - min + new int2(1, 1);
            SetGrid(handler, min, worldY, size, immediate);
        }

        private static void ShowBlueprintFootprint(PlacementHandler handler, int worldY, bool immediate)
        {
            var blueprint = BlueprintManager.ActiveBlueprint;
            if (blueprint == null)
            {
                handler.placeableIcon.SetSize(1, 1);
                return;
            }

            var size = BlueprintManager.GetRotatedSize();
            var origin = BlueprintManager.CachedCursorTilePos + BlueprintManager.GetCursorOffset();
            SetGrid(handler, origin, worldY, size, immediate);
        }

        private static float _nextLog;

        private static void SetGrid(
            PlacementHandler handler,
            int2 origin,
            int worldY,
            int2 size,
            bool immediate)
        {
            var renderPosition = EntityMonoBehaviour.ToRenderFromWorld(
                new Vector3Int(origin.x, worldY, origin.y));

            handler.placeableIcon.SetPosition(renderPosition, true);
            handler.placeableIcon.transform.position = renderPosition;
            handler.placeableIcon.SetSize(size.x, size.y);

            bool isGood = BlueprintManager.State != GadgetState.HoldingBlueprint ||
                          !TileCostTracker.IsBlocked;
            handler.placeableIcon.SetState(isGood, 0, false, default);

            if (Time.realtimeSinceStartup > _nextLog)
            {
                _nextLog = Time.realtimeSinceStartup + 3f;
                var icon = handler.placeableIcon;
                GadgetLog.Trace($"[Tome of Mimicry] preview: state={BlueprintManager.State} origin={origin} " +
                          $"render={renderPosition} iconPos={icon.transform.position} " +
                          $"iconActive={icon.gameObject.activeInHierarchy} size={size}");
            }
        }
    }
}
