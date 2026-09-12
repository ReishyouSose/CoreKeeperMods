using System;
using System.Collections.Generic;
using PugTilemap;
using Unity.Mathematics;

namespace TomeOfMimicry
{
    public static class ShapeBlueprintGenerator
    {
        public static bool TryGenerate(
            BuildTool tool,
            int width,
            int height,
            int sides,
            bool hollow,
            int thickness)
        {
            if (!TryGetWallMaterial(out int tileset))
                return false;

            width = math.clamp(width, 3, 101);
            height = math.clamp(height, 3, 101);
            sides = math.clamp(sides, 3, 16);
            if (tool == BuildTool.Polygon)
                height = width;
            thickness = math.clamp(thickness, 1, math.max(1, math.min(width, height) / 2));

            HashSet<int2> cells = tool == BuildTool.Polygon
                ? RasterizePolygon(width, height, sides, hollow, thickness)
                : RasterizeEllipse(width, height, hollow, thickness);

            var blueprint = new Blueprint(
                tool == BuildTool.Polygon ? $"Polygon_{sides}_{width}x{height}" : $"Oval_{width}x{height}",
                width,
                height);

            foreach (var cell in cells)
            {
                if (cell.x < 0 || cell.y < 0 || cell.x >= width || cell.y >= height)
                    continue;

                blueprint.tiles.Add(new BlueprintTile
                {
                    relativePos = new int3(cell.x, 0, cell.y),
                    hasWall = true,
                    wallTileType = tileset,
                    floorTileKind = TileType.none,
                });
            }

            BlueprintManager.SetGeneratedBlueprint(blueprint);
            return true;
        }

        public static bool TryGetWallMaterial(out int tileset)
        {
            var source = BlueprintManager.ActiveBlueprint;
            if (source != null)
            {
                foreach (var tile in source.tiles)
                {
                    if (!tile.hasWall) continue;
                    tileset = tile.wallTileType;
                    return true;
                }
            }

            tileset = 0;
            return false;
        }

        private static HashSet<int2> RasterizeEllipse(
            int width,
            int height,
            bool hollow,
            int thickness)
        {
            var outer = PlotEllipseOutline(0, 0, width - 1, height - 1);
            var filled = FillRows(outer, width, height);
            if (!hollow)
                return filled;

            return ExtractConnectedBorder(filled, thickness);
        }

        // Ellipse rasterization adapted from Alois Zingl's plotEllipseRect,
        // Copyright (c) 2012-2020 Alois Zingl, MIT License (https://zingl.github.io/bresenham.html).
        private static HashSet<int2> PlotEllipseOutline(int x0, int y0, int x1, int y1)
        {
            var result = new HashSet<int2>();
            long a = Math.Abs(x1 - x0);
            long b = Math.Abs(y1 - y0);
            long bParity = b & 1;
            long dx = 4 * (1 - a) * b * b;
            long dy = 4 * (bParity + 1) * a * a;
            long err = dx + dy + bParity * a * a;

            if (x0 > x1)
            {
                int swap = x0;
                x0 = x1;
                x1 = swap;
            }
            if (y0 > y1)
            {
                int swap = y0;
                y0 = y1;
                y1 = swap;
            }

            y0 += (int)((b + 1) / 2);
            y1 = y0 - (int)bParity;
            a *= 8 * a;
            bParity = 8 * b * b;

            do
            {
                Add(result, x1, y0);
                Add(result, x0, y0);
                Add(result, x0, y1);
                Add(result, x1, y1);

                long e2 = 2 * err;
                if (e2 <= dy)
                {
                    y0++;
                    y1--;
                    dy += a;
                    err += dy;
                }
                if (e2 >= dx || 2 * err > dy)
                {
                    x0++;
                    x1--;
                    dx += bParity;
                    err += dx;
                }
            }
            while (x0 <= x1);

            while (y0 - y1 < b)
            {
                Add(result, x0 - 1, y0);
                Add(result, x1 + 1, y0++);
                Add(result, x0 - 1, y1);
                Add(result, x1 + 1, y1--);
            }

            return result;
        }

        private static HashSet<int2> RasterizePolygon(
            int width,
            int height,
            int sides,
            bool hollow,
            int thickness)
        {
            var outerVertices = CreatePolygonVertices(width, height, sides);
            var filled = FillPolygon(outerVertices, width, height);
            if (!hollow)
                return filled;

            return ExtractConnectedBorder(filled, thickness);
        }

