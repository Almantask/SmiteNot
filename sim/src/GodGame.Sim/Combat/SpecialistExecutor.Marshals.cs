using System;
using System.Collections.Generic;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// Crossing Marshals (spec S2 14.6): when the army's foot path, within 150 m ahead of any of its regiments, reaches a
    /// standing span (bridge segments, a pontoon, a trestle or ice patches, with every standing segment joined to them),
    /// the marshals meter it. One regiment at a time crosses, the one nearest the span along its route first (ties: the
    /// lower slot), in open order with files = max(1, span width ÷ (2 × file spacing)), never more than its usual files;
    /// every other regiment whose route crosses the span stops 20 m short of it, each further back by the depth of the
    /// regiments queued ahead, and the next goes once the last soldier of the one before has stepped off. The gate is a
    /// fixed rule of occupancy and never reads strain (brief 2026-10-09). A crossing ends when no regiment's route uses
    /// the span any more, or after 300 s; it lapses at once when the marshals are wiped or rout.
    /// <para>
    /// Determinism: the gate runs serially in slot order before the regiment orders are decided; its scratch (who is held,
    /// distances along routes, soldiers on the span) is rebuilt from the world every AI step; what persists is in
    /// <see cref="ArmyState"/> (the span's segments and entry, the crossing regiment, the elapsed time, open order and
    /// files, and the Hold orders with their points).
    /// </para>
    /// </summary>
    public sealed partial class SpecialistExecutor
    {
        /// <summary>Cause event detail and job kind: metering a crossing.</summary>
        public const int MeterAction = 7;

        /// <summary>A metered span's job targets: its entry cell, its width in mm, and the marshals' post (x, y in mm).</summary>
        public const int MeterTargets = 4;

        // Scratch, rebuilt from the world every AI step.
        private bool[] _held = new bool[0];
        private int[] _onSpan = new int[0];
        private long[] _toSpan = new long[0];
        private bool[] _inSpan = new bool[0];
        private readonly List<int> _queue = new List<int>();

        /// <summary>Whether a gate holds regiment <paramref name="r"/> short of its span this AI step.</summary>
        public bool Holds(int r) => r < _held.Length && _held[r];

        /// <summary>
        /// Runs every active gate, in slot order, before the regiments decide their orders: advances the crossing,
        /// ends it, and marks the regiments to hold.
        /// </summary>
        public void Gates(TickContext context)
        {
            var world = context.World;
            int n = world.Regiments.Capacity;
            if (_held.Length < n)
            {
                _held = new bool[n];
                _onSpan = new int[n];
                _toSpan = new long[n];
            }
            Array.Clear(_held, 0, _held.Length);
            if (_rules.Specialists.MeterCrossing == null)
            {
                return;
            }
            var army = world.Army;
            for (int m = 0; m < world.Regiments.HighWater; m++)
            {
                if (
                    world.Regiments.IsAlive(m)
                    && world.RegimentAlive[m] > 0
                    && army.Job[m] == SpecialistJob.Active
                    && army.JobKind[m] == MeterAction
                    && !army.Wiped[m]
                    && army.Order[m] != RegimentOrder.Rout
                )
                {
                    Gate(context, m);
                }
            }
        }

        /// <summary>The marshals' own step: wait for a span on the army's route, then stand beside its near end.</summary>
        private bool Meter(TickContext context, int r)
        {
            var world = context.World;
            var army = world.Army;
            if (army.Job[r] == SpecialistJob.Idle)
            {
                if (!FindSpan(context, r))
                {
                    return false;
                }
                army.Job[r] = SpecialistJob.Active;
                army.JobKind[r] = MeterAction;
                army.JobRef[r] = -1;
                army.WorkProgress[r] = 0;
                world.Causes?.Add(
                    CauseKind.SpecialistStarted,
                    context.Tick,
                    r,
                    MeterAction,
                    CauseLog.None
                );
            }
            if (army.Job[r] != SpecialistJob.Active)
            {
                return false;
            }
            // Back to the post beside the span (after falling back from a threat, too).
            var targets = army.JobTargets[r]!;
            army.Point[r] = new FixVec2(
                Fix.FromRatio(targets[2], 1000),
                Fix.FromRatio(targets[3], 1000)
            );
            army.Order[r] = RegimentOrder.Work;
            army.Target[r] = -1;
            return true;
        }

        /// <summary>Wiped or routed marshals: the regiment on the span goes back to its own order, and the queue surges on.</summary>
        private void Lapse(World world, int m)
        {
            int c = world.Army.JobRef[m];
            if (c >= 0 && c < world.Regiments.HighWater && world.Regiments.IsAlive(c))
            {
                Restore(world, c);
            }
            world.Army.JobRef[m] = -1;
        }

        private void Gate(TickContext context, int m)
        {
            var world = context.World;
            var army = world.Army;
            var rules = _rules.Specialists.MeterCrossing!;
            var terrain = _navigation.Terrain;
            bool standing = MarkSpan(world, army.JobCells[m]!);
            // Soldiers of the army on the span, and each regiment's distance to it along its own route.
            int regiments = world.Regiments.HighWater;
            Array.Clear(_onSpan, 0, _onSpan.Length);
            var side = world.RegimentSide[m];
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (!world.Units.IsAlive(u) || world.RegimentSide[world.UnitRegiment[u]] != side)
                {
                    continue;
                }
                terrain.CellOf(world.Position[u], out int x, out int y);
                if (OnSpan(terrain, terrain.Index(x, y)))
                {
                    _onSpan[world.UnitRegiment[u]]++;
                }
            }
            for (int q = 0; q < regiments; q++)
            {
                _toSpan[q] = Eligible(world, m, q) ? RouteToSpan(context, q) : -1;
            }
            int c = army.JobRef[m];
            if (
                c >= 0
                && (
                    c >= regiments
                    || !world.Regiments.IsAlive(c)
                    || world.RegimentAlive[c] == 0
                    || army.Wiped[c]
                    || army.Order[c] == RegimentOrder.Rout
                    || (_onSpan[c] == 0 && Across(context, m, c))
                    // Turned on an enemy short of the span (a working default): the slot is free again.
                    || (_onSpan[c] == 0 && army.Order[c] == RegimentOrder.Engage)
                )
            )
            {
                // Across (the last soldier stepped off), or gone: the next may go.
                if (c < regiments && world.Regiments.IsAlive(c))
                {
                    Restore(world, c);
                }
                c = -1;
                army.JobRef[m] = -1;
            }
            army.WorkProgress[m] += TickOrder.RegimentAiRate.Period;
            // The queue: every other regiment whose route crosses the span, nearest first (ties: the lower slot).
            _queue.Clear();
            for (int q = 0; q < regiments; q++)
            {
                if (q == c || _toSpan[q] < 0 || CrossingElsewhere(world, m, q))
                {
                    continue;
                }
                int at = _queue.Count;
                while (at > 0 && _toSpan[_queue[at - 1]] > _toSpan[q])
                {
                    at--; // insertion in ascending slot order keeps ties by slot
                }
                _queue.Insert(at, q);
            }
            if (
                !standing
                || army.WorkProgress[m] >= rules.CrossingEndsTicks
                || (c < 0 && _queue.Count == 0)
            )
            {
                EndCrossing(context, m, c);
                return;
            }
            // The next goes once it is near: at the holding line, with room to reform into open order before the span.
            if (c < 0 && _toSpan[_queue[0]] <= rules.HoldingLineMm + Depth(world, _queue[0]))
            {
                c = _queue[0];
                _queue.RemoveAt(0);
                army.JobRef[m] = c;
                OpenOrder(world, c, army.JobTargets[m]![1]);
            }
            long hold = rules.HoldingLineMm;
            foreach (int q in _queue)
            {
                if (_toSpan[q] <= hold)
                {
                    _held[q] = true;
                }
                hold += Depth(world, q);
            }
        }

        /// <summary>
        /// Whether the last soldier of regiment <paramref name="c"/> has stepped off (S2 14.6): every one on his feet is
        /// nearer the goal, along the regiment's march field, than the span's far end. A gap in an open-order column can
        /// leave the span empty while its tail is still to come.
        /// </summary>
        private bool Across(TickContext context, int m, int c)
        {
            var world = context.World;
            var terrain = _navigation.Terrain;
            var field = _navigation.MarchField(
                _rules.UnitTypes[world.RegimentUnitType[c]].MovementClass,
                context.Runner
            );
            if (field == null)
            {
                return true;
            }
            var distances = field.Distances;
            long far = FlowField.Unreachable;
            foreach (int s in world.Army.JobCells[m]!)
            {
                if (s < 0 || s >= terrain.SegmentCount)
                {
                    continue;
                }
                foreach (int cell in terrain.Segment(s).Cells)
                {
                    far = System.Math.Min(far, distances[cell]);
                }
            }
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (
                    !world.Units.IsAlive(u)
                    || world.UnitRegiment[u] != c
                    || (world.Flags[u] & (UnitFlags.Swept | UnitFlags.Launched)) != 0
                )
                {
                    continue;
                }
                terrain.CellOf(world.Position[u], out int x, out int y);
                long d = distances[terrain.Index(x, y)];
                if (d != FlowField.Unreachable && d >= far)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Whether another active gate is letting regiment <paramref name="q"/> across its own span.</summary>
        private static bool CrossingElsewhere(World world, int m, int q)
        {
            var army = world.Army;
            for (int g = 0; g < world.Regiments.HighWater; g++)
            {
                if (
                    g != m
                    && army.Job[g] == SpecialistJob.Active
                    && army.JobKind[g] == MeterAction
                    && army.JobRef[g] == q
                )
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>A regiment of the marshals' army the gate may meter: alive, marching (not a specialist at work), not routing.</summary>
        private static bool Eligible(World world, int m, int q)
        {
            var army = world.Army;
            return q != m
                && world.Regiments.IsAlive(q)
                && world.RegimentAlive[q] > 0
                && world.RegimentSide[q] == world.RegimentSide[m]
                && world.RegimentAnchor[q] >= 0
                && !army.Wiped[q]
                && army.Order[q] != RegimentOrder.Rout
                && army.Job[q] != SpecialistJob.Active;
        }

        private void EndCrossing(TickContext context, int m, int c)
        {
            var world = context.World;
            var army = world.Army;
            if (c >= 0 && c < world.Regiments.HighWater && world.Regiments.IsAlive(c))
            {
                Restore(world, c);
            }
            world.Causes?.Add(
                CauseKind.SpecialistEnded,
                context.Tick,
                m,
                MeterAction,
                CauseLog.None
            );
            army.Spent[m]++;
            army.Job[m] =
                army.Spent[m] < _rules.Specialists.MeterCrossing!.Crossings
                    ? SpecialistJob.Idle
                    : SpecialistJob.Done;
            army.JobKind[m] = 0;
            army.JobRef[m] = -1;
            army.JobCells[m] = null;
            army.JobTargets[m] = null;
            army.WorkProgress[m] = 0;
            army.Order[m] = RegimentOrder.March;
        }

        /// <summary>The crossing regiment takes open order, as many files as the span is wide (S2 14.6).</summary>
        private void OpenOrder(World world, int c, int widthMm)
        {
            var formation = _rules.UnitTypes[world.RegimentUnitType[c]].Formation;
            long openSpacing =
                (long)formation.FileSpacingMm * _rules.Combat.OpenSpacingPermille / 1000;
            int fit = (int)System.Math.Max(1, widthMm / System.Math.Max(1, openSpacing));
            int usual = Formation.FilesFor(formation, world.RegimentStartingSize[c]);
            world.Army.Open[c] = true;
            world.RegimentFiles[c] = System.Math.Min(usual, fit);
        }

        /// <summary>Back to close order and its usual files.</summary>
        private void Restore(World world, int c)
        {
            var formation = _rules.UnitTypes[world.RegimentUnitType[c]].Formation;
            world.Army.Open[c] = false;
            world.RegimentFiles[c] = Formation.FilesFor(formation, world.RegimentStartingSize[c]);
        }

        /// <summary>A regiment's depth in close order, officer included, in mm: how much room it takes in the queue.</summary>
        private long Depth(World world, int q)
        {
            var formation = _rules.UnitTypes[world.RegimentUnitType[q]].Formation;
            int ranks = Formation.Ranks(world.RegimentAlive[q], world.RegimentFiles[q]);
            return (long)formation.RankSpacingMm * (ranks + 1);
        }

        /// <summary>Marks the span's segments; false when none of them stands any more.</summary>
        private bool MarkSpan(World world, int[] segments)
        {
            var terrain = _navigation.Terrain;
            if (_inSpan.Length < terrain.SegmentCount)
            {
                _inSpan = new bool[terrain.SegmentCount];
            }
            Array.Clear(_inSpan, 0, _inSpan.Length);
            bool standing = false;
            var states = world.Structures;
            foreach (int s in segments)
            {
                if (s >= 0 && s < terrain.SegmentCount)
                {
                    _inSpan[s] = true;
                    standing |= states == null || s >= states.Count || !states.Broken[s];
                }
            }
            return standing;
        }

        private bool OnSpan(Terrain terrain, int cell)
        {
            int s = terrain.StandingSegment(cell);
            return s >= 0 && s < _inSpan.Length && _inSpan[s];
        }

        /// <summary>How far (mm) regiment <paramref name="q"/>'s march route runs from its officer to the span; -1 when it doesn't reach it.</summary>
        private long RouteToSpan(TickContext context, int q)
        {
            var world = context.World;
            var terrain = _navigation.Terrain;
            var field = _navigation.MarchField(
                _rules.UnitTypes[world.RegimentUnitType[q]].MovementClass,
                context.Runner
            );
            if (field == null)
            {
                return -1;
            }
            var directions = field.Directions;
            terrain.CellOf(world.Position[world.RegimentAnchor[q]], out int x, out int y);
            long distance = 0;
            int limit = 2 * (terrain.Columns + terrain.Rows);
            for (int k = 0; k < limit; k++)
            {
                if (OnSpan(terrain, terrain.Index(x, y)))
                {
                    return distance;
                }
                byte d = directions[terrain.Index(x, y)];
                if (d >= FlowField.AtGoal)
                {
                    return -1;
                }
                var (dx, dy) = FlowField.Offset(d);
                if (!terrain.InBounds(x + dx, y + dy))
                {
                    return -1;
                }
                x += dx;
                y += dy;
                distance +=
                    dx != 0 && dy != 0 ? terrain.CellSizeMm * 1414L / 1000 : terrain.CellSizeMm;
            }
            return -1;
        }

        /// <summary>
        /// The trigger (S2 14.6): the nearest standing deck on the army's foot path within reach ahead of any of its
        /// regiments; the span is that segment with every standing segment joined to it. Records the span, its entry and
        /// width, and the marshals' post beside its near end.
        /// </summary>
        private bool FindSpan(TickContext context, int r)
        {
            var world = context.World;
            var rules = _rules.Specialists.MeterCrossing!;
            var terrain = _navigation.Terrain;
            var field = _navigation.MarchField(MovementClass.Foot, context.Runner);
            var states = world.Structures;
            if (field == null || states == null || terrain.SegmentCount == 0)
            {
                return false;
            }
            var directions = field.Directions;
            long best = long.MaxValue;
            int entry = -1;
            int before = -1;
            for (int q = 0; q < world.Regiments.HighWater; q++)
            {
                if (!Eligible(world, r, q))
                {
                    continue;
                }
                terrain.CellOf(world.Position[world.RegimentAnchor[q]], out int x, out int y);
                int previous = -1;
                long distance = 0;
                while (distance <= rules.AheadMm && distance < best)
                {
                    int cell = terrain.Index(x, y);
                    if (terrain.StandingSegment(cell) >= 0)
                    {
                        best = distance;
                        entry = cell;
                        before = previous;
                        break;
                    }
                    byte d = directions[cell];
                    if (d >= FlowField.AtGoal)
                    {
                        break;
                    }
                    var (dx, dy) = FlowField.Offset(d);
                    if (!terrain.InBounds(x + dx, y + dy))
                    {
                        break;
                    }
                    previous = cell;
                    x += dx;
                    y += dy;
                    distance +=
                        dx != 0 && dy != 0 ? terrain.CellSizeMm * 1414L / 1000 : terrain.CellSizeMm;
                }
            }
            if (entry < 0)
            {
                return false;
            }
            // Another regiment of marshals already meters this span: one gate per span.
            int first = terrain.StandingSegment(entry);
            for (int g = 0; g < world.Regiments.HighWater; g++)
            {
                if (
                    g != r
                    && world.Army.Job[g] == SpecialistJob.Active
                    && world.Army.JobKind[g] == MeterAction
                    && world.Army.JobCells[g] != null
                    && Array.IndexOf(world.Army.JobCells[g]!, first) >= 0
                )
                {
                    return false;
                }
            }
            // The span: the entry's segment and every standing segment joined to it, in ascending order.
            var span = new List<int> { terrain.StandingSegment(entry) };
            for (int i = 0; i < span.Count; i++)
            {
                foreach (int n in terrain.Segment(span[i]).Neighbours)
                {
                    if (
                        n >= 0
                        && n < terrain.SegmentCount
                        && (n >= states.Count || !states.Broken[n])
                        && !span.Contains(n)
                    )
                    {
                        span.Add(n);
                    }
                }
            }
            var segments = span.ToArray();
            for (int i = 1; i < segments.Length; i++)
            {
                int v = segments[i];
                int j = i;
                while (j > 0 && segments[j - 1] > v)
                {
                    segments[j] = segments[j - 1];
                    j--;
                }
                segments[j] = v;
            }
            MarkSpan(world, segments);
            // Its width across the path at the entry.
            int ex = entry % terrain.Columns;
            int ey = entry / terrain.Columns;
            int sx;
            int sy;
            if (before >= 0)
            {
                sx = ex - before % terrain.Columns;
                sy = ey - before / terrain.Columns;
            }
            else
            {
                var (fx, fy) = FlowField.Offset(
                    directions[entry] < FlowField.AtGoal ? directions[entry] : (byte)0
                );
                sx = fx;
                sy = fy;
            }
            int px = -sy;
            int py = sx;
            int across = 1;
            foreach (int sign in new[] { 1, -1 })
            {
                int qx = ex + sign * px;
                int qy = ey + sign * py;
                while (terrain.InBounds(qx, qy) && OnSpan(terrain, terrain.Index(qx, qy)))
                {
                    across++;
                    qx += sign * px;
                    qy += sign * py;
                }
            }
            int widthMm = across * terrain.CellSizeMm;
            var army = world.Army;
            army.JobCells[r] = segments;
            var post = Post(world, r, before >= 0 ? before : entry, px, py, widthMm);
            army.JobTargets[r] = new[]
            {
                entry,
                widthMm,
                (int)RegimentSummary.Mm(post.X.Raw),
                (int)RegimentSummary.Mm(post.Y.Raw),
            };
            army.Point[r] = post;
            return true;
        }

        /// <summary>
        /// The marshals' post (a working default; S2 says only "at the near end"): beside the bank cell before the span,
        /// half the span's width plus their own frontage off the path, on whichever side foot can stand; else on the bank.
        /// </summary>
        private FixVec2 Post(World world, int r, int bank, int px, int py, int widthMm)
        {
            var terrain = _navigation.Terrain;
            var formation = _rules.UnitTypes[world.RegimentUnitType[r]].Formation;
            int bx = bank % terrain.Columns;
            int by = bank / terrain.Columns;
            var centre = terrain.CellCentre(bx, by);
            if (px == 0 && py == 0)
            {
                return centre;
            }
            long offsetMm = widthMm / 2 + (long)formation.Files * formation.FileSpacingMm;
            var side = new FixVec2(Fix.FromInt(px), Fix.FromInt(py)).WithLength(
                Fix.FromRatio(offsetMm, 1000)
            );
            foreach (var candidate in new[] { centre + side, centre - side })
            {
                var point = Clamp(candidate);
                terrain.CellOf(point, out int x, out int y);
                if (
                    _navigation.Costs.Cost(MovementClass.Foot, x, y) != CostFields.Impassable
                    && terrain.StandingSegment(terrain.Index(x, y)) < 0
                )
                {
                    return point;
                }
            }
            return centre;
        }
    }
}
