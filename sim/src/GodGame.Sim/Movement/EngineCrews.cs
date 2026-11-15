using System;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// Engines and their crews (spec S2 2.1, decision D4). An engine regiment (siege engines, the Pontoon Train's boats)
    /// holds its engines in formation slots 0 to E − 1 and their crews after them, engine by engine: crew soldier k ≥ E
    /// belongs to engine (k − E) / crewPerEngine and stands on a ring around it. A crew soldier whose engine is gone
    /// follows the regiment's lowest-slot engine still standing. An engine moves at full speed with at least crewToMove
    /// crew within crewRadius, at crawlPermille with fewer, and stands with none.
    /// <para>
    /// Derived, never stored: <see cref="Gather"/> reads the world as it is (serial, in unit slot order) and the queries
    /// read only what it gathered, so parallel kernels may call them.
    /// </para>
    /// </summary>
    public sealed class EngineCrews
    {
        public const int MaxEngines = RulesetLoader.MaxEngines;

        private readonly Ruleset _rules;
        private int[] _engineUnit = new int[0]; // [regiment × MaxEngines + engine]: the engine's unit slot, or -1
        private int[] _firstEngine = new int[0]; // per regiment: its lowest-slot live engine, or -1
        private int[] _livingEngines = new int[0];
        private int[] _livingCrew = new int[0];
        private int[] _crewNear = new int[0]; // per unit slot: crew within crewRadius of this engine
        private readonly long[] _crewRadiusRaw; // per type, for engine types

        public EngineCrews(Ruleset rules)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _crewRadiusRaw = new long[rules.UnitTypes.Count];
            for (int t = 0; t < rules.UnitTypes.Count; t++)
            {
                var engine = rules.UnitTypes[t].Engine;
                _crewRadiusRaw[t] =
                    engine == null ? 0 : Fix.FromRatio(engine.CrewRadiusMm, 1000).Raw;
            }
        }

        /// <summary>Whether a unit is an engine (its regiment's own type, in an engine regiment).</summary>
        public static bool IsEngine(World world, Ruleset rules, int u) =>
            rules.UnitTypes[world.UnitType[u]].Engine != null;

        /// <summary>
        /// Where crew soldier <paramref name="j"/> of <paramref name="perEngine"/> stands around his engine: evenly on a ring
        /// of radius <paramref name="ring"/>, the first a half step off the engine's left.
        /// </summary>
        public static FixVec2 CrewOffset(int j, int perEngine, Fix ring, Angle facing)
        {
            int step = Angle.UnitsPerTurn / System.Math.Max(1, perEngine);
            return FixVec2.FromAngle(
                facing + Angle.QuarterTurn + Angle.FromUnits(step / 2 + j * step),
                ring
            );
        }

        /// <summary>Reads the engines and crews of every engine regiment; with <paramref name="countNear"/>, the crew beside each engine.</summary>
        public void Gather(World world, bool countNear = true)
        {
            int regiments = world.Regiments.Capacity;
            if (_firstEngine.Length < regiments)
            {
                _engineUnit = new int[regiments * MaxEngines];
                _firstEngine = new int[regiments];
                _livingEngines = new int[regiments];
                _livingCrew = new int[regiments];
            }
            if (_crewNear.Length < world.Units.Capacity)
            {
                _crewNear = new int[world.Units.Capacity];
            }
            for (int r = 0; r < regiments; r++)
            {
                _firstEngine[r] = -1;
                _livingEngines[r] = 0;
                _livingCrew[r] = 0;
            }
            for (int i = 0; i < regiments * MaxEngines; i++)
            {
                _engineUnit[i] = -1;
            }
            var types = _rules.UnitTypes;
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (!world.Units.IsAlive(u))
                {
                    continue;
                }
                int r = world.UnitRegiment[u];
                if (types[world.RegimentUnitType[r]].Engine == null)
                {
                    continue;
                }
                if (world.UnitType[u] == world.RegimentUnitType[r])
                {
                    _crewNear[u] = 0;
                    int e = world.FormationSlot[u];
                    if (e < MaxEngines)
                    {
                        _engineUnit[r * MaxEngines + e] = u;
                        if (_firstEngine[r] < 0 || e < world.FormationSlot[_firstEngine[r]])
                        {
                            _firstEngine[r] = u;
                        }
                    }
                    _livingEngines[r]++;
                }
                else
                {
                    _livingCrew[r]++;
                }
            }
            if (!countNear)
            {
                return;
            }
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (
                    !world.Units.IsAlive(u)
                    || (world.Flags[u] & (UnitFlags.Launched | UnitFlags.Swept | UnitFlags.Stunned))
                        != 0
                )
                {
                    continue; // airborne, swept or lying stunned: not working the engine
                }
                int r = world.UnitRegiment[u];
                int regimentType = world.RegimentUnitType[r];
                if (types[regimentType].Engine == null || world.UnitType[u] == regimentType)
                {
                    continue;
                }
                long radius = _crewRadiusRaw[regimentType];
                var p = world.Position[u];
                for (int e = 0; e < MaxEngines; e++)
                {
                    int engine = _engineUnit[r * MaxEngines + e];
                    if (engine < 0)
                    {
                        continue;
                    }
                    long dx = world.Position[engine].X.Raw - p.X.Raw;
                    long dy = world.Position[engine].Y.Raw - p.Y.Raw;
                    // Axis test first, so soldiers far apart never overflow the square.
                    if (
                        dx <= radius
                        && dx >= -radius
                        && dy <= radius
                        && dy >= -radius
                        && dx * dx + dy * dy <= radius * radius
                    )
                    {
                        _crewNear[engine]++;
                    }
                }
            }
        }

        /// <summary>Crew on their feet within crewRadius of engine unit <paramref name="engine"/>, from the last gather.</summary>
        public int CrewNear(int engine) => _crewNear[engine];

        public int LivingEngines(int regiment) => _livingEngines[regiment];

        public int LivingCrew(int regiment) => _livingCrew[regiment];

        /// <summary>The engine unit of slot <paramref name="e"/> in regiment <paramref name="regiment"/>, or -1.</summary>
        public int EngineUnit(int regiment, int e) => _engineUnit[regiment * MaxEngines + e];

        /// <summary>
        /// The engine crew soldier <paramref name="u"/> walks beside: his own while it stands, else his regiment's
        /// lowest-slot engine; -1 when none is left.
        /// </summary>
        public int EngineOf(World world, int u)
        {
            int r = world.UnitRegiment[u];
            var type = _rules.UnitTypes[world.RegimentUnitType[r]];
            var engine = type.Engine;
            if (engine == null)
            {
                return -1;
            }
            int engines = type.EnginesIn(world.RegimentStartingSize[r]);
            int e = (world.FormationSlot[u] - engines) / engine.CrewPerEngine;
            if (e >= 0 && e < MaxEngines && _engineUnit[r * MaxEngines + e] >= 0)
            {
                return _engineUnit[r * MaxEngines + e];
            }
            return _firstEngine[r];
        }

        /// <summary>His place on his engine's crew ring (0 to crewPerEngine − 1).</summary>
        public int CrewIndex(World world, int u)
        {
            int r = world.UnitRegiment[u];
            var type = _rules.UnitTypes[world.RegimentUnitType[r]];
            int per = type.Engine!.CrewPerEngine;
            int engines = type.EnginesIn(world.RegimentStartingSize[r]);
            return System.Math.Max(0, world.FormationSlot[u] - engines) % per;
        }

        /// <summary>
        /// The cells a wreck covers (spec S2 13.2): those whose centre lies within the engine's body radius of its position,
        /// and always the cell under it; in row-major order.
        /// </summary>
        public static int[] WreckCells(Terrain terrain, FixVec2 position, int radiusMm)
        {
            terrain.CellOf(position, out int cx, out int cy);
            int reach = radiusMm / System.Math.Max(1, terrain.CellSizeMm) + 1;
            long radius = Fix.FromRatio(radiusMm, 1000).Raw;
            var cells = new System.Collections.Generic.List<int>();
            for (int y = cy - reach; y <= cy + reach; y++)
            {
                for (int x = cx - reach; x <= cx + reach; x++)
                {
                    if (!terrain.InBounds(x, y))
                    {
                        continue;
                    }
                    var centre = terrain.CellCentre(x, y);
                    long dx = centre.X.Raw - position.X.Raw;
                    long dy = centre.Y.Raw - position.Y.Raw;
                    if ((x == cx && y == cy) || dx * dx + dy * dy <= radius * radius)
                    {
                        cells.Add(terrain.Index(x, y));
                    }
                }
            }
            return cells.ToArray();
        }

        /// <summary>An engine's speed share by the crew beside it, ‰: full, the crawl, or 0 (it stands).</summary>
        public static int SpeedPermille(EngineRules engine, int crewNear) =>
            crewNear >= engine.CrewToMove ? 1000
            : crewNear > 0 ? engine.CrawlPermille
            : 0;
    }
}
