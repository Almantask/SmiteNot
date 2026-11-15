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

        private readonly SpecialistExecutor? _specialists;
        private readonly HerdSystem? _herds;
        private readonly Charges? _charges;
        private bool[] _wasHold = new bool[0];
        private bool[] _beingCharged = new bool[0];

        public RegimentOrderSystem(Ruleset rules, MapData map, Terrain terrain)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
        }

        /// <summary>With the specialist executor (spec S2 14.1): specialists' actions run here, before the targeting rules.</summary>
        public RegimentOrderSystem(Ruleset rules, Navigation navigation, BattlePlan plan)
            : this(rules, navigation.Map, navigation.Terrain)
        {
            _specialists = new SpecialistExecutor(rules, navigation, plan, _summary);
            _herds = new HerdSystem(rules, navigation);
            _charges = new Charges(rules, navigation);
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
            // The herds' minds first (S2 11.2), from the drovers as they stand, so every regiment sees the same herds.
            _herds?.Update(context, _summary);
            _specialists?.Washout(context);
            // Crossing Marshals' gates first (S2 14.6), so every regiment decides against the same gate state.
            _specialists?.Gates(context);
            if (_wasHold.Length < world.Regiments.Capacity)
            {
                _wasHold = new bool[world.Regiments.Capacity];
                _beingCharged = new bool[world.Regiments.Capacity];
            }
            // Who is being charged, as the step finds it (charges begun on earlier updates), so a line's hold can't
            // depend on whether its charger's slot comes before or after its own.
            BeingCharged(world);
            for (int r = 0; r < world.Regiments.HighWater; r++)
            {
                // Read before the loop rewrites every order: a regiment still held keeps the point it stopped at.
                _wasHold[r] = army.Order[r] == RegimentOrder.Hold;
            }
            for (int r = 0; r < world.Regiments.HighWater; r++)
            {
                if (
                    !world.Regiments.IsAlive(r)
                    || world.RegimentAlive[r] == 0
                    || world.RegimentSide[r] == Side.Neutral
                )
                {
                    continue; // herds have minds of their own (HerdSystem)
                }
                if (_specialists != null && _specialists.Run(context, r))
                {
                    continue; // working, or paused by a threat
                }
                if (army.Order[r] == RegimentOrder.Rout)
                {
                    continue; // the morale step's until it rallies
                }
                var type = _rules.UnitTypes[world.RegimentUnitType[r]];
                var side = world.RegimentSide[r];
                if (_beingCharged[r] && type.Braces && !army.Open[r])
                {
                    // Brief 2026-11-15: close-order line infantry hold and brace against the charge, until it ends.
                    // They stand where their officer stands: a newly promoted officer (the old one thrown or killed)
                    // holds where he is rather than walking the line to his predecessor's spot.
                    int anchor = world.RegimentAnchor[r];
                    army.Point[r] = anchor >= 0 ? world.Position[anchor] : _summary.Centroid[r];
                    army.Order[r] = RegimentOrder.Hold;
                    army.Target[r] = -1;
                    continue;
                }
                if (
                    type.Charges
                    && _charges?.Rules != null
                    && _charges.Decide(
                        context,
                        _summary,
                        r,
                        Nearest(world, r, _charges.Rules.StartMaxMm, meleeOnly: false)
                    )
                )
                {
                    continue; // charging (S2 10.1)
                }
                if (type.Projectile != null && type.Projectile.IsStone)
                {
                    DecideSiege(world, r, type.Projectile);
                    continue;
                }
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
            if (_specialists != null)
            {
                Hold(world);
                _specialists.FootClearing(context);
            }
        }

        /// <summary>
        /// A regiment a gate holds short of its span and that would march stands where it stopped instead (S2 14.6). One
        /// that turned on an enemy fights as any regiment does (a working default: the gate meters marching only).
        /// </summary>
        private void Hold(World world)
        {
            var army = world.Army;
            for (int r = 0; r < world.Regiments.HighWater; r++)
            {
                if (
                    !world.Regiments.IsAlive(r)
                    || world.RegimentAlive[r] == 0
                    || army.Order[r] != RegimentOrder.March
                    || !_specialists!.Holds(r)
                )
                {
                    continue;
                }
                if (!_wasHold[r])
                {
                    int anchor = world.RegimentAnchor[r];
                    army.Point[r] = anchor >= 0 ? world.Position[anchor] : _summary.Centroid[r];
                }
                army.Order[r] = RegimentOrder.Hold;
                army.Target[r] = -1;
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

        /// <summary>
        /// Siege engines (spec S2 6.4, S2 5's targeting default): throw at the nearest enemy regiment whose outline is
        /// between the stone's minimum range and its range (after wind and height), keeping the current target within the
        /// hysteresis; march otherwise. Engines never fall back: they are too slow to.
        /// </summary>
        private void DecideSiege(World world, int r, ProjectileRules projectile)
        {
            var army = world.Army;
            int current = army.Order[r] == RegimentOrder.Shoot ? army.Target[r] : -1;
            if (
                current >= 0
                && Targetable(world, r, current)
                && InRange(world, r, current, projectile, HysteresisMm)
                && _summary.Gap(r, current) + HysteresisMm >= projectile.MinRangeMm
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
                if (gap >= projectile.MinRangeMm && gap < bestGap)
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

        /// <summary>
        /// Marks the regiments an enemy regiment is charging right now (brief 2026-11-15), when the rules let a line hold
        /// against a charge.
        /// </summary>
        private void BeingCharged(World world)
        {
            Array.Clear(_beingCharged, 0, _beingCharged.Length);
            if (_rules.Combat.Charge == null || !_rules.Combat.Charge.BraceHolds)
            {
                return;
            }
            var army = world.Army;
            for (int e = 0; e < world.Regiments.HighWater; e++)
            {
                int target = army.Target[e];
                if (
                    world.Regiments.IsAlive(e)
                    && army.ChargeSince[e] > 0
                    && target >= 0
                    && target < _beingCharged.Length
                    && world.Regiments.IsAlive(target)
                    && Sides.Hostile(world.RegimentSide[e], world.RegimentSide[target])
                )
                {
                    _beingCharged[target] = true;
                }
            }
        }

        /// <summary>An enemy regiment with soldiers on the map that isn't wiped (routing ones may still be ridden down).</summary>
        private bool Targetable(World world, int r, int e) =>
            e >= 0
            && e < world.Regiments.HighWater
            && world.Regiments.IsAlive(e)
            && Sides.Hostile(world.RegimentSide[e], world.RegimentSide[r])
            && _summary.Count[e] > 0
            && !world.Army.Wiped[e];
    }
}
