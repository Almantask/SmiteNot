using System;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// Cavalry charges (spec S2 10; D7, D8; brief 2026-10-11: trampled by a charge):
    /// <list type="bullet">
    /// <item>At the regiment AI rate, <see cref="Decide"/>: a charging unit type whose nearest enemy outline is 20–80 m
    /// away, along a straight line open for horse with no deep-ish mud, water or ice on it (D8), charges it; the charge
    /// ends after its run, when half its riders have made contact, or when its target is gone; then a cooldown, and a
    /// regiment that has charged needs the recharge gap to charge again.</item>
    /// <item>Every tick, <see cref="Contact"/> (the impacts step): a rider still at charging speed who reaches deep mud or
    /// water stops dead, stunned, and ends his regiment's charge (D7); one who reaches enemy soldiers on their feet takes
    /// at most two in front of him, launching or trampling each (a braced line: fewer launches, and it hurts him), then
    /// slows to fight.</item>
    /// </list>
    /// Movement reads <see cref="ArmyState.ChargeSince"/> and <see cref="UnitFlags.Charged"/> for the riders' pace.
    /// <para>Determinism: serial, in regiment then unit slot order; each rider's rolls are their own stream (rider, tick).</para>
    /// </summary>
    public sealed class Charges
    {
        /// <summary>A charge contact's rolls (<see cref="SimRandom.Stream"/>: entity = the rider's slot).</summary>
        public const int ChargeSystemId = 6_102;

        private const int MaxNeighbours = 64;

        private readonly Ruleset _rules;
        private readonly Navigation _navigation;
        private readonly ChargeRules? _charge;
        private readonly long[] _typeRadiusRaw;
        private readonly long _maxRadiusRaw;
        private readonly int[] _targets = new int[16];
        private readonly long[] _targetDist = new long[16];
        private bool[] _ends = new bool[0];

        public Charges(Ruleset rules, Navigation navigation)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _charge = rules.Combat.Charge;
            _typeRadiusRaw = new long[rules.UnitTypes.Count];
            for (int t = 0; t < rules.UnitTypes.Count; t++)
            {
                _typeRadiusRaw[t] = Fix.FromRatio(rules.UnitTypes[t].RadiusMm, 1000).Raw;
                _maxRadiusRaw = System.Math.Max(_maxRadiusRaw, _typeRadiusRaw[t]);
            }
        }

        /// <summary>The rules, or null when nothing charges.</summary>
        public ChargeRules? Rules => _charge;

        /// <summary>Whether any regiment is charging (the impacts step skips <see cref="Contact"/> otherwise).</summary>
        public static bool Any(World world)
        {
            for (int r = 0; r < world.Regiments.HighWater; r++)
            {
                if (world.Regiments.IsAlive(r) && world.Army.ChargeSince[r] > 0)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The regiment AI's charge rule for cavalry regiment <paramref name="r"/>, whose nearest enemy within the start range
        /// is <paramref name="nearest"/> (or -1). Returns true while it charges: its order is then Engage on the charge's
        /// target, and the targeting rules leave it alone.
        /// </summary>
        public bool Decide(TickContext context, RegimentSummary summary, int r, int nearest)
        {
            var c = _charge;
            if (c == null)
            {
                return false;
            }
            var world = context.World;
            var army = world.Army;
            int tick = context.Tick;
            if (army.ChargeSince[r] > 0)
            {
                int target = army.Target[r];
                long run = Mm((summary.Centroid[r] - army.ChargeFrom[r]).Length.Raw);
                if (
                    target < 0
                    || !world.Regiments.IsAlive(target)
                    || summary.Count[target] == 0
                    || army.Order[r] == RegimentOrder.Rout
                    || run >= c.MaxRunMm
                    || HalfContacted(world, r)
                )
                {
                    End(world, r, tick);
                    return false;
                }
                army.Order[r] = RegimentOrder.Engage;
                return true;
            }
            if (nearest < 0 || tick < army.ChargeReady[r] || army.Order[r] == RegimentOrder.Rout)
            {
                return false;
            }
            long gap = summary.Gap(r, nearest);
            long min =
                army.ChargeReady[r] == 0 // never charged (a charge's end always sets a later tick)
                    ? c.StartMinMm
                    : System.Math.Max(c.StartMinMm, c.RechargeSeparationMm);
            if (gap < min || gap > c.StartMaxMm)
            {
                return false;
            }
            if (!LineOpen(world, summary.Centroid[r], summary.Centroid[nearest]))
            {
                return false;
            }
            army.ChargeSince[r] = tick + 1;
            army.ChargeFrom[r] = summary.Centroid[r];
            army.ChargeEvent[r] =
                world.Causes?.Add(CauseKind.ChargeStarted, tick, r, nearest, CauseLog.None)
                ?? CauseLog.None;
            army.Order[r] = RegimentOrder.Engage;
            army.Target[r] = nearest;
            return true;
        }

        /// <summary>Every tick, after the flights: deep-ground stops and contacts of the riders still at speed.</summary>
        public void Contact(TickContext context, SpatialHash hash, PendingDeaths deaths)
        {
            var c = _charge!;
            var world = context.World;
            var army = world.Army;
            var terrain = _navigation.Terrain;
            int tick = context.Tick;
            if (_ends.Length != world.Regiments.Capacity)
            {
                _ends = new bool[world.Regiments.Capacity];
            }
            Array.Clear(_ends, 0, _ends.Length);
            var fields = world.Fields;
            int[]? mud =
                fields != null && fields.Rules.IndexOf(GroundModel.Mud) >= 0
                    ? fields.Get(GroundModel.Mud).Values
                    : null;
            int[]? water =
                fields != null && fields.Rules.IndexOf(WaterModel.Depth) >= 0
                    ? fields.Get(WaterModel.Depth).Values
                    : null;
            long perTick = 1000L * SimConstants.TicksPerSecond;
            long charging = Fix.FromRatio(c.ChargingMinMmPerS, perTick).Raw;
            Fix after = Fix.FromRatio(c.AfterContactMmPerS, perTick);
            int arc = HerdSystem.Units(c.FrontArcCd);
            int facingArc = HerdSystem.Units(c.BracedFacingArcCd);
            long braceSpeed = Fix.FromRatio(c.BracedMaxSpeedMmPerS, perTick).Raw;
            var source = _rules.Launches.Source("charge");
            var log = world.Causes;
            Span<int> near = stackalloc int[MaxNeighbours];
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (!world.Units.IsAlive(u))
                {
                    continue;
                }
                int r = world.UnitRegiment[u];
                var flags = world.Flags[u];
                if (
                    army.ChargeSince[r] == 0
                    || _ends[r]
                    || (flags & UnitFlags.Charged) != 0
                    || !Impacts.OnFeet(flags)
                    || !_rules.UnitTypes[world.UnitType[u]].Charges
                )
                {
                    continue;
                }
                var v = world.Velocity[u];
                if (v.X.Raw * v.X.Raw + v.Y.Raw * v.Y.Raw < charging * charging)
                {
                    continue;
                }
                var p = world.Position[u];
                terrain.CellOf(p, out int x, out int y);
                int cell = terrain.Index(x, y);
                if (
                    (mud != null && mud[cell] >= c.StopMudAtLeast)
                    || (water != null && (long)water[cell] >= (long)c.StopWaterAtLeastMm * 1000)
                )
                {
                    // D7: thrown in the mud. Stopped dead, stunned, unhurt; the regiment's charge is over.
                    world.Velocity[u] = FixVec2.Zero;
                    world.Flags[u] = (flags & ~UnitFlags.Teetering) | UnitFlags.Stunned;
                    world.HazardTimer[u] = c.StopStunTicks;
                    log?.Add(CauseKind.RiderThrown, tick, r, u, army.ChargeEvent[r]);
                    _ends[r] = true;
                    continue;
                }
                var heading = v.Direction;
                long radius = _typeRadiusRaw[world.UnitType[u]];
                int found = hash.QueryRadius(world, p, Fix.FromRaw(radius + _maxRadiusRaw), near);
                int count = System.Math.Min(found, MaxNeighbours);
                int taken = 0;
                int max = System.Math.Min(c.TargetsPerRider, _targets.Length);
                for (int k = 0; k < count && max > 0; k++)
                {
                    int j = near[k];
                    if (
                        j == u
                        || !world.Units.IsAlive(j)
                        || !Impacts.OnFeet(world.Flags[j])
                        || world.Health[j] <= 0
                        || !Sides.Hostile(
                            world.RegimentSide[world.UnitRegiment[j]],
                            world.RegimentSide[r]
                        )
                        || _rules.UnitTypes[world.UnitType[j]].IsAnimal
                        || EngineCrews.IsEngine(world, _rules, j)
                    )
                    {
                        continue;
                    }
                    var d = world.Position[j] - p;
                    long contact = radius + _typeRadiusRaw[world.UnitType[j]];
                    long dSq = d.X.Raw * d.X.Raw + d.Y.Raw * d.Y.Raw;
                    if (dSq >= contact * contact)
                    {
                        continue;
                    }
                    if (d != FixVec2.Zero && System.Math.Abs(heading.DeltaTo(d.Direction)) > arc)
                    {
                        continue;
                    }
                    // Keep the nearest `max`, ties to the lower slot (the hash lists slots in a fixed order).
                    int at = taken;
                    while (
                        at > 0
                        && (
                            _targetDist[at - 1] > dSq
                            || (_targetDist[at - 1] == dSq && _targets[at - 1] > j)
                        )
                    )
                    {
                        at--;
                    }
                    if (at >= max)
                    {
                        continue;
                    }
                    for (int m = System.Math.Min(taken, max - 1); m > at; m--)
                    {
                        _targets[m] = _targets[m - 1];
                        _targetDist[m] = _targetDist[m - 1];
                    }
                    _targets[at] = j;
                    _targetDist[at] = dSq;
                    taken = System.Math.Min(taken + 1, max);
                }
                if (taken == 0)
                {
                    continue;
                }
                var rng = SimRandom.Stream(context.Seed, ChargeSystemId, u, tick);
                var reverse = heading + Angle.FromUnits(Angle.UnitsPerTurn / 2);
                int riderDamage = 0;
                int firstTargetRegiment = world.UnitRegiment[_targets[0]];
                for (int k = 0; k < taken; k++)
                {
                    int j = _targets[k];
                    int tr = world.UnitRegiment[j];
                    var tv = world.Velocity[j];
                    bool braced =
                        _rules.UnitTypes[world.UnitType[j]].Braces
                        && !army.Open[tr]
                        && tv.X.Raw * tv.X.Raw + tv.Y.Raw * tv.Y.Raw < braceSpeed * braceSpeed
                        && System.Math.Abs(world.RegimentFacing[tr].DeltaTo(reverse)) <= facingArc;
                    if (braced)
                    {
                        riderDamage += c.RiderDamagePerTarget;
                    }
                    uint roll = rng.NextUInt(1000);
                    if (
                        source != null
                        && roll < (uint)(braced ? c.BracedLaunchPermille : c.LaunchPermille)
                    )
                    {
                        terrain.CellOf(world.Position[j], out int jx, out int jy);
                        Ballistics.Launch(
                            world,
                            _rules.Launches,
                            j,
                            source,
                            r,
                            heading,
                            army.ChargeEvent[r],
                            tick,
                            context.Seed,
                            Ballistics.SurfaceMm(terrain, world, jx, jy)
                        );
                        continue;
                    }
                    Impacts.Trample(
                        world,
                        deaths,
                        j,
                        c.TrampleDamage,
                        c.TrampleStunTicks,
                        army.ChargeEvent[r],
                        tick
                    );
                }
                world.Flags[u] |= UnitFlags.Charged;
                world.Velocity[u] = v.WithLength(after);
                if (riderDamage > 0)
                {
                    world.Health[u] -= riderDamage;
                    if (world.Health[u] <= 0)
                    {
                        // Killed by the braced line he rode into (S2 10.3); detail: its regiment.
                        deaths.Add(u, CauseKind.SoldierKilled, firstTargetRegiment, CauseLog.None);
                    }
                }
            }
            for (int r = 0; r < _ends.Length; r++)
            {
                if (_ends[r])
                {
                    End(world, r, tick);
                }
            }
        }

        /// <summary>Ends regiment <paramref name="r"/>'s charge: the cooldown starts and its riders may take targets again next time.</summary>
        private void End(World world, int r, int tick)
        {
            world.Army.ChargeSince[r] = 0;
            world.Army.ChargeReady[r] = tick + _charge!.CooldownTicks;
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (world.Units.IsAlive(u) && world.UnitRegiment[u] == r)
                {
                    world.Flags[u] &= ~UnitFlags.Charged;
                }
            }
        }

        private static bool HalfContacted(World world, int r)
        {
            int riders = 0;
            int contacted = 0;
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (world.Units.IsAlive(u) && world.UnitRegiment[u] == r)
                {
                    riders++;
                    if ((world.Flags[u] & UnitFlags.Charged) != 0)
                    {
                        contacted++;
                    }
                }
            }
            return riders > 0 && contacted * 2 >= riders;
        }

        /// <summary>
        /// The straight line from <paramref name="from"/> to <paramref name="to"/>, every half cell: open for horse, with no
        /// cell of mud or water at the charge's limits and no ice (S2 10.1, D8).
        /// </summary>
        private bool LineOpen(World world, FixVec2 from, FixVec2 to)
        {
            var c = _charge!;
            var terrain = _navigation.Terrain;
            var costs = _navigation.Costs;
            var fields = world.Fields;
            int[]? mud =
                fields != null && fields.Rules.IndexOf(GroundModel.Mud) >= 0
                    ? fields.Get(GroundModel.Mud).Values
                    : null;
            int[]? water =
                fields != null && fields.Rules.IndexOf(WaterModel.Depth) >= 0
                    ? fields.Get(WaterModel.Depth).Values
                    : null;
            var line = to - from;
            long length = Mm(line.Length.Raw);
            int step = System.Math.Max(1, terrain.CellSizeMm / 2);
            for (long d = 0; d <= length; d += step)
            {
                var at = length == 0 ? from : from + line * Fix.FromRatio(d, length);
                terrain.CellOf(at, out int x, out int y);
                int cell = terrain.Index(x, y);
                if (
                    costs.Cost(MovementClass.Horse, x, y) == CostFields.Impassable
                    || terrain.IsIce(cell)
                    || terrain.IsBurning(cell)
                    || (mud != null && mud[cell] >= c.BlockedByMudAtLeast)
                    || (water != null && (long)water[cell] >= (long)c.BlockedByWaterMm * 1000)
                )
                {
                    return false;
                }
            }
            return true;
        }

        private static long Mm(long raw) => raw * 1000 >> Fix.FractionalBits;
    }
}
