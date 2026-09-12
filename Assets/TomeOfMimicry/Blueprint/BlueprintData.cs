using System;
using System.Collections.Generic;
using PugTilemap;
using Unity.Mathematics;

namespace TomeOfMimicry
{
    public enum BlueprintCopyMode
    {
        Full,
        FloorsOnly,
        WallsOnly,
        ObjectsOnly
    }

    public enum BlueprintPasteMode
    {
        Replace,
        MergeEmpty
    }

    public struct BlueprintTile
    {
        public int3 relativePos;
        public int floorTileType;
        public TileType floorTileKind;
        public int groundTileType;
        public int wallTileType;
        public int objectID;
        public bool hasFloor;
        public bool hasGround;
        public bool hasWall;
        public bool hasObject;
        public bool hasWire;
        public int wireTileset;
        public bool hasRail;
        public int railTileset;
        public List<int2> extras;
    }

    public static class BlueprintLayers
    {
        public static readonly TileType[] Extras =
        {
            TileType.thinWall,
            TileType.fence,
            TileType.rug,
            TileType.litFloor,
            TileType.looseFlooring,
            TileType.ancientCircuitPlate,
        };

        public static bool IsExtra(TileType type)
        {
            foreach (var t in Extras)
                if (t == type) return true;
            return false;
        }
    }

    public struct BlueprintObject
    {
        public int2 relativePos;
        public ObjectID objectId;
        public int variation;
        public float3 direction;
        public bool dirByVariation;
        public int2 dbvDirection;
        public int2 tileSize;

        public bool hasPaint;
        public int paintColor;

        public bool hasObjectFilter;
        public int objectFilterType;
        public ObjectID objectFilterObject;
        public int objectFilterVariation;

        public bool hasMoverFilter;
        public int moverFilterType;
        public ObjectID moverFilterObject;
        public int moverFilterVariation;
        public int moverFilterCategory;
    }

    public class Blueprint
    {
        public const int CurrentSchemaVersion = 2;

        public int schemaVersion = CurrentSchemaVersion;
        public string id;
        public string name;
        public string description;
        public string createdUtc;
        public string modifiedUtc;
        public string sourceGameVersion;
        public List<string> tags = new();
        public int unsupportedObjectCount;
        public int width;
        public int height;
        public List<BlueprintTile> tiles = new();
        public List<BlueprintObject> objects = new();

        public Blueprint(string name, int width, int height)
        {
            id = Guid.NewGuid().ToString("N");
            this.name = name;
            this.width = width;
            this.height = height;
            createdUtc = DateTime.UtcNow.ToString("O");
            modifiedUtc = createdUtc;
        }
    }
}
