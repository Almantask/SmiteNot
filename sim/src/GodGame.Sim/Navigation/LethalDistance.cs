using System;
using System.Diagnostics;

namespace GodGame.Sim
{
    /// <summary>
    /// For every cell, how far it is to the nearest lethal cell, in mm, capped at <see cref="MaxDistanceMm"/> (tech
    /// notes, sections 3 and 5): the teeter, push-over-the-edge and landing rules read it, and the preview draws it. M2
    /// knows two kinds of lethal cell: the map edge (a soldier who leaves the map is gone) and a cliff top, a cell with a
    /// 4-neighbour at least the ruleset's lethal drop below it. Water that would sweep a foot soldier away is lethal
    /// too (<see cref="FieldSpeed"/>, from the hashed speed codes); fire and broken ice join through the same rebuild.
    /// <para>
    /// Distances are chamfer distances on the 8-neighbour grid (a cell east-west or north-south, 1.414 cells
    /// diagonally, both rounded to mm), computed by the classic two passes: forward (south-west to north-east) and back.
    /// Integer arithmetic in a fixed order, so the result is the same everywhere. A partial <see cref="Rebuild"/> over
    /// an edit recomputes only the window the cap lets the edit reach and gives exactly what a full one gives.
    /// </para>
    /// <para>Derived state from the terrain and the ruleset, like the cost fields.</para>
    /// </summary>
    public sealed class LethalDistance
    {
        /// <summary>The cap: farther than 50 m from anything lethal reads as 50 m.</summary>
        public const int MaxDistanceMm = 50_000;

        private readonly Terrain _terrain;
        private readonly int _lethalDropMm;
        private readonly int _orthogonalMm;
        private readonly int _diagonalMm;
        private readonly int _reachCells;
        private readonly int[] _distanceMm;
        private int[]? _fieldCodes;

        public LethalDistance(Terrain terrain, int lethalDropMm)
        {
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            if (lethalDropMm < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(lethalDropMm));
            }
            _lethalDropMm = lethalDropMm;
            _orthogonalMm = terrain.CellSizeMm;
            _diagonalMm = (int)((long)terrain.CellSizeMm * 1414 / 1000);
            _reachCells = MaxDistanceMm / terrain.CellSizeMm + 2;
            _distanceMm = new int[terrain.CellCount];
            Rebuild(terrain.All);
        }

        /// <summary>The terrain version the field last caught up with.</summary>
        public int BuiltFromVersion { get; private set; }

        public int LethalDropMm => _lethalDropMm;

        public int DistanceMm(int x, int y)
        {
            AssertFresh();
            return _distanceMm[_terrain.Index(x, y)];
        }

        /// <summary>Row-major distances, for overlays.</summary>
        public ReadOnlySpan<int> Distances
        {
            get
            {
                AssertFresh();
                return _distanceMm;
            }
        }

        /// <summary>The field speed codes later rebuilds read, as <see cref="CostFields.UseFieldCodes"/>.</summary>
        public void UseFieldCodes(int[]? codes)
        {
            if (codes != null && codes.Length != _terrain.CellCount)
            {
                throw new ArgumentException("One code per terrain cell.", nameof(codes));
            }
            _fieldCodes = codes;
        }

        /// <summary>Lethal itself: on the map edge, a cliff top, or water that sweeps a foot soldier away.</summary>
        public bool IsLethal(int x, int y)
        {
            if (_terrain.StandingSegment(_terrain.Index(x, y)) >= 0)
            {
                return false; // a standing deck is footing, whatever drop or water lies under it (S2 8.1)
            }
            if (x == 0 || y == 0 || x == _terrain.Columns - 1 || y == _terrain.Rows - 1)
            {
                return true;
            }
            if (
                _fieldCodes != null
                && FieldSpeed.Code(_fieldCodes[_terrain.Index(x, y)], Rules.MovementClass.Foot)
                    == FieldSpeed.Swept
            )
            {
                return true;
            }
            int h = _terrain.HeightMm(x, y);
            return h - _terrain.HeightMm(x - 1, y) >= _lethalDropMm
                || h - _terrain.HeightMm(x + 1, y) >= _lethalDropMm
                || h - _terrain.HeightMm(x, y - 1) >= _lethalDropMm
                || h - _terrain.HeightMm(x, y + 1) >= _lethalDropMm;
        }

        [Conditional("DEBUG")]
        private void AssertFresh() =>
            Debug.Assert(
                BuiltFromVersion == _terrain.Version,
                "Lethal distances read after a height edit without a rebuild."
            );

        /// <summary>
        /// Recomputes the cells an edit over <paramref name="dirty"/> can reach (the terrain's DirtyAround rectangle,
        /// which already covers every cell whose lethal status can change): the rectangle grown by the cap.
        /// </summary>
        public void Rebuild(CellRect dirty)
        {
            if (dirty.IsEmpty)
            {
                BuiltFromVersion = _terrain.Version;
                return;
            }
            int columns = _terrain.Columns;
            int rows = _terrain.Rows;
            int minX = System.Math.Max(0, dirty.MinX - _reachCells);
            int minY = System.Math.Max(0, dirty.MinY - _reachCells);
            int maxX = System.Math.Min(columns - 1, dirty.MaxX + _reachCells);
            int maxY = System.Math.Min(rows - 1, dirty.MaxY + _reachCells);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    _distanceMm[_terrain.Index(x, y)] = IsLethal(x, y) ? 0 : MaxDistanceMm;
                }
            }
            // Cells outside the window keep their values: they lie farther than the cap from the edit, so the edit
            // can't change them, and they feed the passes as fixed borders.
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int i = _terrain.Index(x, y);
                    int d = _distanceMm[i];
                    d = Relax(d, x - 1, y, _orthogonalMm);
                    d = Relax(d, x - 1, y - 1, _diagonalMm);
                    d = Relax(d, x, y - 1, _orthogonalMm);
                    d = Relax(d, x + 1, y - 1, _diagonalMm);
                    _distanceMm[i] = d;
                }
            }
            for (int y = maxY; y >= minY; y--)
            {
                for (int x = maxX; x >= minX; x--)
                {
                    int i = _terrain.Index(x, y);
                    int d = _distanceMm[i];
                    d = Relax(d, x + 1, y, _orthogonalMm);
                    d = Relax(d, x + 1, y + 1, _diagonalMm);
                    d = Relax(d, x, y + 1, _orthogonalMm);
                    d = Relax(d, x - 1, y + 1, _diagonalMm);
                    _distanceMm[i] = d;
                }
            }
            BuiltFromVersion = _terrain.Version;
        }

        private int Relax(int current, int x, int y, int step)
        {
            if (!_terrain.InBounds(x, y))
            {
                return current;
            }
            int via = _distanceMm[_terrain.Index(x, y)] + step;
            return via < current ? via : current;
        }
    }
}
