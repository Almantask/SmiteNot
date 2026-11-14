using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using GodGame.Sim.Math;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim
{
    /// <summary>An axis-aligned rectangle in millimetres, minimum inclusive and maximum exclusive.</summary>
    public readonly struct RectMm
    {
        public readonly int MinX;
        public readonly int MinY;
        public readonly int MaxX;
        public readonly int MaxY;

        public RectMm(int minX, int minY, int maxX, int maxY)
        {
            if (maxX <= minX || maxY <= minY)
            {
                throw new ArgumentException("A rectangle needs a positive size.");
            }
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public int Width => MaxX - MinX;

        public int Depth => MaxY - MinY;
    }

    /// <summary>A side's deployment zone, where its regiments start (brief: terrain pieces keep 100 m from these).</summary>
    /// <summary>
    /// Where water enters the map (spec S1, section 7): a constant discharge spread evenly over the cells of an area
    /// (usually a short line at the map edge).
    /// </summary>
    public readonly struct WaterSource
    {
        public readonly RectMm Area;
        public readonly int LitresPerSecond;

        public WaterSource(RectMm area, int litresPerSecond)
        {
            Area = area;
            LitresPerSecond = litresPerSecond;
        }
    }

    /// <summary>An area of the map with a named soil (spec S1, section 6: a firm road); elsewhere the rules' default soil.</summary>
    public readonly struct SoilArea
    {
        public readonly string Soil;
        public readonly RectMm Area;

        public SoilArea(string soil, RectMm area)
        {
            Soil = soil;
            Area = area;
        }
    }

    public readonly struct DeploymentZone
    {
        public readonly Side Side;
        public readonly RectMm Area;

        public DeploymentZone(Side side, RectMm area)
        {
            Side = side;
            Area = area;
        }
    }

    /// <summary>
    /// Map format v0 (plan, section 3.8): <c>map.json</c> with the size, the cell size and the deployment zones, plus
    /// an optional height layer (<c>height.bin</c>: int32 little-endian millimetres per cell, rows south to north,
    /// each west to east). Without a height layer the ground is flat at 0. Later versions add soil, vegetation,
    /// water and snow layers, climate and wind.
    /// </summary>
    public sealed class MapData
    {
        public const int FormatVersion = 0;

        /// <summary>30 km: inside the range where FixVec2 lengths and squared distances stay exact (about 32 km).</summary>
        public const int MaxSideMm = 30_000_000;

        /// <summary>16 million cells (a 1.2 × 0.8 km map at 1.5 m is about 430,000).</summary>
        public const long MaxCells = 16_000_000;

        /// <summary>
        /// The movement step's neighbour grid (SpatialHash, 2 m cells) holds at most 16 million cells too, so a map is
        /// at most about 64 km² (8 × 8 km), far above the working map (about 1 km²).
        /// </summary>
        public const int NeighbourGridCellMm = 2_000;

        /// <summary>The strongest prevailing wind a map may set (spec S2, section 12.4).</summary>
        public const int MaxWindMmPerS = 15_000;

        /// <summary>Ground heights within ±10 km.</summary>
        public const int MaxHeightMm = 10_000_000;

        private MapData(
            string id,
            int widthMm,
            int depthMm,
            int cellSizeMm,
            int[] heightMm,
            DeploymentZone[] zones,
            WaterSource[] sources,
            RectMm[] sinks,
            SoilArea[] soils,
            ulong hash
        )
        {
            SoilAreas = soils;
            WaterSources = sources;
            WaterSinks = sinks;
            Id = id;
            WidthMm = widthMm;
            DepthMm = depthMm;
            CellSizeMm = cellSizeMm;
            Columns = (widthMm + cellSizeMm - 1) / cellSizeMm;
            Rows = (depthMm + cellSizeMm - 1) / cellSizeMm;
            HeightMm = heightMm;
            DeploymentZones = zones;
            Hash = hash;
        }

        public string Id { get; }

        /// <summary>The map's prevailing wind toward +x (east), mm/s; 0 and 0 is calm (spec S2, D9). In the map hash with the rest of map.json.</summary>
        public int WindXMmPerS { get; private set; }

        /// <summary>The prevailing wind toward +y (north), mm/s.</summary>
        public int WindYMmPerS { get; private set; }

        public int WidthMm { get; }

        public int DepthMm { get; }

        /// <summary>Field cell edge; 1.5 m is the plan's shared value (section 8).</summary>
        public int CellSizeMm { get; }

        public int Columns { get; }

        public int Rows { get; }

        public Fix Width => Fix.FromRatio(WidthMm, 1000);

        public Fix Depth => Fix.FromRatio(DepthMm, 1000);

        /// <summary>Ground height per cell, row-major from the south-west corner.</summary>
        public int[] HeightMm { get; }

        public IReadOnlyList<DeploymentZone> DeploymentZones { get; }

        /// <summary>Soil areas in file order; a later area wins where two overlap.</summary>
        public IReadOnlyList<SoilArea> SoilAreas { get; }

        /// <summary>Constant discharges into the map, in file order.</summary>
        public IReadOnlyList<WaterSource> WaterSources { get; }

        /// <summary>Areas on the map edge where water leaves freely (S1, section 7); every other edge is closed.</summary>
        public IReadOnlyList<RectMm> WaterSinks { get; }

        /// <summary>Over the canonical map.json and the height layer: what replays record as the map hash.</summary>
        public ulong Hash { get; }

        /// <summary>
        /// Cells closed to every class whatever their slope (boulders, from the plan's terrain pieces), or null. Never in
        /// a map file: <see cref="WithTerrain"/> sets it.
        /// </summary>
        public bool[]? Blocked { get; private set; }

        /// <summary>
        /// Per cell, the crossing speed codes the plan's line pieces set (hedgerows, walls: <see cref="FieldSpeed"/>
        /// packing, <see cref="FieldSpeed.AllDry"/> where there is none), or null when no line was placed. Never in a map
        /// file: <see cref="WithTerrain"/> sets it.
        /// </summary>
        public int[]? PieceSpeed { get; private set; }

        /// <summary>
        /// The battle's decks (bridges and ice, spec S2 8.1), or null when there are none. Only on a played map:
        /// <see cref="TerrainPieces.Apply"/> builds them from the map's stone bridges and ponds and the plan's bridges.
        /// </summary>
        public DeckLayout? Decks { get; private set; }

        /// <summary>The plan's boulders in plan order, with the ground under each (on a played map only).</summary>
        public IReadOnlyList<BoulderSpec> Boulders { get; private set; } =
            Array.Empty<BoulderSpec>();

        /// <summary>Stone bridges the map authors (spec S2 13.6): ends, width and deck height.</summary>
        public IReadOnlyList<StoneBridgeSpec> StoneBridges { get; private set; } =
            Array.Empty<StoneBridgeSpec>();

        /// <summary>Frozen ponds the map authors (spec S2 13.6): a rectangle of ice of one thickness.</summary>
        public IReadOnlyList<FrozenPondSpec> FrozenPonds { get; private set; } =
            Array.Empty<FrozenPondSpec>();

        /// <summary>
        /// True for a map made by <see cref="WithTerrain"/>: it already carries a plan's pieces, so it must never be fed
        /// back into a battle or a plan, which would stamp the pieces twice under the same id and hash.
        /// </summary>
        public bool IsPlayed { get; private set; }

        /// <summary>
        /// The same map with the plan's terrain pieces stamped in (<see cref="TerrainPieces"/>): other heights, closed
        /// cells and extra water sources. The id and hash stay the base map's: the plan's hash covers the pieces.
        /// </summary>
        internal MapData WithTerrain(
            int[] heightMm,
            bool[] blocked,
            WaterSource[] sources,
            int[]? pieceSpeed = null,
            DeckLayout? decks = null,
            BoulderSpec[]? boulders = null
        )
        {
            if (pieceSpeed != null && pieceSpeed.Length != HeightMm.Length)
            {
                throw new ArgumentException("One piece speed code per cell.");
            }
            if (heightMm.Length != HeightMm.Length || blocked.Length != HeightMm.Length)
            {
                throw new ArgumentException("One height and one closed flag per cell.");
            }
            return new MapData(
                Id,
                WidthMm,
                DepthMm,
                CellSizeMm,
                heightMm,
                (DeploymentZone[])DeploymentZones,
                sources,
                (RectMm[])WaterSinks,
                (SoilArea[])SoilAreas,
                Hash
            )
            {
                Blocked = blocked,
                PieceSpeed = pieceSpeed,
                Decks = decks,
                Boulders = boulders ?? Array.Empty<BoulderSpec>(),
                IsPlayed = true,
                InitialFields = InitialFields,
                WindXMmPerS = WindXMmPerS,
                WindYMmPerS = WindYMmPerS,
                StoneBridges = StoneBridges,
                FrozenPonds = FrozenPonds,
            };
        }

        public DeploymentZone ZoneOf(Side side)
        {
            foreach (var zone in DeploymentZones)
            {
                if (zone.Side == side)
                {
                    return zone;
                }
            }
            throw new DataFormatException("Map " + Id + " has no " + side + " deployment zone.");
        }

        /// <summary>
        /// The map's starting field values (spec S1, section 7: the exporter settles the rivers offline, so every battle
        /// starts with them flowing), by field id, one value per cell in the field's unit. Empty when the map has no
        /// field layer: every field then starts at its rules' initial value.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, int[]>> InitialFields { get; private set; } =
            Array.Empty<KeyValuePair<string, int[]>>();

        /// <summary>The field layer's format (water.bin): "GGFL", version, count, then per field its id and values.</summary>
        public const int FieldLayerMagic = 0x4C464747;

        /// <summary>Writes a field layer for <see cref="Load"/> (little-endian; ids in ASCII).</summary>
        public static byte[] WriteFieldLayer(IReadOnlyList<KeyValuePair<string, int[]>> fields)
        {
            var writer = new ByteWriter(1024);
            writer.WriteInt32(FieldLayerMagic);
            writer.WriteInt32(0);
            writer.WriteInt32(fields.Count);
            foreach (var field in fields)
            {
                byte[] id = System.Text.Encoding.ASCII.GetBytes(field.Key);
                writer.WriteInt32(id.Length);
                foreach (byte b in id)
                {
                    writer.WriteByte(b);
                }
                writer.WriteInt32(field.Value.Length);
                foreach (int v in field.Value)
                {
                    writer.WriteInt32(v);
                }
            }
            return writer.ToArray();
        }

        private static KeyValuePair<string, int[]>[] ReadFieldLayer(byte[] layer, int cells)
        {
            var reader = new ByteReader(layer);
            if (reader.ReadInt32() != FieldLayerMagic || reader.ReadInt32() != 0)
            {
                throw new DataFormatException("water.bin: not a field layer of version 0.");
            }
            int count = reader.ReadInt32();
            if (count < 0 || count > 64)
            {
                throw new DataFormatException("water.bin: implausible field count.");
            }
            var fields = new KeyValuePair<string, int[]>[count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int f = 0; f < count; f++)
            {
                int length = reader.ReadInt32();
                if (length < 1 || length > 64)
                {
                    throw new DataFormatException("water.bin: a field id is empty or too long.");
                }
                var id = new byte[length];
                for (int i = 0; i < length; i++)
                {
                    id[i] = reader.ReadByte();
                    if (id[i] < 0x21 || id[i] > 0x7E)
                    {
                        throw new DataFormatException("water.bin: a field id is not plain ASCII.");
                    }
                }
                if (!ids.Add(System.Text.Encoding.ASCII.GetString(id)))
                {
                    throw new DataFormatException("water.bin: a field is listed twice.");
                }
                if (reader.ReadInt32() != cells)
                {
                    throw new DataFormatException(
                        "water.bin: a field has another number of cells than the map."
                    );
                }
                var values = new int[cells];
                for (int i = 0; i < cells; i++)
                {
                    values[i] = reader.ReadInt32();
                }
                fields[f] = new KeyValuePair<string, int[]>(
                    System.Text.Encoding.ASCII.GetString(id),
                    values
                );
            }
            if (!reader.AtEnd)
            {
                throw new DataFormatException("water.bin: trailing bytes.");
            }
            return fields;
        }

        public static MapData Load(
            string mapJson,
            byte[]? heightLayer = null,
            byte[]? fieldLayer = null
        )
        {
            var root = JsonReader.Parse(mapJson, "map.json");
            root.OnlyKeys(
                "$comment",
                "version",
                "id",
                "widthMm",
                "depthMm",
                "cellSizeMm",
                "deployment",
                "water",
                "soils",
                "wind",
                "stoneBridges",
                "frozenPonds"
            );
            if (root.Int("version") != FormatVersion)
            {
                throw new DataFormatException("map.json: version must be 0.");
            }
            string id = DataIds.Require(root["id"]);
            int width = root.Int("widthMm", 1_000, MaxSideMm);
            int depth = root.Int("depthMm", 1_000, MaxSideMm);
            int cell = root.Int("cellSizeMm", 100, 100_000);

            var zones = new List<DeploymentZone>();
            foreach (var item in root.Array("deployment"))
            {
                item.OnlyKeys("side", "minXMm", "minYMm", "maxXMm", "maxYMm");
                var side = SideNames.Parse(item["side"]);
                foreach (var existing in zones)
                {
                    if (existing.Side == side)
                    {
                        throw new DataFormatException(item.Path + ": a second " + side + " zone.");
                    }
                }
                int minX = item.Int("minXMm", 0, width);
                int minY = item.Int("minYMm", 0, depth);
                int maxX = item.Int("maxXMm", 0, width);
                int maxY = item.Int("maxYMm", 0, depth);
                if (maxX <= minX || maxY <= minY)
                {
                    throw new DataFormatException(
                        item.Path + ": the zone needs max above min on both axes."
                    );
                }
                var area = new RectMm(minX, minY, maxX, maxY);
                zones.Add(new DeploymentZone(side, area));
            }
            zones.Sort((a, b) => a.Side.CompareTo(b.Side)); // sides are unique, so this order is total

            int columns = (width + cell - 1) / cell;
            int rows = (depth + cell - 1) / cell;
            long gridColumns = (width + NeighbourGridCellMm - 1) / NeighbourGridCellMm;
            long gridRows = (depth + NeighbourGridCellMm - 1) / NeighbourGridCellMm;
            if (gridColumns * gridRows > SpatialHash.MaxCells)
            {
                throw new DataFormatException(
                    "map.json: the map is larger than the movement step's neighbour grid holds (about 64 km²)."
                );
            }
            if ((long)columns * rows > MaxCells)
            {
                throw new DataFormatException("map.json: too many cells; use a larger cellSizeMm.");
            }
            var height = new int[columns * rows];
            if (heightLayer != null)
            {
                if (heightLayer.Length != height.Length * 4)
                {
                    throw new DataFormatException(
                        "height.bin: expected "
                            + (height.Length * 4L).ToString(CultureInfo.InvariantCulture)
                            + " bytes for "
                            + columns.ToString(CultureInfo.InvariantCulture)
                            + " × "
                            + rows.ToString(CultureInfo.InvariantCulture)
                            + " cells."
                    );
                }
                for (int i = 0; i < height.Length; i++)
                {
                    int h = BinaryPrimitives.ReadInt32LittleEndian(heightLayer.AsSpan(i * 4, 4));
                    if (h < -MaxHeightMm || h > MaxHeightMm)
                    {
                        throw new DataFormatException("height.bin: a height is beyond ±10 km.");
                    }
                    height[i] = h;
                }
            }

            var hash = new SimHash();
            byte[] canonical = JsonCanonical.ToUtf8(root);
            hash.AppendInt32(canonical.Length);
            hash.Append(canonical);
            hash.AppendInt32s(height);
            var initialFields =
                fieldLayer == null
                    ? Array.Empty<KeyValuePair<string, int[]>>()
                    : ReadFieldLayer(fieldLayer, height.Length);
            if (initialFields.Length > 0)
            {
                hash.AppendInt32(initialFields.Length); // maps without a layer keep their hash
            }
            foreach (var field in initialFields)
            {
                byte[] fieldId = System.Text.Encoding.ASCII.GetBytes(field.Key);
                hash.AppendInt32(fieldId.Length);
                hash.Append(fieldId);
                hash.AppendInt32s(field.Value);
            }
            var soils = new List<SoilArea>();
            if (root.Has("soils"))
            {
                foreach (var item in root.Array("soils"))
                {
                    item.OnlyKeys("soil", "minXMm", "minYMm", "maxXMm", "maxYMm");
                    soils.Add(
                        new SoilArea(DataIds.Require(item["soil"]), ReadArea(item, width, depth))
                    );
                }
            }
            var sources = new List<WaterSource>();
            var sinks = new List<RectMm>();
            if (root.Has("water"))
            {
                var water = root["water"];
                water.OnlyKeys("$comment", "sources", "sinks");
                if (water.Has("sources"))
                {
                    foreach (var item in water.Array("sources"))
                    {
                        item.OnlyKeys("minXMm", "minYMm", "maxXMm", "maxYMm", "litresPerSecond");
                        sources.Add(
                            new WaterSource(
                                ReadArea(item, width, depth),
                                item.Int("litresPerSecond", 1, MaxLitresPerSecond)
                            )
                        );
                    }
                }
                if (water.Has("sinks"))
                {
                    foreach (var item in water.Array("sinks"))
                    {
                        item.OnlyKeys("minXMm", "minYMm", "maxXMm", "maxYMm");
                        var area = ReadArea(item, width, depth);
                        if (
                            area.MinX > 0
                            && area.MinY > 0
                            && area.MaxX < width
                            && area.MaxY < depth
                        )
                        {
                            throw new DataFormatException(
                                item.Path + ": a sink must touch the map edge."
                            );
                        }
                        sinks.Add(area);
                    }
                }
            }

            var stoneBridges = new List<StoneBridgeSpec>();
            if (root.Has("stoneBridges"))
            {
                foreach (var item in root.Array("stoneBridges"))
                {
                    item.OnlyKeys("$comment", "points", "widthMm", "deckHeightMm");
                    var points = item.Array("points");
                    if (points.Count != 2)
                    {
                        throw new DataFormatException(
                            item.Path + ".points: a stone bridge has two ends."
                        );
                    }
                    var a = points[0].Items;
                    var b = points[1].Items;
                    if (a.Count != 2 || b.Count != 2)
                    {
                        throw new DataFormatException(
                            item.Path + ".points: a point is [xMm, yMm]."
                        );
                    }
                    int ax = (int)a[0].AsInteger;
                    int ay = (int)a[1].AsInteger;
                    int bx = (int)b[0].AsInteger;
                    int by = (int)b[1].AsInteger;
                    foreach (int v in new[] { ax, bx })
                    {
                        if (v < 0 || v >= width)
                        {
                            throw new DataFormatException(item.Path + ".points: off the map.");
                        }
                    }
                    foreach (int v in new[] { ay, by })
                    {
                        if (v < 0 || v >= depth)
                        {
                            throw new DataFormatException(item.Path + ".points: off the map.");
                        }
                    }
                    stoneBridges.Add(
                        new StoneBridgeSpec(
                            ax,
                            ay,
                            bx,
                            by,
                            item.Int("widthMm", 1_000, 100_000),
                            item.Int("deckHeightMm", -MaxHeightMm, MaxHeightMm)
                        )
                    );
                }
            }
            var ponds = new List<FrozenPondSpec>();
            if (root.Has("frozenPonds"))
            {
                foreach (var item in root.Array("frozenPonds"))
                {
                    item.OnlyKeys(
                        "$comment",
                        "minXMm",
                        "minYMm",
                        "maxXMm",
                        "maxYMm",
                        "thicknessMm"
                    );
                    var area = new RectMm(
                        item.Int("minXMm", 0, width),
                        item.Int("minYMm", 0, depth),
                        item.Int("maxXMm", 0, width),
                        item.Int("maxYMm", 0, depth)
                    );
                    if (area.MaxX <= area.MinX || area.MaxY <= area.MinY)
                    {
                        throw new DataFormatException(item.Path + ": an empty pond.");
                    }
                    // The thickness range is the ruleset's (load.json ice); a map only promises a positive one.
                    ponds.Add(new FrozenPondSpec(area, item.Int("thicknessMm", 1, 10_000)));
                }
            }

            int windX = 0;
            int windY = 0;
            if (root.Has("wind"))
            {
                // The prevailing wind (spec S2, D9): calm by default, at most 15 m/s.
                var wind = root["wind"];
                wind.OnlyKeys("$comment", "xMmPerS", "yMmPerS");
                windX = wind.Int("xMmPerS", -MaxWindMmPerS, MaxWindMmPerS);
                windY = wind.Int("yMmPerS", -MaxWindMmPerS, MaxWindMmPerS);
                if ((long)windX * windX + (long)windY * windY > (long)MaxWindMmPerS * MaxWindMmPerS)
                {
                    throw new DataFormatException(
                        wind.Path + ": the wind is stronger than 15,000 mm/s."
                    );
                }
            }

            return new MapData(
                id,
                width,
                depth,
                cell,
                height,
                zones.ToArray(),
                sources.ToArray(),
                sinks.ToArray(),
                soils.ToArray(),
                hash.Digest()
            )
            {
                InitialFields = initialFields,
                WindXMmPerS = windX,
                WindYMmPerS = windY,
                StoneBridges = stoneBridges.ToArray(),
                FrozenPonds = ponds.ToArray(),
            };
        }

        /// <summary>The largest single source: 10,000 m³/s, far above any river a battle needs.</summary>
        public const int MaxLitresPerSecond = 10_000_000;

        private static RectMm ReadArea(JsonValue item, int width, int depth)
        {
            int minX = item.Int("minXMm", 0, width);
            int minY = item.Int("minYMm", 0, depth);
            int maxX = item.Int("maxXMm", 0, width);
            int maxY = item.Int("maxYMm", 0, depth);
            if (maxX <= minX || maxY <= minY)
            {
                throw new DataFormatException(
                    item.Path + ": the area needs max above min on both axes."
                );
            }
            return new RectMm(minX, minY, maxX, maxY);
        }
    }

    /// <summary>The data spelling of <see cref="Side"/>.</summary>
    public static class SideNames
    {
        public static Side Parse(JsonValue value)
        {
            switch (value.AsString)
            {
                case "attacker":
                    return Side.Attacker;
                case "defender":
                    return Side.Defender;
                default:
                    throw new DataFormatException(
                        value.Path + ": side must be \"attacker\" or \"defender\"."
                    );
            }
        }
    }
}
