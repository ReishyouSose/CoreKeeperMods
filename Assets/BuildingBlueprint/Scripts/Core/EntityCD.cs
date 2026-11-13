using Newtonsoft.Json;
using Pug.UnityExtensions;
using System;
using Unity.Entities;
using Unity.Mathematics;

namespace Assets.BuildingBlueprint.Scripts.Core
{
    [Serializable]
    public struct EntityCD
    {
        public ObjectID ObjectID;
        public int X;
        public int Y;
        public int Variation;
        [JsonIgnore]
        public int2 Position;
        [JsonIgnore]
        public Entity Entity;
        public int2 Direction;
        public PaintableColor Color;
        // Robot arm or item collector filter (the game's ObjectFilteringCD); left out of the file when unset
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public FilterType FilterType;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public ObjectID FilterObject;
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int FilterVariation;
        public readonly float2 GetEntityOffset(out int2 size)
        {
            size = new(X, Y);
            var offset = new float2(X - 1, Y - 1) / 2f;
            //var corner = GetEntityOffset(ObjectID, Variation, Direction);
            int2 corner = PugDatabase.GetObjectInfo(ObjectID, Variation).prefabCornerOffset.ToInt2();
            return new(corner.x + offset.x, corner.y + offset.y);
        }
        /*public static float3 GetEntityOffset(ObjectID id, int variation, float3 direction)
        {
            var info = PugDatabase.GetObjectInfo(id, variation);
            int2 size = info.prefabTileSize.ToInt2();
            int2 corner = info.prefabCornerOffset.ToInt2();
            int rotationVariation = 0;
            if (direction.x > 0.5f)
                rotationVariation = 1;      // (1,0,0) → 90°
            else if (direction.x < -0.5f)
                rotationVariation = 3; // (-1,0,0) → 270°
            else if (direction.z > 0.5f)
                rotationVariation = 0;  // (0,0,1) → 0°
            else if (direction.z < -0.5f)
                rotationVariation = 2; // (0,0,-1) → 180°

            float3 centerOffset = new float3(size.x - 1, 0f, size.y - 1) / 2f;

            float3 finalOffset;
            if (rotationVariation > 0)
            {
                DirectionCD.RotateTransform(
                    quaternion.identity,
                    centerOffset,
                    rotationVariation,
                    corner,
                    size,
                    out var _,
                    out finalOffset
                );
            }
            else
            {
                finalOffset = centerOffset;
            }
            return finalOffset;
        }*/
    }
}
