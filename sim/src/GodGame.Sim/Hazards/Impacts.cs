using System;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// Bodies running into soldiers (spec S2 10.3, 11.4; brief 2026-10-11), in the ballistics-and-impacts step after the
    /// flights: a stampeding animal at speed in body contact with a soldier on his feet (either side; not an engine, not
    /// another animal) launches him (stampede source, the herd's heading) or else tramples him (damage, armour ignored,
    /// and a stun). One victim per animal per cooldown, after which it runs slower for a moment. A victim is no longer
    /// on his feet, so nobody is hit twice in a row, and a landing only knocks down (S2 9.3): nothing chains.
    /// <para>Determinism: serial, in unit slot order, from the positions movement left; each roll is its own stream
    /// (animal, tick).</para>
    /// </summary>
    public sealed class Impacts
    {
        /// <summary>A stampede contact's roll (<see cref="SimRandom.Stream"/>: entity = the animal's slot).</summary>
        public const int StampedeSystemId = 6_101;

        private const int MaxNeighbours = 64;

        private readonly Ruleset _rules;
        private readonly Navigation _navigation;
        private readonly long[] _typeRadiusRaw;
        private readonly long _maxRadiusRaw;
        private SpatialHash? _hash;
        private int[] _herdOf = new int[0];
        private readonly Charges _charges;
        private readonly PendingDeaths _deaths = new PendingDeaths();

        public Impacts(Ruleset rules, Navigation navigation)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            _charges = new Charges(rules, navigation);
            _typeRadiusRaw = new long[rules.UnitTypes.Count];
            for (int t = 0; t < rules.UnitTypes.Count; t++)
            {
                _typeRadiusRaw[t] = Fix.FromRatio(rules.UnitTypes[t].RadiusMm, 1000).Raw;
                _maxRadiusRaw = System.Math.Max(_maxRadiusRaw, _typeRadiusRaw[t]);
            }
        }

        public void Run(TickContext context)
        {
            var world = context.World;
            bool stampede = AnyStampede(world);
            bool charging = _charges.Rules != null && Charges.Any(world);
            if (!stampede && !charging)
            {
                return;
            }
            var hash = Hash(world);
            hash.Build(world);
            if (stampede)
            {
                Stampedes(context);
            }
            if (charging)
            {
                // The hash still lists the stampede's victims: none of them is on his feet, so no rider takes them.
                _charges.Contact(context, hash, _deaths);
            }
            // The fallen leave only now, in slot order, so neither loop queried a hash a despawn had outdated.
            _deaths.Flush(world, context.Tick);
        }

        /// <summary>Which regiments are stampeding herds (into <see cref="_herdOf"/>), and whether any is.</summary>
        private bool AnyStampede(World world)
        {
            var herds = world.Herds;
            if (_rules.Herds == null || herds.Count == 0)
            {
                return false;
            }
            bool any = false;
            if (_herdOf.Length != world.Regiments.Capacity)
            {
                _herdOf = new int[world.Regiments.Capacity];
            }
            for (int r = 0; r < _herdOf.Length; r++)
            {
                _herdOf[r] = -1;
            }
            for (int h = 0; h < herds.Count; h++)
            {
                if (herds.Mode[h] == HerdMode.Stampede)
                {
                    _herdOf[herds.Regiment[h]] = h;
                    any = true;
                }
            }
            return any;
        }

        private void Stampedes(TickContext context)
        {
            var world = context.World;
            var herds = world.Herds;
            var rules = _rules.Herds!;
            var source = _rules.Launches.Source("stampede");
            var terrain = _navigation.Terrain;
            var log = world.Causes;
            int tick = context.Tick;
            long minSpeed = Fix.FromRatio(
                rules.MinImpactMmPerS,
                1000L * SimConstants.TicksPerSecond
            ).Raw;
            Span<int> near = stackalloc int[MaxNeighbours];
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (!world.Units.IsAlive(u))
                {
                    continue;
                }
                int h = _herdOf[world.UnitRegiment[u]];
                if (h < 0 || !OnFeet(world.Flags[u]))
                {
                    continue;
                }
                var v = world.Velocity[u];
                if (v.X.Raw * v.X.Raw + v.Y.Raw * v.Y.Raw < minSpeed * minSpeed)
                {
                    continue;
                }
                var hits = herds.LastHit[h]!;
                int slot = world.FormationSlot[u];
                if (
                    slot < 0
                    || slot >= hits.Length
                    || tick - hits[slot] < rules.VictimCooldownTicks
                )
                {
                    continue;
                }
                var p = world.Position[u];
                long radius = _typeRadiusRaw[world.UnitType[u]];
                int found = _hash!.QueryRadius(world, p, Fix.FromRaw(radius + _maxRadiusRaw), near);
                int count = System.Math.Min(found, MaxNeighbours);
                int victim = -1;
                long best = long.MaxValue;
                for (int k = 0; k < count; k++)
                {
                    int j = near[k];
                    if (
                        j == u
                        || !world.Units.IsAlive(j)
                        || !OnFeet(world.Flags[j])
                        || world.Health[j] <= 0
                        || _rules.UnitTypes[world.UnitType[j]].IsAnimal
                        || EngineCrews.IsEngine(world, _rules, j)
                    )
                    {
                        continue;
                    }
                    long dx = world.Position[j].X.Raw - p.X.Raw;
                    long dy = world.Position[j].Y.Raw - p.Y.Raw;
                    long contact = radius + _typeRadiusRaw[world.UnitType[j]];
                    long dSq = dx * dx + dy * dy;
                    if (dSq < contact * contact && (dSq < best || (dSq == best && j < victim)))
                    {
                        best = dSq;
                        victim = j;
                    }
                }
                if (victim < 0)
                {
                    continue;
                }
                hits[slot] = tick;
                var rng = SimRandom.Stream(context.Seed, StampedeSystemId, u, tick);
                if (source != null && rng.NextUInt(1000) < (uint)rules.LaunchPermille)
                {
                    terrain.CellOf(world.Position[victim], out int vx, out int vy);
                    Ballistics.Launch(
                        world,
                        _rules.Launches,
                        victim,
                        source,
                        h,
                        Angle.FromUnits(herds.Heading[h]),
                        herds.Event[h],
                        tick,
                        context.Seed,
                        Ballistics.SurfaceMm(terrain, world, vx, vy)
                    );
                    continue;
                }
                Trample(
                    world,
                    _deaths,
                    victim,
                    rules.TrampleDamage,
                    rules.TrampleStunTicks,
                    herds.Event[h],
                    tick
                );
            }
        }

        /// <summary>
        /// Trampled (brief 2026-10-11): health off, armour ignored, and stunned; at 0 he dies (cause trampled, linked to
        /// <paramref name="cause"/>, the stampede or the charge), leaving with <paramref name="deaths"/> when the step ends.
        /// </summary>
        public static void Trample(
            World world,
            PendingDeaths deaths,
            int victim,
            int damage,
            int stunTicks,
            int cause,
            int tick
        )
        {
            world.Health[victim] -= damage;
            if (world.Health[victim] <= 0)
            {
                deaths.Add(victim, CauseKind.SoldierTrampled, victim, cause);
                return;
            }
            world.Flags[victim] = (world.Flags[victim] & ~UnitFlags.Teetering) | UnitFlags.Stunned;
            world.HazardTimer[victim] = System.Math.Max(world.HazardTimer[victim], stunTicks);
        }

        /// <summary>On its feet: not swept, stunned, launched or teetering.</summary>
        public static bool OnFeet(UnitFlags flags) =>
            (
                flags
                & (UnitFlags.Swept | UnitFlags.Stunned | UnitFlags.Launched | UnitFlags.Teetering)
            ) == 0;

        private SpatialHash Hash(World world) =>
            _hash ??= new SpatialHash(
                _navigation.Map.Width,
                _navigation.Map.Depth,
                MovementSystem.HashCellSize,
                world.Units.Capacity
            );
    }

    /// <summary>
    /// Deaths an impacts pass decides, applied when it ends (<see cref="Flush"/>): logged and despawned in ascending unit
    /// slot order, so the pass reads one unchanged spatial hash and the order of events never depends on which loop
    /// decided them. A soldier is added at most once: one at 0 health is no one's target.
    /// </summary>
    public sealed class PendingDeaths
    {
        private int[] _unit = new int[16];
        private CauseKind[] _kind = new CauseKind[16];
        private int[] _detail = new int[16];
        private int[] _cause = new int[16];
        private int _count;

        public void Add(int unit, CauseKind kind, int detail, int cause)
        {
            if (_count == _unit.Length)
            {
                Array.Resize(ref _unit, _count * 2);
                Array.Resize(ref _kind, _count * 2);
                Array.Resize(ref _detail, _count * 2);
                Array.Resize(ref _cause, _count * 2);
            }
            // Insertion in slot order (slots are unique, so the order is total).
            int at = _count;
            while (at > 0 && _unit[at - 1] > unit)
            {
                _unit[at] = _unit[at - 1];
                _kind[at] = _kind[at - 1];
                _detail[at] = _detail[at - 1];
                _cause[at] = _cause[at - 1];
                at--;
            }
            _unit[at] = unit;
            _kind[at] = kind;
            _detail[at] = detail;
            _cause[at] = cause;
            _count++;
        }

        public void Flush(World world, int tick)
        {
            for (int k = 0; k < _count; k++)
            {
                int u = _unit[k];
                if (!world.Units.IsAlive(u))
                {
                    continue;
                }
                world.Causes?.Add(_kind[k], tick, world.UnitRegiment[u], _detail[k], _cause[k]);
                world.DespawnUnit(world.Units.IdOf(u));
                world.Counters.Killed++;
            }
            _count = 0;
        }
    }
}
