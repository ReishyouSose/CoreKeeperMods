using PugTilemap;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Assets.BuildingBlueprint.Scripts.Core
{
    public struct TilesInfo
    {
        public int2 Position;
        public Dictionary<TileCD, bool> Tiles;
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
