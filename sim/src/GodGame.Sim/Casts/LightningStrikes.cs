using System;
using System.Collections.Generic;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// Lightning v0 and fire v0 (spec S2 12.1–12.2, 13.2; D10, D11), run by the plan-events step:
    /// <list type="bullet">
    /// <item>The strike, when the wind-up ends: logged, recorded as a fear source for herds, and it fells the tallest
    /// standing tree within the search radius (ties: the lower tree index) along the cast's heading, landing the fall's
    /// ticks later; and every fuel cell within the ignition radius whose wetness is under the limit catches, burning for
    /// the burn's ticks as one closed, lethal blocker that never spreads. Rain beforehand wets the fuel: no fire, but the
    /// tree still falls.</item>
    /// <item>The landing: the felled tree is stamped as a battle-made blocker (trunk 1 cell wide and as long as the tree,
    /// foot climbs it; crown over the last 4.5 m, 3 cells wide, closed). Soldiers in its cells are crushed; those within
    /// 1.5 m of it are launched away from it (falling-tree source); animals are never crushed (D6) and walk out.</item>
    /// <item>A fire burns out at its end tick and its cells open again.</item>
    /// </list>
    /// Serial, in cast order then fall and fire order.
    /// </summary>
    public sealed class LightningStrikes
    {
        /// <summary>A crown's length from the tip (S2 13.2).</summary>
        public const int CrownMm = 4_500;

        /// <summary>Soldiers this close to a landing tree's cells, but not in them, are launched (S2 13.2).</summary>
        public const int LaunchWithinMm = 1_500;

        private readonly Navigation _navigation;
        private readonly Ruleset _rules;
        private readonly List<int> _cells = new List<int>();
        private readonly List<byte> _profile = new List<byte>();

        public LightningStrikes(Navigation navigation)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _rules = navigation.Rules;
        }

        /// <summary>The strike of cast <paramref name="c"/> (its wind-up just ended).</summary>
        public void Strike(TickContext context, int c, PlannedCast cast, int castEvent)
        {
            var world = context.World;
            var rules = cast.Power.Lightning!;
            var terrain = _navigation.Terrain;
            int tick = context.Tick;
            var at = new FixVec2(Fix.FromRatio(cast.XMm, 1000), Fix.FromRatio(cast.YMm, 1000));
            terrain.CellOf(at, out int sx, out int sy);
            var log = world.Causes;
            int struck =
                log?.Add(CauseKind.LightningStruck, tick, c, terrain.Index(sx, sy), castEvent)
                ?? CauseLog.None;
            world.Strikes.AddStrike(cast.XMm, cast.YMm, tick, struck);

            // The tallest standing tree within reach.
            var trees = _navigation.Map.Trees;
            int best = -1;
            long reach = (long)rules.TreeSearchMm * rules.TreeSearchMm;
            for (int t = 0; t < trees.Count; t++)
            {
                if (world.Strikes.IsFelled(trees[t].Index))
                {
                    continue;
                }
                long dx = CentreMm(trees[t].Cell % terrain.Columns) - cast.XMm;
                long dy = CentreMm(trees[t].Cell / terrain.Columns) - cast.YMm;
                if (dx * dx + dy * dy > reach)
                {
                    continue;
                }
                if (best < 0 || trees[t].HeightMm > trees[best].HeightMm)
                {
                    best = t; // ties keep the lower index (trees are in index order)
                }
            }
            if (best >= 0)
            {
                world.Strikes.AddFall(
                    trees[best].Index,
                    HerdSystem.Units(cast.HeadingCd),
                    tick + System.Math.Max(1, rules.FallTicks),
                    struck
                );
            }

            // Dry fuel within the ignition radius catches (D11).
            var fuel = _navigation.Map.Fuel;
            if (fuel == null)
            {
                return;
            }
            var fields = world.Fields;
            int[]? wetness =
                fields != null && fields.Rules.IndexOf(GroundModel.Wetness) >= 0
                    ? fields.Get(GroundModel.Wetness).Values
                    : null;
            int cellMm = terrain.CellSizeMm;
            int span = rules.IgnitionRadiusMm / cellMm + 1;
            long ignite = (long)rules.IgnitionRadiusMm * rules.IgnitionRadiusMm;
            _cells.Clear();
            for (
                int y = System.Math.Max(0, sy - span);
                y <= System.Math.Min(terrain.Rows - 1, sy + span);
                y++
            )
            {
                for (
                    int x = System.Math.Max(0, sx - span);
                    x <= System.Math.Min(terrain.Columns - 1, sx + span);
                    x++
                )
                {
                    int cell = terrain.Index(x, y);
                    long dx = CentreMm(x) - cast.XMm;
                    long dy = CentreMm(y) - cast.YMm;
                    if (
                        !fuel[cell]
                        || dx * dx + dy * dy > ignite
                        || (wetness != null && wetness[cell] >= rules.IgnitionMaxWetness)
                        || terrain.IsBurning(cell)
                        || _cells.Count >= Blockers.MaxCells
                    )
                    {
                        continue;
                    }
                    _cells.Add(cell);
                }
            }
            if (_cells.Count == 0)
            {
                return;
            }
            int fire = world.Strikes.FireCount;
            int started =
                log?.Add(CauseKind.FireStarted, tick, fire, world.Blockers.Count, struck)
                ?? CauseLog.None;
            var cells = _cells.ToArray();
            int blocker = _navigation.AddBlocker(
                context,
                BlockerKind.Fire,
                cells,
                new byte[cells.Length],
                started
            );
            world.Strikes.AddFire(blocker, tick + System.Math.Max(1, rules.BurnTicks), started);
        }

        /// <summary>Every tick: falls that land now, fires that burn out now.</summary>
        public void Update(TickContext context)
        {
            var world = context.World;
            var strikes = world.Strikes;
            int tick = context.Tick;
            for (int f = 0; f < strikes.FallCount; f++)
            {
                if (!strikes.FallLanded(f) && strikes.FallTick(f) <= tick)
                {
                    strikes.Land(f);
                    LandTree(context, f);
                }
            }
            for (int f = 0; f < strikes.FireCount; f++)
            {
                if (!strikes.FireOut(f) && strikes.FireEnds(f) <= tick)
                {
                    strikes.PutOut(f);
                    _navigation.ClearBlocker(context, strikes.FireBlocker(f));
                }
            }
        }

        private void LandTree(TickContext context, int f)
        {
            var world = context.World;
            var strikes = world.Strikes;
            var terrain = _navigation.Terrain;
            TreeSpec? tree = null;
            foreach (var t in _navigation.Map.Trees)
            {
                if (t.Index == strikes.FallTree(f))
                {
                    tree = t;
                    break;
                }
            }
            if (tree == null || _rules.Terrain.FelledTree == null)
            {
                return;
            }
            Footprint(tree, Angle.FromUnits(strikes.FallHeading(f)));
            if (_cells.Count == 0)
            {
                return;
            }
            var log = world.Causes;
            int felled =
                log?.Add(
                    CauseKind.TreeFelled,
                    context.Tick,
                    tree.Index,
                    world.Blockers.Count,
                    strikes.FallCause(f)
                )
                ?? CauseLog.None;
            var cells = _cells.ToArray();
            var profile = _profile.ToArray();

            // Crush and launch from the world as the tree finds it, in unit slot order.
            var source = _rules.Launches.Source("falling-tree");
            long near = Fix.FromRatio(LaunchWithinMm + terrain.CellSizeMm / 2, 1000).Raw;
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (
                    !world.Units.IsAlive(u)
                    || (world.Flags[u] & UnitFlags.Launched) != 0
                    || _rules.UnitTypes[world.UnitType[u]].IsAnimal
                )
                {
                    continue;
                }
                var p = world.Position[u];
                terrain.CellOf(p, out int ux, out int uy);
                int own = terrain.Index(ux, uy);
                long bestSq = long.MaxValue;
                FixVec2 nearest = p;
                bool inside = false;
                foreach (int cell in cells)
                {
                    if (cell == own)
                    {
                        inside = true;
                        break;
                    }
                    var centre = terrain.CellCentre(cell % terrain.Columns, cell / terrain.Columns);
                    long dx = p.X.Raw - centre.X.Raw;
                    long dy = p.Y.Raw - centre.Y.Raw;
                    long dSq = dx * dx + dy * dy;
                    if (dSq < bestSq)
                    {
                        bestSq = dSq;
                        nearest = centre;
                    }
                }
                if (inside)
                {
                    int r = world.UnitRegiment[u];
                    if (EngineCrews.IsEngine(world, _rules, u))
                    {
                        CombatSystem.Wreck(world, _navigation, context, u, CauseKind.EngineWrecked);
                        continue;
                    }
                    log?.Add(CauseKind.SoldierCrushed, context.Tick, r, u, felled);
                    world.DespawnUnit(world.Units.IdOf(u));
                    world.Counters.Killed++;
                    continue;
                }
                if (
                    source == null
                    || bestSq > near * near
                    || EngineCrews.IsEngine(world, _rules, u)
                )
                {
                    continue;
                }
                var away = p - nearest;
                Ballistics.Launch(
                    world,
                    _rules.Launches,
                    u,
                    source,
                    tree.Index,
                    away == FixVec2.Zero ? Angle.FromUnits(strikes.FallHeading(f)) : away.Direction,
                    felled,
                    context.Tick,
                    context.Seed,
                    Ballistics.SurfaceMm(terrain, world, ux, uy)
                );
            }
            _navigation.AddBlocker(context, BlockerKind.FelledTree, cells, profile, felled);
        }

        /// <summary>
        /// The felled tree's cells, trunk then crown (profile 0 and 1): from the stump's neighbour along the heading, a
        /// cell every half cell, as long as the tree; the last <see cref="CrownMm"/> also take the cells either side.
        /// </summary>
        private void Footprint(TreeSpec tree, Angle heading)
        {
            var terrain = _navigation.Terrain;
            _cells.Clear();
            _profile.Clear();
            var stump = terrain.CellCentre(
                tree.Cell % terrain.Columns,
                tree.Cell / terrain.Columns
            );
            int half = terrain.CellSizeMm / 2;
            var along = FixVec2.FromAngle(heading, Fix.One);
            var across = FixVec2.FromAngle(
                heading + Angle.FromUnits(Angle.UnitsPerTurn / 4),
                Fix.One
            );
            Fix side = Fix.FromRatio(terrain.CellSizeMm, 1000);
            for (int d = half; d <= tree.HeightMm; d += half)
            {
                var point = stump + along * Fix.FromRatio(d, 1000);
                bool crown = d > tree.HeightMm - CrownMm;
                Add(point, crown);
                if (crown)
                {
                    Add(point + across * side, true);
                    Add(point - across * side, true);
                }
            }
        }

        private void Add(FixVec2 point, bool crown)
        {
            var terrain = _navigation.Terrain;
            var map = _navigation.Map;
            if (
                point.X < Fix.Zero
                || point.Y < Fix.Zero
                || point.X >= map.Width
                || point.Y >= map.Depth
                || _cells.Count >= Blockers.MaxCells
            )
            {
                return;
            }
            terrain.CellOf(point, out int x, out int y);
            int cell = terrain.Index(x, y);
            int at = _cells.IndexOf(cell);
            if (at >= 0)
            {
                if (crown)
                {
                    _profile[at] = 1; // a crown cell wins over the trunk it overlaps
                }
                return;
            }
            _cells.Add(cell);
            _profile.Add(crown ? (byte)1 : (byte)0);
        }

        private long CentreMm(int index) =>
            (long)index * _navigation.Terrain.CellSizeMm + _navigation.Terrain.CellSizeMm / 2;
    }
}
