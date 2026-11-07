using System;
using System.Collections.Generic;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// The plan-events step for the casts layer (spec S1, section 8; brief, 2026-10-08):
    /// <list type="bullet">
    /// <item>At a cast's tick, the range rule: the whole circle must be at least the power's range (100 m) from every
    /// regiment's outline on both sides, or the cast fizzles, with its reasons, and writes nothing. The wind-up starts
    /// at the same tick (S1 D1); the check never runs again.</item>
    /// <item>A fired cast writes nothing during its wind-up, then adds each of its writes over its circle at every field
    /// step for its duration (Rain: 3 mm/s of water), before the field models step, so the water model moves it at
    /// once and the soil soaks its share. Overlapping casts add.</item>
    /// </list>
    /// A regiment's outline is the coarse cells (the reachability grid's, 4.5 m) holding at least
    /// <see cref="PowerRules.MinSoldiersPerCoarseCell"/> of its soldiers, so a lone straggler never blocks a cast; swept,
    /// stunned and teetering soldiers count like any other (S1 D9). Serial: casts are few, and their writes are summed
    /// in plan order.
    /// </summary>
    public sealed class CastSystem : ISimSystem
    {
        private readonly BattlePlan _plan;
        private readonly Navigation _navigation;
        private readonly int[] _writeOffset;
        private readonly int _coarseMm;
        private readonly int _coarseColumns;
        private readonly Dictionary<long, int> _outlineCounts = new Dictionary<long, int>();
        private int _tick; // the tick being run, for the events Check adds

        public CastSystem(BattlePlan plan, Navigation navigation)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _writeOffset = WriteOffsets(plan, out _);
            if (TickOrder.FieldsRate.Period != RulesetLoader.FieldStepTicks)
            {
                throw new InvalidOperationException("Power timings assume the fields' step period.");
            }
            _coarseMm = navigation.Terrain.CellSizeMm * navigation.Rules.Routes.CoarseCells;
            _coarseColumns = (navigation.Map.WidthMm + _coarseMm - 1) / _coarseMm;
        }

        public TickStep Step => TickStep.PlanEvents;

        public TickRate Rate => TickRate.EveryTick;

        /// <summary>The cast state a world needs for this plan (one remainder per write of every cast).</summary>
        public static CastStates StatesFor(BattlePlan plan)
        {
            WriteOffsets(plan, out int writes);
            return new CastStates(plan.Casts.Count, writes);
        }

        private static int[] WriteOffsets(BattlePlan plan, out int total)
        {
            var offsets = new int[plan.Casts.Count];
            total = 0;
            for (int c = 0; c < offsets.Length; c++)
            {
                offsets[c] = total;
                total += plan.Casts[c].Power.Writes.Count;
            }
            return offsets;
        }

        public void Run(TickContext context)
        {
            var world = context.World;
            var states =
                world.Casts
                ?? throw new InvalidOperationException(
                    "The world has no cast states for this plan."
                );
            if (states.Count != _plan.Casts.Count)
            {
                throw new InvalidOperationException(
                    "The world's cast states belong to another plan."
                );
            }
            int tick = context.Tick;
            _tick = tick;
            bool fieldStep = TickOrder.FieldsRate.IsDue(tick);
            for (int c = 0; c < _plan.Casts.Count; c++)
            {
                var cast = _plan.Casts[c];
                if (tick == cast.Tick && states.Status[c] == CastStatus.Pending)
                {
                    Check(world, states, c, cast);
                }
                if (!fieldStep || states.Status[c] != CastStatus.Fired)
                {
                    continue;
                }
                long start = (long)cast.Tick + cast.Power.WindUpTicks;
                if (tick < start || tick >= start + cast.Power.DurationTicks)
                {
                    continue;
                }
                for (int w = 0; w < cast.Power.Writes.Count; w++)
                {
                    Write(world, states, _writeOffset[c] + w, cast, cast.Power.Writes[w], states.Event[c]);
                }
            }
        }

        /// <summary>The range rule at the cast tick: the nearest outline of each side, and a fizzle if either is in range.</summary>
        private void Check(World world, CastStates states, int c, PlannedCast cast)
        {
            // Pass 1: soldiers per (regiment, coarse cell). Looked up only, never iterated, so its order can't matter.
            var counts = _outlineCounts;
            counts.Clear();
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (world.Units.IsAlive(u))
                {
                    long key = OutlineKey(world, u);
                    counts.TryGetValue(key, out int n);
                    counts[key] = n + 1;
                }
            }
            // Pass 2, in slot order: the nearest outline cell of each side (ties keep the first, the lower slot).
            long bestAttacker = long.MaxValue;
            long bestDefender = long.MaxValue;
            int attacker = CastStates.NoRegiment;
            int defender = CastStates.NoRegiment;
            int min = cast.Power.MinSoldiersPerCoarseCell;
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (!world.Units.IsAlive(u) || counts[OutlineKey(world, u)] < min)
                {
                    continue;
                }
                long d = DistanceSquared(world.Position[u], cast);
                int r = world.UnitRegiment[u];
                if (world.RegimentSide[r] == Side.Attacker)
                {
                    if (d < bestAttacker)
                    {
                        bestAttacker = d;
                        attacker = r;
                    }
                }
                else if (d < bestDefender)
                {
                    bestDefender = d;
                    defender = r;
                }
            }
            long reach = (long)cast.RadiusMm + cast.Power.RangeMm;
            var reasons = FizzleReasons.None;
            if (attacker != CastStates.NoRegiment && bestAttacker < reach * reach)
            {
                reasons |= FizzleReasons.TooCloseToAttacker;
            }
            if (defender != CastStates.NoRegiment && bestDefender < reach * reach)
            {
                reasons |= FizzleReasons.TooCloseToDefender;
            }
            states.Status[c] =
                reasons == FizzleReasons.None ? CastStatus.Fired : CastStatus.Fizzled;
            states.Reasons[c] = reasons;
            states.NearestAttacker[c] = attacker;
            states.AttackerDistanceMm[c] = EdgeDistance(bestAttacker, cast.RadiusMm, attacker);
            states.NearestDefender[c] = defender;
            states.DefenderDistanceMm[c] = EdgeDistance(bestDefender, cast.RadiusMm, defender);
            var log = world.Causes;
            states.Event[c] =
                log == null
                    ? CauseLog.None
                    : log.Add(
                        reasons == FizzleReasons.None ? CauseKind.CastFired : CauseKind.CastFizzled,
                        _tick,
                        c,
                        (int)reasons,
                        CauseLog.None
                    );
        }

        private long OutlineKey(World world, int u)
        {
            _navigation.Terrain.CellOf(world.Position[u], out int x, out int y);
            int cellMm = _navigation.Terrain.CellSizeMm;
            long coarse = (long)(y * cellMm / _coarseMm) * _coarseColumns + x * cellMm / _coarseMm;
            return (long)world.UnitRegiment[u] << 32 | coarse;
        }

        /// <summary>Squared mm from the circle's centre to the nearest point of the soldier's coarse cell.</summary>
        private long DistanceSquared(FixVec2 position, PlannedCast cast)
        {
            _navigation.Terrain.CellOf(position, out int x, out int y);
            int cellMm = _navigation.Terrain.CellSizeMm;
            long minX = (long)(x * cellMm / _coarseMm) * _coarseMm;
            long minY = (long)(y * cellMm / _coarseMm) * _coarseMm;
            long dx = System.Math.Max(
                0,
                System.Math.Max(minX - cast.XMm, cast.XMm - (minX + _coarseMm))
            );
            long dy = System.Math.Max(
                0,
                System.Math.Max(minY - cast.YMm, cast.YMm - (minY + _coarseMm))
            );
            return dx * dx + dy * dy; // both within the map (at most a few km): far inside a long
        }

        /// <summary>Whether any cell centre of the tile lies inside the cast's circle.</summary>
        private static bool TouchesCircle(CellRect cells, PlannedCast cast, int cellMm)
        {
            long r = cast.RadiusMm;
            for (int y = cells.MinY; y <= cells.MaxY; y++)
            {
                long dy = (long)y * cellMm + cellMm / 2 - cast.YMm;
                for (int x = cells.MinX; x <= cells.MaxX; x++)
                {
                    long dx = (long)x * cellMm + cellMm / 2 - cast.XMm;
                    if (dx * dx + dy * dy <= r * r)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static int EdgeDistance(long distanceSquared, int radiusMm, int regiment) =>
            regiment == CastStates.NoRegiment
                ? 0
                : (int)(IntMath.Sqrt((ulong)distanceSquared) - radiusMm);

        /// <summary>One field step's worth of a write over the circle (cells whose centres lie inside it).</summary>
        private void Write(
            World world,
            CastStates states,
            int carryIndex,
            PlannedCast cast,
            PowerWrite write,
            int cause
        )
        {
            var fields = world.Fields;
            if (fields == null)
            {
                return;
            }
            // rate × ticks per field step, in units × ticks / s; dividing by ticks per second leaves a remainder that
            // the next step carries, so every second adds exactly the rate.
            long total =
                (long)write.RatePerSecond * TickOrder.FieldsRate.Period + states.Carry[carryIndex];
            long amount = total / SimConstants.TicksPerSecond;
            states.Carry[carryIndex] = (int)(total % SimConstants.TicksPerSecond);
            if (amount == 0)
            {
                return;
            }
            var grid = fields.Get(write.Field);
            var terrain = _navigation.Terrain;
            int cellMm = terrain.CellSizeMm;
            long r = cast.RadiusMm;
            int minX = System.Math.Max(0, (int)((cast.XMm - r) / cellMm));
            int maxX = System.Math.Min(terrain.Columns - 1, (int)((cast.XMm + r) / cellMm));
            int minY = System.Math.Max(0, (int)((cast.YMm - r) / cellMm));
            int maxY = System.Math.Min(terrain.Rows - 1, (int)((cast.YMm + r) / cellMm));
            for (int y = minY; y <= maxY; y++)
            {
                long dy = (long)y * cellMm + cellMm / 2 - cast.YMm;
                for (int x = minX; x <= maxX; x++)
                {
                    long dx = (long)x * cellMm + cellMm / 2 - cast.XMm;
                    if (dx * dx + dy * dy > r * r)
                    {
                        continue;
                    }
                    int i = y * grid.Columns + x;
                    grid.Values[i] = grid.Clamp(grid.Values[i] + amount);
                }
            }
            // Wake every tile the circle touches and their neighbours, so the field models step the new water this very
            // tick and its runoff can leave the circle.
            int tile = grid.TileCells;
            var log = string.Equals(write.Field, WaterModel.Depth, StringComparison.Ordinal)
                ? world.Causes
                : null;
            for (int ty = minY / tile; ty <= maxY / tile; ty++)
            {
                for (int tx = minX / tile; tx <= maxX / tile; tx++)
                {
                    int t = grid.TileOf(tx * tile, ty * tile);
                    grid.WakeAround(t);
                    // The water it adds carries the cast as its cause (S1, section 12): the newest cause wins, on tiles
                    // the circle actually rains on.
                    if (log != null && TouchesCircle(grid.TileRect(t), cast, cellMm))
                    {
                        log.TileTag[t] = System.Math.Max(log.TileTag[t], cause);
                    }
                }
            }
        }
    }
}
