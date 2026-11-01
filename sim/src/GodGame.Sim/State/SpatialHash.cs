using System;
using System.Diagnostics;
using GodGame.Sim.Math;

namespace GodGame.Sim
{
    /// <summary>
    /// A uniform grid of buckets that answers "who is near here" (tech notes, section 2) for separation, melee
    /// contact and crowd pressure. <see cref="Build"/> fills it from the live units' positions with a counting sort,
    /// so every cell lists its units in ascending slot order, and queries visit cells row by row (south to north,
    /// west to east): the same world always gives the same neighbours in the same order.
    /// <para>
    /// It is derived state: rebuild it from <see cref="World.Position"/> before the systems that read it, and never
    /// carry it across ticks, so it needs no place in the state hash or in snapshots. Building is serial; once built,
    /// any number of kernels may read it in parallel.
    /// </para>
    /// </summary>
    public sealed class SpatialHash
    {
        private readonly long _cellRaw;
        private readonly int[] _cellStart; // CellCount + 1 prefix sums into _cellUnits
        private readonly int[] _cellUnits;
        private readonly int[] _unitCell;
        private readonly int[] _cursor; // scratch for Build, overwritten every time
        private World? _builtFrom;
        private int _builtVersion;

        /// <summary>The largest radius a query accepts: keeps every squared distance far inside a long.</summary>
        public static readonly Fix MaxQueryRadius = Fix.FromInt(1000);

