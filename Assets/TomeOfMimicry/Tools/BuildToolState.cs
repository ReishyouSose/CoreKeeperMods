using Unity.Mathematics;
using UnityEngine;

namespace TomeOfMimicry
{
    public enum BuildTool
    {
        Blueprint,
        Cut,
        WallArea,
        Circle,
        Polygon
    }

    public enum ShapeAnchor
    {
        Cursor,
        Player
    }

    public static class BuildToolState
    {
        public static BuildTool ActiveTool = BuildTool.Blueprint;
        public static ShapeAnchor Anchor = ShapeAnchor.Cursor;
        public static int ShapeWidth = 11;
        public static int ShapeHeight = 11;
        public static int PolygonSides = 6;
        public static int OutlineThickness = 1;
        public static bool Hollow = true;

        public static int2 ResolveAnchor(int2 cursorTile)
        {
            if (Anchor != ShapeAnchor.Player || Manager.main?.player == null)
                return cursorTile;

            var render = Manager.main.player.transform.position;
            var world = EntityMonoBehaviour.ToWorldFromRender(
                new Vector3Int(Mathf.RoundToInt(render.x), 0, Mathf.RoundToInt(render.z)));
            return new int2(world.x, world.z);
        }

        public static bool UsesShapeAnchor =>
            ActiveTool == BuildTool.Circle || ActiveTool == BuildTool.Polygon;
    }
}
