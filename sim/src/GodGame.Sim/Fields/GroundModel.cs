using System;
using System.Collections.Generic;
using GodGame.Sim.Rules;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim
{
    /// <summary>
    /// Wetness and mud (spec S1, sections 5 and 6; power palette: "mud follows from wetness; worsens as many feet
    /// churn it"). Runs after water in the field step, on the same tiles' wetness, mud and water:
    /// <list type="bullet">
    /// <item>Standing water soaks into soil that isn't full, at <see cref="GroundRules.InfiltrationUmPerS"/>, and
    /// leaves the water field.</item>
    /// <item>Ground with no water at all dries at <see cref="GroundRules.DryingPerS"/> down to the field's initial wetness
    /// (the map's natural moisture), so a dry map stays asleep.</item>
    /// <item>Mud follows wetness between two curves: untrodden wet ground is mud of (wetness − base start) by
    /// itself, and the most mud a wetness allows is (wetness − cap start) × num / den, scaled by the cell's soil.</item>
    /// <item>Each moving soldier churns the cell it stands in by its class's weight × the churn at full wetness ×
    /// the soil's churn share × how wet the ground is, so the vanguard strides through and the rear wades.</item>
    /// </list>
    /// Water wakes the wetness tiles under it, and soldiers moving over wet ground wake theirs. The churn tally and
    /// the soil map are scratch and derived data; everything lasting lives in the grids.
    /// </summary>
    public sealed class GroundModel : IFieldModel
    {
        public const string Wetness = "wetness";
        public const string Mud = "mud";

        public const int Full = 65_535;

        private static readonly IReadOnlyList<string> Writes = Array.AsReadOnly(
            new[] { Mud, WaterModel.Depth }
        );

        private readonly Ruleset _rules;
        private readonly GroundRules _ground;
        private readonly long _infiltrationPerStep;
        private readonly int _dryingPerStep;
        private readonly byte[] _soil; // soil index per cell
        private readonly Terrain _terrain;

        // Scratch.
        private long[] _churn = new long[0];
        private int[] _touched = new int[0];
        private bool[] _due = new bool[0];
        private int[] _awake = new int[0];
        private int[] _tileList = new int[0];

        public GroundModel(Terrain terrain, MapData map, Ruleset rules, TickRate rate)
        {
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }
            _ground =
                rules.Fields.Ground
                ?? throw new ArgumentException("The rules have no ground section.", nameof(rules));
            Rate = rate;
            int stepMs = rate.Period * 1000 / SimConstants.TicksPerSecond;
            _infiltrationPerStep = (long)_ground.InfiltrationUmPerS * stepMs / 1000;
            _dryingPerStep = _ground.DryingPerS * stepMs / 1000;

            _soil = new byte[terrain.CellCount];
            foreach (var area in map.SoilAreas)
            {
                int soil = _ground.SoilIndex(area.Soil);
                if (soil < 0)
                {
                    throw new DataFormatException(
                        "Map "
                            + map.Id
                            + " uses the soil \""
                            + area.Soil
                            + "\", which the rules don't define."
                    );
                }
                var cells = terrain.CellsOf(area.Area);
                for (int y = cells.MinY; y <= cells.MaxY; y++)
                {
                    for (int x = cells.MinX; x <= cells.MaxX; x++)
                    {
                        _soil[terrain.Index(x, y)] = (byte)soil;
                    }
                }
            }
        }

        public string FieldId => Wetness;

        public IReadOnlyList<string> AlsoWrites => Writes;

        public TickRate Rate { get; }

        public bool Staggered => false;

        /// <summary>The soil index of a cell (0, the default, where no soil area covers it).</summary>
        public int SoilAt(int cell) => _soil[cell];

        public void BeforeStep(FieldStepContext context)
        {
            if (!Rate.IsDue(context.Tick.Tick))
            {
                return;
            }
            var fields = context.Fields;
            var wetness = fields.Get(Wetness);
            var water = fields.Get(WaterModel.Depth);
            if (_awake.Length < water.TileCount)
            {
                _awake = new int[water.TileCount];
            }
            // Water on a tile soaks into it: wake the same tile's ground.
            int awake = water.AwakeTiles(_awake);
            for (int k = 0; k < awake; k++)
            {
                wetness.Wake(_awake[k]);
            }
            // Soldiers churning wet ground.
            var world = context.Tick.World;
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (!world.Units.IsAlive(u) || world.Velocity[u] == Math.FixVec2.Zero)
                {
                    continue;
                }
                _terrain.CellOf(world.Position[u], out int x, out int y);
                if (wetness.Get(x, y) > _ground.CapStart)
                {
                    wetness.Wake(wetness.TileOf(x, y));
                }
            }
        }

        public void Step(FieldStepContext context, FieldGrid grid, ReadOnlySpan<int> tiles)
        {
            var fields = context.Fields;
            var mud = fields.Get(Mud);
            var water = fields.Get(WaterModel.Depth);
            int cells = grid.Columns * grid.Rows;
            if (_churn.Length != cells)
            {
                _churn = new long[cells];
                _touched = new int[256];
            }
            if (_due.Length != grid.TileCount)
            {
                _due = new bool[grid.TileCount];
            }
            Array.Clear(_due, 0, _due.Length);
            if (_tileList.Length < tiles.Length)
            {
                _tileList = new int[grid.TileCount];
            }
            for (int t = 0; t < tiles.Length; t++)
            {
                _due[tiles[t]] = true;
                _tileList[t] = tiles[t];
            }

            // Serial, in slot order: every moving soldier on a stepping tile adds its churn to its cell.
            int touched = 0;
            var world = context.Tick.World;
            long span = Full - _ground.CapStart;
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (!world.Units.IsAlive(u) || world.Velocity[u] == Math.FixVec2.Zero)
                {
                    continue;
                }
                _terrain.CellOf(world.Position[u], out int x, out int y);
                int i = _terrain.Index(x, y);
                long wet = grid.Values[i] - _ground.CapStart;
                if (wet <= 0 || !_due[grid.TileOf(x, y)])
                {
                    continue;
                }
                var movementClass = _rules.UnitTypes[world.UnitType[u]].MovementClass;
                long churn =
                    (long)_ground.ChurnWeight(movementClass)
                    * _ground.ChurnAtFullWetness
                    * _ground.Soils[_soil[i]].ChurnPermille
                    / 1000
                    * wet
                    / span;
                if (churn == 0)
                {
                    continue;
                }
                if (_churn[i] == 0)
                {
                    if (touched == _touched.Length)
                    {
                        Array.Resize(ref _touched, _touched.Length * 2);
                    }
                    _touched[touched++] = i;
                }
                _churn[i] += churn;
            }

            context.Runner.For(tiles.Length, 4, new Kernel(this, grid, mud, water, _tileList));

            for (int k = 0; k < touched; k++)
            {
                _churn[_touched[k]] = 0;
            }
        }

        private readonly struct Kernel : IChunkKernel
        {
            private readonly GroundModel _m;
            private readonly FieldGrid _wetness;
            private readonly FieldGrid _mud;
            private readonly FieldGrid _water;
            private readonly int[] _tiles;

            public Kernel(
                GroundModel model,
                FieldGrid wetness,
                FieldGrid mud,
                FieldGrid water,
                int[] tiles
            )
            {
                _m = model;
                _wetness = wetness;
                _mud = mud;
                _water = water;
                _tiles = tiles;
            }

            public void Execute(int start, int end)
            {
                var g = _m._ground;
                int natural = _wetness.Definition.Initial;
                for (int t = start; t < end; t++)
                {
                    var r = _wetness.TileRect(_tiles[t]);
                    for (int y = r.MinY; y <= r.MaxY; y++)
                    {
                        for (int x = r.MinX; x <= r.MaxX; x++)
                        {
                            int i = y * _wetness.Columns + x;
                            long w = _wetness.Values[i];
                            long d = _water.Values[i];
                            if (d > 0 && w < Full)
                            {
                                // Soak: as much as the rate, the water and the soil's room allow.
                                long room = (Full - w) / g.UnitsPerUm; // rounded down: no water soaks into nowhere
                                long soak = System.Math.Min(
                                    _m._infiltrationPerStep,
                                    System.Math.Min(d, room)
                                );
                                w = System.Math.Min(Full, w + soak * g.UnitsPerUm);
                                d -= soak;
                            }
                            else if (d == 0 && w > natural)
                            {
                                w = System.Math.Max(natural, w - _m._dryingPerStep);
                            }

                            long mud = _mud.Values[i];
                            long baseMud = System.Math.Max(0, w - g.BaseStart);
                            long cap =
                                System.Math.Min(
                                    Full,
                                    System.Math.Max(0, w - g.CapStart) * g.CapNum / g.CapDen
                                )
                                * g.Soils[_m._soil[i]].CapPermille
                                / 1000;
                            mud = System.Math.Max(baseMud, mud + _m._churn[i]);
                            mud = System.Math.Max(0, System.Math.Min(cap, mud));

                            _wetness.Next[i] = (int)w;
                            _mud.Next[i] = (int)mud;
                            _water.Next[i] = (int)d;
                        }
                    }
                }
            }
        }
    }
}
