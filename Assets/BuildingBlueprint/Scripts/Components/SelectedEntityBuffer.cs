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
        [GhostField]
        public ObjectID ObjectID;
        [GhostField]
        public int X;
        [GhostField]
        public int Y;
        [GhostField]
        public int Variation;
        [GhostField]
        public int2 Position;
        [GhostField]
        public int2 Direction;
        [GhostField]
        public PaintableColor Color;

        public static implicit operator EntityCD(SelectedEntityBuffer buffer)
        {
            return new()
            {
                ObjectID = buffer.ObjectID,
                X = buffer.X,
                Y = buffer.Y,
                Variation = buffer.Variation,
                Position = buffer.Position,
                Direction = buffer.Direction,
                Color = buffer.Color,
                Entity = buffer.Entity,
            };
        }
        public readonly float2 GetEntityOffset(BlobAssetReference<PugDatabase.PugDatabaseBank> database, out int2 size)
        {
            size = new(X, Y);
            var offset = new float2(X - 1, Y - 1) / 2f;
            //var corner = GetEntityOffset(ObjectID, Variation, Direction);
            int2 corner = PugDatabase.GetEntityObjectInfo(ObjectID, database, Variation).prefabCornerOffset;
            return new(corner.x + offset.x, corner.y + offset.y);
        }
    }
}
