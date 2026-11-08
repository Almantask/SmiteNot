using System;
using System.Collections.Generic;
using GodGame.Sim.Math;

namespace GodGame.Sim
{
    /// <summary>
    /// The ground as soldiers changed it during the battle (spec S2, section 14: dug ditches and banks, fascine fills):
    /// each edited cell's height, in the order cells were first edited, so the played map plus this list is the ground
    /// now. Fascine fills also keep how much fill each cell holds and how long a surge has run over it, for washout.
    /// Hashed and snapshotted with the world; navigation rebuilds the terrain from it after a restore.
    /// </summary>
    public sealed class GroundEdits
    {
        private readonly List<int> _cells = new List<int>();
        private readonly List<int> _heights = new List<int>();
        private readonly List<int> _fillCells = new List<int>();
        private readonly List<int> _fillMm = new List<int>();
        private readonly List<int> _surgeTicks = new List<int>();

        public int Count => _cells.Count;

        public int CellAt(int i) => _cells[i];

        public int HeightAt(int i) => _heights[i];

        /// <summary>Records a cell's new height (a linear search: a battle edits a few hundred cells).</summary>
        public void Set(int cell, int heightMm)
        {
            int i = _cells.IndexOf(cell);
            if (i < 0)
            {
                _cells.Add(cell);
                _heights.Add(heightMm);
            }
            else
            {
                _heights[i] = heightMm;
            }
        }

        public int FillCount => _fillCells.Count;

        public int FillCellAt(int i) => _fillCells[i];

        public int FillMmAt(int i) => _fillMm[i];

        public int SurgeTicksAt(int i) => _surgeTicks[i];

        /// <summary>Adds fill to a cell, creating its entry the first time.</summary>
        public void AddFill(int cell, int mm)
        {
            int i = _fillCells.IndexOf(cell);
            if (i < 0)
            {
                _fillCells.Add(cell);
                _fillMm.Add(mm);
                _surgeTicks.Add(0);
            }
            else
            {
                _fillMm[i] += mm;
            }
        }

        public void SetFill(int i, int mm, int surgeTicks)
        {
            _fillMm[i] = mm;
            _surgeTicks[i] = surgeTicks;
        }

        internal void WriteTo(ByteWriter writer)
        {
            writer.WriteInt32(_cells.Count);
            for (int i = 0; i < _cells.Count; i++)
            {
                writer.WriteInt32(_cells[i]);
                writer.WriteInt32(_heights[i]);
            }
            writer.WriteInt32(_fillCells.Count);
            for (int i = 0; i < _fillCells.Count; i++)
            {
                writer.WriteInt32(_fillCells[i]);
                writer.WriteInt32(_fillMm[i]);
                writer.WriteInt32(_surgeTicks[i]);
            }
        }

        internal void ReadFrom(ByteReader reader, int cellCount)
        {
            _cells.Clear();
            _heights.Clear();
            _fillCells.Clear();
            _fillMm.Clear();
            _surgeTicks.Clear();
            int n = reader.ReadInt32();
            if (n < 0 || n > cellCount)
            {
                throw new FormatException("Snapshot ground edits are implausibly many.");
            }
            for (int i = 0; i < n; i++)
            {
                int cell = reader.ReadInt32();
                int height = reader.ReadInt32();
                if (cell < 0 || cell >= cellCount || _cells.Contains(cell))
                {
                    throw new FormatException("Snapshot ground edit is off the map or repeated.");
                }
                if (height < -MapData.MaxHeightMm || height > MapData.MaxHeightMm)
                {
                    throw new FormatException("Snapshot ground edit height is out of range.");
                }
                _cells.Add(cell);
                _heights.Add(height);
            }
            int fills = reader.ReadInt32();
            if (fills < 0 || fills > cellCount)
            {
                throw new FormatException("Snapshot fills are implausibly many.");
            }
            for (int i = 0; i < fills; i++)
            {
                int cell = reader.ReadInt32();
                int mm = reader.ReadInt32();
                int surge = reader.ReadInt32();
                if (
                    cell < 0
                    || cell >= cellCount
                    || mm < 0
                    || surge < 0
                    || _fillCells.Contains(cell)
                )
                {
                    throw new FormatException("Snapshot fill is invalid.");
                }
                _fillCells.Add(cell);
                _fillMm.Add(mm);
                _surgeTicks.Add(surge);
            }
        }

        internal void AppendTo(SimHash hash)
        {
            hash.AppendInt32(_cells.Count);
            for (int i = 0; i < _cells.Count; i++)
            {
                hash.AppendInt32(_cells[i]);
                hash.AppendInt32(_heights[i]);
            }
            hash.AppendInt32(_fillCells.Count);
            for (int i = 0; i < _fillCells.Count; i++)
            {
                hash.AppendInt32(_fillCells[i]);
                hash.AppendInt32(_fillMm[i]);
                hash.AppendInt32(_surgeTicks[i]);
            }
        }
    }
}
