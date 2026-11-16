using Assets.BuildingBlueprint.Scripts.Enums;
using PugTilemap;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Assets.BuildingBlueprint.Scripts.Components
{
    public struct PlaceCommandRPC : IRpcCommand
    {
        public Entity Player;
        public PlaceOperator Op;
        public int Num1;
        public int Num2;
        public int Num3;
        public int Num4;

        // 执行放置：Player + Origin，ID = LocalId
        public static PlaceCommandRPC Place(Entity player, int id, int2 origin)
        {
            return new()
            {
                Player = player,
                Op = PlaceOperator.Place,
                Num1 = id,
                Num2 = origin.x,
                Num3 = origin.y,
            };
        }
        public readonly void Place(out int id, out int2 origin)
        {
            id = Num1;
            origin = new(Num2, Num3);
        }

        public static PlaceCommandRPC DestroyTile(Entity player, TileCD tile, int2 pos)
        {
            return new()
            {
                Player = player,
                Op = PlaceOperator.DestroyTile,
                Num1 = tile.tileset,
                Num2 = (int)tile.tileType,
                Num3 = pos.x,
                Num4 = pos.y,
            };
        }
        public readonly void DestroyTile(out TileCD tile, out int2 pos)
        {
            tile = new()
            {
                tileset = Num1,
                tileType = (TileType)Num2,
            };
            pos = new(Num3, Num4);
        }

        private static bool TryGetGhost(Entity target, ComponentLookup<GhostInstance> lookup, out GhostInstance ghost)
        {
            if (lookup.TryGetComponent(target, out ghost) && ghost.spawnTick.IsValid)
                return true;
            ghost = default;
            return false;
        }

        public static PlaceCommandRPC DestroyEntity(Entity player, GhostInstance target)
        {
            return new()
            {
                Player = player,
                Op = PlaceOperator.DestroyEntity,
                Num1 = target.ghostId,
                Num2 = unchecked((int)target.spawnTick.TickIndexForValidTick),
            };
        }
        public readonly void DestroyEntity(out int ghostId, out NetworkTick spawnTick)
        {
            ghostId = Num1;
            spawnTick = new NetworkTick(unchecked((uint)Num2));
        }
    }
}