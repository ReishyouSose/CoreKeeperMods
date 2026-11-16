using Assets.BuildingBlueprint.Scripts.Components;
using System;
using Unity.Collections;
using Unity.Entities;

namespace Assets.BuildingBlueprint.Scripts.Core
{
    public struct BuildingCache
    {
        public struct Key : IEquatable<Key>
        {
            public Entity Connect;
            public int ID;

            public readonly bool Equals(Key key)
            {
                return key.Connect == Connect && key.ID == ID;
            }
            public override int GetHashCode()
            {
                // 只用 blittable 字段和简单运算，不调用任何 base 或托管方法
                return (Connect.GetHashCode() * 397) ^ ID;
            }
            public static implicit operator Key((Entity connect, int id) value) => new() { Connect = value.connect, ID = value.id };
        }
        public int ID;
        public int Total;
        public bool Completed;
        public int Count;
        public NativeList<PlaceCommand> Commands;
        public void Check()
        {
            if (Total <= 0)
                return;
            if (Count >= Total)
                Completed = true;
        }
    }
}
