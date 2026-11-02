using System;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// One world field (tech notes, section 6): an integer per map cell in the definition's unit, plus which tiles are
    /// awake. Field models step only awake tiles (plan, section 3.4: "active tiles only"); a tile wakes when something
    /// writes to it or when a neighbouring tile changed, and sleeps after <see cref="FieldRules.SleepAfterSteps"/>
    /// quiet steps. Values and tile activity both decide the future, so both are hashed and snapshotted.
    /// <para>
    /// Steps use two buffers: a model reads <see cref="Values"/> (every cell as the step began) and writes the tiles it
    /// steps into <see cref="Next"/>; <see cref="Commit"/> then copies those tiles back in tile order. Each tile writes
    /// only its own cells, so a step gives the same result whatever runner and chunking computed it.
    /// </para>
    /// </summary>
    public sealed class FieldGrid
    {
        private readonly int _sleepAfter;
        private readonly byte[] _quiet; // quiet steps per tile; at _sleepAfter the tile sleeps
        private readonly bool[] _changed; // per tile, set by models during a step

        public FieldGrid(
            FieldDefinition definition,
            int columns,
            int rows,
            int tileCells,
            int sleepAfterSteps
        )
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            if (
                columns < 1
                || rows < 1
                || tileCells < 1
                || sleepAfterSteps < 1
                || sleepAfterSteps > 200
            )
            {
                throw new ArgumentOutOfRangeException(nameof(columns));
            }
            Columns = columns;
            Rows = rows;
            TileCells = tileCells;
            TileColumns = (columns + tileCells - 1) / tileCells;
            TileRows = (rows + tileCells - 1) / tileCells;
            _sleepAfter = sleepAfterSteps;
            Values = new int[columns * rows];
            Next = new int[columns * rows];
            _quiet = new byte[TileColumns * TileRows];
            _changed = new bool[TileColumns * TileRows];
            for (int i = 0; i < Values.Length; i++)
            {
                Values[i] = definition.Initial;
            }
            for (int t = 0; t < _quiet.Length; t++)
            {
                _quiet[t] = (byte)_sleepAfter; // everything starts asleep: an untouched field costs nothing
            }
        }

        public FieldDefinition Definition { get; }

        public int Columns { get; }

        public int Rows { get; }

        public int TileCells { get; }

        public int TileColumns { get; }

        public int TileRows { get; }

        public int TileCount => TileColumns * TileRows;

        /// <summary>Row-major cell values as of the last commit. Models read these; only writes through this class keep activity right.</summary>
        public int[] Values { get; }

        /// <summary>
        /// The step's output buffer (models only): it holds each stepped tile's current values when the model starts;
        /// the model writes the cells it changes, in the tiles it was given and nowhere else.
        /// </summary>
        public int[] Next { get; }

        public int Get(int x, int y) => Values[y * Columns + x];

        public bool IsAwake(int tile) => _quiet[tile] < _sleepAfter;

        public int TileOf(int x, int y) => y / TileCells * TileColumns + x / TileCells;

        /// <summary>The cells a tile covers (the last row and column of tiles may be partial).</summary>
        public CellRect TileRect(int tile)
        {
            int tx = tile % TileColumns;
            int ty = tile / TileColumns;
            return new CellRect(
                tx * TileCells,
                ty * TileCells,
                System.Math.Min(Columns, (tx + 1) * TileCells) - 1,
                System.Math.Min(Rows, (ty + 1) * TileCells) - 1
            );
        }

        /// <summary>
        /// Sets one cell from outside the field step (a cast, a terrain piece, a unit's action), clamped to the field's
        /// range; wakes its tile and the tiles around it. Serial code only, never from a parallel kernel.
        /// </summary>
        public void Set(int x, int y, int value)
        {
            if (x < 0 || y < 0 || x >= Columns || y >= Rows)
            {
                throw new ArgumentOutOfRangeException(nameof(x));
            }
            Values[y * Columns + x] = Clamp(value);
            WakeAround(TileOf(x, y));
        }

        /// <summary>Adds to one cell (clamped), as <see cref="Set"/>.</summary>
        public void Add(int x, int y, int amount) => Set(x, y, Clamp((long)Get(x, y) + amount));

        public int Clamp(long value) =>
            value < Definition.Min ? Definition.Min
            : value > Definition.Max ? Definition.Max
            : (int)value;

        /// <summary>Wakes a tile now and its 8 neighbours, which may receive its changes.</summary>
        public void WakeAround(int tile)
        {
            int tx = tile % TileColumns;
            int ty = tile / TileColumns;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = tx + dx;
                    int ny = ty + dy;
                    if (nx >= 0 && ny >= 0 && nx < TileColumns && ny < TileRows)
                    {
                        _quiet[ny * TileColumns + nx] = 0;
                    }
                }
            }
        }

        /// <summary>
        /// Before a model steps: fills the stepped tiles' next buffer with their current values, so a cell the model
        /// leaves alone stays as it was and the step depends only on hashed state.
        /// </summary>
        internal void PrepareStep(ReadOnlySpan<int> steppedTiles)
        {
            for (int k = 0; k < steppedTiles.Length; k++)
            {
                var r = TileRect(steppedTiles[k]);
                for (int y = r.MinY; y <= r.MaxY; y++)
                {
                    Array.Copy(Values, y * Columns + r.MinX, Next, y * Columns + r.MinX, r.Width);
                }
            }
        }

        /// <summary>
        /// Copies the stepped tiles' new values in, clamped to the field's range, then updates activity: a tile whose
        /// values moved, and its neighbours, stay awake; a quiet stepped tile counts one step closer to sleep. Serial,
        /// in the given (ascending) tile order; whether a tile changed is read off the two buffers, not reported by the
        /// model.
        /// </summary>
        internal void Commit(ReadOnlySpan<int> steppedTiles)
        {
            for (int k = 0; k < steppedTiles.Length; k++)
            {
                int tile = steppedTiles[k];
                var r = TileRect(tile);
                bool changed = false;
                for (int y = r.MinY; y <= r.MaxY; y++)
                {
                    int row = y * Columns;
                    for (int x = r.MinX; x <= r.MaxX; x++)
                    {
                        int next = Clamp(Next[row + x]);
                        changed |= next != Values[row + x];
                        Values[row + x] = next;
                    }
                }
                _changed[tile] = changed;
            }
            for (int k = 0; k < steppedTiles.Length; k++)
            {
                int tile = steppedTiles[k];
                if (!_changed[tile] && _quiet[tile] < _sleepAfter)
                {
                    _quiet[tile]++;
                }
            }
            for (int k = 0; k < steppedTiles.Length; k++)
            {
                int tile = steppedTiles[k];
                if (_changed[tile])
                {
                    _changed[tile] = false;
                    WakeAround(tile);
                }
            }
        }

        /// <summary>Every awake tile, ascending, into <paramref name="tiles"/>; returns how many.</summary>
        public int AwakeTiles(int[] tiles)
        {
            int count = 0;
            for (int t = 0; t < _quiet.Length; t++)
            {
                if (_quiet[t] < _sleepAfter)
                {
                    tiles[count++] = t;
                }
            }
            return count;
        }

        public void AppendTo(SimHash hash)
        {
            hash.AppendInt32s(Values);
            hash.Append(_quiet);
        }

        /// <summary>Sparse by tile: a tile whose cells all hold the initial value is one byte.</summary>
        internal void WriteTo(ByteWriter writer)
        {
            writer.WriteInt32(Columns);
            writer.WriteInt32(Rows);
            for (int t = 0; t < TileCount; t++)
            {
                writer.WriteByte(_quiet[t]);
                var r = TileRect(t);
                bool initial = true;
                for (int y = r.MinY; y <= r.MaxY && initial; y++)
                {
                    for (int x = r.MinX; x <= r.MaxX; x++)
                    {
                        if (Values[y * Columns + x] != Definition.Initial)
                        {
                            initial = false;
                            break;
                        }
                    }
                }
                writer.WriteByte(initial ? (byte)0 : (byte)1);
                if (!initial)
                {
                    for (int y = r.MinY; y <= r.MaxY; y++)
                    {
                        for (int x = r.MinX; x <= r.MaxX; x++)
                        {
                            writer.WriteInt32(Values[y * Columns + x]);
                        }
                    }
                }
            }
        }

        internal void ReadFrom(ByteReader reader)
        {
            if (reader.ReadInt32() != Columns || reader.ReadInt32() != Rows)
            {
                throw new FormatException("Snapshot field " + Definition.Id + " has another size.");
            }
            for (int t = 0; t < TileCount; t++)
            {
                byte quiet = reader.ReadByte();
                if (quiet > _sleepAfter)
                {
                    throw new FormatException(
                        "Snapshot field " + Definition.Id + " has a bad tile state."
                    );
                }
                _quiet[t] = quiet;
                byte stored = reader.ReadByte();
                if (stored > 1)
                {
                    throw new FormatException(
                        "Snapshot field " + Definition.Id + " has a bad tile flag."
                    );
                }
                var r = TileRect(t);
                for (int y = r.MinY; y <= r.MaxY; y++)
                {
                    for (int x = r.MinX; x <= r.MaxX; x++)
                    {
                        int v = stored == 1 ? reader.ReadInt32() : Definition.Initial;
                        if (v < Definition.Min || v > Definition.Max)
                        {
                            throw new FormatException(
                                "Snapshot field " + Definition.Id + " is out of range."
                            );
                        }
                        Values[y * Columns + x] = v;
                    }
                }
            }
            Array.Clear(_changed, 0, _changed.Length);
        }
    }
}
