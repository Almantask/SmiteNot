using System;
using System.Collections.Generic;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>A map-authored stone bridge (spec S2 13.6): its two ends, its width and its deck height.</summary>
    public readonly struct StoneBridgeSpec
    {
        public StoneBridgeSpec(int x0, int y0, int x1, int y1, int widthMm, int deckHeightMm)
        {
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
            WidthMm = widthMm;
            DeckHeightMm = deckHeightMm;
        }

        public int X0 { get; }
        public int Y0 { get; }
        public int X1 { get; }
        public int Y1 { get; }
        public int WidthMm { get; }
        public int DeckHeightMm { get; }
    }

    /// <summary>A placed boulder (spec S1 9.1): its cells and the ground under it, for Engineers to clear (S2 14.7).</summary>
    public sealed class BoulderSpec
    {
        internal BoulderSpec(string id, int[] cells, int[] groundHeights)
        {
            Id = id;
            Cells = cells;
            GroundHeights = groundHeights;
        }

        public string Id { get; }

        public int[] Cells { get; }

        /// <summary>Each cell's height before the boulder was set on it.</summary>
        public int[] GroundHeights { get; }
    }

    /// <summary>A map-authored frozen pond (spec S2 13.6): a rectangle of ice of one thickness over the map's water.</summary>
    public readonly struct FrozenPondSpec
    {
        public FrozenPondSpec(RectMm area, int thicknessMm)
        {
            Area = area;
            ThicknessMm = thicknessMm;
        }

        public RectMm Area { get; }

        public int ThicknessMm { get; }
    }

    /// <summary>
    /// One deck segment (spec S2 8.1): a bridge's 2-cell stretch or a 4 × 4-cell ice patch. Its cells, the classes it
    /// carries, its strength when sound and its neighbours (the segments that lose strength when it fails).
    /// </summary>
    public sealed class DeckSegment
    {
        internal DeckSegment(
            StructureKindRules kind,
            int[] cells,
            long maxStrengthKg,
            int deckHeightMm
        )
        {
            Kind = kind;
            Cells = cells;
            MaxStrengthKg = maxStrengthKg;
            DeckHeightMm = deckHeightMm;
        }

        public StructureKindRules Kind { get; }

        public int[] Cells { get; }

        /// <summary>Deck area × kg/m² (ice: × thickness); the load it holds at 1,000‰ strength.</summary>
        public long MaxStrengthKg { get; }

        /// <summary>The deck's surface; ice decks sit on their pond and report 0 (units on intact ice are on no water).</summary>
        public int DeckHeightMm { get; }

        public int[] Neighbours { get; internal set; } = Array.Empty<int>();
    }

    /// <summary>
    /// The battle's decks (spec S2 8.1), derived from the map's stone bridges and frozen ponds and the plan's bridges:
    /// every segment in a fixed order (map bridges, map ponds row by row, then plan bridges in plan order), each cell's
    /// segment (-1 for none) and the approach cells at bridge ends, flat for the classes the bridge carries. Whether a
    /// segment still stands is battle state (<see cref="StructureStates"/>); this layout never changes.
    /// </summary>
    public sealed class DeckLayout
    {
        private DeckLayout(DeckSegment[] segments, int[] segmentOfCell, byte[] approach)
        {
            Segments = segments;
            SegmentOfCell = segmentOfCell;
            ApproachMask = approach;
        }

        public IReadOnlyList<DeckSegment> Segments { get; }

        /// <summary>Per cell, the segment whose deck covers it, or -1.</summary>
        public int[] SegmentOfCell { get; }

        /// <summary>Per cell, the class bits of a bridge whose approach this is (0 for none).</summary>
        public byte[] ApproachMask { get; }

        /// <summary>Builds the layout for a played map; null when there is no deck at all.</summary>
        internal static DeckLayout? Build(
            MapData map,
            int[] heights,
            IReadOnlyList<PlannedPiece> pieces,
            StructureRules rules,
            int dryDepthUm
        )
        {
            var segments = new List<DeckSegment>();
            int cells = map.Columns * map.Rows;
            var segmentOf = new int[cells];
            for (int i = 0; i < cells; i++)
            {
                segmentOf[i] = -1;
            }
            var approach = new byte[cells];
            var neighbours = new List<List<int>>();
            var stone = rules.Kind("stone-bridge");
            foreach (var bridge in map.StoneBridges)
            {
                if (stone == null)
                {
                    throw new InvalidOperationException(
                        "The map has stone bridges but load.json has no stone-bridge kind."
                    );
                }
                AddBridge(
                    map,
                    stone,
                    bridge.X0,
                    bridge.Y0,
                    bridge.X1,
                    bridge.Y1,
                    bridge.WidthMm,
                    bridge.DeckHeightMm,
                    0,
                    segments,
                    neighbours,
                    segmentOf,
                    approach
                );
            }
            var ice = FindIce(rules);
            int[]? water = null;
            foreach (var layer in map.InitialFields)
            {
                if (layer.Key == WaterModel.Depth)
                {
                    water = layer.Value;
                }
            }
            foreach (var pond in map.FrozenPonds)
            {
                if (ice == null)
                {
                    throw new InvalidOperationException(
                        "The map has frozen ponds but load.json has no ice kind."
                    );
                }
                AddPond(map, ice, pond, water, dryDepthUm, segments, neighbours, segmentOf);
            }
            foreach (var piece in pieces)
            {
                if (piece.Rules.Kind != PieceKind.Bridge)
                {
                    continue;
                }
                int[] p = piece.PointsMm;
                int deck = System.Math.Max(
                    Height(map, heights, p[0], p[1]),
                    Height(map, heights, p[2], p[3])
                );
                AddBridge(
                    map,
                    piece.Rules.Structure!,
                    p[0],
                    p[1],
                    p[2],
                    p[3],
                    piece.WidthMm,
                    deck,
                    piece.Rules.FootprintEndCells,
                    segments,
                    neighbours,
                    segmentOf,
                    approach
                );
            }
            if (segments.Count == 0)
            {
                bool anyApproach = false;
                foreach (byte b in approach)
                {
                    anyApproach |= b != 0;
                }
                if (!anyApproach)
                {
                    return null;
                }
            }
            for (int s = 0; s < segments.Count; s++)
            {
                neighbours[s].Sort();
                segments[s].Neighbours = neighbours[s].ToArray();
            }
            return new DeckLayout(segments.ToArray(), segmentOf, approach);
        }

        private static StructureKindRules? FindIce(StructureRules rules)
        {
            foreach (var k in rules.Kinds)
            {
                if (k.IsIce)
                {
                    return k;
                }
            }
            return null;
        }

        private static int Height(MapData map, int[] heights, int xMm, int yMm) =>
            heights[
                System.Math.Min(map.Rows - 1, yMm / map.CellSizeMm) * map.Columns
                    + System.Math.Min(map.Columns - 1, xMm / map.CellSizeMm)
            ];

        /// <summary>
        /// The cells of a bridge from (x0, y0) to (x1, y1), <paramref name="widthMm"/> wide: a cell whose centre lies within
        /// half the width of the line, its projection between the ends, is deck, in the segment its projection falls in;
        /// within <paramref name="endCells"/> cells past either end, it is approach. Cells already taken stay with the first.
        /// </summary>
        private static void AddBridge(
            MapData map,
            StructureKindRules kind,
            long x0,
            long y0,
            long x1,
            long y1,
            int widthMm,
            int deckHeightMm,
            int endCells,
            List<DeckSegment> segments,
            List<List<int>> neighbours,
            int[] segmentOf,
            byte[] approach
        )
        {
            int cell = map.CellSizeMm;
            long dx = x1 - x0;
            long dy = y1 - y0;
            long span = (long)IntMath.Sqrt((ulong)(dx * dx + dy * dy));
            if (span == 0)
            {
                return;
            }
            long half = widthMm / 2;
            long ends = (long)endCells * cell;
            long reach = half + ends + cell;
            long segmentMm = (long)kind.SegmentCellsAlong * cell;
            // Whole segments only: a remainder shorter than a segment joins the last one, so no stub at the end is weaker
            // than the rest (strength scales with area, so the longer one holds proportionally more).
            int count = (int)System.Math.Max(1, span / segmentMm);
            var lists = new List<int>[count];
            for (int k = 0; k < count; k++)
            {
                lists[k] = new List<int>();
            }
            int cx0 = (int)System.Math.Max(0, (System.Math.Min(x0, x1) - reach) / cell);
            int cx1 = (int)
                System.Math.Min(map.Columns - 1, (System.Math.Max(x0, x1) + reach) / cell);
            int cy0 = (int)System.Math.Max(0, (System.Math.Min(y0, y1) - reach) / cell);
            int cy1 = (int)System.Math.Min(map.Rows - 1, (System.Math.Max(y0, y1) + reach) / cell);
            for (int y = cy0; y <= cy1; y++)
            {
                for (int x = cx0; x <= cx1; x++)
                {
                    long px = (long)x * cell + cell / 2 - x0;
                    long py = (long)y * cell + cell / 2 - y0;
                    long along = (px * dx + py * dy) / span; // mm along the span from the first end
                    long across = System.Math.Abs(px * dy - py * dx) / span;
                    if (across > half)
                    {
                        continue;
                    }
                    int i = y * map.Columns + x;
                    if (segmentOf[i] >= 0)
                    {
                        continue;
                    }
                    if (along >= 0 && along <= span)
                    {
                        lists[(int)System.Math.Min(count - 1, along / segmentMm)].Add(i);
                    }
                    else if (along >= -ends && along <= span + ends)
                    {
                        approach[i] = (byte)(approach[i] | kind.ClassMask);
                    }
                }
            }
            int first = segments.Count;
            for (int k = 0; k < count; k++)
            {
                if (lists[k].Count == 0)
                {
                    continue; // every cell already belongs to another deck: no empty segment
                }
                int s = segments.Count;
                long areaMm2 = (long)lists[k].Count * cell * cell;
                segments.Add(
                    new DeckSegment(
                        kind,
                        lists[k].ToArray(),
                        areaMm2 * kind.StrengthKgPerM2 / 1_000_000,
                        deckHeightMm
                    )
                );
                neighbours.Add(new List<int>());
                foreach (int i in lists[k])
                {
                    segmentOf[i] = s;
                    approach[i] = 0;
                }
                if (s > first)
                {
                    neighbours[s].Add(s - 1);
                    neighbours[s - 1].Add(s);
                }
            }
        }

        /// <summary>
        /// A frozen pond's patches: the map-aligned blocks of patchCells × patchCells cells, each holding the pond's cells
        /// over tick-0 water (all of them on a map without settled water); patches sharing an edge are neighbours.
        /// </summary>
        private static void AddPond(
            MapData map,
            StructureKindRules ice,
            FrozenPondSpec pond,
            int[]? water,
            int dryDepthUm,
            List<DeckSegment> segments,
            List<List<int>> neighbours,
            int[] segmentOf
        )
        {
            int cell = map.CellSizeMm;
            int n = ice.PatchCells;
            int x0 = System.Math.Max(0, pond.Area.MinX / cell);
            int y0 = System.Math.Max(0, pond.Area.MinY / cell);
            int x1 = System.Math.Min(map.Columns - 1, (pond.Area.MaxX - 1) / cell);
            int y1 = System.Math.Min(map.Rows - 1, (pond.Area.MaxY - 1) / cell);
            int bx0 = x0 / n;
            int by0 = y0 / n;
            int bx1 = x1 / n;
            int by1 = y1 / n;
            int columns = bx1 - bx0 + 1;
            var patchOf = new int[columns * (by1 - by0 + 1)];
            for (int by = by0; by <= by1; by++)
            {
                for (int bx = bx0; bx <= bx1; bx++)
                {
                    var list = new List<int>();
                    for (
                        int y = System.Math.Max(y0, by * n);
                        y <= System.Math.Min(y1, by * n + n - 1);
                        y++
                    )
                    {
                        for (
                            int x = System.Math.Max(x0, bx * n);
                            x <= System.Math.Min(x1, bx * n + n - 1);
                            x++
                        )
                        {
                            int i = y * map.Columns + x;
                            if (segmentOf[i] < 0 && (water == null || water[i] >= dryDepthUm))
                            {
                                list.Add(i);
                            }
                        }
                    }
                    int slot = (by - by0) * columns + (bx - bx0);
                    if (list.Count == 0)
                    {
                        patchOf[slot] = -1;
                        continue;
                    }
                    int s = segments.Count;
                    long areaMm2 = (long)list.Count * cell * cell;
                    segments.Add(
                        new DeckSegment(
                            ice,
                            list.ToArray(),
                            areaMm2 * pond.ThicknessMm * ice.IceKgPerM2PerMm / 1_000_000,
                            0
                        )
                    );
                    neighbours.Add(new List<int>());
                    foreach (int i in list)
                    {
                        segmentOf[i] = s;
                    }
                    patchOf[slot] = s;
                    if (bx > bx0 && patchOf[slot - 1] >= 0)
                    {
                        neighbours[s].Add(patchOf[slot - 1]);
                        neighbours[patchOf[slot - 1]].Add(s);
                    }
                    if (by > by0 && patchOf[slot - columns] >= 0)
                    {
                        neighbours[s].Add(patchOf[slot - columns]);
                        neighbours[patchOf[slot - columns]].Add(s);
                    }
                }
            }
        }

        /// <summary>A plan bridge's footprint for the buffer and placement checks: its deck and approach cells.</summary>
        internal static List<int> BridgeFootprint(MapData map, PlannedPiece piece)
        {
            int[] p = piece.PointsMm;
            var segments = new List<DeckSegment>();
            var neighbours = new List<List<int>>();
            int cells = map.Columns * map.Rows;
            var segmentOf = new int[cells];
            for (int i = 0; i < cells; i++)
            {
                segmentOf[i] = -1;
            }
            var approach = new byte[cells];
            AddBridge(
                map,
                piece.Rules.Structure!,
                p[0],
                p[1],
                p[2],
                p[3],
                piece.WidthMm,
                0,
                piece.Rules.FootprintEndCells,
                segments,
                neighbours,
                segmentOf,
                approach
            );
            var footprint = new List<int>();
            for (int i = 0; i < cells; i++)
            {
                if (segmentOf[i] >= 0 || approach[i] != 0)
                {
                    footprint.Add(i);
                }
            }
            return footprint;
        }
    }
}
