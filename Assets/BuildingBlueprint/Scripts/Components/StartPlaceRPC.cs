using Unity.Entities;
using Unity.NetCode;

namespace Assets.BuildingBlueprint.Scripts.Components
{
    public struct StartPlaceRPC : IRpcCommand
    {
        public int ID;
        public Entity Player;
        public int Count_1;
        public int Count_2;
        public int Count_3;
        public int Count_4;
        public int Count_5;
    }
}