        /// <param name="width">Map width (X) in metres; positions outside are clamped to the edge cells.</param>
        /// <param name="depth">Map depth (Y) in metres.</param>
        /// <param name="cellSize">Cell edge in metres; at least the largest query radius keeps queries to 3 × 3 cells.</param>
        /// <param name="unitCapacity">The world's unit capacity.</param>
        public SpatialHash(Fix width, Fix depth, Fix cellSize, int unitCapacity)
        {
            if (cellSize <= Fix.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(cellSize));
            }
            if (width <= Fix.Zero || depth <= Fix.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "The map must have a size.");
            }
            if (unitCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unitCapacity));
            }
            _cellRaw = cellSize.Raw;
            CellSize = cellSize;
            long columns = width.Raw / _cellRaw + (width.Raw % _cellRaw == 0 ? 0 : 1);
            long rows = depth.Raw / _cellRaw + (depth.Raw % _cellRaw == 0 ? 0 : 1);
            if (columns * rows > MaxCells)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(cellSize),
                    "Too many cells for this map and cell size."
                );
            }
            Columns = (int)columns;
            Rows = (int)rows;
            _cellStart = new int[Columns * Rows + 1];
            _cellUnits = new int[unitCapacity];
            _unitCell = new int[unitCapacity];
            _cursor = new int[Columns * Rows];
        }

        /// <summary>16 million cells: a 1.2 × 0.8 km map at 0.25 m cells is about 15 million.</summary>
        public const long MaxCells = 16_000_000;

        public Fix CellSize { get; }

        public int Columns { get; }

        public int Rows { get; }

        public int CellCount => Columns * Rows;

        /// <summary>Units placed by the last <see cref="Build"/>.</summary>
        public int Count => _cellStart[CellCount];

        public int CellX(Fix x) => Clamp(x.Raw / _cellRaw, Columns);

        public int CellY(Fix y) => Clamp(y.Raw / _cellRaw, Rows);

        public int CellOf(FixVec2 position) => CellY(position.Y) * Columns + CellX(position.X);

        /// <summary>The units in one cell, in ascending slot order.</summary>
        public ReadOnlySpan<int> UnitsIn(int cell) =>
            new ReadOnlySpan<int>(
                _cellUnits,
                _cellStart[cell],
                _cellStart[cell + 1] - _cellStart[cell]
            );

        /// <summary>Rebuilds the grid from every live unit's position. Serial, O(units + cells).</summary>
        public void Build(World world)
        {
            if (world.Units.Capacity > _unitCell.Length)
            {
                throw new ArgumentException(
                    "The world holds more units than this hash was sized for.",
                    nameof(world)
                );
            }
            Array.Clear(_cellStart, 0, _cellStart.Length);
            int highWater = world.Units.HighWater;
            FixVec2[] positions = world.Position;

            // Count per cell (shifted by one, so the prefix sum below yields start offsets).
            for (int u = 0; u < highWater; u++)
            {
                if (!world.Units.IsAlive(u))
                {
                    _unitCell[u] = -1;
                    continue;
                }
                int cell = CellOf(positions[u]);
                _unitCell[u] = cell;
                _cellStart[cell + 1]++;
            }
            for (int c = 0; c < CellCount; c++)
            {
                _cellStart[c + 1] += _cellStart[c];
            }
            // Place in ascending slot order: each cell's list comes out sorted with no sort.
            int[] cursor = _cursor;
            Array.Copy(_cellStart, cursor, CellCount);
            for (int u = 0; u < highWater; u++)
            {
                int cell = _unitCell[u];
                if (cell >= 0)
                {
                    _cellUnits[cursor[cell]++] = u;
                }
            }
            _builtFrom = world;
            _builtVersion = world.Units.Version;
        }

        /// <summary>
        /// Finds the units within <paramref name="radius"/> of <paramref name="center"/> (exact: squared distance in
        /// raw units ≤ radius²), in cell order then slot order, including a unit standing on the centre (callers skip
        /// themselves). Writes the first results.Length of them and returns how many there are in total, so a return
        /// above results.Length means the buffer was too small; keep its size a fixed constant, never derived from the
        /// machine.
        /// <para>
        /// The grid is only as fresh as the last <see cref="Build"/>: build it after the last position write, spawn or
        /// despawn that comes before its readers, in every tick they run. Debug builds check spawns and despawns.
        /// From parallel kernels, give each chunk its own buffer (<c>stackalloc</c> inside the chunk); a buffer
        /// shared through the kernel struct is a data race.
        /// </para>
        /// </summary>
        public int QueryRadius(World world, FixVec2 center, Fix radius, Span<int> results)
        {
            if (radius < Fix.Zero || radius > MaxQueryRadius)
            {
                throw new ArgumentOutOfRangeException(nameof(radius));
            }
            Debug.Assert(
                ReferenceEquals(world, _builtFrom) && world.Units.Version == _builtVersion,
                "SpatialHash queried after a spawn or despawn, or for another world: rebuild it first."
            );
            long r = radius.Raw;
            long radiusSquared = r * r;
            int x0 = CellX(center.X - radius);
            int x1 = CellX(center.X + radius);
            int y0 = CellY(center.Y - radius);
            int y1 = CellY(center.Y + radius);
            FixVec2[] positions = world.Position;
            int found = 0;
            for (int cy = y0; cy <= y1; cy++)
            {
                for (int cx = x0; cx <= x1; cx++)
                {
                    ReadOnlySpan<int> units = UnitsIn(cy * Columns + cx);
                    for (int i = 0; i < units.Length; i++)
                    {
                        int u = units[i];
                        // Reject on each axis first: a unit flung far off the map sits in an edge cell, and its
                        // square would overflow. After this test both squares are at most radius².
                        long dx = positions[u].X.Raw - center.X.Raw;
                        long dy = positions[u].Y.Raw - center.Y.Raw;
                        if (dx > r || dx < -r || dy > r || dy < -r)
                        {
                            continue;
                        }
                        if (dx * dx + dy * dy <= radiusSquared)
                        {
                            if (found < results.Length)
                            {
                                results[found] = u;
                            }
                            found++;
                        }
                    }
                }
            }
            return found;
        }

        private static int Clamp(long cell, int count) =>
            cell < 0 ? 0
            : cell >= count ? count - 1
            : (int)cell;
    }
}
