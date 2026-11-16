using Assets.BuildingBlueprint.Scripts.Components;
using Assets.BuildingBlueprint.Scripts.Enums;
using PugTilemap;
using Unity.Mathematics;

namespace Assets.BuildingBlueprint.Scripts.Core
{
    public interface IPlaceCommand
    {
        PlaceCommand Write();
        void Read(PlaceCommand rpc);
        (string, string, string, string) GetNames();

        public static (string, string, string, string) GetNames(PlaceOperator op)
        {
            return op switch
            {
                PlaceOperator.Tile => default(TileCommand).GetNames(),
                PlaceOperator.Entity => default(EntityCommand).GetNames(),
                PlaceOperator.Size => default(SizeCommand).GetNames(),
                PlaceOperator.Direction => default(DirectionCommand).GetNames(),
                PlaceOperator.Paint => default(PaintCommand).GetNames(),
                PlaceOperator.ObjFilter => default(ObjFilterCommand).GetNames(),
                PlaceOperator.Container => default(ContainerCommand).GetNames(),
                _ => throw new System.Exception("undefined command operator")
            };
        }
        public static T ReadCommand<T>(PlaceCommand rpc) where T : IPlaceCommand
        {
            var t = default(T);
            t.Read(rpc);
            return t;
        }
    }

    public struct EntityCommand : IPlaceCommand
    {
        public ObjectID ObjectID;
        public int Variation;
        public int2 Pos;

        public readonly PlaceCommand Write()
            => new()
            {
                Op = PlaceOperator.Entity,
                Num1 = (int)ObjectID,
                Num2 = Variation,
                Num3 = Pos.x,
                Num4 = Pos.y,
            };

        public void Read(PlaceCommand rpc)
        {
            ObjectID = (ObjectID)rpc.Num1;
            Variation = rpc.Num2;
            Pos = new(rpc.Num3, rpc.Num4);
        }

        public readonly (string, string, string, string) GetNames()
            => ("objectID", "variation", "x", "y");
    }

    public struct TileCommand : IPlaceCommand
    {
        public TileCD TileCD;
        public int2 Pos;

        public readonly PlaceCommand Write()
            => new()
            {
                Op = PlaceOperator.Tile,
                Num1 = TileCD.tileset,
                Num2 = (int)TileCD.tileType,
                Num3 = Pos.x,
                Num4 = Pos.y,
            };

        public void Read(PlaceCommand rpc)
        {
            TileCD = new() { tileset = rpc.Num1, tileType = (TileType)rpc.Num2 };
            Pos = new(rpc.Num3, rpc.Num4);
        }

        public readonly (string, string, string, string) GetNames()
            => ("tileset", "tileType", "x", "y");
    }

    public struct DirectionCommand : IPlaceCommand
    {
        public int2 Direction;

        public readonly PlaceCommand Write()
            => new()
            {
                Op = PlaceOperator.Direction,
                Num1 = Direction.x,
                Num2 = Direction.y,
            };

        public void Read(PlaceCommand rpc)
        {
            Direction = new(rpc.Num1, rpc.Num2);
        }

        public readonly (string, string, string, string) GetNames()
            => ("dirX", "dirY", null, null);
    }
    public struct SizeCommand : IPlaceCommand
    {
        public int X;
        public int Y;
        public PlaceCommand Write()
        => new()
        {
            Op = PlaceOperator.Size,
            Num1 = X,
            Num2 = Y,
        };

        public void Read(PlaceCommand rpc)
        {
            X = rpc.Num1;
            Y = rpc.Num2;
        }
        public readonly (string, string, string, string) GetNames()
        {
            return ("X", "Y", null, null);
        }
    }


    public struct PaintCommand : IPlaceCommand
    {
        public PaintableColor Color;

        public readonly PlaceCommand Write()
            => new()
            {
                Op = PlaceOperator.Paint,
                Num1 = (int)Color,
            };

        public void Read(PlaceCommand rpc)
        {
            Color = (PaintableColor)rpc.Num1;
        }

        public readonly (string, string, string, string) GetNames()
            => ("color", null, null, null);
    }

    public struct ObjFilterCommand : IPlaceCommand
    {
        public FilterType Type;
        public ObjectID Object;
        public int Variation;

        public readonly PlaceCommand Write()
            => new()
            {
                Op = PlaceOperator.ObjFilter,
                Num1 = (int)Type,
                Num2 = (int)Object,
                Num3 = Variation,
            };

        public void Read(PlaceCommand rpc)
        {
            Type = (FilterType)rpc.Num1;
            Object = (ObjectID)rpc.Num2;
            Variation = rpc.Num3;
        }

        public readonly (string, string, string, string) GetNames()
            => ("filterType", "filterObject", "filterVariation", null);
    }
    public struct ContainerCommand : IPlaceCommand
    {
        public int Index;
        public ObjectID ObjectID;
        public int Variation;
        public PlaceCommand Write()
            => new()
            {
                Op = PlaceOperator.Container,
                Num1 = Index,
                Num2 = (int)ObjectID,
                Num3 = Variation,
            };
        public void Read(PlaceCommand cmd)
        {
            Index = cmd.Num1;
            ObjectID = (ObjectID)cmd.Num2;
            Variation = cmd.Num3;
        }

        public readonly (string, string, string, string) GetNames()
        {
            return ("index", "objectID", "variation", null);
        }
    }
}