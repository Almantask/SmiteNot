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
        /// True for a map made by <see cref="WithTerrain"/>: it already carries a plan's pieces, so it must never be fed
        /// back into a battle or a plan, which would stamp the pieces twice under the same id and hash.
        /// </summary>
        public bool IsPlayed { get; private set; }

        /// <summary>
        /// The same map with the plan's terrain pieces stamped in (<see cref="TerrainPieces"/>): other heights, closed
        /// cells and extra water sources. The id and hash stay the base map's: the plan's hash covers the pieces.
        /// </summary>
        internal MapData WithTerrain(int[] heightMm, bool[] blocked, WaterSource[] sources)
        {
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
                IsPlayed = true,
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

        public static MapData Load(string mapJson, byte[]? heightLayer = null)
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
                "soils"
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
            );
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
