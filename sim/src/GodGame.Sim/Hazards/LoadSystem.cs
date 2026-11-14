using System;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// The structures-and-load step (spec S2 section 8), every field step (4 ticks): Rot under way lowers wooden
    /// segments' strength; each standing segment's load is the mass of the units whose centre is on it (not swept ones);
    /// its strain stage follows load ÷ strength with hysteresis, for the player's eyes only; at or over strength for
    /// <see cref="StructureRules.FailChecks"/> checks in a row it fails. A failed segment's cells fall back to what lies
    /// below, its neighbours lose strength (lost support), and the units on it go into the water: swept if the water
    /// below sweeps their class (on broken ice always, with the cold-water timer), else stunned where they land.
    /// <para>
    /// The launch arc between the deck and the water joins with launches (M5 item 5); until then a collapse puts its
    /// units straight into the state they would land in.
    /// </para>
    /// <para>Determinism: serial, in unit then segment order; everything that persists is in <see cref="StructureStates"/>.</para>
    /// </summary>
    public sealed class LoadSystem : ISimSystem
    {
        private readonly Ruleset _rules;
        private readonly Navigation _navigation;
        private readonly int[] _typeMass;
        private long[] _load = new long[0];
        private int[] _failed = new int[0];

        public LoadSystem(Ruleset rules, Navigation navigation)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _typeMass = new int[rules.UnitTypes.Count];
            for (int t = 0; t < _typeMass.Length; t++)
            {
                _typeMass[t] = rules.UnitTypes[t].MassKg;
            }
        }

        public TickStep Step => TickStep.StructuresAndLoad;

        /// <summary>Every field step, like the fields: one jostle at a single tick doesn't decide a bridge.</summary>
        public TickRate Rate => TickOrder.FieldsRate;

        public void Run(TickContext context)
        {
            var world = context.World;
            var states = world.Structures;
            var terrain = _navigation.Terrain;
            if (states == null || states.Count == 0)
            {
                return;
            }
            int segments = states.Count;
            Rot(states, context.Tick);
            if (_load.Length < states.Capacity)
            {
                _load = new long[states.Capacity];
                _failed = new int[states.Capacity];
            }
            Array.Clear(_load, 0, segments);
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (
                    !world.Units.IsAlive(u)
                    || (world.Flags[u] & (UnitFlags.Swept | UnitFlags.Launched)) != 0
                )
                {
                    continue;
                }
                terrain.CellOf(world.Position[u], out int x, out int y);
                int s = terrain.StandingSegment(terrain.Index(x, y));
                if (s >= 0)
                {
                    _load[s] += _typeMass[world.UnitType[u]];
                }
            }
            var rules = _rules.Structures;
            int failures = 0;
            for (int s = 0; s < segments; s++)
            {
                if (states.Broken[s])
                {
                    continue;
                }
                long strength =
                    terrain.Segment(s).MaxStrengthKg
                    * states.Strength[s]
                    / 1000
                    * Afloat(terrain, s, world)
                    / 1000;
                long ratio =
                    strength <= 0
                        ? 1_000_000
                        : System.Math.Min(1_000_000, _load[s] * 1000 / strength);
                states.LoadPermille[s] = (int)ratio;
                states.Stage[s] = NextStage(states.Stage[s], ratio, rules);
                // A load that reaches strength fails it; an empty span never falls by itself (an unloaded segment with
                // its support gone waits for a load, however small).
                states.OverChecks[s] =
                    _load[s] > 0 && _load[s] >= strength ? states.OverChecks[s] + 1 : 0;
                if (states.OverChecks[s] >= rules.FailChecks)
                {
                    _failed[failures++] = s;
                }
            }
            for (int k = 0; k < failures; k++)
            {
                Fail(context, _failed[k]);
            }
        }

        /// <summary>
        /// A floating deck's share of its strength (S2 8.1): full afloat, the grounded share where the water under it is
        /// shallow (its shallowest cell), linear between. 1,000 for every other deck.
        /// </summary>
        private int Afloat(Terrain terrain, int s, World world)
        {
            var kind = terrain.Segment(s).Kind;
            var fields = world.Fields;
            if (!kind.Floats || fields == null || fields.Rules.IndexOf(WaterModel.Depth) < 0)
            {
                return 1000;
            }
            var depth = fields.Get(WaterModel.Depth).Values;
            long shallowest = long.MaxValue;
            foreach (int cell in terrain.Segment(s).Cells)
            {
                shallowest = System.Math.Min(shallowest, depth[cell] / 1000);
            }
            if (shallowest >= kind.AfloatDepthMm)
            {
                return 1000;
            }
            if (shallowest <= kind.GroundedDepthMm || kind.AfloatDepthMm == kind.GroundedDepthMm)
            {
                return kind.GroundedPermille;
            }
            return (int)(
                kind.GroundedPermille
                + (1000L - kind.GroundedPermille)
                    * (shallowest - kind.GroundedDepthMm)
                    / (kind.AfloatDepthMm - kind.GroundedDepthMm)
            );
        }

        /// <summary>The strain stage (S2 8.3): up at a threshold, back down only 50‰ below it, so nothing flickers.</summary>
        private static StrainStage NextStage(StrainStage stage, long ratio, StructureRules rules)
        {
            int down = rules.StageHysteresisPermille;
            var up =
                ratio >= rules.SagPermille ? StrainStage.Sag
                : ratio >= rules.CrackPermille ? StrainStage.Crack
                : ratio >= rules.CreakPermille ? StrainStage.Creak
                : StrainStage.Quiet;
            if (up >= stage)
            {
                return up;
            }
            int threshold =
                stage == StrainStage.Sag ? rules.SagPermille
                : stage == StrainStage.Crack ? rules.CrackPermille
                : rules.CreakPermille;
            return ratio < threshold - down ? up : stage;
        }

        /// <summary>Rot under way (S2 12.3): strength falls linearly from where it was to where the Rot takes it.</summary>
        private static void Rot(StructureStates states, int tick)
        {
            for (int s = 0; s < states.Count; s++)
            {
                if (states.RotStart[s] == 0 || states.Broken[s])
                {
                    continue;
                }
                int elapsed = tick - states.RotStart[s];
                if (elapsed >= states.RotTicks[s])
                {
                    states.Strength[s] = System.Math.Min(states.Strength[s], states.RotTo[s]);
                    states.RotStart[s] = 0;
                    continue;
                }
                int now =
                    states.RotFrom[s]
                    - (states.RotFrom[s] - states.RotTo[s]) * elapsed / states.RotTicks[s];
                states.Strength[s] = System.Math.Min(states.Strength[s], now);
            }
        }

        private void Fail(TickContext context, int s)
        {
            var world = context.World;
            var states = world.Structures!;
            var segment = _navigation.Terrain.Segment(s);
            bool ice = segment.Kind.IsIce;
            states.Broken[s] = true;
            states.Stage[s] = StrainStage.Quiet;
            states.OverChecks[s] = 0;
            states.RotStart[s] = 0;
            var log = world.Causes;
            int failed =
                log?.Add(CauseKind.SegmentFailed, context.Tick, s, ice ? 2 : 1, states.RotCause[s])
                ?? CauseLog.None;
            world.Counters.SegmentsFailed++;
            // Lost support: the neighbours weaken at once (S2 8.2), which can bring the next one down under its load.
            int loss = ice
                ? _rules.Structures.IceNeighbourLossPermille
                : _rules.Structures.NeighbourLossPermille;
            foreach (int n in segment.Neighbours)
            {
                if (!states.Broken[n])
                {
                    states.Strength[n] = System.Math.Max(0, states.Strength[n] - loss);
                }
            }
            _navigation.SegmentFailed(context, s);

            // The units on it go into what lies below (S2 8.4).
            var terrain = _navigation.Terrain;
            var fields = world.Fields;
            var water = _rules.Fields.Water;
            var depth = fields != null && water != null ? fields.Get(WaterModel.Depth) : null;
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (
                    !world.Units.IsAlive(u)
                    || (world.Flags[u] & (UnitFlags.Swept | UnitFlags.Launched)) != 0
                )
                {
                    continue;
                }
                terrain.CellOf(world.Position[u], out int x, out int y);
                int cell = terrain.Index(x, y);
                if (terrain.SegmentAt(cell) != s)
                {
                    continue;
                }
                var cls = _rules.UnitTypes[world.UnitType[u]].MovementClass;
                var response = _rules.Movement(cls).Water;
                long depthMm = 0;
                long flow = 0;
                if (depth != null && depth.Get(x, y) >= water!.DryDepthUm)
                {
                    depthMm = depth.Get(x, y) / 1000;
                    WaterModel.Velocity(
                        fields!,
                        terrain.CellSizeMm,
                        x,
                        y,
                        out int east,
                        out int north
                    );
                    flow = IntMath.Sqrt((ulong)((long)east * east + (long)north * north));
                }
                var flags = world.Flags[u] & ~(UnitFlags.Teetering | UnitFlags.Stunned);
                if (ice || response.Sweeps(depthMm, flow))
                {
                    world.Flags[u] = flags | UnitFlags.Swept;
                    world.HazardTimer[u] =
                        ice && (int)cls < _rules.Structures.ColdDrowningTicks.Length
                            ? _rules.Structures.ColdDrowningTicks[(int)cls]
                            : response.DrowningTicks;
                    world.Counters.Swept++;
                    if (log != null)
                    {
                        log.UnitSwept[u] = failed;
                    }
                }
                else if (_rules.Hazards.BankExitStunTicks > 0)
                {
                    world.Flags[u] = flags | UnitFlags.Stunned;
                    world.HazardTimer[u] = _rules.Hazards.BankExitStunTicks;
                }
                else
                {
                    world.Flags[u] = flags;
                    world.HazardTimer[u] = 0;
                }
            }
        }
    }
}
