using System;
using System.Collections.Generic;
using GodGame.Sim.Rules;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim
{
    /// <summary>
    /// Reads the plan's terrain layer and refuses placements that break the rules every piece shares (spec S1, sections
    /// 9.1 to 9.5): sizes within the piece's limits and on the map; the whole footprint at least the buffer (100 m)
    /// from both deployment zones (brief, 2026-10-08); no boulder on a boulder and no spring on one; and no full trap
    /// (brief, 2026-10-09): if the map alone keeps every guaranteed route but the map with the pieces doesn't, the plan
    /// is refused, naming the failing route. Everything here is planning: none of it is a fizzle.
    /// </summary>
    public static class TerrainPlacement
    {
        /// <summary>The pieces of a plan's terrain layer, in ordinal id order; throws on the first piece that can't be placed.</summary>
        public static PlannedPiece[] Read(JsonValue layer, MapData map, Ruleset rules)
        {
            var pieces = new List<PlannedPiece>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in layer.Items)
            {
                string id = DataIds.Require(item["id"]);
                if (!ids.Add(id))
                {
                    throw new DataFormatException(
                        item.Path + ": ids must be unique and not empty (\"" + id + "\")."
                    );
                }
                string pieceId = item.Str("piece");
                var piece =
                    rules.Terrain.Piece(pieceId)
                    ?? throw new DataFormatException(
                        item.Path + ": no piece \"" + pieceId + "\" in terrain.json."
                    );
                pieces.Add(ReadOne(item, id, piece, map));
            }
            // Ids are unique, so the ordinal sort is total.
            pieces.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            var result = pieces.ToArray();
            CheckFootprints(result, map, rules);
            CheckRoutes(result, map, rules);
            return result;
        }

        private static PlannedPiece ReadOne(
            JsonValue item,
            string id,
            PieceRules rules,
            MapData map
        )
        {
            switch (rules.Kind)
            {
                case PieceKind.Spring:
                    item.OnlyKeys("id", "piece", "xMm", "yMm", "litresPerSecond");
                    CheckFits(item, rules.SourceBlockCells, map);
                    return new PlannedPiece(
                        id,
                        rules,
                        Point(item, map),
                        0,
                        0,
                        0,
                        item.Int(
                            "litresPerSecond",
                            rules.MinDischargeLPerS,
                            rules.MaxDischargeLPerS
                        )
                    );
                case PieceKind.Bridge:
                    return ReadBridge(item, id, rules, map);
                case PieceKind.Boulder:
                    item.OnlyKeys("id", "piece", "xMm", "yMm");
                    CheckFits(item, System.Math.Max(1, rules.SizeMm / map.CellSizeMm), map);
                    return new PlannedPiece(id, rules, Point(item, map), 0, 0, 0, 0);
            }
            string size = rules.Kind == PieceKind.Ridge ? "heightMm" : "depthMm";
            if (rules.Kind == PieceKind.Line)
            {
                item.OnlyKeys("id", "piece", "points");
            }
            else if (rules.Kind == PieceKind.Channel)
            {
                item.OnlyKeys("id", "piece", "points", "widthMm", "depthMm");
            }
            else
            {
                item.OnlyKeys("id", "piece", "points", size);
            }
            var points = item.Array("points");
            if (points.Count < rules.MinPoints || points.Count > rules.MaxPoints)
            {
                throw new DataFormatException(
                    item.Path
                        + ".points: "
                        + rules.MinPoints
                        + " to "
                        + rules.MaxPoints
                        + " points for a "
                        + rules.Id
                        + "."
                );
            }
            var xy = new int[points.Count * 2];
            long length = 0;
            for (int k = 0; k < points.Count; k++)
            {
                var pair = points[k].Items;
                if (pair.Count != 2)
                {
                    throw new DataFormatException(points[k].Path + ": a point is [xMm, yMm].");
                }
                long x = pair[0].AsInteger;
                long y = pair[1].AsInteger;
                if (x < 0 || y < 0 || x >= map.WidthMm || y >= map.DepthMm)
                {
                    throw new DataFormatException(points[k].Path + ": the point is off the map.");
                }
                xy[2 * k] = (int)x;
                xy[2 * k + 1] = (int)y;
                if (k > 0)
                {
                    long dx = x - xy[2 * k - 2];
                    long dy = y - xy[2 * k - 1];
                    length += GodGame.Sim.Math.IntMath.Sqrt((ulong)(dx * dx + dy * dy));
                }
            }
            if (length < rules.MinLengthMm || length > rules.MaxLengthMm)
            {
                throw new DataFormatException(
                    item.Path
                        + ".points: a "
                        + rules.Id
                        + " is "
                        + rules.MinLengthMm
                        + " to "
                        + rules.MaxLengthMm
                        + " mm long, this one "
                        + length
                        + "."
                );
            }
            switch (rules.Kind)
            {
                case PieceKind.Channel:
                    return new PlannedPiece(
                        id,
                        rules,
                        xy,
                        item.Int("widthMm", rules.MinWidthMm, rules.MaxWidthMm),
                        item.Int("depthMm", rules.MinDepthMm, rules.MaxDepthMm),
                        0,
                        0
                    );
                case PieceKind.Ridge:
                    return new PlannedPiece(
                        id,
                        rules,
                        xy,
                        0,
                        0,
                        item.Int("heightMm", rules.MinHeightMm, rules.MaxHeightMm),
                        0
                    );
                case PieceKind.Line:
                    return new PlannedPiece(id, rules, xy, 0, 0, 0, 0);
                default:
                    return new PlannedPiece(
                        id,
                        rules,
                        xy,
                        0,
                        item.Int("depthMm", rules.MinDepthMm, rules.MaxDepthMm),
                        0,
                        0
                    );
            }
        }

        /// <summary>
        /// A bridge (spec S2 13.4): two bank points on dry ground (tick-0 water below the bank threshold), a span in
        /// range and a width its structure kind allows.
        /// </summary>
        private static PlannedPiece ReadBridge(
            JsonValue item,
            string id,
            PieceRules rules,
            MapData map
        )
        {
            item.OnlyKeys("id", "piece", "points", "widthMm");
            var points = item.Array("points");
            if (points.Count != 2)
            {
                throw new DataFormatException(item.Path + ".points: a bridge has two bank points.");
            }
            var xy = new int[4];
            for (int k = 0; k < 2; k++)
            {
                var pair = points[k].Items;
                if (pair.Count != 2)
                {
                    throw new DataFormatException(points[k].Path + ": a point is [xMm, yMm].");
                }
                long x = pair[0].AsInteger;
                long y = pair[1].AsInteger;
                if (x < 0 || y < 0 || x >= map.WidthMm || y >= map.DepthMm)
                {
                    throw new DataFormatException(points[k].Path + ": the point is off the map.");
                }
                xy[2 * k] = (int)x;
                xy[2 * k + 1] = (int)y;
                int cell = (int)(y / map.CellSizeMm) * map.Columns + (int)(x / map.CellSizeMm);
                foreach (var layer in map.InitialFields)
                {
                    if (
                        layer.Key == WaterModel.Depth
                        && layer.Value[cell] / 1000 >= rules.BankMinAboveWaterMm
                    )
                    {
                        throw new DataFormatException(
                            points[k].Path
                                + ": a bank point stands in the water; put it on the dry bank."
                        );
                    }
                }
            }
            long dx = xy[2] - (long)xy[0];
            long dy = xy[3] - (long)xy[1];
            long span = GodGame.Sim.Math.IntMath.Sqrt((ulong)(dx * dx + dy * dy));
            if (span < rules.MinSpanMm || span > rules.MaxSpanMm)
            {
                throw new DataFormatException(
                    item.Path
                        + ".points: a "
                        + rules.Id
                        + " spans "
                        + rules.MinSpanMm
                        + " to "
                        + rules.MaxSpanMm
                        + " mm, this one "
                        + span
                        + "."
                );
            }
            var kind = rules.Structure!;
            return new PlannedPiece(
                id,
                rules,
                xy,
                item.Int("widthMm", kind.MinWidthMm, kind.MaxWidthMm),
                0,
                0,
                0
            );
        }

        private static int[] Point(JsonValue item, MapData map)
        {
            return new[]
            {
                item.Int("xMm", 0, map.WidthMm - 1),
                item.Int("yMm", 0, map.DepthMm - 1),
            };
        }

        /// <summary>A boulder or spring block wider than the map can't be placed on it.</summary>
        private static void CheckFits(JsonValue item, int cells, MapData map)
        {
            if (cells > map.Columns || cells > map.Rows)
            {
                throw new DataFormatException(item.Path + ": the piece is wider than the map.");
            }
        }

        private static void CheckFootprints(PlannedPiece[] pieces, MapData map, Ruleset rules)
        {
            long buffer = rules.Terrain.BufferFromDeploymentMm;
            var boulders = new bool[map.Columns * map.Rows];
            // Decks may not overlap (the audit's empty segments): the map's own and each bridge already placed.
            var decked = new bool[map.Columns * map.Rows];
            var mapDecks = TerrainPieces.Apply(map, Array.Empty<PlannedPiece>(), rules).Decks;
            if (mapDecks != null)
            {
                for (int i = 0; i < decked.Length; i++)
                {
                    decked[i] = mapDecks.SegmentOfCell[i] >= 0 || mapDecks.ApproachMask[i] != 0;
                }
            }
            int[]? water = null;
            foreach (var layer in map.InitialFields)
            {
                if (layer.Key == WaterModel.Depth)
                {
                    water = layer.Value;
                }
            }
            int dry = rules.Fields.Water?.DryDepthUm ?? int.MaxValue;
            // Boulders first (in id order), so springs can be checked against all of them.
            foreach (
                var kind in new[]
                {
                    PieceKind.Boulder,
                    PieceKind.Channel,
                    PieceKind.Ridge,
                    PieceKind.Hollow,
                    PieceKind.Spring,
                    PieceKind.Line,
                    PieceKind.Bridge,
                }
            )
            {
                foreach (var piece in pieces)
                {
                    if (piece.Rules.Kind != kind)
                    {
                        continue;
                    }
                    foreach (int i in TerrainPieces.Footprint(map, piece))
                    {
                        int x = i % map.Columns;
                        int y = i / map.Columns;
                        foreach (var zone in map.DeploymentZones)
                        {
                            if (GapSquared(map, x, y, zone.Area) < buffer * buffer)
                            {
                                throw new DataFormatException(
                                    "plan.json: terrain piece \""
                                        + piece.Id
                                        + "\" comes within "
                                        + buffer
                                        + " mm of the "
                                        + zone.Side
                                        + " deployment zone."
                                );
                            }
                        }
                        if (
                            water != null
                            && water[i] >= dry
                            && kind
                                is PieceKind.Ridge
                                    or PieceKind.Hollow
                                    or PieceKind.Spring
                                    or PieceKind.Line
                        )
                        {
                            // S1 D5: dams are their own piece; a spring in a river adds little.
                            throw new DataFormatException(
                                "plan.json: "
                                    + piece.Rules.Id
                                    + " \""
                                    + piece.Id
                                    + "\" stands in the map's water; place it on dry ground."
                            );
                        }
                        if (kind == PieceKind.Bridge)
                        {
                            if (decked[i] || boulders[i])
                            {
                                throw new DataFormatException(
                                    "plan.json: bridge \""
                                        + piece.Id
                                        + "\" overlaps another deck or a boulder."
                                );
                            }
                            decked[i] = true;
                        }
                        if (kind == PieceKind.Boulder)
                        {
                            if (boulders[i])
                            {
                                throw new DataFormatException(
                                    "plan.json: boulder \""
                                        + piece.Id
                                        + "\" stands on another boulder."
                                );
                            }
                            boulders[i] = true;
                        }
                        else if (
                            (kind == PieceKind.Spring || kind == PieceKind.Line) && boulders[i]
                        )
                        {
                            throw new DataFormatException(
                                "plan.json: "
                                    + piece.Rules.Id
                                    + " \""
                                    + piece.Id
                                    + "\" is on a boulder."
                            );
                        }
                    }
                }
            }
        }

        /// <summary>Squared mm between a cell and a zone (0 when they touch or overlap).</summary>
        private static long GapSquared(MapData map, int x, int y, RectMm zone)
        {
            long cell = map.CellSizeMm;
            long minX = x * cell;
            long minY = y * cell;
            long dx = System.Math.Max(
                0,
                System.Math.Max(zone.MinX - (minX + cell), minX - zone.MaxX)
            );
            long dy = System.Math.Max(
                0,
                System.Math.Max(zone.MinY - (minY + cell), minY - zone.MaxY)
            );
            return dx * dx + dy * dy;
        }

        private static void CheckRoutes(PlannedPiece[] pieces, MapData map, Ruleset rules)
        {
            bool shapesGround = false;
            foreach (var piece in pieces)
            {
                shapesGround |= piece.Rules.Kind != PieceKind.Spring; // a spring's water is battle-made (S1 D4)
            }
            if (!shapesGround || map.DeploymentZones.Count < 2)
            {
                return;
            }
            var runner = new SerialRunner();
            var with = Check(TerrainPieces.Apply(map, pieces, rules), rules, runner);
            if (with.Ok)
            {
                return;
            }
            // A map that fails on its own is the author's problem, not this plan's.
            if (
                !Check(
                    TerrainPieces.Apply(map, Array.Empty<PlannedPiece>(), rules),
                    rules,
                    runner
                ).Ok
            )
            {
                return;
            }
            var failure = with.FirstFailure!.Value;
            throw new DataFormatException(
                "plan.json: the terrain pieces "
                    + (failure.Reached ? "make the route too long" : "close the last route")
                    + " for "
                    + failure.Side
                    + " "
                    + failure.MovementClass
                    + " to "
                    + failure.Target
                    + " (brief, 2026-10-09: no placement may fully trap either army)."
            );
        }

        private static ReachabilityResult Check(MapData map, Ruleset rules, IParallelRunner runner)
        {
            var costs = new CostFields(new Terrain(map), rules, runner);
            return new Reachability(costs, map, rules.Routes).Check(runner);
        }
    }
}
