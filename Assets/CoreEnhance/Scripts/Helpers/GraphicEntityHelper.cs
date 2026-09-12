using Interaction;
using Pug.Sprite;
using Pug.UnityExtensions;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Assets.CoreEnhance.Scripts.Helpers
{
    public static class GraphicEntityHelper
    {
        public static void UpdateOutline(InteractableObjectReferenceCD interactRef, Color color)
        {
            var interact = interactRef.Value.Value;
            bool flag = color == Color.clear;
            if (interact != null)
            {
                if (interact.optionalOutlineController != null)
                {
                    interact.optionalOutlineController.showOutline = flag;
                    if (flag)
                    {
                        interact.optionalOutlineController.SetColor(color);
                    }
                    else
                    {
                        interact.optionalOutlineController.ResetColor();
                    }
                }
                foreach(var sub in interact.subInteractingData)
                {
                    foreach (OutlineController outlineController in sub.optionalOutlineControllers)
                    {
                        outlineController.showOutline = flag;
                        if (flag)
                        {
                            outlineController.SetColor(color);
                        }
                        else
                        {
                            outlineController.ResetColor();
                        }
                    }
                    foreach (SpriteObject spo in sub.optionalSpriteObjectOutlines)
                    {
                        spo.outlineColor = color;
                    }
                }
            }
        }
        public static Rect GetInteractableRect(in LocalTransform transform, in InteractableCD interactableCD, Direction.Id direction, bool useInteractRadius = false)
        {
            ref InteractableBlobData value = ref interactableCD.interactableData.Value;
            ref BlobArray<InteractablePointData> interactPoints =
                ref interactableCD.interactablePointOffsetsData.Value.GetInteractablePointsInDirection(direction);

            // 基准点
            float3 basePoint = transform.Position +
                value.directionOffset.GetDataInDirection(direction, value.directionOffset.forward).ToFloat3();

            float radius = useInteractRadius ? math.sqrt(value.interactRadiusSqr) : 0.5f;
            if (interactPoints.Length == 0)
            {
                // 没有交互点，使用基准点 + 交互半径
                return new Rect(
                    basePoint.x - radius,
                    basePoint.z - radius,
                    radius * 2,
                    radius * 2);
            }

            // 有交互点，计算所有交互点的2D包围盒
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i < interactPoints.Length; i++)
            {
                float3 worldPoint = basePoint + interactPoints[i].point;
                minX = math.min(minX, worldPoint.x);
                maxX = math.max(maxX, worldPoint.x);
                minZ = math.min(minZ, worldPoint.z);
                maxZ = math.max(maxZ, worldPoint.z);
            }

            // 加上交互半径作为边距
            return new Rect(
                minX - radius,
                minZ - radius,
                (maxX - minX) + radius * 2,
                (maxZ - minZ) + radius * 2);
        }
    }
}
