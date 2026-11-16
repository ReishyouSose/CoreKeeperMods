using Assets.BuildingBlueprint.Scripts.Components;
using Pug.UnityExtensions;
using PugTilemap;
using Unity.Mathematics;
using UnityEngine;

namespace Assets.BuildingBlueprint.Scripts.Core
{
    public static class MiscHelper
    {
        public static Rect GetEntityRect(SelectedEntityBuffer buffer, ref PugDatabase.EntityObjectInfo info)
        {
            var offset = buffer.GetEntityOffset(ref info, out var size);
            Vector2 s = size.ToVec2Int();
            var origin = buffer.Position.ToFloat2() + offset - (float2)s / 2f;
            return new Rect(origin, s);
        }

        public static bool Contains(this Rect self, Rect other)
        {
            return self.xMin <= other.xMin &&
                   self.xMax >= other.xMax &&
                   self.yMin <= other.yMin &&
                   self.yMax >= other.yMax;
        }
        public static ObjectDataCD TileToObject(TileCD tile, in TileWithTilesetToObjectDataMapCD tileSetMap)
        {
            TileType type = tile.tileType;
            switch (tile.tileType)
            {
                case TileType.ground:
                    type = TileType.wall;
                    break;
                case TileType.roofHole:
                    return new()
                    {
                        objectID = ObjectID.RoofingTool,
                        variation = 0
                    };
                case TileType.water:
                    return new()
                    {
                        objectID = ObjectID.Bucket,
                        variation = tile.tileset + 1
                    };
                case TileType.dugUpGround:
                    return new()
                    {
                        objectID = ObjectID.WoodHoe
                    };
            }
            return PugDatabase.TryGetTileItemInfo(type, (Tileset)tile.tileset, tileSetMap);
        }
        public static int GetPlaceOrder(TileType tileType)
        {
            return tileType switch
            {
                TileType.pit or TileType.water or TileType.ground => 0,
                TileType.dugUpGround or TileType.smallGrass or TileType.floorCrack or TileType.bridge => 1,
                TileType.wallGrass or TileType.wallCrack or TileType.ore or TileType.ancientCrystal => 3,
                _ => 2,
            };
        }
    }
}
