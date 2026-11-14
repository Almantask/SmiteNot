using System;
using System.Collections.Generic;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// The shared specialist executor (spec S2, section 14.1), run inside the regiment AI step at 4 Hz. Every specialist
    /// regiment's action is data on its unit type; the executor runs them in regiment slot order: a regiment that is
    /// wiped or routs loses its unspent charges; an enemy outline within 40 m pauses the work while the specialists fall
    /// back 30 m, and it resumes after 10 s clear; edits go in batches through <see cref="Navigation.ApplyGroundEdit"/>,
    /// which skips a batch that would close the last route. Actions in M5 so far: Spademen dig the ditch and bank the
    /// player drew (S2 14.3); Fascine Carriers fill one crossing where their path meets a channel, ditch, mud or a ford
    /// (S2 14.4). Each action logs a cause event when it starts, when it ends and when a batch is skipped.
    /// <para>
    /// Determinism: everything here is serial and integer; the ditch's cells are derived from the plan and the played map
    /// (rebuilt the same way after a restore); everything that changes is in <see cref="ArmyState"/> and
    /// <see cref="GroundEdits"/>.
    /// </para>
    /// </summary>
    public sealed class SpecialistExecutor
    {
        /// <summary>Cause event detail: the action.</summary>
        public const int DigDitchAction = 1;

        public const int FillCrossingAction = 2;

        /// <summary>The officer works once he is this close to the work point (placeholder; no spec sets it).</summary>
        public const int WorkReachMm = 6_000;

        /// <summary>Fills are applied once a second, so the route check runs at most once a second per regiment.</summary>
        public const int FillBatchTicks = 20;

        private readonly Ruleset _rules;
        private readonly Navigation _navigation;
        private readonly BattlePlan _plan;
        private readonly RegimentSummary _summary;
        private Ditch?[] _ditches = new Ditch?[0]; // derived from the plan, per regiment slot
        private bool[] _ditchBuilt = new bool[0];
        private int[] _cells = new int[64];
        private int[] _heights = new int[64];

        public SpecialistExecutor(
            Ruleset rules,
            Navigation navigation,
            BattlePlan plan,
            RegimentSummary summary
        )
        {
            _rules = rules;
            _navigation = navigation;
            _plan = plan;
            _summary = summary;
        }

        /// <summary>
        /// Runs regiment <paramref name="r"/>'s action this AI tick; false when the regiment is free for the ordinary
        /// targeting rules (no action, waiting for its trigger, or finished).
        /// </summary>
        public bool Run(TickContext context, int r)
        {
            var world = context.World;
            var army = world.Army;
            var type = _rules.UnitTypes[world.RegimentUnitType[r]];
            if (
                type.Action == null
                || army.Job[r] == SpecialistJob.Lost
                || army.Job[r] == SpecialistJob.Done
            )
            {
                return false;
            }
            bool engineers = type.Does("bridgework") || type.Does("clear-blocker");
            int code =
                engineers ? army.JobKind[r]
                : string.Equals(type.Action, "dig-ditch", StringComparison.Ordinal) ? DigDitchAction
                : FillCrossingAction;
            if (army.Wiped[r] || army.Order[r] == RegimentOrder.Rout)
            {
                // Wiped or routed: the unspent charges are lost (brief 2026-10-10).
                if (army.Job[r] == SpecialistJob.Active)
                {
                    world.Causes?.Add(
                        CauseKind.SpecialistEnded,
                        context.Tick,
                        r,
                        code,
                        CauseLog.None
                    );
                }
                army.Job[r] = SpecialistJob.Lost;
                army.JobCells[r] = null;
                army.JobTargets[r] = null;
                return false;
            }
            if (army.Job[r] == SpecialistJob.Active && Threatened(world, r, context.Tick))
            {
                return true;
            }
            if (engineers)
            {
                return Engineer(context, r, type);
            }
            if (code == DigDitchAction)
            {
                return Dig(context, r);
            }
            return Fill(context, r);
        }

        // ---- Engineers: bridgework and clearing (S2 14.7) ----

        public const int TrestleAction = 3;
        public const int RepairAction = 4;
        public const int ClearAction = 5;

        private readonly List<int> _path = new List<int>();

        /// <summary>
        /// Engineers take one job at a time, in this order: clear a boulder on the path ahead, span a short gap on it with a
        /// trestle, repair a weakened wooden span nearby. Between jobs they march with their army; they are done when
        /// both their timber and their clearing time are spent.
        /// </summary>
        private bool Engineer(TickContext context, int r, UnitTypeRules type)
        {
            var world = context.World;
            var army = world.Army;
            var bridgework = type.Does("bridgework") ? _rules.Specialists.Bridgework : null;
            var clearing = type.Does("clear-blocker") ? _rules.Specialists.ClearBlocker : null;
            int start = world.RegimentStartingSize[r];
            int timber =
                bridgework == null
                    ? 0
                    : (int)((long)start * bridgework.TimberMmPer40Soldiers / 40) - army.Spent[r];
            int pool =
                clearing == null
                    ? 0
                    : (int)((long)start * clearing.TicksPer40Soldiers / 40) - army.Spent2[r];
            if (army.Job[r] == SpecialistJob.Idle)
            {
                if (
                    (bridgework == null || timber <= 0)
                    && (clearing == null || pool < clearing.BoulderTicks)
                )
                {
                    army.Job[r] = SpecialistJob.Done;
                    return false;
                }
                int kind =
                    clearing != null
                    && pool >= clearing.BoulderTicks
                    && FindBoulder(world, r, clearing)
                        ? ClearAction
                    : bridgework != null && FindGap(world, r, bridgework, timber) ? TrestleAction
                    : bridgework != null && timber > 0 && FindWeakSpan(world, r, bridgework)
                        ? RepairAction
                    : 0;
                if (kind == 0)
                {
                    return false; // marching with the army until a job turns up
                }
                army.Job[r] = SpecialistJob.Active;
                army.JobKind[r] = kind;
                army.WorkProgress[r] = 0;
                world.Causes?.Add(
                    CauseKind.SpecialistStarted,
                    context.Tick,
                    r,
                    kind,
                    CauseLog.None
                );
            }
            army.Order[r] = RegimentOrder.Work;
            army.Target[r] = -1;
            WorkPoint(world, r);
            if (!AtWork(world, r))
            {
                return true;
            }
            bool finished;
            switch (army.JobKind[r])
            {
                case ClearAction:
                    if (world.Ground.IsCleared(army.JobRef[r]))
                    {
                        finished = true; // another regiment finished it first: nothing to clear, nothing spent
                        break;
                    }
                    army.WorkProgress[r] += TickOrder.RegimentAiRate.Period;
                    finished = army.WorkProgress[r] >= clearing!.BoulderTicks;
                    if (finished)
                    {
                        int boulder = army.JobRef[r];
                        _navigation.ClearBoulder(context, boulder);
                        world.Ground.AddCleared(boulder);
                        army.Spent2[r] += clearing.BoulderTicks;
                    }
                    break;
                case TrestleAction:
                    finished = BuildTrestle(context, r, bridgework!);
                    break;
                default:
                    finished = Repair(world, r, bridgework!, timber);
                    break;
            }
            if (!finished)
            {
                return true;
            }
            world.Causes?.Add(
                CauseKind.SpecialistEnded,
                context.Tick,
                r,
                army.JobKind[r],
                CauseLog.None
            );
            army.Job[r] = SpecialistJob.Idle;
            army.JobKind[r] = 0;
            army.JobRef[r] = -1;
            army.JobCells[r] = null;
            army.JobTargets[r] = null;
            army.WorkStep[r] = 0;
            army.Order[r] = RegimentOrder.March;
            return false;
        }

        /// <summary>Where the engineers stand for their job: by the boulder, at the near bank, on the weak span.</summary>
        private void WorkPoint(World world, int r)
        {
            var army = world.Army;
            var terrain = _navigation.Terrain;
            int cell = army.JobKind[r] switch
            {
                ClearAction => army.WorkStep[r],
                TrestleAction => army.WorkStep[r] < 0
                    ? 0
                    : army.JobTargets[r]![army.JobTargets[r]!.Length - 1],
                _ => terrain.Segment(army.JobRef[r]).Cells[0],
            };
            army.Point[r] = terrain.CellCentre(cell % terrain.Columns, cell / terrain.Columns);
        }

        /// <summary>The cells of the regiment's path ahead of its officer, along its flow field, up to a distance.</summary>
        private List<int> PathAhead(World world, int r, int aheadMm)
        {
            _path.Clear();
            var field = _navigation.FieldOf(r);
            int officer = world.RegimentAnchor[r];
            if (field == null || officer < 0)
            {
                return _path;
            }
            var directions = field.FreshDirections();
            var terrain = _navigation.Terrain;
            terrain.CellOf(world.Position[officer], out int x, out int y);
            int steps = aheadMm / terrain.CellSizeMm;
            _path.Add(terrain.Index(x, y));
            for (int k = 0; k < steps; k++)
            {
                byte d = directions[terrain.Index(x, y)];
                if (d >= FlowField.AtGoal)
                {
                    break;
                }
                var (dx, dy) = FlowField.Offset(d);
                if (!terrain.InBounds(x + dx, y + dy))
                {
                    break;
                }
                x += dx;
                y += dy;
                _path.Add(terrain.Index(x, y));
            }
            return _path;
        }

        /// <summary>Whether another regiment (any slot) is already clearing this boulder.</summary>
        private static bool HeldByAnother(World world, int r, int boulder)
        {
            var army = world.Army;
            for (int o = 0; o < world.Regiments.HighWater; o++)
            {
                if (
                    o != r
                    && world.Regiments.IsAlive(o)
                    && army.Job[o] == SpecialistJob.Active
                    && army.JobKind[o] == ClearAction
                    && army.JobRef[o] == boulder
                )
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A placed, uncleared boulder within reach and close to the path ahead (path order, then boulder order).</summary>
        private bool FindBoulder(World world, int r, ClearBlockerRules clearing)
        {
            var boulders = _navigation.Map.Boulders;
            if (boulders.Count == 0)
            {
                return false;
            }
            var terrain = _navigation.Terrain;
            int near = System.Math.Max(1, clearing.NearPathMm / terrain.CellSizeMm);
            var path = PathAhead(world, r, clearing.WithinMm);
            foreach (int p in path)
            {
                int px = p % terrain.Columns;
                int py = p / terrain.Columns;
                for (int b = 0; b < boulders.Count; b++)
                {
                    if (world.Ground.IsCleared(b) || HeldByAnother(world, r, b))
                    {
                        continue;
                    }
                    foreach (int cell in boulders[b].Cells)
                    {
                        if (
                            System.Math.Abs(cell % terrain.Columns - px) <= near
                            && System.Math.Abs(cell / terrain.Columns - py) <= near
                        )
                        {
                            world.Army.JobRef[r] = b;
                            world.Army.WorkStep[r] = p; // where to stand: the path beside it
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// A gap on the path ahead (S2 14.7): a run of path cells closed to wheeled over water, a channel or a ditch, ending
        /// at a cell open to wheeled, no longer than the trestle's reach and within the timber left. The run's cells go to
        /// the job; the last target entry is the near bank, where the engineers stand; every other target is the deck height.
        /// </summary>
        private bool FindGap(World world, int r, BridgeworkRules bridgework, int timber)
        {
            var terrain = _navigation.Terrain;
            var costs = _navigation.Costs;
            var path = PathAhead(world, r, bridgework.AheadMm);
            var fields = world.Fields;
            int[]? depth =
                fields != null && fields.Rules.IndexOf(WaterModel.Depth) >= 0
                    ? fields.Get(WaterModel.Depth).Values
                    : null;
            int dry = _rules.Fields.Water?.DryDepthUm ?? int.MaxValue;
            for (int k = 1; k < path.Count; k++)
            {
                int cell = path[k];
                int x = cell % terrain.Columns;
                int y = cell / terrain.Columns;
                int bank = path[k - 1];
                int rim = terrain.HeightMm(bank % terrain.Columns, bank / terrain.Columns);
                bool closed = costs.Cost(MovementClass.Wheeled, x, y) == CostFields.Impassable;
                bool gap =
                    closed
                    && !terrain.IsBlocked(cell)
                    && terrain.StandingSegment(cell) < 0
                    && (
                        terrain.HeightMm(x, y) <= rim - 300 || (depth != null && depth[cell] >= dry)
                    );
                if (!gap)
                {
                    continue;
                }
                var run = new List<int>();
                long lengthMm = 0;
                int j = k;
                for (; j < path.Count; j++)
                {
                    int c = path[j];
                    if (terrain.SegmentAt(c) >= 0)
                    {
                        return false; // under another deck (a failed span): not a trestle's gap
                    }
                    bool open =
                        costs.Cost(MovementClass.Wheeled, c % terrain.Columns, c / terrain.Columns)
                        != CostFields.Impassable;
                    bool low =
                        terrain.HeightMm(c % terrain.Columns, c / terrain.Columns) <= rim - 300
                        || (depth != null && depth[c] >= dry);
                    if (open && !low)
                    {
                        break; // the far bank: back at the rim's level, open to wheeled
                    }
                    int prev = path[j - 1];
                    bool diagonal =
                        c % terrain.Columns != prev % terrain.Columns
                        && c / terrain.Columns != prev / terrain.Columns;
                    lengthMm += diagonal ? terrain.CellSizeMm * 1414L / 1000 : terrain.CellSizeMm;
                    run.Add(c);
                }
                var trestle = _rules.Structures.Kind("trestle");
                if (j >= path.Count || trestle == null || lengthMm > bridgework.MaxGapMm)
                {
                    return false; // no far bank in sight, or too wide: not a trestle's job
                }
                // The deck rests on both banks, so it starts and ends on ground at the rim's level: the bank cells beside a
                // channel are steep (a cell's slope is its steepest step), and only a deck makes them flat for wheeled.
                run.Insert(0, bank);
                run.Add(path[j]);
                int segments =
                    (run.Count + trestle.SegmentCellsAlong - 1) / trestle.SegmentCellsAlong;
                if ((long)segments * bridgework.TimberMmPerSegment > timber)
                {
                    return false;
                }
                // Targets: the deck height for each run cell, then the near bank cell (where the engineers stand).
                var targets = new int[run.Count + 1];
                for (int t = 0; t < run.Count; t++)
                {
                    targets[t] = rim;
                }
                targets[run.Count] = k >= 2 ? path[k - 2] : bank; // stand just short of the near end
                world.Army.JobCells[r] = run.ToArray();
                world.Army.JobTargets[r] = targets;
                world.Army.WorkStep[r] = 0;
                world.Army.JobRef[r] = -1;
                return true;
            }
            return false;
        }

        /// <summary>One trestle segment per its ticks, from the near bank (S2 14.7). True once the whole gap is spanned.</summary>
        private bool BuildTrestle(TickContext context, int r, BridgeworkRules bridgework)
        {
            var world = context.World;
            var army = world.Army;
            var trestle = _rules.Structures.Kind("trestle")!;
            army.WorkProgress[r] += TickOrder.RegimentAiRate.Period;
            if (army.WorkProgress[r] < bridgework.TicksPerSegment)
            {
                return false;
            }
            army.WorkProgress[r] -= bridgework.TicksPerSegment;
            var run = army.JobCells[r]!;
            var terrain = _navigation.Terrain;
            int along = trestle.SegmentCellsAlong;
            int from = army.WorkStep[r] * along;
            var cells = new List<int>();
            for (int k = from; k < System.Math.Min(run.Length, from + along); k++)
            {
                int c = run[k];
                int prev = k == 0 ? army.JobTargets[r]![run.Length] : run[k - 1];
                int dx = c % terrain.Columns - prev % terrain.Columns;
                int dy = c / terrain.Columns - prev / terrain.Columns;
                // The deck's width: the path cell and its neighbours across the path, widthCells in all.
                for (int w = 0; w < System.Math.Max(1, trestle.WidthCells); w++)
                {
                    int qx = c % terrain.Columns - w * dy;
                    int qy = c / terrain.Columns + w * dx;
                    if (!terrain.InBounds(qx, qy))
                    {
                        continue;
                    }
                    int q = terrain.Index(qx, qy);
                    if (terrain.SegmentAt(q) < 0 && !cells.Contains(q))
                    {
                        cells.Add(q);
                    }
                }
            }
            if (cells.Count > 0)
            {
                int built = _navigation.BuildDeck(
                    context,
                    trestle,
                    cells.ToArray(),
                    army.JobTargets[r]![0],
                    army.JobRef[r]
                );
                if (built >= 0)
                {
                    army.JobRef[r] = built;
                    army.Spent[r] += bridgework.TimberMmPerSegment; // timber only for a segment actually laid
                }
            }
            army.WorkStep[r]++;
            return army.WorkStep[r] * along >= run.Length;
        }

        /// <summary>The lowest-numbered standing wooden segment within reach below the repair line (S2 14.7: strength, never load).</summary>
        private bool FindWeakSpan(World world, int r, BridgeworkRules bridgework)
        {
            var states = world.Structures;
            int officer = world.RegimentAnchor[r];
            if (states == null || officer < 0)
            {
                return false;
            }
            var terrain = _navigation.Terrain;
            var at = world.Position[officer];
            long reach = bridgework.RepairWithinMm;
            for (int s = 0; s < states.Count; s++)
            {
                var segment = terrain.Segment(s);
                if (
                    !segment.Kind.IsWood
                    || states.Broken[s]
                    || states.Strength[s] >= bridgework.RepairBelowPermille
                    || segment.Cells.Length == 0
                )
                {
                    continue;
                }
                var centre = terrain.CellCentre(
                    segment.Cells[0] % terrain.Columns,
                    segment.Cells[0] / terrain.Columns
                );
                long dx = RegimentSummary.Mm(centre.X.Raw - at.X.Raw);
                long dy = RegimentSummary.Mm(centre.Y.Raw - at.Y.Raw);
                if (dx * dx + dy * dy <= reach * reach)
                {
                    world.Army.JobRef[r] = s;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Repairs the span (S2 14.7): ‰ per second up to the repair ceiling, a set length of timber per 250‰; the new planks
        /// end any Rot under way. True when sound again, out of timber, or the span has failed.
        /// </summary>
        private bool Repair(World world, int r, BridgeworkRules bridgework, int timber)
        {
            var army = world.Army;
            var states = world.Structures!;
            int s = army.JobRef[r];
            if (
                states.Broken[s]
                || states.Strength[s] >= bridgework.RepairUpToPermille
                || timber <= 0
            )
            {
                return true;
            }
            states.RotStart[s] = 0;
            army.WorkProgress[r] += bridgework.RepairPermillePerS * TickOrder.RegimentAiRate.Period;
            int gained = army.WorkProgress[r] / SimConstants.TicksPerSecond;
            army.WorkProgress[r] -= gained * SimConstants.TicksPerSecond;
            int timberPerPermille = System.Math.Max(1, bridgework.TimberMmPer250Permille / 250);
            gained = System.Math.Min(
                gained,
                System.Math.Min(
                    bridgework.RepairUpToPermille - states.Strength[s],
                    timber / timberPerPermille
                )
            );
            states.Strength[s] += gained;
            army.Spent[r] += gained * timberPerPermille;
            return states.Strength[s] >= bridgework.RepairUpToPermille
                || timber - gained * timberPerPermille < timberPerPermille;
        }

        /// <summary>
        /// An enemy outline within the pause distance: fall back away from it and wait; resume once it has been clear
        /// for the resume time. True while paused.
        /// </summary>
        private bool Threatened(World world, int r, int tick)
        {
            var army = world.Army;
            var common = _rules.Specialists.Common;
            int nearest = -1;
            long best = long.MaxValue;
            for (int e = 0; e < world.Regiments.HighWater; e++)
            {
                if (
                    !world.Regiments.IsAlive(e)
                    || world.RegimentSide[e] == world.RegimentSide[r]
                    || _summary.Count[e] == 0
                    || army.Wiped[e]
                    || !_summary.Within(r, e, common.PauseWhenEnemyWithinMm)
                )
                {
                    continue;
                }
                long gap = _summary.Gap(r, e);
                if (gap < best)
                {
                    best = gap;
                    nearest = e;
                }
            }
            if (nearest >= 0)
            {
                if (army.Order[r] != RegimentOrder.FallBack)
                {
                    var away = _summary.Centroid[r] - _summary.Centroid[nearest];
                    if (away == FixVec2.Zero)
                    {
                        away = FixVec2.FromAngle(world.RegimentFacing[r], Fix.One) * -1;
                    }
                    var point =
                        _summary.Centroid[r]
                        + away.WithLength(Fix.FromRatio(common.FallBackMm, 1000));
                    army.Order[r] = RegimentOrder.FallBack;
                    army.Point[r] = Clamp(point);
                    army.Target[r] = -1;
                }
                army.ClearTicks[r] = 0;
                return true;
            }
            if (army.Order[r] == RegimentOrder.FallBack)
            {
                army.ClearTicks[r] += TickOrder.RegimentAiRate.Period;
                if (army.ClearTicks[r] < common.ResumeAfterClearTicks)
                {
                    return true;
                }
                army.ClearTicks[r] = 0;
                army.Order[r] = RegimentOrder.Work; // the action picks its point again below
            }
            return false;
        }

        // ---- Spademen: dig the ditch and bank (S2 14.3) ----

        private sealed class Ditch
        {
            public int Sections;
            public int[] SectionStart = new int[0]; // index into Cells of each section's first cell; one more at the end
            public int[] Cells = new int[0];
            public int[] Heights = new int[0];
            public FixVec2[] WorkPoints = new FixVec2[0];
        }

        private bool Dig(TickContext context, int r)
        {
            var world = context.World;
            var army = world.Army;
            var dig = _rules.Specialists.DigDitch!;
            var ditch = DitchOf(world, r);
            if (ditch == null || army.WorkStep[r] >= ditch.Sections)
            {
                if (army.Job[r] == SpecialistJob.Active)
                {
                    world.Causes?.Add(
                        CauseKind.SpecialistEnded,
                        context.Tick,
                        r,
                        DigDitchAction,
                        CauseLog.None
                    );
                }
                army.Job[r] = SpecialistJob.Done;
                army.Order[r] = RegimentOrder.March;
                return false;
            }
            if (army.Job[r] == SpecialistJob.Idle)
            {
                army.Job[r] = SpecialistJob.Active;
                world.Causes?.Add(
                    CauseKind.SpecialistStarted,
                    context.Tick,
                    r,
                    DigDitchAction,
                    CauseLog.None
                );
            }
            int section = army.WorkStep[r];
            army.Order[r] = RegimentOrder.Work;
            army.Point[r] = ditch.WorkPoints[section];
            army.Target[r] = -1;
            if (!AtWork(world, r))
            {
                return true;
            }
            int start = world.RegimentStartingSize[r];
            int rate = System.Math.Max(
                dig.MinRatePermille,
                (int)((long)world.RegimentAlive[r] * 1000 / System.Math.Max(1, start))
            );
            army.WorkProgress[r] += TickOrder.RegimentAiRate.Period * rate;
            if (army.WorkProgress[r] < dig.TicksPerSection * 1000)
            {
                return true;
            }
            army.WorkProgress[r] -= dig.TicksPerSection * 1000;
            int from = ditch.SectionStart[section];
            int count = ditch.SectionStart[section + 1] - from;
            Ensure(count);
            Array.Copy(ditch.Cells, from, _cells, 0, count);
            Array.Copy(ditch.Heights, from, _heights, 0, count);
            if (!_navigation.ApplyGroundEdit(context, _cells, _heights, count, checkRoutes: true))
            {
                // The section would close the last route for some class: left undug, a gap (brief 2026-10-09).
                world.Causes?.Add(
                    CauseKind.SpecialistEditSkipped,
                    context.Tick,
                    r,
                    DigDitchAction,
                    CauseLog.None
                );
            }
            army.WorkStep[r]++;
            return true;
        }

        /// <summary>
        /// The ditch's cells, section by section, from the plan's line and the played map (derived: the same every
        /// time). A cell within half a cell's diagonal of the line is ditch (the line pieces' raster, so a diagonal ditch
        /// has no corner gap); past the berm, one cell on the regiment's side is bank. Sections run from the first point.
        /// </summary>
        private Ditch? DitchOf(World world, int r)
        {
            if (_ditches.Length != world.Regiments.Capacity)
            {
                _ditches = new Ditch?[world.Regiments.Capacity];
                _ditchBuilt = new bool[world.Regiments.Capacity];
            }
            if (_ditchBuilt[r])
            {
                return _ditches[r];
            }
            _ditchBuilt[r] = true;
            var planned = r < _plan.Army.Count ? _plan.Army[r] : null;
            if (
                planned == null
                || planned.Soldiers != world.RegimentStartingSize[r] // slot r is plan.Army[r] (deployed in plan order)
                || planned.DitchMm == null
                || !string.Equals(
                    planned.UnitType,
                    _rules.UnitTypes[world.RegimentUnitType[r]].Id,
                    StringComparison.Ordinal
                )
            )
            {
                return null;
            }
            var dig = _rules.Specialists.DigDitch!;
            var map = _navigation.Map;
            int cellMm = map.CellSizeMm;
            int[] p = planned.DitchMm;
            long reach = ((long)cellMm * 7072 + 9999) / 10000;
            long bankFrom = reach + (long)dig.BermCells * cellMm;
            long bankTo = bankFrom + cellMm;
            int side = Side(p, planned.XMm, planned.YMm);
            if (side == 0)
            {
                side = 1;
            }
            long sectionMm = (long)dig.SectionCellsAlong * cellMm;
            long total = 0;
            for (int k = 0; k + 3 < p.Length; k += 2)
            {
                total += Length(p[k + 2] - p[k], p[k + 3] - p[k + 1]);
            }
            int sections = (int)System.Math.Max(1, (total + sectionMm - 1) / sectionMm);
            var lists = new List<(int Section, int Cell, int Height)>();
            long minX = long.MaxValue,
                minY = long.MaxValue,
                maxX = long.MinValue,
                maxY = long.MinValue;
            for (int k = 0; k < p.Length; k += 2)
            {
                minX = System.Math.Min(minX, p[k]);
                maxX = System.Math.Max(maxX, p[k]);
                minY = System.Math.Min(minY, p[k + 1]);
                maxY = System.Math.Max(maxY, p[k + 1]);
            }
            int x0 = (int)System.Math.Max(0, (minX - bankTo) / cellMm);
            int x1 = (int)System.Math.Min(map.Columns - 1, (maxX + bankTo) / cellMm);
            int y0 = (int)System.Math.Max(0, (minY - bankTo) / cellMm);
            int y1 = (int)System.Math.Min(map.Rows - 1, (maxY + bankTo) / cellMm);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    long cx = (long)x * cellMm + cellMm / 2;
                    long cy = (long)y * cellMm + cellMm / 2;
                    Nearest(p, cx, cy, out long distSq, out int cellSide, out long arc);
                    int i = y * map.Columns + x;
                    int ground = map.HeightMm[i];
                    int section = (int)System.Math.Min(sections - 1, arc / sectionMm);
                    if (distSq <= reach * reach)
                    {
                        lists.Add((section, i, ClampHeight(ground - dig.DitchDepthMm)));
                    }
                    else if (
                        cellSide == side
                        && distSq > bankFrom * bankFrom
                        && distSq <= bankTo * bankTo
                    )
                    {
                        lists.Add((section, i, ClampHeight(ground + dig.BankHeightMm)));
                    }
                }
            }
            lists.Sort(
                (a, b) =>
                    a.Section != b.Section
                        ? a.Section.CompareTo(b.Section)
                        : a.Cell.CompareTo(b.Cell)
            );
            var result = new Ditch
            {
                Sections = sections,
                SectionStart = new int[sections + 1],
                Cells = new int[lists.Count],
                Heights = new int[lists.Count],
                WorkPoints = new FixVec2[sections],
            };
            int at = 0;
            for (int s = 0; s < sections; s++)
            {
                result.SectionStart[s] = at;
                while (at < lists.Count && lists[at].Section == s)
                {
                    result.Cells[at] = lists[at].Cell;
                    result.Heights[at] = lists[at].Height;
                    at++;
                }
                // Behind the bank, on the regiment's side, level with the section's middle.
                long arcMid = System.Math.Min(total, s * sectionMm + sectionMm / 2);
                PointAt(p, arcMid, out long px, out long py, out long nx, out long ny);
                long behind = bankTo + cellMm + cellMm / 2;
                result.WorkPoints[s] = Clamp(
                    new FixVec2(
                        Fix.FromRatio(px + side * nx * behind / 1000, 1000),
                        Fix.FromRatio(py + side * ny * behind / 1000, 1000)
                    )
                );
            }
            result.SectionStart[sections] = at;
            _ditches[r] = result;
            return result;
        }

        // ---- Fascine Carriers: fill one crossing (S2 14.4) ----

        private bool Fill(TickContext context, int r)
        {
            var world = context.World;
            var army = world.Army;
            var fill = _rules.Specialists.FillCrossing!;
            int left = world.RegimentAlive[r] - army.Spent[r];
            if (left <= 0)
            {
                if (army.Job[r] == SpecialistJob.Active)
                {
                    world.Causes?.Add(
                        CauseKind.SpecialistEnded,
                        context.Tick,
                        r,
                        FillCrossingAction,
                        CauseLog.None
                    );
                }
                army.Job[r] = SpecialistJob.Done;
                army.JobCells[r] = null;
                army.JobTargets[r] = null;
                army.Order[r] = RegimentOrder.March;
                return false;
            }
            if (army.Job[r] == SpecialistJob.Idle)
            {
                if (!FindCrossing(world, r, fill))
                {
                    return false; // marching on with the army until a fillable run lies ahead
                }
                army.Job[r] = SpecialistJob.Active;
                army.WorkProgress[r] = 0;
                world.Causes?.Add(
                    CauseKind.SpecialistStarted,
                    context.Tick,
                    r,
                    FillCrossingAction,
                    CauseLog.None
                );
            }
            army.Order[r] = RegimentOrder.Work;
            army.Target[r] = -1;
            int rimCell = army.WorkStep[r]; // where the carriers stand to fill: the near rim
            army.Point[r] = _navigation.Terrain.CellCentre(
                rimCell % _navigation.Terrain.Columns,
                rimCell / _navigation.Terrain.Columns
            );
            if (!AtWork(world, r))
            {
                return true;
            }
            army.WorkProgress[r] += TickOrder.RegimentAiRate.Period;
            if (context.Tick % FillBatchTicks != 0)
            {
                return true;
            }
            int bundles = System.Math.Min(left, army.WorkProgress[r] / fill.TicksPerBundle);
            army.WorkProgress[r] -= bundles * fill.TicksPerBundle;
            var cells = army.JobCells[r]!;
            var targets = army.JobTargets[r]!;
            var terrain = _navigation.Terrain;
            var fields = world.Fields;
            int[]? mud =
                fields != null && fields.Rules.IndexOf(GroundModel.Mud) >= 0
                    ? fields.Get(GroundModel.Mud).Values
                    : null;
            Ensure(cells.Length);
            int edits = 0;
            int laid = 0;
            for (int b = 0; b < bundles; b++)
            {
                int k = NextCell(cells, targets, terrain, mud, edits, fill);
                if (k < 0)
                {
                    break;
                }
                int cell = cells[k];
                int current = CurrentHeight(cell, terrain, edits);
                int raised = System.Math.Min(targets[k], current + fill.BundleRaiseMm);
                int slot = IndexOf(cell, edits);
                if (slot < 0)
                {
                    slot = edits++;
                    _cells[slot] = cell;
                }
                _heights[slot] = System.Math.Max(current, raised);
                laid++;
            }
            if (edits > 0)
            {
                // Remember each cell's fill before the edit, for washout.
                var before = new int[edits];
                for (int e = 0; e < edits; e++)
                {
                    before[e] = terrain.HeightMm(
                        _cells[e] % terrain.Columns,
                        _cells[e] / terrain.Columns
                    );
                }
                if (
                    _navigation.ApplyGroundEdit(context, _cells, _heights, edits, checkRoutes: true)
                )
                {
                    for (int e = 0; e < edits; e++)
                    {
                        world.Ground.AddFill(_cells[e], _heights[e] - before[e]);
                        if (mud != null)
                        {
                            mud[_cells[e]] = 0; // the bundles firm the cell
                        }
                    }
                }
                else
                {
                    world.Causes?.Add(
                        CauseKind.SpecialistEditSkipped,
                        context.Tick,
                        r,
                        FillCrossingAction,
                        CauseLog.None
                    );
                    // The crossing would close the last route for some class: the regiment gives up its fills
                    // rather than retry the same crossing every second (S2 14.1, the skipped batch leaves the gap).
                    world.Causes?.Add(
                        CauseKind.SpecialistEnded,
                        context.Tick,
                        r,
                        FillCrossingAction,
                        CauseLog.None
                    );
                    army.Job[r] = SpecialistJob.Done;
                    army.JobCells[r] = null;
                    army.JobTargets[r] = null;
                    army.Order[r] = RegimentOrder.March;
                    return false;
                }
            }
            army.Spent[r] += laid;
            if (army.JobCells[r] == null || NextCell(cells, targets, terrain, mud, 0, fill) < 0)
            {
                // The crossing is filled: leftover bundles wait for the next fillable run (S2 14.4).
                world.Causes?.Add(
                    CauseKind.SpecialistEnded,
                    context.Tick,
                    r,
                    FillCrossingAction,
                    CauseLog.None
                );
                army.Job[r] = SpecialistJob.Idle;
                army.JobCells[r] = null;
                army.JobTargets[r] = null;
                army.Order[r] = RegimentOrder.March;
                return false;
            }
            return true;
        }

        /// <summary>The first crossing cell still below its target or still muddy, or -1.</summary>
        private int NextCell(
            int[] cells,
            int[] targets,
            Terrain terrain,
            int[]? mud,
            int edits,
            FillCrossingRules fill
        )
        {
            for (int k = 0; k < cells.Length; k++)
            {
                int h = CurrentHeight(cells[k], terrain, edits);
                // A cell already bundled in this batch is firm, whatever the mud field says until the batch lands.
                bool muddy =
                    mud != null && mud[cells[k]] >= fill.MudAtLeast && IndexOf(cells[k], edits) < 0;
                if (h < targets[k] || muddy)
                {
                    return k;
                }
            }
            return -1;
        }

        private int CurrentHeight(int cell, Terrain terrain, int edits)
        {
            int slot = IndexOf(cell, edits);
            return slot >= 0
                ? _heights[slot]
                : terrain.HeightMm(cell % terrain.Columns, cell / terrain.Columns);
        }

        private int IndexOf(int cell, int edits)
        {
            for (int e = 0; e < edits; e++)
            {
                if (_cells[e] == cell)
                {
                    return e;
                }
            }
            return -1;
        }

        /// <summary>
        /// Walks the regiment's flow field from its officer up to the trigger distance; at the first fillable run, sets
        /// the crossing (a strip across the path through the run to the first cell open for wheeled, at most the
        /// maximum length) and the work point at the near rim. False when nothing fillable lies ahead.
        /// </summary>
        private bool FindCrossing(World world, int r, FillCrossingRules fill)
        {
            var field = _navigation.FieldOf(r);
            int officer = world.RegimentAnchor[r];
            if (field == null || officer < 0)
            {
                return false;
            }
            var directions = field.FreshDirections();
            var terrain = _navigation.Terrain;
            terrain.CellOf(world.Position[officer], out int x, out int y);
            int steps = fill.AheadMm / terrain.CellSizeMm;
            int rim = terrain.HeightMm(x, y);
            int rimCell = terrain.Index(x, y);
            for (int s = 0; s < steps; s++)
            {
                byte d = directions[terrain.Index(x, y)];
                if (d >= FlowField.AtGoal)
                {
                    return false;
                }
                var (dx, dy) = FlowField.Offset(d);
                int nx = x + dx;
                int ny = y + dy;
                if (!terrain.InBounds(nx, ny))
                {
                    return false;
                }
                if (!Fillable(world, nx, ny, rim, fill))
                {
                    x = nx;
                    y = ny;
                    rim = terrain.HeightMm(x, y);
                    rimCell = terrain.Index(x, y);
                    continue;
                }
                // The run: along the path from here to the first cell open for wheeled beyond it.
                var run = new List<int>();
                int farRim = rim;
                int cx = nx;
                int cy = ny;
                while (run.Count < fill.MaxLengthCells)
                {
                    run.Add(terrain.Index(cx, cy));
                    byte step = directions[terrain.Index(cx, cy)];
                    if (step >= FlowField.AtGoal)
                    {
                        break;
                    }
                    var (sx, sy) = FlowField.Offset(step);
                    int fx = cx + sx;
                    int fy = cy + sy;
                    if (!terrain.InBounds(fx, fy))
                    {
                        break;
                    }
                    if (
                        !Fillable(world, fx, fy, rim, fill)
                        && _navigation.Costs.Cost(MovementClass.Wheeled, fx, fy)
                            != CostFields.Impassable
                    )
                    {
                        farRim = terrain.HeightMm(fx, fy);
                        break;
                    }
                    cx = fx;
                    cy = fy;
                }
                int target = System.Math.Min(rim, farRim);
                // Across the path: the perpendicular to the step that entered the run, widthCells wide.
                int px = -dy;
                int py = dx;
                int half = fill.WidthCells / 2;
                var cells = new List<int>();
                foreach (int along in run)
                {
                    int ax = along % terrain.Columns;
                    int ay = along / terrain.Columns;
                    for (int j = -half; j <= fill.WidthCells - 1 - half; j++)
                    {
                        int qx = ax + j * px;
                        int qy = ay + j * py;
                        int q = terrain.Index(qx, qy);
                        if (terrain.InBounds(qx, qy) && !cells.Contains(q))
                        {
                            cells.Add(q);
                        }
                    }
                }
                var targets = new int[cells.Count];
                for (int k = 0; k < targets.Length; k++)
                {
                    targets[k] = target;
                }
                world.Army.JobCells[r] = cells.ToArray();
                world.Army.JobTargets[r] = targets;
                world.Army.WorkStep[r] = rimCell;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Fillable (S2 14.4): below the rim by the trigger depth (a channel, ditch or hollow), deep mud, or a ford's
        /// water over a bed below the rim.
        /// </summary>
        private bool Fillable(World world, int x, int y, int rim, FillCrossingRules fill)
        {
            var terrain = _navigation.Terrain;
            int h = terrain.HeightMm(x, y);
            if (h <= rim - fill.BelowRimMm)
            {
                return true;
            }
            var fields = world.Fields;
            if (fields == null)
            {
                return false;
            }
            int i = terrain.Index(x, y);
            if (
                fields.Rules.IndexOf(GroundModel.Mud) >= 0
                && fields.Get(GroundModel.Mud).Values[i] >= fill.MudAtLeast
            )
            {
                return true;
            }
            if (fields.Rules.IndexOf(WaterModel.Depth) >= 0 && h < rim)
            {
                long depthMm = fields.Get(WaterModel.Depth).Values[i] / 1000;
                return depthMm >= fill.WaterMinMm && depthMm <= fill.WaterMaxMm;
            }
            return false;
        }

        /// <summary>
        /// A filled cell where depth × flow speed reaches the washout line long enough loses fill (S2 14.4: a brush weir
        /// that washes out in a surge). Water-made, so no route check. Once per AI tick for the whole battle.
        /// </summary>
        public void Washout(TickContext context)
        {
            var world = context.World;
            var ground = world.Ground;
            var fields = world.Fields;
            var fill = _rules.Specialists.FillCrossing;
            if (
                ground.FillCount == 0
                || fields == null
                || fill == null
                || fields.Rules.IndexOf(WaterModel.Depth) < 0
            )
            {
                return;
            }
            var terrain = _navigation.Terrain;
            var depth = fields.Get(WaterModel.Depth).Values;
            for (int i = 0; i < ground.FillCount; i++)
            {
                int cell = ground.FillCellAt(i);
                int filled = ground.FillMmAt(i);
                if (filled <= 0)
                {
                    continue;
                }
                int x = cell % terrain.Columns;
                int y = cell / terrain.Columns;
                WaterModel.Velocity(fields, terrain.CellSizeMm, x, y, out int east, out int north);
                long speed = IntMath.Sqrt((ulong)((long)east * east + (long)north * north)); // mm/s
                long depthSpeed = depth[cell] / 1000 * speed / 1000; // mm × m/s
                int surge =
                    depthSpeed >= fill.WashoutDepthSpeed
                        ? ground.SurgeTicksAt(i) + TickOrder.RegimentAiRate.Period
                        : 0;
                if (surge < fill.WashoutTicks)
                {
                    ground.SetFill(i, filled, surge);
                    continue;
                }
                int loss = System.Math.Min(fill.WashoutLossMm, filled);
                Ensure(1);
                _cells[0] = cell;
                _heights[0] = terrain.HeightMm(x, y) - loss;
                _navigation.ApplyGroundEdit(context, _cells, _heights, 1, checkRoutes: false);
                ground.SetFill(i, filled - loss, 0);
            }
        }

        // ---- Geometry and helpers ----

        private bool AtWork(World world, int r)
        {
            int officer = world.RegimentAnchor[r];
            var at = officer >= 0 ? world.Position[officer] : _summary.Centroid[r];
            long dx = RegimentSummary.Mm(at.X.Raw - world.Army.Point[r].X.Raw);
            long dy = RegimentSummary.Mm(at.Y.Raw - world.Army.Point[r].Y.Raw);
            return dx * dx + dy * dy <= (long)WorkReachMm * WorkReachMm;
        }

        private FixVec2 Clamp(FixVec2 p)
        {
            Fix maxX = _navigation.Map.Width - Fix.FromRaw(1);
            Fix maxY = _navigation.Map.Depth - Fix.FromRaw(1);
            return new FixVec2(Fix.Clamp(p.X, Fix.Zero, maxX), Fix.Clamp(p.Y, Fix.Zero, maxY));
        }

        private void Ensure(int n)
        {
            if (_cells.Length < n)
            {
                _cells = new int[n];
                _heights = new int[n];
            }
        }

        private static int ClampHeight(long mm) =>
            (int)System.Math.Max(-MapData.MaxHeightMm, System.Math.Min(MapData.MaxHeightMm, mm));

        private static long Length(long dx, long dy) =>
            (long)IntMath.Sqrt((ulong)(dx * dx + dy * dy));

        /// <summary>Which side of the polyline a point lies on, by its nearest segment (+1, -1, or 0 on it).</summary>
        private static int Side(int[] p, long x, long y)
        {
            Nearest(p, x, y, out _, out int side, out _);
            return side;
        }

        /// <summary>
        /// The squared distance from a point to the polyline, the side it lies on (by the nearest segment; ties to the
        /// first) and its arc position: the length along the line to the foot of the perpendicular.
        /// </summary>
        private static void Nearest(
            int[] p,
            long x,
            long y,
            out long distSq,
            out int side,
            out long arc
        )
        {
            distSq = long.MaxValue;
            side = 0;
            arc = 0;
            long before = 0;
            for (int k = 0; k + 3 < p.Length; k += 2)
            {
                long ax = p[k];
                long ay = p[k + 1];
                long bx = p[k + 2];
                long by = p[k + 3];
                long d = TerrainPieces.SegmentDistanceSq(x, y, ax, ay, bx, by);
                long length = Length(bx - ax, by - ay);
                if (d < distSq)
                {
                    distSq = d;
                    long cross = (bx - ax) * (y - ay) - (by - ay) * (x - ax);
                    side =
                        cross > 0 ? 1
                        : cross < 0 ? -1
                        : 0;
                    long dot = (x - ax) * (bx - ax) + (y - ay) * (by - ay);
                    long t =
                        length == 0 ? 0 : System.Math.Max(0, System.Math.Min(length, dot / length));
                    arc = before + t;
                }
                before += length;
            }
        }

        /// <summary>The point at an arc position along the polyline and the unit left normal there (in ‰, so × 1/1000).</summary>
        private static void PointAt(
            int[] p,
            long arc,
            out long x,
            out long y,
            out long nx,
            out long ny
        )
        {
            long before = 0;
            for (int k = 0; k + 3 < p.Length; k += 2)
            {
                long ax = p[k];
                long ay = p[k + 1];
                long bx = p[k + 2];
                long by = p[k + 3];
                long length = Length(bx - ax, by - ay);
                bool last = k + 5 >= p.Length;
                if (arc <= before + length || last)
                {
                    long t = System.Math.Max(0, System.Math.Min(length, arc - before));
                    x = length == 0 ? ax : ax + (bx - ax) * t / length;
                    y = length == 0 ? ay : ay + (by - ay) * t / length;
                    nx = length == 0 ? 0 : -(by - ay) * 1000 / length;
                    ny = length == 0 ? 0 : (bx - ax) * 1000 / length;
                    return;
                }
                before += length;
            }
            x = p[0];
            y = p[1];
            nx = 0;
            ny = 0;
        }
    }
}
