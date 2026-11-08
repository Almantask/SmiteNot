using System;
using GodGame.Sim.Math;

namespace GodGame.Sim
{
    /// <summary>
    /// The battle's ground: the map's heightfield on its field grid (1.5 m cells by default), with cell lookups, height
    /// at any point and slope per cell. Built once from <see cref="MapData"/>; heights change only through
    /// <see cref="SetHeight"/>, which reports what to rebuild (planning edits now, unit-made digs and fills later).
    /// Cell (x, y) covers [x × cell, (x + 1) × cell) east and [y × cell, (y + 1) × cell) north from the south-west corner.
    /// </summary>
    public sealed class Terrain
    {
        private readonly int[] _heightMm;
        private readonly bool[]? _blocked;

        public Terrain(MapData map)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }
            Columns = map.Columns;
            Rows = map.Rows;
            CellSizeMm = map.CellSizeMm;
            _heightMm = (int[])map.HeightMm.Clone();
            _blocked = map.Blocked == null ? null : (bool[])map.Blocked.Clone();
            _pieceSpeed = map.PieceSpeed == null ? null : (int[])map.PieceSpeed.Clone();
        }

        private readonly int[]? _pieceSpeed;

        /// <summary>The cell's crossing speed codes from line pieces (<see cref="FieldSpeed"/> packing; all dry where there is none).</summary>
        public int PieceCodes(int index) =>
            _pieceSpeed == null ? FieldSpeed.AllDry : _pieceSpeed[index];

        /// <summary>Whether any line piece stands on the map (the cost and movement kernels skip the lookup otherwise).</summary>
        public bool HasPieceSpeed => _pieceSpeed != null;

        /// <summary>Bumped by every <see cref="SetHeight"/>, so derived fields can tell they are out of date.</summary>
        public int Version { get; private set; }

        public int Columns { get; }

        public int Rows { get; }

        public int CellSizeMm { get; }

        public int CellCount => Columns * Rows;

        public int Index(int x, int y) => y * Columns + x;

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Columns && y < Rows;

        /// <summary>Closed to every class whatever its slope (a boulder).</summary>
        public bool IsBlocked(int index) => _blocked != null && _blocked[index];

        public int HeightMm(int x, int y) => _heightMm[Index(Clamp(x, Columns), Clamp(y, Rows))];

        /// <summary>The centre of a cell, in metres.</summary>
        public FixVec2 CellCentre(int x, int y) =>
            new FixVec2(
                Fix.FromRatio((long)x * CellSizeMm + CellSizeMm / 2, 1000),
                Fix.FromRatio((long)y * CellSizeMm + CellSizeMm / 2, 1000)
            );

        /// <summary>The cell a position falls in, clamped to the map.</summary>
        public void CellOf(FixVec2 position, out int x, out int y)
        {
            x = Clamp(MmFloor(position.X) / CellSizeMm, Columns);
            y = Clamp(MmFloor(position.Y) / CellSizeMm, Rows);
        }

        /// <summary>
        /// Ground height in millimetres at a position, interpolated between cell centres (bilinear, integer
        /// arithmetic, rounded toward negative infinity); positions off the map take the edge cells' heights.
        /// </summary>
        public int HeightMmAt(FixVec2 position)
        {
            // Offsets from the first cell centre, in mm, then split into a cell and a fraction of 2^16.
            long fx = MmFloor(position.X) * 65536L / CellSizeMm - 32768;
            long fy = MmFloor(position.Y) * 65536L / CellSizeMm - 32768;
            int x0 = (int)(fx >> 16);
            int y0 = (int)(fy >> 16);
            long tx = fx & 0xFFFF;
            long ty = fy & 0xFFFF;
            long h00 = HeightMm(x0, y0);
            long h10 = HeightMm(x0 + 1, y0);
            long h01 = HeightMm(x0, y0 + 1);
            long h11 = HeightMm(x0 + 1, y0 + 1);
            long south = h00 * 65536 + (h10 - h00) * tx;
            long north = h01 * 65536 + (h11 - h01) * tx;
            long mixed = south * 65536 + (north - south) * ty;
            return (int)(mixed >> 32);
        }

        /// <summary>
        /// The steepest step from this cell to any of its four neighbours, in ‰ (rise over run × 1000, so 1000 is
        /// 45°). Steps over the map edge don't count. A cliff is simply a very steep step.
        /// </summary>
        public int SlopePermille(int x, int y)
        {
            int h = _heightMm[Index(x, y)];
            int steepest = 0;
            if (x > 0)
            {
                steepest = System.Math.Max(steepest, Step(h, _heightMm[Index(x - 1, y)]));
            }
            if (x + 1 < Columns)
            {
                steepest = System.Math.Max(steepest, Step(h, _heightMm[Index(x + 1, y)]));
            }
            if (y > 0)
            {
                steepest = System.Math.Max(steepest, Step(h, _heightMm[Index(x, y - 1)]));
            }
            if (y + 1 < Rows)
            {
                steepest = System.Math.Max(steepest, Step(h, _heightMm[Index(x, y + 1)]));
            }
            return steepest;
        }

        /// <summary>
        /// Sets one cell's height. Its own slope and its four neighbours' slopes change, so the caller rebuilds the cost
        /// fields over <see cref="DirtyAround"/>.
        /// </summary>
        public void SetHeight(int x, int y, int heightMm)
        {
            if (!InBounds(x, y))
            {
                throw new ArgumentOutOfRangeException(nameof(x));
            }
            if (heightMm < -MapData.MaxHeightMm || heightMm > MapData.MaxHeightMm)
            {
                throw new ArgumentOutOfRangeException(nameof(heightMm));
            }
            _heightMm[Index(x, y)] = heightMm;
            Version = unchecked(Version + 1);
        }

        /// <summary>The cells whose slope a height change in [minX, maxX] × [minY, maxY] can reach: one cell further each way.</summary>
        public CellRect DirtyAround(int minX, int minY, int maxX, int maxY)
        {
            if (minX > maxX || minY > maxY || !InBounds(minX, minY) || !InBounds(maxX, maxY))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minX),
                    "The edited cells must be on the map, min before max."
                );
            }
            return new CellRect(
                System.Math.Max(0, minX - 1),
                System.Math.Max(0, minY - 1),
                System.Math.Min(Columns - 1, maxX + 1),
                System.Math.Min(Rows - 1, maxY + 1)
            );
        }

        public CellRect All => new CellRect(0, 0, Columns - 1, Rows - 1);

        /// <summary>The cells an area in mm overlaps (a deployment zone, a goal), clamped to the map.</summary>
        public CellRect CellsOf(RectMm area) =>
            new CellRect(
                Clamp(area.MinX / CellSizeMm, Columns),
                Clamp(area.MinY / CellSizeMm, Rows),
                Clamp((area.MaxX - 1) / CellSizeMm, Columns),
                Clamp((area.MaxY - 1) / CellSizeMm, Rows)
            );

        /// <summary>Every height, row-major, in a fixed order: for snapshots and hashes once battle-time edits exist.</summary>
        public ReadOnlySpan<int> Heights => _heightMm;

        // Heights are within ±10 km and cells at least 100 mm, so |dh| × 1000 stays far inside an int (2 × 10^10 ÷ 100).
        private int Step(int from, int to) =>
            (int)((long)System.Math.Abs(to - from) * 1000 / CellSizeMm);

        // Positions are Fix metres; × 1000 gives mm, floored. Maps are at most 30 km, so this fits a long easily.
        private static long MmFloor(Fix value) => (value.Raw * 1000) >> Fix.FractionalBits;

        private static int Clamp(long value, int count) =>
            value < 0 ? 0
            : value >= count ? count - 1
            : (int)value;
    }

    /// <summary>An inclusive rectangle of cells.</summary>
    public readonly struct CellRect
    {
        public readonly int MinX;
        public readonly int MinY;
        public readonly int MaxX;
        public readonly int MaxY;

        public CellRect(int minX, int minY, int maxX, int maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public bool IsEmpty => MaxX < MinX || MaxY < MinY;

        public int Width => IsEmpty ? 0 : MaxX - MinX + 1;

        public int Height => IsEmpty ? 0 : MaxY - MinY + 1;
    }
}
