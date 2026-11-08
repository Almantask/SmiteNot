using System;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// The regiment AI step at 4 Hz, with spec S2's targeting working default until the regiment AI (M7): attackers
    /// march on the defender zone and turn on any enemy regiment whose outline comes within 30 m; defenders hold and
    /// engage within 40 m; ranged regiments stop and shoot at the nearest enemy in range and fall back 30 m (once per
    /// 20 s) when enemy melee comes within 25 m. Routing regiments are the morale step's; wiped remnants are no
    /// target. Every regiment decides on the same tick from one summary, in slot order, so the result can't depend on
    /// the order regiments are visited. Engagements keep their target while it stays within the engage distance plus
    /// <see cref="HysteresisMm"/>, so two regiments at the threshold don't flicker.
    /// </summary>
    public sealed class RegimentOrderSystem : ISimSystem
    {
        /// <summary>Hysteresis on every distance trigger (placeholder; no spec sets it).</summary>
        public const int HysteresisMm = 10_000;

        /// <summary>A falling-back regiment is there once its centroid is this close to the point (placeholder).</summary>
        public const int ArrivedMm = 3_000;

        private readonly Ruleset _rules;
        private readonly MapData _map;
        private readonly Terrain _terrain;
        private readonly RegimentSummary _summary = new RegimentSummary();

        public RegimentOrderSystem(Ruleset rules, MapData map, Terrain terrain)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        }

        public TickStep Step => TickStep.RegimentAi;

        public TickRate Rate => TickOrder.RegimentAiRate;

        public void Run(TickContext context)
        {
            var world = context.World;
            var army = world.Army;
            var combat = _rules.Combat;
            _summary.Gather(world);
            int tick = context.Tick;
            for (int r = 0; r < world.Regiments.HighWater; r++)
            {
                if (!world.Regiments.IsAlive(r) || world.RegimentAlive[r] == 0)
                {
                    continue;
                }
                if (army.Order[r] == RegimentOrder.Rout)
                {
                    continue; // the morale step's until it rallies
                }
                var type = _rules.UnitTypes[world.RegimentUnitType[r]];
                var side = world.RegimentSide[r];
                if (type.Projectile != null)
                {
                    DecideRanged(world, r, type.Projectile, combat, tick);
                    continue;
                }
                if (type.Melee == null)
                {
                    army.Order[r] = RegimentOrder.March; // engines and the like wait for their own rules (M5 item 6)
                    army.Target[r] = -1;
                    continue;
                }
                int engage =
                    side == Side.Attacker ? combat.AttackerEngageMm : combat.DefenderEngageMm;
                int current = army.Order[r] == RegimentOrder.Engage ? army.Target[r] : -1;
                if (
                    current >= 0
                    && Targetable(world, r, current)
                    && _summary.Gap(r, current) <= engage + HysteresisMm
                )
                {
                    continue;
                }
                int nearest = Nearest(world, r, engage, meleeOnly: false);
                army.Order[r] = nearest >= 0 ? RegimentOrder.Engage : RegimentOrder.March;
                army.Target[r] = nearest;
            }
        }

        private void DecideRanged(
            World world,
            int r,
            ProjectileRules projectile,
            CombatRules combat,
            int tick
        )
        {
            var army = world.Army;
            if (army.Order[r] == RegimentOrder.FallBack)
            {
                long left =
                    (army.Point[r] - _summary.Centroid[r]).Length.Raw * 1000 >> Fix.FractionalBits;
                if (left > ArrivedMm && tick < army.FallBackReady[r])
                {
                    return; // still walking back
                }
            }
            // Enemy melee too close: fall back, away from it, at most once per cooldown.
            int threat = Nearest(world, r, combat.RangedFallbackTriggerMm, meleeOnly: true);
            if (threat >= 0 && tick >= army.FallBackReady[r])
            {
                var away = _summary.Centroid[r] - _summary.Centroid[threat];
                if (away == FixVec2.Zero)
                {
                    away = FixVec2.FromAngle(world.RegimentFacing[r], Fix.One) * -1;
                }
                var point =
                    _summary.Centroid[r]
                    + away.WithLength(Fix.FromRatio(combat.RangedFallbackMm, 1000));
                Fix maxX = _map.Width - Fix.FromRaw(1);
                Fix maxY = _map.Depth - Fix.FromRaw(1);
                army.Order[r] = RegimentOrder.FallBack;
                army.Point[r] = new FixVec2(
                    Fix.Clamp(point.X, Fix.Zero, maxX),
                    Fix.Clamp(point.Y, Fix.Zero, maxY)
                );
                army.Target[r] = -1;
                army.FallBackReady[r] = tick + combat.RangedFallbackCooldownTicks;
                return;
            }
            // Shoot the nearest enemy within range (with hysteresis on the current target).
            int current = army.Order[r] == RegimentOrder.Shoot ? army.Target[r] : -1;
            if (
                current >= 0
                && Targetable(world, r, current)
                && InRange(world, r, current, projectile, HysteresisMm)
            )
            {
                return;
            }
            int best = -1;
            long bestGap = long.MaxValue;
            for (int e = 0; e < world.Regiments.HighWater; e++)
            {
                if (!Targetable(world, r, e) || !InRange(world, r, e, projectile, 0))
                {
                    continue;
                }
                long gap = _summary.Gap(r, e);
                if (gap < bestGap)
                {
                    best = e;
                    bestGap = gap; // ascending slots: ties keep the lower one
                }
            }
            army.Order[r] = best >= 0 ? RegimentOrder.Shoot : RegimentOrder.March;
            army.Target[r] = best;
        }

        /// <summary>The target's outline lies within the shooter's range this moment (wind and height, S2 6.3).</summary>
        private bool InRange(
            World world,
            int r,
            int target,
            ProjectileRules projectile,
            int slackMm
        )
        {
            var from = _summary.Centroid[r];
            var to = _summary.Centroid[target];
            long range = Volleys.RangeMm(projectile, _map, _terrain, from, to);
            return _summary.GapFrom(from, target) <= range + slackMm;
        }

        /// <summary>The nearest live, unwiped enemy regiment whose outline is within <paramref name="withinMm"/>, or -1.</summary>
        private int Nearest(World world, int r, long withinMm, bool meleeOnly)
        {
            int best = -1;
            long bestGap = long.MaxValue;
            for (int e = 0; e < world.Regiments.HighWater; e++)
            {
                if (!Targetable(world, r, e))
                {
                    continue;
                }
                if (meleeOnly)
                {
                    var type = _rules.UnitTypes[world.RegimentUnitType[e]];
                    if (type.Melee == null || type.Projectile != null)
                    {
                        continue;
                    }
                }
                if (!_summary.Within(r, e, withinMm))
                {
                    continue;
                }
                long gap = _summary.Gap(r, e);
                if (gap < bestGap)
                {
                    best = e;
                    bestGap = gap;
                }
            }
            return best;
        }

        /// <summary>An enemy regiment with soldiers on the map that isn't wiped (routing ones may still be ridden down).</summary>
        private bool Targetable(World world, int r, int e) =>
            e >= 0
            && e < world.Regiments.HighWater
            && world.Regiments.IsAlive(e)
            && world.RegimentSide[e] != world.RegimentSide[r]
            && _summary.Count[e] > 0
            && !world.Army.Wiped[e];
    }
}
