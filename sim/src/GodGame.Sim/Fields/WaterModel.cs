using System;
using System.Collections.Generic;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// Shallow water on the heightfield by virtual pipes (tech notes, section 5; spec S1, section 2). Each cell holds a
    /// depth in µm and four outflows (east, north, west, south) in µm/s of its own depth; ground comes from the
    /// terrain, so carved or raised ground steers water with no special code.
    /// <para>
    /// Every field step runs <see cref="WaterRules.SubstepsPerFieldStep"/> substeps of two passes over the stepped
    /// tiles. The first sets each outflow from the head difference to the neighbour (ground plus water), slowed by bed
    /// friction, stopped when both it and the head behind it are tiny, and scaled down so a cell never sends more than
    /// it holds. The second moves those amounts: what a
    /// cell sends one way is computed once, as an integer, and both cells use that integer, so water is conserved to
    /// the µm. Map edges are closed except along sinks, which drain into a virtual cell at the edge cell's ground
    /// height; sources add their discharge spread exactly over their cells. A pipe into a tile that isn't stepping
    /// this tick is a wall for that tick: the tile wakes, and the water arrives one step later instead of vanishing.
    /// </para>
    /// <para>
    /// The scratch arrays are overwritten every step before they are read; everything lasting lives in the grids.
    /// </para>
    /// </summary>
    public sealed class WaterModel : IFieldModel
    {
        public const string Depth = "water";
        public const string OutEast = "water_out_e";
        public const string OutNorth = "water_out_n";
        public const string OutWest = "water_out_w";
        public const string OutSouth = "water_out_s";

        private static readonly IReadOnlyList<string> Outflows = Array.AsReadOnly(
            new[] { OutEast, OutNorth, OutWest, OutSouth }
        );

        /// <summary>Outflows (and the speeds friction reads) stay within what the grids store.</summary>
        public const long MaxOutflowUmPerS = 1_000_000_000;

        /// <summary>Head differences beyond 100 m push no harder (a waterfall, as far as a pipe can tell).</summary>
        public const long MaxHeadDifferenceUm = 100_000_000;

        private static long Clamp(long value, long limit) =>
            value > limit ? limit
            : value < -limit ? -limit
            : value;

        private readonly Terrain _terrain;
        private readonly WaterRules _rules;
        private readonly int _substepMs;
        private readonly long _cellUm;
        private readonly int[] _sourceCells; // cell index per source cell, sources in file order
        private readonly long[] _sourceUmPerSubstep; // per source: µm·cells added each substep, before the split
        private readonly int[] _sourceStart; // per source: first entry in _sourceCells; one more entry at the end
        private readonly bool[] _sinkEdge; // per cell × 4 directions: the off-map neighbour that way is a sink

        // Ground height per cell in µm, cached from the terrain and refreshed when its version moves.
        private long[] _groundUm = new long[0];
        private int _groundVersion = -1;

        // Scratch.
        private long[] _depth = new long[0];
        private long[] _depthNext = new long[0];
        private long[][] _out = new long[0][];
        private long[][] _outNext = new long[0][];
        private bool[] _steppedTile = new bool[0];
        private int[] _tiles = new int[0];

        public WaterModel(Terrain terrain, MapData map, WaterRules rules, TickRate rate)
        {
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }
            Rate = rate;
            int stepMs = rate.Period * 1000 / SimConstants.TicksPerSecond;
            _substepMs = System.Math.Max(1, stepMs / rules.SubstepsPerFieldStep);
            _cellUm = terrain.CellSizeMm * 1000L;

            long cellArea = (long)terrain.CellSizeMm * terrain.CellSizeMm;
            var cells = new List<int>();
            _sourceStart = new int[map.WaterSources.Count + 1];
            _sourceUmPerSubstep = new long[map.WaterSources.Count];
            for (int s = 0; s < map.WaterSources.Count; s++)
            {
                _sourceStart[s] = cells.Count;
                var area = terrain.CellsOf(map.WaterSources[s].Area);
                for (int y = area.MinY; y <= area.MaxY; y++)
                {
                    for (int x = area.MinX; x <= area.MaxX; x++)
                    {
                        cells.Add(terrain.Index(x, y));
                    }
                }
                // L/s × 10^6 mm³ per litre × dt in s, over one cell's area in mm², is mm of depth on one cell; × 1000
                // gives µm. Split over the source's cells when it is applied (the remainder goes to the first cells).
                _sourceUmPerSubstep[s] =
                    map.WaterSources[s].LitresPerSecond * 1_000_000L * _substepMs / cellArea;
            }
            _sourceStart[map.WaterSources.Count] = cells.Count;
            _sourceCells = cells.ToArray();

            _sinkEdge = new bool[terrain.CellCount * 4];
            foreach (var sink in map.WaterSinks)
            {
                var area = terrain.CellsOf(sink);
                for (int y = area.MinY; y <= area.MaxY; y++)
                {
                    for (int x = area.MinX; x <= area.MaxX; x++)
                    {
                        int i = terrain.Index(x, y) * 4;
                        _sinkEdge[i + 0] |= x == terrain.Columns - 1;
                        _sinkEdge[i + 1] |= y == terrain.Rows - 1;
                        _sinkEdge[i + 2] |= x == 0;
                        _sinkEdge[i + 3] |= y == 0;
                    }
                }
            }
        }

        public string FieldId => Depth;

        public IReadOnlyList<string> AlsoWrites => Outflows;

        public TickRate Rate { get; }

        public bool Staggered => false;

        /// <summary>Shallower water has no flow speed (S1: avoids huge speeds from dividing by a film).</summary>
        public const int VelocityMinDepthUm = 20_000;

        /// <summary>Flow speeds are capped here for units and rendering (S1: 5 m/s is already a torrent).</summary>
        public const int MaxFlowSpeedMmPerS = 5_000;

        /// <summary>
        /// The water's velocity in a cell, in mm/s east and north: the cell's own net outflow each way (east minus west,
        /// north minus south) × cell / depth. Zero in water shallower than <see cref="VelocityMinDepthUm"/>; each
        /// component capped at <see cref="MaxFlowSpeedMmPerS"/>.
        /// </summary>
        public static void Velocity(
            FieldStore fields,
            int cellSizeMm,
            int x,
            int y,
            out int eastMmPerS,
            out int northMmPerS
        )
        {
            var depth = fields.Get(Depth);
            long d = depth.Get(x, y);
            if (d < VelocityMinDepthUm)
            {
                eastMmPerS = northMmPerS = 0;
                return;
            }
            long e = fields.Get(OutEast).Get(x, y) - fields.Get(OutWest).Get(x, y);
            long n = fields.Get(OutNorth).Get(x, y) - fields.Get(OutSouth).Get(x, y);
            // Outflows are µm/s of this cell's depth; × cell (µm) / depth (µm) is µm/s of speed; / 1000 is mm/s.
            long cellUm = cellSizeMm * 1000L;
            eastMmPerS = (int)
                System.Math.Max(
                    -MaxFlowSpeedMmPerS,
                    System.Math.Min(MaxFlowSpeedMmPerS, e * cellUm / d / 1000)
                );
            northMmPerS = (int)
                System.Math.Max(
                    -MaxFlowSpeedMmPerS,
                    System.Math.Min(MaxFlowSpeedMmPerS, n * cellUm / d / 1000)
                );
        }

        /// <summary>
        /// Keeps the tiles under every source awake before each field step: a river in exact balance changes no depth,
        /// and its source tile would otherwise fall asleep and stop adding water.
        /// </summary>
        public void BeforeStep(FieldStepContext context) => WakeSources(context.Fields);

        /// <summary>Wakes the tiles under every source.</summary>
        public void WakeSources(FieldStore fields)
        {
            var depth = fields.Get(Depth);
            foreach (int cell in _sourceCells)
            {
                depth.WakeAround(depth.TileOf(cell % depth.Columns, cell / depth.Columns));
            }
        }

        public void Step(FieldStepContext context, FieldGrid grid, ReadOnlySpan<int> tiles)
        {
            var fields = context.Fields;
            var outGrids = new FieldGrid[4];
            for (int k = 0; k < 4; k++)
            {
                outGrids[k] = fields.Get(Outflows[k]);
            }
            int cells = grid.Columns * grid.Rows;
            if (_depth.Length != cells)
            {
                _depth = new long[cells];
                _depthNext = new long[cells];
                _out = new long[4][];
                _outNext = new long[4][];
                for (int k = 0; k < 4; k++)
                {
                    _out[k] = new long[cells];
                    _outNext[k] = new long[cells];
                }
            }
            if (_steppedTile.Length != grid.TileCount)
            {
                _steppedTile = new bool[grid.TileCount];
                _tiles = new int[grid.TileCount];
            }
            if (_groundUm.Length != cells || _groundVersion != _terrain.Version)
            {
                _groundUm = new long[cells];
                for (int i = 0; i < cells; i++)
                {
                    _groundUm[i] = _terrain.HeightMm(i % grid.Columns, i / grid.Columns) * 1000L;
                }
                _groundVersion = _terrain.Version;
            }
            Array.Clear(_steppedTile, 0, _steppedTile.Length);
            for (int t = 0; t < tiles.Length; t++)
            {
                _steppedTile[tiles[t]] = true;
                _tiles[t] = tiles[t];
            }
            int count = tiles.Length;

            // Load the stepped tiles into scratch.
            for (int t = 0; t < count; t++)
            {
                var r = grid.TileRect(_tiles[t]);
                for (int y = r.MinY; y <= r.MaxY; y++)
                {
                    for (int x = r.MinX; x <= r.MaxX; x++)
                    {
                        int i = y * grid.Columns + x;
                        _depth[i] = grid.Values[i];
                        for (int k = 0; k < 4; k++)
                        {
                            _out[k][i] = outGrids[k].Values[i];
                        }
                    }
                }
            }

            for (int s = 0; s < _rules.SubstepsPerFieldStep; s++)
            {
                AddSources(grid);
                context.Runner.For(count, 4, new OutflowKernel(this, grid));
                context.Runner.For(count, 4, new DepthKernel(this, grid));
                // The new values become the current ones for the next substep.
                Swap(ref _depth, ref _depthNext);
                Swap(ref _out, ref _outNext);
            }

            // Store the result.
            for (int t = 0; t < count; t++)
            {
                var r = grid.TileRect(_tiles[t]);
                for (int y = r.MinY; y <= r.MaxY; y++)
                {
                    for (int x = r.MinX; x <= r.MaxX; x++)
                    {
                        int i = y * grid.Columns + x;
                        grid.Next[i] = grid.Clamp(_depth[i]); // the field's cap is the model's (100 m)
                        for (int k = 0; k < 4; k++)
                        {
                            outGrids[k].Next[i] = outGrids[k].Clamp(_out[k][i]);
                        }
                    }
                }
            }
        }

        // Serial: each source's water, split exactly over its cells in a fixed order (only cells on stepped tiles,
        // which every source's are: BeforeStep wakes them each field step).
        private void AddSources(FieldGrid grid)
        {
            for (int s = 0; s < _sourceUmPerSubstep.Length; s++)
            {
                int start = _sourceStart[s];
                int n = _sourceStart[s + 1] - start;
                if (n == 0)
                {
                    continue;
                }
                long each = _sourceUmPerSubstep[s] / n;
                long extra = _sourceUmPerSubstep[s] % n;
                for (int c = 0; c < n; c++)
                {
                    int i = _sourceCells[start + c];
                    if (_steppedTile[grid.TileOf(i % grid.Columns, i / grid.Columns)])
                    {
                        _depth[i] = System.Math.Min(
                            grid.Definition.Max,
                            _depth[i] + each + (c < extra ? 1 : 0)
                        );
                    }
                }
            }
        }

        private static void Swap<T>(ref T a, ref T b)
        {
            T t = a;
            a = b;
            b = t;
        }

        /// <summary>Whether the neighbour of (x, y) in direction k takes part this substep.</summary>
        private bool NeighbourStepped(FieldGrid grid, int nx, int ny) =>
            nx >= 0
            && ny >= 0
            && nx < grid.Columns
            && ny < grid.Rows
            && _steppedTile[grid.TileOf(nx, ny)];

        private static int Dx(int k) =>
            k == 0 ? 1
            : k == 2 ? -1
            : 0;

        private static int Dy(int k) =>
            k == 1 ? 1
            : k == 3 ? -1
            : 0;

        /// <summary>How much water, in µm of the sender's depth, an outflow moves in one substep.</summary>
        private long Moved(long outflowUmPerS) => outflowUmPerS * _substepMs / 1000;

        private readonly struct OutflowKernel : IChunkKernel
        {
            private readonly WaterModel _m;
            private readonly FieldGrid _grid;

            public OutflowKernel(WaterModel model, FieldGrid grid)
            {
                _m = model;
                _grid = grid;
            }

            public void Execute(int start, int end)
            {
                var ground = _m._groundUm;
                var rules = _m._rules;
                var depth = _m._depth;
                for (int t = start; t < end; t++)
                {
                    var r = _grid.TileRect(_m._tiles[t]);
                    for (int y = r.MinY; y <= r.MaxY; y++)
                    {
                        for (int x = r.MinX; x <= r.MaxX; x++)
                        {
                            int i = y * _grid.Columns + x;
                            long head = ground[i] + depth[i];
                            long total = 0;
                            for (int k = 0; k < 4; k++)
                            {
                                int nx = x + Dx(k);
                                int ny = y + Dy(k);
                                long neighbourHead;
                                if (_m.NeighbourStepped(_grid, nx, ny))
                                {
                                    neighbourHead =
                                        ground[ny * _grid.Columns + nx]
                                        + depth[ny * _grid.Columns + nx];
                                }
                                else if (_m._sinkEdge[i * 4 + k])
                                {
                                    neighbourHead = ground[i]; // free outflow
                                }
                                else
                                {
                                    _m._outNext[k][i] = 0; // a closed edge, or a tile not stepping this tick
                                    continue;
                                }
                                // Gravity adds to the outflow in proportion to the head difference and to the water's
                                // own depth (the shallow-water momentum form: a deep river carries more for the same
                                // slope); bed friction takes c_f × speed × dt / depth of it, with speed = outflow ×
                                // cell / depth (depth floored at 1 mm, so a film can't divide by nothing). Together
                                // they settle at speed √(g·depth·slope / c_f).
                                // Every product is bounded first (head differences to ±100 m, outflows and speeds to
                                // 10^9 µm/s), so nothing overflows a long even at the loader's extremes.
                                long h = System.Math.Max(depth[i], 1_000);
                                long dh = Clamp(head - neighbourHead, MaxHeadDifferenceUm);
                                long push = Clamp(
                                    dh * rules.PipeGainPermille / 1000 * h / _m._cellUm,
                                    MaxOutflowUmPerS
                                );
                                long f = Clamp(_m._out[k][i] + push, MaxOutflowUmPerS);
                                long speed = System.Math.Min(
                                    MaxOutflowUmPerS,
                                    _m._out[k][i] * _m._cellUm / h
                                );
                                long loss = speed * _m._substepMs / 1000 * rules.BedFrictionPpm / h;
                                f = System.Math.Max(0, f) * 1_000_000 / (1_000_000 + loss);
                                // Still water goes quiet: a slow flow with almost no head behind it stops (a slow
                                // flow down a real slope keeps building, so films drain).
                                if (
                                    f < rules.FluxDeadbandUmPerS
                                    && head - neighbourHead < rules.HeadDeadbandUm
                                )
                                {
                                    f = 0;
                                }
                                f = System.Math.Min(f, MaxOutflowUmPerS);
                                _m._outNext[k][i] = f;
                                total += _m.Moved(f);
                            }
                            // Never send more than the cell holds: scale the amounts moved by the same ratio
                            // (rounding down), then store outflows that move exactly those amounts or less.
                            if (total > depth[i])
                            {
                                for (int k = 0; k < 4; k++)
                                {
                                    long moved = _m.Moved(_m._outNext[k][i]) * depth[i] / total;
                                    _m._outNext[k][i] = moved * 1000 / _m._substepMs;
                                }
                            }
                        }
                    }
                }
            }
        }

        private readonly struct DepthKernel : IChunkKernel
        {
            private readonly WaterModel _m;
            private readonly FieldGrid _grid;

            public DepthKernel(WaterModel model, FieldGrid grid)
            {
                _m = model;
                _grid = grid;
            }

            public void Execute(int start, int end)
            {
                var depth = _m._depth;
                var next = _m._outNext;
                for (int t = start; t < end; t++)
                {
                    var r = _grid.TileRect(_m._tiles[t]);
                    for (int y = r.MinY; y <= r.MaxY; y++)
                    {
                        for (int x = r.MinX; x <= r.MaxX; x++)
                        {
                            int i = y * _grid.Columns + x;
                            long d = depth[i];
                            for (int k = 0; k < 4; k++)
                            {
                                d -= _m.Moved(next[k][i]);
                                int nx = x + Dx(k);
                                int ny = y + Dy(k);
                                if (_m.NeighbourStepped(_grid, nx, ny))
                                {
                                    // The neighbour's outflow toward this cell is in the opposite direction.
                                    d += _m.Moved(next[(k + 2) & 3][ny * _grid.Columns + nx]);
                                }
                            }
                            _m._depthNext[i] = d;
                        }
                    }
                }
            }
        }
    }
}
