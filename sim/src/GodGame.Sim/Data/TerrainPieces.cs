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
            int litresPerSecond,
            int radiusMm = 0,
            int animals = 0
        )
        {
            RadiusMm = radiusMm;
            Animals = animals;
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

        /// <summary>A grove's or a herd's circle radius (its one point is the centre); 0 for other kinds.</summary>
        public int RadiusMm { get; }

        /// <summary>A herd's animals; 0 for other kinds.</summary>
        public int Animals { get; }
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
        public static MapData Apply(
            MapData map,
            IReadOnlyList<PlannedPiece> pieces,
            Ruleset? rules = null
        )
        {
            bool mapDecks = map.StoneBridges.Count > 0 || map.FrozenPonds.Count > 0;
            if (pieces.Count == 0 && (rules == null || !mapDecks))
            {
                return map;
            }
            int[] original = map.HeightMm;
            int[] heights = (int[])original.Clone();
            var blocked = new bool[heights.Length];
            var sources = new List<WaterSource>(map.WaterSources);
            var boulders = new List<BoulderSpec>();
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
                    var under = new List<int>();
                    var ground = new List<int>();
                    ForEach(
                        map,
                        cells,
                        i =>
                        {
                            under.Add(i);
                            ground.Add(heights[i]);
                        }
                    );
                    boulders.Add(new BoulderSpec(piece.Id, under.ToArray(), ground.ToArray()));
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
            // Lines last (S2 13.3): they edit no height and only set each class's crossing speed, the slowest line
            // winning where two cross.
            int[]? speed = null;
            foreach (var piece in pieces)
            {
                if (piece.Rules.Kind != PieceKind.Line)
                {
                    continue;
                }
                if (speed == null)
                {
                    speed = new int[heights.Length];
                    for (int i = 0; i < speed.Length; i++)
                    {
                        speed[i] = FieldSpeed.AllDry;
                    }
                }
                int codes = 0;
                for (int c = 0; c < FieldSpeed.Classes; c++)
                {
                    codes |= FieldSpeed.CodeForPermille(piece.Rules.CrossingPermille[c]) << (c * 8);
                }
                var stamped = speed;
                ForEachNear(
                    map,
                    piece,
                    HalfFootprint(map, piece),
                    (i, d) => stamped[i] = FieldSpeed.Combine(stamped[i], codes)
                );
            }
            // Trees (S2 13.1): trunk cells and a grove's woodland set crossing codes like lines; all of it, and hedgerows,
            // is fuel for Lightning v0's fire. Herds are only recorded: their animals deploy as units.
            var trees = new List<TreeSpec>();
            var herds = new List<HerdSpec>();
            bool[]? fuel = null;
            foreach (var piece in pieces)
            {
                if (piece.Rules.Kind == PieceKind.Herd)
                {
                    herds.Add(
                        new HerdSpec(
                            piece.Id,
                            piece.PointsMm[0],
                            piece.PointsMm[1],
                            piece.RadiusMm,
                            piece.Animals
                        )
                    );
                    continue;
                }
                bool hedge =
                    piece.Rules.Kind == PieceKind.Line
                    && string.Equals(piece.Rules.Material, "hedge", StringComparison.Ordinal);
                if (piece.Rules.Kind != PieceKind.Trees && !hedge)
                {
                    continue;
                }
                fuel ??= new bool[heights.Length];
                if (hedge)
                {
                    var burns = fuel;
                    ForEachNear(map, piece, HalfFootprint(map, piece), (i, d) => burns[i] = true);
                    continue;
                }
                if (speed == null)
                {
                    speed = new int[heights.Length];
                    for (int i = 0; i < speed.Length; i++)
                    {
                        speed[i] = FieldSpeed.AllDry;
                    }
                }
                int trunk = Codes(piece.Rules.TrunkPermille);
                var trunks = TreeCells(map, piece);
                for (int k = 0; k < trunks.Count; k++)
                {
                    int i = trunks[k];
                    speed[i] = FieldSpeed.Combine(speed[i], trunk);
                    fuel[i] = true;
                    trees.Add(new TreeSpec(i, TreeHeight(piece, k), trees.Count));
                }
                if (piece.Rules.WoodlandPermille != null)
                {
                    int wood = Codes(piece.Rules.WoodlandPermille);
                    foreach (int i in CircleCells(map, piece))
                    {
                        fuel[i] = true;
                        if (!trunks.Contains(i))
                        {
                            speed[i] = FieldSpeed.Combine(speed[i], wood);
                        }
                    }
                }
            }
            // Decks last (S2 8.1): the map's stone bridges and ponds, then the plan's bridges over the finished ground.
            var decks =
                rules == null
                    ? null
                    : DeckLayout.Build(
                        map,
                        heights,
                        pieces,
                        rules.Structures,
                        rules.Fields.Water?.DryDepthUm ?? 1
                    );
            return map.WithTerrain(
                heights,
                blocked,
                sources.ToArray(),
                speed,
                decks,
                boulders.ToArray(),
                trees.ToArray(),
                fuel,
                herds.ToArray()
            );
        }

        private static int Codes(int[] permille)
        {
            int codes = 0;
            for (int c = 0; c < FieldSpeed.Classes; c++)
            {
                codes |= FieldSpeed.CodeForPermille(permille[c]) << (c * 8);
            }
            return codes;
        }

        /// <summary>A stable hash of a piece id and a number: what "seeded" means for pieces, which the plan alone decides.</summary>
        private static uint PieceHash(string id, int k)
        {
            var hash = new SimHash();
            hash.AppendInt32(id.Length);
            foreach (char c in id)
            {
                hash.AppendInt32(c);
            }
            hash.AppendInt32(k);
            return unchecked((uint)hash.Digest());
        }

        /// <summary>Tree <paramref name="k"/>'s height, seeded by the piece's id between the piece's limits.</summary>
        private static int TreeHeight(PlannedPiece piece, int k)
        {
            var rules = piece.Rules;
            uint span = (uint)(rules.TreeMaxHeightMm - rules.TreeMinHeightMm + 1);
            return rules.TreeMinHeightMm + (int)(PieceHash(piece.Id, k + 1) % span);
        }

        /// <summary>Cells whose centre lies within a circle piece's radius of its centre, row by row.</summary>
        public static List<int> CircleCells(MapData map, PlannedPiece piece)
        {
            var cells = new List<int>();
            long cx = piece.PointsMm[0];
            long cy = piece.PointsMm[1];
            long r = piece.RadiusMm;
            int cell = map.CellSizeMm;
            int x0 = (int)System.Math.Max(0, (cx - r) / cell);
            int x1 = (int)System.Math.Min(map.Columns - 1, (cx + r) / cell);
            int y0 = (int)System.Math.Max(0, (cy - r) / cell);
            int y1 = (int)System.Math.Min(map.Rows - 1, (cy + r) / cell);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    long dx = (long)x * cell + cell / 2 - cx;
                    long dy = (long)y * cell + cell / 2 - cy;
                    if (dx * dx + dy * dy <= r * r)
                    {
                        cells.Add(y * map.Columns + x);
                    }
                }
            }
            return cells;
        }

        /// <summary>
        /// A tree piece's trunk cells, in a fixed order (S2 13.1): a grove's on a grid of blocks treeEveryCells square,
        /// offset by a hash of its id; a row's every treeSpacingMm along it from its first point.
        /// </summary>
        public static List<int> TreeCells(MapData map, PlannedPiece piece)
        {
            var cells = new List<int>();
            var rules = piece.Rules;
            if (rules.IsCircle)
            {
                int n = System.Math.Max(1, rules.TreeEveryCells);
                uint h = PieceHash(piece.Id, 0);
                int ox = (int)(h % (uint)n);
                int oy = (int)((h / (uint)n) % (uint)n);
                foreach (int i in CircleCells(map, piece))
                {
                    int x = i % map.Columns;
                    int y = i / map.Columns;
                    if ((x + n - ox) % n == 0 && (y + n - oy) % n == 0)
                    {
                        cells.Add(i);
                    }
                }
                return cells;
            }
            int[] p = piece.PointsMm;
            long carry = 0; // distance along the line to the next tree, from the start of this segment
            for (int k = 0; k + 3 < p.Length; k += 2)
            {
                long dx = p[k + 2] - p[k];
                long dy = p[k + 3] - p[k + 1];
                long length = IntMath.Sqrt((ulong)(dx * dx + dy * dy));
                long at = carry;
                for (; at <= length; at += rules.TreeSpacingMm)
                {
                    long x = length == 0 ? p[k] : p[k] + dx * at / length;
                    long y = length == 0 ? p[k + 1] : p[k + 1] + dy * at / length;
                    int i =
                        (int)System.Math.Min(map.Rows - 1, y / map.CellSizeMm) * map.Columns
                        + (int)System.Math.Min(map.Columns - 1, x / map.CellSizeMm);
                    if (!cells.Contains(i))
                    {
                        cells.Add(i);
                    }
                }
                carry = at - length;
            }
            return cells;
        }

        /// <summary>
        /// How far from a line's centre a cell's centre may lie and still be one of its cells: half a cell's diagonal,
        /// rounded up. Every cell the line passes through is within it, so a diagonal line's cells touch edge to edge and
        /// leave no corner a horse could slip through; a line along the grid stays one cell wide.
        /// </summary>
        private static long LineReach(MapData map) => ((long)map.CellSizeMm * 7072 + 9999) / 10000;

        /// <summary>The cells a piece covers, for the buffer and overlap checks (the whole footprint, S1 9.1).</summary>
        public static List<int> Footprint(MapData map, PlannedPiece piece)
        {
            var cells = new List<int>();
            switch (piece.Rules.Kind)
            {
                case PieceKind.Boulder:
                    ForEach(map, BoulderCells(map, piece), cells.Add);
                    break;
                case PieceKind.Bridge:
                    cells.AddRange(DeckLayout.BridgeFootprint(map, piece));
                    break;
                case PieceKind.Trees when piece.Rules.IsCircle:
                case PieceKind.Herd:
                    cells.AddRange(CircleCells(map, piece));
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
                case PieceKind.Line:
                case PieceKind.Trees:
                    return LineReach(map);
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