        private static HashSet<int2> ExtractConnectedBorder(HashSet<int2> filled, int thickness)
        {
            var border = new HashSet<int2>();
            var remaining = new HashSet<int2>(filled);
            var layerCells = new List<int2>();
            var neighbours = new[]
            {
                new int2(-1, -1),
                new int2(-1, 0),
                new int2(-1, 1),
                new int2(0, -1),
                new int2(0, 1),
                new int2(1, -1),
                new int2(1, 0),
                new int2(1, 1),
            };

            for (int layer = 0; layer < thickness && remaining.Count > 0; layer++)
            {
                layerCells.Clear();
                foreach (var cell in remaining)
                {
                    for (int i = 0; i < neighbours.Length; i++)
                    {
                        if (remaining.Contains(cell + neighbours[i])) continue;
                        layerCells.Add(cell);
                        break;
                    }
                }

                if (layerCells.Count == 0) break;
                foreach (var cell in layerCells)
                {
                    border.Add(cell);
                    remaining.Remove(cell);
                }
            }

            return border;
        }

        private static double2[] CreatePolygonVertices(int width, int height, int sides)
        {
            double cx = (width - 1) / 2.0;
            double cy = (height - 1) / 2.0;
            var unit = new double2[sides];
            double minX = double.MaxValue;
            double maxX = double.MinValue;
            double minY = double.MaxValue;
            double maxY = double.MinValue;

            double offset = sides % 2 == 0 ? Math.PI / sides : 0.0;

            for (int i = 0; i < sides; i++)
            {
                double angle = -Math.PI / 2.0 + offset + i * Math.PI * 2.0 / sides;
                unit[i] = new double2(Math.Cos(angle), Math.Sin(angle));
                minX = Math.Min(minX, unit[i].x);
                maxX = Math.Max(maxX, unit[i].x);
                minY = Math.Min(minY, unit[i].y);
                maxY = Math.Max(maxY, unit[i].y);
            }

            var vertices = new double2[sides];
            double scaleX = (width - 1) / (maxX - minX);
            double scaleY = (height - 1) / (maxY - minY);
            double unitCenterX = (minX + maxX) * 0.5;
            double unitCenterY = (minY + maxY) * 0.5;
            for (int i = 0; i < sides; i++)
            {
                vertices[i] = new double2(
                    cx + (unit[i].x - unitCenterX) * scaleX,
                    cy + (unit[i].y - unitCenterY) * scaleY);
            }
            return vertices;
        }

        private static void PlotLine(int2 start, int2 end, HashSet<int2> result)
        {
            int x = start.x;
            int y = start.y;
            int dx = Math.Abs(end.x - start.x);
            int sx = start.x < end.x ? 1 : -1;
            int dy = -Math.Abs(end.y - start.y);
            int sy = start.y < end.y ? 1 : -1;
            int error = dx + dy;

            while (true)
            {
                result.Add(new int2(x, y));
                if (x == end.x && y == end.y) break;
                int e2 = 2 * error;
                if (e2 >= dy) { error += dy; x += sx; }
                if (e2 <= dx) { error += dx; y += sy; }
            }
        }

        private static HashSet<int2> FillRows(HashSet<int2> outline, int width, int height)
        {
            var result = new HashSet<int2>(outline);
            for (int y = 0; y < height; y++)
            {
                int minX = width;
                int maxX = -1;
                foreach (var cell in outline)
                {
                    if (cell.y != y) continue;
                    minX = Math.Min(minX, cell.x);
                    maxX = Math.Max(maxX, cell.x);
                }
                for (int x = minX; x <= maxX; x++) result.Add(new int2(x, y));
            }
            return result;
        }

        private static HashSet<int2> FillPolygon(double2[] vertices, int width, int height)
        {
            var result = new HashSet<int2>();
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (Contains(vertices, x, y) || DistanceToEdges(vertices, x, y) <= 0.5)
                    result.Add(new int2(x, y));
            }
            return result;
        }

        private static bool Contains(double2[] vertices, double x, double y)
        {
            bool inside = false;
            for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
            {
                var a = vertices[i];
                var b = vertices[j];
                bool crosses = (a.y > y) != (b.y > y) &&
                               x < (double)(b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x;
                if (crosses) inside = !inside;
            }
            return inside;
        }

        private static double DistanceToEdges(double2[] vertices, double x, double y)
        {
            var point = new double2(x, y);
            double best = double.MaxValue;
            for (int i = 0; i < vertices.Length; i++)
            {
                double2 a = vertices[i];
                double2 b = vertices[(i + 1) % vertices.Length];
                double2 edge = b - a;
                double lengthSq = math.lengthsq(edge);
                double t = lengthSq <= double.Epsilon
                    ? 0.0
                    : math.clamp(math.dot(point - a, edge) / lengthSq, 0.0, 1.0);
                best = Math.Min(best, math.distance(point, a + edge * t));
            }
            return best;
        }

        private static void Add(HashSet<int2> result, int x, int y)
        {
            result.Add(new int2(x, y));
        }
    }
}
