using System;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>How a launched soldier came down (spec S2 9.3), in his <see cref="CauseKind.SoldierLanded"/> event.</summary>
    public enum LandingOutcome
    {
        /// <summary>Dry ground, mud, a road, a standing deck, or thrown off the map and clamped to its edge: hurt and stunned.</summary>
        Ground = 0,

        /// <summary>Intact ice, as ground; he is load again.</summary>
        Ice = 1,

        /// <summary>Water that doesn't sweep his class: stunned, no damage.</summary>
        ShallowWater = 2,

        /// <summary>Deep water within the fringe of a cell he can stand in: moved there, stunned, no damage.</summary>
        Fringe = 3,

        /// <summary>Deep or sweeping water beyond the fringe: swept (drift, paddle, drowning timer).</summary>
        Swept = 4,

        /// <summary>Ground at least hazards.json's lethal drop below the surface he was launched from: killed.</summary>
        Fell = 5,

        /// <summary>Ground a hard fall below it: hurt more, stunned longer.</summary>
        HardFall = 6,

        /// <summary>Into a hard blocker (a boulder, a wall): moved back along the arc to the nearest open cell, hurt.</summary>
        Blocked = 7,

        /// <summary>The landing's damage killed him.</summary>
        Killed = 8,
    }

    /// <summary>Packs and reads a <see cref="CauseKind.SoldierLanded"/> event's detail: the landing cell and the outcome.</summary>
    public static class Landing
    {
        public static int Detail(int cell, LandingOutcome outcome) => cell * 16 + (int)outcome;

        public static int Cell(int detail) => detail >> 4;

        public static LandingOutcome Outcome(int detail) => (LandingOutcome)(detail & 15);

        /// <summary>Red in the landing-spread preview (S2 9.3): water beyond the fringe, a cliff, a killing blow.</summary>
        public static bool IsLethal(LandingOutcome outcome) =>
            outcome == LandingOutcome.Swept
            || outcome == LandingOutcome.Fell
            || outcome == LandingOutcome.Killed;
    }

    /// <summary>
    /// Launching soldiers (spec S2 9.1–9.2; brief, 2026-10-09): a launched soldier becomes a ballistic body, thrown along
    /// the source's heading (or any heading, for a collapse) with a fixed spread seeded per (source event, soldier), never
    /// from the contact geometry, so a small edit moves the landing spread smoothly. Sources call <see cref="Launch"/> from
    /// serial code; <see cref="BallisticsSystem"/> flies and lands the bodies.
    /// </summary>
    public static class Ballistics
    {
        /// <summary>
        /// The spread's random streams (<see cref="SimRandom.Stream"/>): system = this × 64 + the source's index in
        /// launches.json, entity = the source's stable key, tick = the unit slot.
        /// </summary>
        public const int LaunchSystemId = 6_001;

        /// <summary>launches.json holds at most this many sources (the source index packs into 6 bits).</summary>
        public const int MaxSources = 64;

        /// <summary>
        /// A launch's stable source identity, as the <see cref="CauseKind.SoldierLaunched"/> event's detail: the source's
        /// index in launches.json and its key (a collapse: the failed segment; a charge: the rider's regiment; a stampede:
        /// the herd; a tree: the tree). Unlike the source's cause-log index it doesn't move when an unrelated event is logged
        /// earlier, so a small edit moves the landing spread smoothly (brief, 2026-10-09).
        /// </summary>
        public static int SourceId(LaunchSourceRules source, int key) =>
            key * MaxSources + (source ?? throw new ArgumentNullException(nameof(source))).Index;

        /// <summary>The source index packed in <see cref="SourceId"/>.</summary>
        public static int SourceIndexOf(int sourceId) => sourceId & (MaxSources - 1);

        /// <summary>The key packed in <see cref="SourceId"/>.</summary>
        public static int SourceKeyOf(int sourceId) => sourceId >> 6;

        /// <summary>A guard, not a rule: a body still airborne after a minute comes down where it is.</summary>
        public const int MaxFlightTicks = 1_200;

        /// <summary>Launch speeds stay within ±100 m/s, so the arc's integer maths can't overflow.</summary>
        public const int MaxSpeedMmPerS = 100_000;

        /// <summary>Heights stay within ±10 km, like ground and field values.</summary>
        public const int MaxHeightMm = 10_000_000;

        /// <summary>
        /// Launches a soldier with a source's speeds and the seeded spread, and logs <see cref="CauseKind.SoldierLaunched"/>
        /// (cause: <paramref name="sourceEvent"/>, the link only). The spread is seeded by the source and its stable
        /// <paramref name="sourceKey"/> (see <see cref="SourceId"/>) and the soldier, never by the event's index or the
        /// contact geometry. <paramref name="heading"/> is the source's heading, or null for a source that throws any way (a
        /// collapse). <paramref name="surfaceMm"/> is the surface he stands on (<see cref="SurfaceMm"/>, or a deck's height).
        /// Returns the launch event, or <see cref="CauseLog.None"/> (no log, or he was already airborne or dead).
        /// </summary>
        public static int Launch(
            World world,
            LaunchRules rules,
            int unit,
            LaunchSourceRules source,
            int sourceKey,
            Angle? heading,
            int sourceEvent,
            int tick,
            ulong seed,
            int surfaceMm
        )
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
            if (rules == null || source == null)
            {
                throw new ArgumentNullException(rules == null ? nameof(rules) : nameof(source));
            }
            if (sourceKey < 0 || sourceKey > int.MaxValue / MaxSources)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceKey));
            }
            // A fixed draw order whatever the source, so the same (source, key, soldier) always gives the same throw.
            var rng = SimRandom.Stream(
                seed,
                LaunchSystemId * MaxSources + source.Index,
                sourceKey,
                unit
            );
            int offsetCd = rng.NextInt(-rules.SpreadHeadingCd, rules.SpreadHeadingCd + 1);
            int any = (int)rng.NextUInt(Angle.UnitsPerTurn);
            int speed = rng.NextInt(-rules.SpreadSpeedPermille, rules.SpreadSpeedPermille + 1);
            int spin = rng.NextInt(rules.SpinMinCdPerS, rules.SpinMaxCdPerS + 1);
            Angle direction = heading.HasValue
                ? heading.Value
                    + Angle.FromUnits((int)((long)offsetCd * Angle.UnitsPerTurn / 36_000))
                : Angle.FromUnits(any);
            int factor = 1000 + speed;
            return LaunchWith(
                world,
                unit,
                direction,
                (int)((long)source.HorizontalMmPerS * factor / 1000),
                (int)((long)source.VerticalMmPerS * factor / 1000),
                spin,
                sourceEvent,
                tick,
                surfaceMm,
                SourceId(source, sourceKey)
            );
        }

        /// <summary>
        /// Launches a soldier with exact speeds and no spread (<see cref="Launch"/> adds the spread; tests use this
        /// directly). Clears any hazard state: an airborne soldier is neither swept, stunned nor teetering.
        /// </summary>
        public static int LaunchWith(
            World world,
            int unit,
            Angle heading,
            int horizontalMmPerS,
            int verticalMmPerS,
            int spinCdPerS,
            int sourceEvent,
            int tick,
            int surfaceMm,
            int sourceId = 0
        )
        {
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
            if (!world.Units.IsAlive(unit) || (world.Flags[unit] & UnitFlags.Launched) != 0)
            {
                return CauseLog.None;
            }
            horizontalMmPerS = System.Math.Max(
                0,
                System.Math.Min(MaxSpeedMmPerS, horizontalMmPerS)
            );
            verticalMmPerS = System.Math.Max(1, System.Math.Min(MaxSpeedMmPerS, verticalMmPerS));
            surfaceMm = System.Math.Max(-MaxHeightMm, System.Math.Min(MaxHeightMm, surfaceMm));
            world.Flags[unit] =
                (world.Flags[unit] & ~(UnitFlags.Swept | UnitFlags.Stunned | UnitFlags.Teetering))
                | UnitFlags.Launched;
            world.HazardTimer[unit] = 0;
            // mm/s to metres per tick: × 2^16 / (1000 × ticks per second).
            world.Velocity[unit] = FixVec2.FromAngle(
                heading,
                Fix.FromRaw(
                    (long)horizontalMmPerS * Fix.OneRaw / (1000L * SimConstants.TicksPerSecond)
                )
            );
            var arc = world.Ballistic;
            arc.HeightMm[unit] = surfaceMm;
            arc.StartMm[unit] = surfaceMm;
            arc.VerticalMmPerS[unit] = verticalMmPerS;
            arc.Ticks[unit] = 0;
            arc.SpinCdPerS[unit] = spinCdPerS;
            arc.LaunchSurfaceMm[unit] = surfaceMm;
            var log = world.Causes;
            int launched = CauseLog.None;
            if (log != null)
            {
                log.UnitSwept[unit] = CauseLog.None;
                log.UnitPushed[unit] = CauseLog.None;
                launched = log.Add(
                    CauseKind.SoldierLaunched,
                    tick,
                    world.UnitRegiment[unit],
                    sourceId,
                    sourceEvent
                );
            }
            arc.LaunchEvent[unit] = launched;
            return launched;
        }

        /// <summary>The arc's height after <paramref name="ticks"/> ticks of flight, mm (closed form, exact on level ground).</summary>
        public static long HeightAfter(
            long startMm,
            long verticalMmPerS,
            long gravityMmPerS2,
            long ticks
        )
        {
            // z = start + vz × t − g × t² / 2 with t = ticks / 20: in one division by 2 × 20², rounded toward zero.
            long perSecondSq = 2L * SimConstants.TicksPerSecond * SimConstants.TicksPerSecond;
            return startMm
                + (
                    verticalMmPerS * ticks * 2 * SimConstants.TicksPerSecond
                    - gravityMmPerS2 * ticks * ticks
                ) / perSecondSq;
        }

        /// <summary>
        /// The surface a body comes down on in a cell, mm above the datum: a standing bridge or trestle deck (or the water
        /// over it), the water's surface on intact ice, a floating deck or open water, else the ground.
        /// </summary>
        public static int SurfaceMm(Terrain terrain, World world, int x, int y)
        {
            int ground = terrain.HeightMm(x, y);
            long depthMm = DepthUm(world, x, y) / 1000;
            int s = terrain.StandingSegment(terrain.Index(x, y));
            if (s >= 0)
            {
                var segment = terrain.Segment(s);
                if (!segment.Kind.IsIce && !segment.Kind.Floats)
                {
                    return (int)System.Math.Max(segment.DeckHeightMm, ground + depthMm);
                }
            }
            return (int)(ground + depthMm);
        }

        internal static long DepthUm(World world, int x, int y)
        {
            var fields = world.Fields;
            if (fields == null || fields.Rules.IndexOf(WaterModel.Depth) < 0)
            {
                return 0;
            }
            return fields.Get(WaterModel.Depth).Get(x, y);
        }
    }

    /// <summary>
    /// The ballistics-and-impacts step (spec S2 9.1 and 9.3): every launched body takes one arc step a tick and lands the
    /// first tick its height falls to or below the surface under it. What it lands on decides the outcome: ground, a deck
    /// or ice hurt and stun; shallow water stuns; deep water within the fringe of footing snaps him out, stunned; deeper
    /// water sweeps him (S1); a cliff below where he started kills; a hard blocker sends him back along his arc. Every
    /// landing knocks down the soldiers on their feet around it (never launched, never hurt, never chained). Each landing
    /// logs <see cref="CauseKind.SoldierLanded"/> linked to the launch, and each death <see cref="CauseKind.SoldierFell"/>.
    /// <para>Determinism: serial, in unit slot order; the arcs live in <see cref="World.Ballistic"/>.</para>
    /// </summary>
    public sealed class BallisticsSystem : ISimSystem
    {
        /// <summary>How far, in cells, a body thrown into a closed spot with no open cell along its arc looks for one.</summary>
        private const int OpenSearchCells = 8;

        private readonly Ruleset _rules;
        private readonly Navigation _navigation;
        private SpatialHash? _hash;

        public BallisticsSystem(Ruleset rules, Navigation navigation)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        }

        public TickStep Step => TickStep.BallisticsAndImpacts;

        public TickRate Rate => TickRate.EveryTick;

        public void Run(TickContext context)
        {
            var world = context.World;
            var arc = world.Ballistic;
            var terrain = _navigation.Terrain;
            int gravity = _rules.Launches.GravityMmPerS2;
            Fix maxX = _navigation.Map.Width - Fix.FromRaw(1);
            Fix maxY = _navigation.Map.Depth - Fix.FromRaw(1);
            bool hashBuilt = false;
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (!world.Units.IsAlive(u) || (world.Flags[u] & UnitFlags.Launched) == 0)
                {
                    continue;
                }
                int n = ++arc.Ticks[u];
                // Thrown off the map: clamped to its edge (S2 9.3), never fleeing.
                var p = world.Position[u] + world.Velocity[u];
                world.Position[u] = new FixVec2(
                    Fix.Clamp(p.X, Fix.Zero, maxX),
                    Fix.Clamp(p.Y, Fix.Zero, maxY)
                );
                long z = Ballistics.HeightAfter(arc.StartMm[u], arc.VerticalMmPerS[u], gravity, n);
                z = System.Math.Max(-2L * Ballistics.MaxHeightMm, z);
                arc.HeightMm[u] = (int)z;
                terrain.CellOf(world.Position[u], out int x, out int y);
                if (z > Ballistics.SurfaceMm(terrain, world, x, y) && n < Ballistics.MaxFlightTicks)
                {
                    continue;
                }
                if (!hashBuilt)
                {
                    // Only the soldiers on their feet are knocked down, and none of them moves in this step.
                    _hash ??= new SpatialHash(
                        _navigation.Map.Width,
                        _navigation.Map.Depth,
                        Fix.FromInt(2),
                        world.Units.Capacity
                    );
                    _hash.Build(world);
                    hashBuilt = true;
                }
                // A death despawns, which the hash must see before the next query.
                hashBuilt = !Land(context, u);
            }
        }

        /// <summary>Lands the body; true when the landing killed him (his slot is freed).</summary>
        private bool Land(TickContext context, int u)
        {
            var world = context.World;
            var arc = world.Ballistic;
            var rules = _rules.Launches;
            var terrain = _navigation.Terrain;
            var cls = _rules.UnitTypes[world.UnitType[u]].MovementClass;
            var p = world.Position[u];
            terrain.CellOf(p, out int x, out int y);
            int cell = terrain.Index(x, y);
            bool blocked = HardBlocked(cell, cls);
            if (blocked)
            {
                // Back along the arc, a tick at a time, to the first open cell; if the whole arc is closed (he was thrown
                // straight up beside a wall), the nearest open cell around.
                var v = world.Velocity[u];
                for (int k = 0; k < arc.Ticks[u] && HardBlocked(cell, cls); k++)
                {
                    p = Clamp(p - v);
                    terrain.CellOf(p, out x, out y);
                    cell = terrain.Index(x, y);
                }
                if (
                    HardBlocked(cell, cls)
                    && Nearest(
                        world,
                        cls,
                        p,
                        x,
                        y,
                        OpenSearchCells * (long)terrain.CellSizeMm,
                        false,
                        out FixVec2 open
                    )
                )
                {
                    p = open;
                    terrain.CellOf(p, out x, out y);
                }
            }
            // Where he ends up lands by the normal rules (water, drops); the blocker's blow counts only on firm ground.
            var outcome = Surface(world, u, cls, x, y, ref p, out LandingRules effect);
            if (blocked && (outcome == LandingOutcome.Ground || outcome == LandingOutcome.Ice))
            {
                outcome = LandingOutcome.Blocked;
                effect = rules.HardBlocker;
            }
            terrain.CellOf(p, out x, out y);
            cell = terrain.Index(x, y);

            int launch = arc.LaunchEvent[u];
            arc.Clear(u);
            world.Flags[u] &= ~UnitFlags.Launched;
            world.Position[u] = p;
            world.Velocity[u] = FixVec2.Zero;
            world.HazardTimer[u] = 0;
            if (outcome != LandingOutcome.Fell)
            {
                world.Health[u] -= effect.Damage;
                if (world.Health[u] <= 0)
                {
                    outcome = LandingOutcome.Killed;
                }
            }
            var log = world.Causes;
            int landed =
                log?.Add(
                    CauseKind.SoldierLanded,
                    context.Tick,
                    world.UnitRegiment[u],
                    Landing.Detail(cell, outcome),
                    launch
                )
                ?? CauseLog.None;
            KnockDown(world, u, p);

            if (outcome == LandingOutcome.Fell || outcome == LandingOutcome.Killed)
            {
                log?.Add(CauseKind.SoldierFell, context.Tick, world.UnitRegiment[u], u, landed);
                if (outcome == LandingOutcome.Fell)
                {
                    world.Counters.Fell++;
                }
                else
                {
                    world.Counters.LandingKilled++;
                }
                world.DespawnUnit(world.Units.IdOf(u));
                return true;
            }
            if (outcome == LandingOutcome.Swept)
            {
                world.Flags[u] |= UnitFlags.Swept;
                world.HazardTimer[u] = _rules.Movement(cls).Water.DrowningTicks;
                world.Counters.Swept++;
                if (log != null)
                {
                    log.UnitSwept[u] = landed; // drowned → landed → launched → its source
                }
                return false;
            }
            world.Flags[u] |= UnitFlags.Stunned;
            world.HazardTimer[u] = effect.StunTicks;
            return false;
        }

        /// <summary>
        /// What he lands on in cell (x, y): water that sweeps his class first (it breaks the fall: the fringe or swept), then
        /// the drop from the surface he was launched from to the surface here (both <see cref="Ballistics.SurfaceMm"/>:
        /// a deck, the water's surface on ice, a floating deck or open water, else the ground), then intact ice, shallow
        /// water or ground. Moves <paramref name="p"/> for a fringe landing.
        /// </summary>
        private LandingOutcome Surface(
            World world,
            int u,
            MovementClass cls,
            int x,
            int y,
            ref FixVec2 p,
            out LandingRules effect
        )
        {
            var rules = _rules.Launches;
            var terrain = _navigation.Terrain;
            int cell = terrain.Index(x, y);
            var water = _rules.Fields.Water;
            long depthUm = terrain.EffectiveDepthUm(cell, Ballistics.DepthUm(world, x, y));
            bool wet = water != null && depthUm >= water.DryDepthUm;
            if (wet)
            {
                long depthMm = depthUm / 1000;
                WaterModel.Velocity(
                    world.Fields!,
                    terrain.CellSizeMm,
                    x,
                    y,
                    out int east,
                    out int north
                );
                long flow = IntMath.Sqrt((ulong)((long)east * east + (long)north * north));
                if (_rules.Movement(cls).Water.Sweeps(depthMm, flow))
                {
                    if (Nearest(world, cls, p, x, y, rules.FringeMm, true, out FixVec2 footing))
                    {
                        p = footing;
                        effect = rules.Fringe;
                        return LandingOutcome.Fringe;
                    }
                    effect = default;
                    return LandingOutcome.Swept;
                }
            }
            long drop =
                (long)world.Ballistic.LaunchSurfaceMm[u]
                - Ballistics.SurfaceMm(terrain, world, x, y);
            if (drop >= _rules.Hazards.LethalDropMm)
            {
                effect = default;
                return LandingOutcome.Fell;
            }
            if (drop >= rules.HardFallDropMm)
            {
                effect = rules.HardFall;
                return LandingOutcome.HardFall;
            }
            if (terrain.IsIce(cell))
            {
                effect = rules.Ice;
                return LandingOutcome.Ice;
            }
            if (wet)
            {
                effect = rules.ShallowWater;
                return LandingOutcome.ShallowWater;
            }
            effect = rules.Ground;
            return LandingOutcome.Ground;
        }

        /// <summary>
        /// The nearest cell whose nearest point is within <paramref name="within"/> mm of <paramref name="p"/> and that isn't
        /// a hard blocker; with <paramref name="footing"/> also one his class can stand in (its own steering may enter it and
        /// the water there doesn't sweep him: the fringe). Ties to the lower cell index. Its centre, in
        /// <paramref name="centre"/>.
        /// </summary>
        private bool Nearest(
            World world,
            MovementClass cls,
            FixVec2 p,
            int cx,
            int cy,
            long within,
            bool footing,
            out FixVec2 centre
        )
        {
            var terrain = _navigation.Terrain;
            int size = terrain.CellSizeMm;
            int reach = (int)(within / size) + 1;
            var costs = _navigation.Costs.FreshArray(cls);
            long px = p.X.Raw * 1000 >> Fix.FractionalBits;
            long py = p.Y.Raw * 1000 >> Fix.FractionalBits;
            long best = long.MaxValue;
            int bestCell = -1;
            centre = default;
            for (int y = cy - reach; y <= cy + reach; y++)
            {
                for (int x = cx - reach; x <= cx + reach; x++)
                {
                    if (!terrain.InBounds(x, y))
                    {
                        continue;
                    }
                    int i = terrain.Index(x, y);
                    if (
                        HardBlocked(i, cls)
                        || (
                            footing
                            && (costs[i] == CostFields.Impassable || !Footing(world, cls, x, y))
                        )
                    )
                    {
                        continue;
                    }
                    long dx = System.Math.Max(
                        0,
                        System.Math.Max((long)x * size - px, px - ((long)x + 1) * size)
                    );
                    long dy = System.Math.Max(
                        0,
                        System.Math.Max((long)y * size - py, py - ((long)y + 1) * size)
                    );
                    long d = dx * dx + dy * dy;
                    if (d <= within * within && (d < best || (d == best && i < bestCell)))
                    {
                        best = d;
                        bestCell = i;
                        centre = terrain.CellCentre(x, y);
                    }
                }
            }
            return bestCell >= 0;
        }

        /// <summary>
        /// Whether the class could stand in the cell's water now: shallow enough to enter and not sweeping (read from the
        /// water itself, since the cost fields' speed codes refresh only every few ticks).
        /// </summary>
        private bool Footing(World world, MovementClass cls, int x, int y)
        {
            var terrain = _navigation.Terrain;
            var water = _rules.Fields.Water;
            long depthUm = terrain.EffectiveDepthUm(
                terrain.Index(x, y),
                Ballistics.DepthUm(world, x, y)
            );
            if (water == null || depthUm < water.DryDepthUm)
            {
                return true;
            }
            long depthMm = depthUm / 1000;
            var response = _rules.Movement(cls).Water;
            if (depthMm > response.MaxEnterDepthMm)
            {
                return false;
            }
            WaterModel.Velocity(
                world.Fields!,
                terrain.CellSizeMm,
                x,
                y,
                out int east,
                out int north
            );
            long flow = IntMath.Sqrt((ulong)((long)east * east + (long)north * north));
            return !response.Sweeps(depthMm, flow);
        }

        /// <summary>A cell closed to the class whatever the water: a boulder, or a line piece (a wall) it can't cross.</summary>
        private bool HardBlocked(int cell, MovementClass cls)
        {
            var terrain = _navigation.Terrain;
            return terrain.IsBlocked(cell)
                || (
                    terrain.HasPieceSpeed
                    && FieldSpeed.Code(terrain.PieceCodes(cell), cls) <= FieldSpeed.Closed
                );
        }

        /// <summary>
        /// Knocks down every soldier on his feet within the knock-down radius (S2 9.3): stunned for the stagger, no damage,
        /// never launched; already-down soldiers (stunned, swept, airborne) are left alone, so knock-downs never chain.
        /// </summary>
        private void KnockDown(World world, int u, FixVec2 p)
        {
            var rules = _rules.Launches;
            if (rules.KnockDownRadiusMm <= 0 || _hash == null)
            {
                return;
            }
            Fix radius = Fix.FromRatio(rules.KnockDownRadiusMm, 1000);
            Span<int> near = stackalloc int[128];
            int found = System.Math.Min(_hash.QueryRadius(world, p, radius, near), near.Length);
            long r = radius.Raw;
            for (int k = 0; k < found; k++)
            {
                int j = near[k];
                if (
                    j == u
                    || !world.Units.IsAlive(j)
                    || (world.Flags[j] & (UnitFlags.Launched | UnitFlags.Swept | UnitFlags.Stunned))
                        != 0
                )
                {
                    continue;
                }
                long dx = world.Position[j].X.Raw - p.X.Raw;
                long dy = world.Position[j].Y.Raw - p.Y.Raw;
                if (dx * dx + dy * dy > r * r)
                {
                    continue;
                }
                world.Flags[j] = (world.Flags[j] & ~UnitFlags.Teetering) | UnitFlags.Stunned;
                world.HazardTimer[j] = rules.KnockDownTicks;
            }
        }

        private FixVec2 Clamp(FixVec2 p) =>
            new FixVec2(
                Fix.Clamp(p.X, Fix.Zero, _navigation.Map.Width - Fix.FromRaw(1)),
                Fix.Clamp(p.Y, Fix.Zero, _navigation.Map.Depth - Fix.FromRaw(1))
            );
    }
}
