using Assets.BuildingBlueprint.Scripts.Core;
using Unity.Collections;
using Unity.NetCode;

namespace Assets.BuildingBlueprint.Scripts.Components
{
    public struct BuildingCacheRPC : IRpcCommand
    {
        public int ID;
        public int Total;
        public int Length;
        public static implicit operator BuildingCache(BuildingCacheRPC rpc)
        {
            return new()
            {
                ID = rpc.ID,
                Total = rpc.Total,
                Commands = new(rpc.Length, Allocator.Persistent)
            };
        }
    }
}
