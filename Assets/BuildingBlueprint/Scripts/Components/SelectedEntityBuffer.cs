using Assets.BuildingBlueprint.Scripts.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace Assets.BuildingBlueprint.Scripts.Components
{
    [InternalBufferCapacity(0)]
    public struct SelectedEntityBuffer : IBufferElementData
    {
        [GhostField]
        public Entity Entity;

        [GhostField(SendData = false)]
        public ObjectID ObjectID;
        [GhostField(SendData = false)]
        public int X;
        [GhostField(SendData = false)]
        public int Y;
        [GhostField(SendData = false)]
        public int Variation;
        [GhostField(SendData = false)]
        public int2 Position;
        [GhostField(SendData = false)]
        public int2 Direction;

        public static implicit operator SelectedEntity(SelectedEntityBuffer buffer)
        {
            return new()
            {
                ObjectID = buffer.ObjectID,
                X = buffer.X,
                Y = buffer.Y,
                Variation = buffer.Variation,
                Position = buffer.Position,
                Direction = buffer.Direction,
                Entity = buffer.Entity,
            };
        }
        public readonly float2 GetEntityOffset(ref PugDatabase.EntityObjectInfo info, out int2 size)
        {
            size = new(X, Y);
            var offset = new float2(X - 1, Y - 1) / 2f;
            //var corner = GetEntityOffset(ObjectID, Variation, Direction);
            int2 corner = info.prefabCornerOffset;
            return new(corner.x + offset.x, corner.y + offset.y);
        }
    }
}
