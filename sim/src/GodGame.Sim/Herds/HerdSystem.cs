using System;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// The herds' minds (spec S2 11.2–11.3), at the regiment AI rate: each herd's fear is the largest of its sources (a
    /// burning fire v0 patch, a recent lightning strike, drovers at work), measured from its centroid. Reaching the
    /// threshold commits it: agitated (milling, the warning), then the stampede, heading straight away from the strongest
    /// source and steered by the ground: a closed cell ahead turns it toward the cheaper side (left on a tie), and steep
    /// ground bends it downhill. The run ends after its length or time and the herd scatters, never to stampede again.
    /// Drovers' fear, below the threshold, walks it away from them. Herds are never aimed (brief, 2026-10-08).
    /// <para>Determinism: serial, in herd order, from the world as the step finds it.</para>
    /// </summary>
    public sealed class HerdSystem
    {
        private readonly Ruleset _rules;
        private readonly Navigation _navigation;

        public HerdSystem(Ruleset rules, Navigation navigation)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        }

        /// <summary>Angle units in <paramref name="cd"/> centi-degrees.</summary>
        public static int Units(int cd) => (int)((long)cd * Angle.UnitsPerTurn / 36_000);

        public void Update(TickContext context, RegimentSummary summary)
        {
            var world = context.World;
            var herds = world.Herds;
            var rules = _rules.Herds;
            if (herds.Count == 0 || rules == null)
            {
                return;
            }
            int tick = context.Tick;
            var log = world.Causes;
            for (int h = 0; h < herds.Count; h++)
            {
                int r = herds.Regiment[h];
                if (!world.Regiments.IsAlive(r) || summary.Count[r] == 0)
                {
                    continue;
                }
                var centre = summary.Centroid[r];
                int fear = Fear(
                    world,
                    rules,
                    centre,
                    tick,
                    out FixVec2 source,
                    out int cause,
                    out int drovers
                );
                herds.Fear[h] = fear;
                herds.DrivenBy[h] = -1;
                switch (herds.Mode[h])
                {
                    case HerdMode.Grazing:
                        if (fear >= rules.ThresholdFear)
                        {
                            herds.Mode[h] = HerdMode.Agitated;
                            herds.Since[h] = tick;
                            var away = centre - source;
                            herds.Heading[h] = away == FixVec2.Zero ? 0 : away.Direction.Units;
                            herds.Event[h] =
                                log?.Add(CauseKind.HerdAgitated, tick, h, fear, cause)
                                ?? CauseLog.None;
                            break;
                        }
                        Drive(world, herds, h, rules, fear, drovers);
                        if (herds.DrivenBy[h] >= 0)
                        {
                            herds.Anchor[h] = centre; // the grazing circle goes where the herd is walked
                        }
                        break;
                    case HerdMode.Scattered:
                        Drive(world, herds, h, rules, fear, drovers);
                        break;
                    case HerdMode.Agitated:
                        if (tick - herds.Since[h] >= rules.AgitationTicks)
                        {
                            herds.Mode[h] = HerdMode.Stampede;
                            herds.Since[h] = tick;
                            herds.Anchor[h] = centre;
                            herds.Event[h] =
                                log?.Add(
                                    CauseKind.HerdStampeded,
                                    tick,
                                    h,
                                    herds.Heading[h],
                                    herds.Event[h]
                                ) ?? CauseLog.None;
                        }
                        break;
                    case HerdMode.Stampede:
                        long run =
                            (centre - herds.Anchor[h]).Length.Raw * 1000 >> Fix.FractionalBits;
                        if (run >= rules.RunMaxMm || tick - herds.Since[h] >= rules.RunMaxTicks)
                        {
                            herds.Mode[h] = HerdMode.Scattered;
                            herds.Since[h] = tick;
                            herds.Anchor[h] = centre;
                            log?.Add(CauseKind.HerdScattered, tick, h, 0, herds.Event[h]);
                            break;
                        }
                        Steer(herds, h, rules, centre);
                        break;
                }
            }
        }

        /// <summary>Drovers' fear, under the threshold but at least the driven line, walks the herd away from them.</summary>
        private static void Drive(
            World world,
            HerdStates herds,
            int h,
            HerdRules rules,
            int fear,
            int drovers
        )
        {
            if (drovers >= 0 && fear >= rules.DrivenFearAtLeast)
            {
                herds.DrivenBy[h] = drovers;
                herds.DrivenFrom[h] = DroverCentre(world, drovers);
            }
        }

        private static FixVec2 DroverCentre(World world, int r)
        {
            long x = 0;
            long y = 0;
            int n = 0;
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (world.Units.IsAlive(u) && world.UnitRegiment[u] == r)
                {
                    x += world.Position[u].X.Raw;
                    y += world.Position[u].Y.Raw;
                    n++;
                }
            }
            return n == 0 ? FixVec2.Zero : new FixVec2(Fix.FromRaw(x / n), Fix.FromRaw(y / n));
        }

        /// <summary>
        /// The largest fear on a herd at <paramref name="centre"/>, its source's point and cause event, and the drovers'
        /// regiment when theirs is the largest (else -1).
        /// </summary>
        private int Fear(
            World world,
            HerdRules rules,
            FixVec2 centre,
            int tick,
            out FixVec2 source,
            out int cause,
            out int drovers
        )
        {
            int best = 0;
            source = centre;
            cause = CauseLog.None;
            drovers = -1;
            var strikes = world.Strikes;
            var terrain = _navigation.Terrain;
            for (int f = 0; f < strikes.FireCount; f++)
            {
                if (strikes.FireOut(f))
                {
                    continue;
                }
                foreach (int cell in world.Blockers.Cells(strikes.FireBlocker(f)))
                {
                    var at = terrain.CellCentre(cell % terrain.Columns, cell / terrain.Columns);
                    int fear = rules.Fire.At(Mm(at - centre));
                    if (fear > best)
                    {
                        best = fear;
                        source = at;
                        cause = strikes.FireEvent(f);
                    }
                }
            }
            for (int s = 0; s < strikes.StrikeCount; s++)
            {
                var at = new FixVec2(
                    Fix.FromRatio(strikes.StrikeXMm(s), 1000),
                    Fix.FromRatio(strikes.StrikeYMm(s), 1000)
                );
                long decay =
                    (long)rules.Strike.DecayPerS
                    * (tick - strikes.StrikeTick(s))
                    / SimConstants.TicksPerSecond;
                int fear = (int)System.Math.Max(0, rules.Strike.At(Mm(at - centre)) - decay);
                if (fear > best)
                {
                    best = fear;
                    source = at;
                    cause = strikes.StrikeEvent(s);
                }
            }
            var army = world.Army;
            for (int r = 0; r < world.Regiments.HighWater; r++)
            {
                if (
                    !world.Regiments.IsAlive(r)
                    || world.RegimentAlive[r] == 0
                    || army.Job[r] != SpecialistJob.Active
                    || army.JobKind[r] != SpecialistExecutor.DriveAction
                    || tick < army.FallBackReady[r] // the dogs are scattered
                )
                {
                    continue;
                }
                var at = DroverCentre(world, r);
                int fear = rules.Drovers.At(Mm(at - centre));
                if (fear > best)
                {
                    best = fear;
                    source = at;
                    cause = CauseLog.None;
                    drovers = r;
                }
            }
            return best;
        }

        /// <summary>
        /// Each update: a closed cell ahead turns the run by a step toward the cheaper side (left on a tie); on steep ground
        /// whose fall line is within the arc it bends downhill by up to the turn rate.
        /// </summary>
        private void Steer(HerdStates herds, int h, HerdRules rules, FixVec2 centre)
        {
            var terrain = _navigation.Terrain;
            var costs = _navigation.Costs;
            var heading = Angle.FromUnits(herds.Heading[h]);
            Fix ahead = Fix.FromRatio(rules.LookAheadMm, 1000);
            if (Closed(costs, terrain, centre + FixVec2.FromAngle(heading, ahead)))
            {
                var step = Angle.FromUnits(Units(rules.TurnStepCd));
                int left = CostAt(
                    costs,
                    terrain,
                    centre + FixVec2.FromAngle(heading + step, ahead)
                );
                int right = CostAt(
                    costs,
                    terrain,
                    centre + FixVec2.FromAngle(heading - step, ahead)
                );
                heading = right < left ? heading - step : heading + step;
            }
            else
            {
                terrain.CellOf(centre, out int x, out int y);
                long east = terrain.HeightMm(x + 1, y) - (long)terrain.HeightMm(x - 1, y);
                long north = terrain.HeightMm(x, y + 1) - (long)terrain.HeightMm(x, y - 1);
                long slope =
                    IntMath.Sqrt((ulong)(east * east + north * north))
                    * 1000
                    / (2L * terrain.CellSizeMm);
                if (slope >= rules.DownhillMinSlopePermille && (east != 0 || north != 0))
                {
                    var fall = new FixVec2(Fix.FromRaw(-east), Fix.FromRaw(-north)).Direction;
                    int delta = heading.DeltaTo(fall);
                    if (System.Math.Abs(delta) <= Units(rules.DownhillArcCd))
                    {
                        int max = Units(
                            rules.DownhillTurnCdPerS
                                * TickOrder.RegimentAiRate.Period
                                / SimConstants.TicksPerSecond
                        );
                        heading += Angle.FromUnits(
                            System.Math.Max(-max, System.Math.Min(max, delta))
                        );
                    }
                }
            }
            herds.Heading[h] = heading.Units;
        }

        private static bool Closed(CostFields costs, Terrain terrain, FixVec2 at)
        {
            terrain.CellOf(at, out int x, out int y);
            return costs.Cost(MovementClass.Horse, x, y) == CostFields.Impassable
                || terrain.IsBurning(terrain.Index(x, y));
        }

        private static int CostAt(CostFields costs, Terrain terrain, FixVec2 at)
        {
            terrain.CellOf(at, out int x, out int y);
            return terrain.IsBurning(terrain.Index(x, y))
                ? CostFields.Impassable
                : costs.Cost(MovementClass.Horse, x, y);
        }

        private static long Mm(FixVec2 v) => v.Length.Raw * 1000 >> Fix.FractionalBits;
    }
}
