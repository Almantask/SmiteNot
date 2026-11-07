using System;
using System.Collections.Generic;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>One terrain piece in the plan's terrain layer (spec S1, section 9).</summary>
    public sealed class PlannedPiece
    {
        public PlannedPiece(
            string id,
            PieceRules rules,
            int[] pointsMm,
            int widthMm,
            int depthMm,
            int heightMm,
            int litresPerSecond
        )
        {
            Id = id;
            Rules = rules;
            PointsMm = pointsMm;
            WidthMm = widthMm;
            DepthMm = depthMm;
            HeightMm = heightMm;
            LitresPerSecond = litresPerSecond;
        }

        /// <summary>Stable across edits.</summary>
        public string Id { get; }

        public PieceRules Rules { get; }

        /// <summary>x, y pairs in mm: a polyline's points, or the one point a spring or boulder stands on.</summary>
        public int[] PointsMm { get; }

        public int PointCount => PointsMm.Length / 2;

        /// <summary>Channel bottom width; 0 for other kinds.</summary>
        public int WidthMm { get; }

        /// <summary>Channel or hollow depth; 0 for other kinds.</summary>
        public int DepthMm { get; }

        /// <summary>Ridge height; 0 for other kinds.</summary>
        public int HeightMm { get; }

        /// <summary>Spring discharge; 0 for other kinds.</summary>
        public int LitresPerSecond { get; }
    }

    /// <summary>
    /// Stamps the plan's terrain pieces onto the map before tick 0 (spec S1, section 9.1): channels and hollows lower the
    /// ground (by min, relative to the original ground, so the bed follows its slope), then ridges raise it (by max), then
    /// boulders stand 2 m over the highest ground under them and close their cells to every class; springs become water
    /// sources. Within each kind min and max don't care about order, and the kinds go in a fixed order, so the plan's
    /// order never changes the result. Integer geometry only: a cell belongs to a polyline piece by the distance from
    /// its centre to the nearest segment.
    /// </summary>
    public static class TerrainPieces
    {
        /// <summary>The map as the battle plays it: the original map when the plan has no pieces.</summary>
        public static MapData Apply(MapData map, IReadOnlyList<PlannedPiece> pieces)
        {
            if (pieces.Count == 0)
            {
                return map;
            }
            int[] original = map.HeightMm;
            int[] heights = (int[])original.Clone();
            var blocked = new bool[heights.Length];
            var sources = new List<WaterSource>(map.WaterSources);
            foreach (var kind in new[] { PieceKind.Channel, PieceKind.Hollow })
            {
                foreach (var piece in pieces)
                {
                    if (piece.Rules.Kind == kind)
                    {
                        Lower(map, piece, original, heights);
                    }
                }
            }
            foreach (var piece in pieces)
            {
                if (piece.Rules.Kind == PieceKind.Ridge)
                {
                    Raise(map, piece, original, heights);
                }
            }
            foreach (var piece in pieces)
            {
                if (piece.Rules.Kind == PieceKind.Boulder)
                {
                    var cells = BoulderCells(map, piece);
                    int top = int.MinValue;
                    ForEach(map, cells, i => top = System.Math.Max(top, heights[i]));
                    ForEach(
                        map,
                        cells,
                        i =>
                        {
                            heights[i] = top + piece.Rules.BoulderHeightMm;
                            blocked[i] = true;
                        }
                    );
                }
                else if (piece.Rules.Kind == PieceKind.Spring)
                {
                    sources.Add(new WaterSource(SpringArea(map, piece), piece.LitresPerSecond));
                }
            }
            return map.WithTerrain(heights, blocked, sources.ToArray());
        }

        /// <summary>The cells a piece covers, for the buffer and overlap checks (the whole footprint, S1 9.1).</summary>
        public static List<int> Footprint(MapData map, PlannedPiece piece)
        {
            var cells = new List<int>();
            switch (piece.Rules.Kind)
            {
                case PieceKind.Boulder:
                    ForEach(map, BoulderCells(map, piece), cells.Add);
                    break;
                case PieceKind.Spring:
                    var area = SpringArea(map, piece);
                    ForEach(
                        map,
                        new CellRect(
                            area.MinX / map.CellSizeMm,
                            area.MinY / map.CellSizeMm,
                            (area.MaxX - 1) / map.CellSizeMm,
                            (area.MaxY - 1) / map.CellSizeMm
                        ),
                        cells.Add
                    );
                    break;
                default:
                    long reach = HalfFootprint(map, piece);
                    ForEachNear(map, piece, reach, (i, d) => cells.Add(i));
                    break;
            }
            return cells;
        }

        private static void Lower(MapData map, PlannedPiece piece, int[] original, int[] heights)
        {
            var rules = piece.Rules;
            if (rules.Kind == PieceKind.Channel)
            {
                long half = piece.WidthMm / 2;
                long side = half + (long)rules.SideCells * map.CellSizeMm;
                int sideDepth = (int)((long)piece.DepthMm * rules.SideDepthPermille / 1000);
                ForEachNear(
                    map,
                    piece,
                    side,
                    (i, d) =>
                    {
                        int bed = original[i] - (d <= half ? piece.DepthMm : sideDepth);
                        heights[i] = System.Math.Min(heights[i], bed);
                    }
                );
                return;
            }
            // Hollow: the floor at −depth, then sides rising at the side slope to the original ground.
            ForEachNear(
                map,
                piece,
                HalfFootprint(map, piece),
                (i, d) =>
                {
                    long below = Profile(
                        piece.DepthMm,
                        rules.TopWidthMm,
                        rules.SideSlopePermille,
                        d
                    );
                    if (below > 0)
                    {
                        heights[i] = System.Math.Min(heights[i], (int)(original[i] - below));
                    }
                }
            );
        }

        private static void Raise(MapData map, PlannedPiece piece, int[] original, int[] heights)
        {
            var rules = piece.Rules;
            ForEachNear(
                map,
                piece,
                HalfFootprint(map, piece),
                (i, d) =>
                {
                    long raise = Profile(
                        piece.HeightMm,
                        rules.TopWidthMm,
                        rules.SideSlopePermille,
                        d
                    );
                    if (raise > 0)
                    {
                        heights[i] = System.Math.Max(heights[i], (int)(original[i] + raise));
                    }
                }
            );
        }

        /// <summary>A crest or floor <paramref name="top"/> wide at full <paramref name="height"/>, then sloping away.</summary>
        private static long Profile(int height, int top, int slopePermille, long distance) =>
            height - System.Math.Max(0, distance - top / 2) * slopePermille / 1000;

        /// <summary>How far from the line a polyline piece reaches.</summary>
        private static long HalfFootprint(MapData map, PlannedPiece piece)
        {
            var rules = piece.Rules;
            switch (rules.Kind)
            {
                case PieceKind.Channel:
                    return piece.WidthMm / 2 + (long)rules.SideCells * map.CellSizeMm;
                case PieceKind.Ridge:
                    return rules.TopWidthMm / 2
                        + (long)piece.HeightMm * 1000 / rules.SideSlopePermille;
                default:
                    return rules.TopWidthMm / 2
                        + (long)piece.DepthMm * 1000 / rules.SideSlopePermille;
            }
        }

        /// <summary>The 2 × 2-cell block whose lower-left cell holds the point.</summary>
        private static CellRect BoulderCells(MapData map, PlannedPiece piece)
        {
            int side = System.Math.Max(1, piece.Rules.SizeMm / map.CellSizeMm);
            int x = System.Math.Min(piece.PointsMm[0] / map.CellSizeMm, map.Columns - side);
            int y = System.Math.Min(piece.PointsMm[1] / map.CellSizeMm, map.Rows - side);
            return new CellRect(x, y, x + side - 1, y + side - 1);
        }

        /// <summary>The spring's source block, centred on the cell holding its point, as an area in mm.</summary>
        private static RectMm SpringArea(MapData map, PlannedPiece piece)
        {
            int n = piece.Rules.SourceBlockCells;
            int cx = piece.PointsMm[0] / map.CellSizeMm;
            int cy = piece.PointsMm[1] / map.CellSizeMm;
            int x0 = System.Math.Max(0, System.Math.Min(cx - n / 2, map.Columns - n));
            int y0 = System.Math.Max(0, System.Math.Min(cy - n / 2, map.Rows - n));
            int cell = map.CellSizeMm;
            return new RectMm(x0 * cell, y0 * cell, (x0 + n) * cell, (y0 + n) * cell);
        }

        private static void ForEach(MapData map, CellRect cells, Action<int> visit)
        {
            for (int y = cells.MinY; y <= cells.MaxY; y++)
            {
                for (int x = cells.MinX; x <= cells.MaxX; x++)
                {
                    visit(y * map.Columns + x);
                }
            }
        }

        /// <summary>Every cell whose centre lies within <paramref name="reach"/> of the polyline, with that distance.</summary>
        private static void ForEachNear(
            MapData map,
            PlannedPiece piece,
            long reach,
            Action<int, long> visit
        )
        {
            int[] p = piece.PointsMm;
            long minX = long.MaxValue;
            long minY = long.MaxValue;
            long maxX = long.MinValue;
            long maxY = long.MinValue;
            for (int k = 0; k < p.Length; k += 2)
            {
                minX = System.Math.Min(minX, p[k]);
                maxX = System.Math.Max(maxX, p[k]);
                minY = System.Math.Min(minY, p[k + 1]);
                maxY = System.Math.Max(maxY, p[k + 1]);
            }
            int cell = map.CellSizeMm;
            int x0 = (int)System.Math.Max(0, (minX - reach) / cell);
            int x1 = (int)System.Math.Min(map.Columns - 1, (maxX + reach) / cell);
            int y0 = (int)System.Math.Max(0, (minY - reach) / cell);
            int y1 = (int)System.Math.Min(map.Rows - 1, (maxY + reach) / cell);
            long reachSq = reach * reach;
            for (int y = y0; y <= y1; y++)
            {
                long cy = (long)y * cell + cell / 2;
                for (int x = x0; x <= x1; x++)
                {
                    long cx = (long)x * cell + cell / 2;
                    long best = long.MaxValue;
                    for (int k = 0; k + 3 < p.Length; k += 2)
                    {
                        best = System.Math.Min(
                            best,
                            SegmentDistanceSq(cx, cy, p[k], p[k + 1], p[k + 2], p[k + 3])
                        );
                    }
                    if (best <= reachSq)
                    {
                        visit(y * map.Columns + x, IntMath.Sqrt((ulong)best));
                    }
                }
            }
        }

        /// <summary>
        /// Squared mm from a point to a segment. The placement checks keep every segment under the loader's longest
        /// piece (1 km), so with r &lt; lengthSq the product dx × r stays below 1e6 × 1e12; squares of on-map distances
        /// are far inside a long.
        /// </summary>
        internal static long SegmentDistanceSq(long px, long py, long ax, long ay, long bx, long by)
        {
            long dx = bx - ax;
            long dy = by - ay;
            long lengthSq = dx * dx + dy * dy;
            long t = lengthSq == 0 ? 0 : (px - ax) * dx + (py - ay) * dy;
            long ex;
            long ey;
            if (t <= 0)
            {
                ex = px - ax;
                ey = py - ay;
            }
            else if (t >= lengthSq)
            {
                ex = px - bx;
                ey = py - by;
            }
            else
            {
                // The foot of the perpendicular, rounded toward zero the same way everywhere. t / lengthSq is split into
                // quotient and remainder so no product exceeds |d| × lengthSq, whatever the segment's length.
                long q = t / lengthSq;
                long r = t % lengthSq;
                ex = px - (ax + dx * q + dx * r / lengthSq);
                ey = py - (ay + dy * q + dy * r / lengthSq);
            }
            return ex * ex + ey * ey;
        }
    }
}
