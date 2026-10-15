using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace Assets.BuildingBlueprint.Scripts.Components
{
    public struct PlaceEntityRpc : IRpcCommand
    {
        public int ID;
        public ObjectID ObjectID;
        public int Variation;
        public int2 Pos;
        public int2 Direction;
        public Entity Player;
        public PaintableColor Color;
        public Entity Entity;
        [DontSerializeForCommand]
        public TickTimer Timer;
    }
}
