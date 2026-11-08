using System;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>Volley range with wind along the shot and height (spec S2, section 6.3).</summary>
    public static class Volleys
    {
        /// <summary>
        /// Calm range × the clamped wind factor, plus the capped height gain, in mm. Along-shot wind is the map's
        /// prevailing wind projected on the shot (positive = tailwind; the Wind shift plume joins with M5 item 7); a
        /// regiment's height is its centroid's ground (S2 says the officer's cell: the centroid keeps a regiment
        /// whose officer fell readable).
        /// </summary>
        public static long RangeMm(
            ProjectileRules p,
            MapData map,
            Terrain terrain,
            FixVec2 from,
            FixVec2 to
        )
        {
            long dx = RegimentSummary.Mm(to.X.Raw - from.X.Raw);
            long dy = RegimentSummary.Mm(to.Y.Raw - from.Y.Raw);
            long length = RegimentSummary.Length(dx, dy);
            long along =
                length == 0
                    ? 0
                    : ((long)map.WindXMmPerS * dx + (long)map.WindYMmPerS * dy) / length;
            long factor = 1000 + along * p.WindPermillePerMPerS / 1000;
            factor = System.Math.Max(
                p.WindClampMinPermille,
                System.Math.Min(p.WindClampMaxPermille, factor)
            );
            long height =
                (long)p.HeightGainPerMm * (terrain.HeightMmAt(from) - terrain.HeightMmAt(to));
            height = System.Math.Max(
                -p.HeightGainCapMm,
                System.Math.Min(p.HeightGainCapMm, height)
            );
            return System.Math.Max(0, p.RangeMm * factor / 1000 + height);
        }
    }

    /// <summary>
    /// The combat step (spec S2, sections 5 and 6). Melee per soldier through the spatial hash: a soldier on its feet
    /// swings at the nearest enemy within body contact plus its reach (ties: the lower slot), once per swing interval
    /// at a seeded phase, and hits on the seeded stream (combat, soldier, tick). Volleys per regiment, never per arrow:
    /// a ranged regiment with a Shoot order, ammunition and a loaded volley shoots with every archer on its feet and
    /// not in melee; the hits land after the flight time on soldiers of the target within the impact radius of the
    /// aim point, picked on the seeded stream, and hits with nobody there miss.
    /// <para>
    /// Determinism: the swing pass runs in parallel and writes only its own soldier's strike; damage is applied
    /// serially in striker slot order, then volleys land in shooter slot order, then the dead are despawned in slot
    /// order, each with the regiment whose blow killed it (the first to take its health to 0) as the cause's source.
    /// </para>
    /// </summary>
    public sealed class CombatSystem : ISimSystem
    {
        public const int SwingSystemId = 7_001;
        public const int VolleySystemId = 7_002;
        public const int VolleyLandSystemId = 7_003;
        public const int ChunkSize = 512;

        /// <summary>The swing search keeps at most this many neighbours (a fixed buffer; contact range is a metre or two).</summary>
        public const int MaxNeighbours = 256;

        private readonly Ruleset _rules;
        private readonly MapData _map;
        private readonly Terrain _terrain;
        private readonly RegimentSummary _summary = new RegimentSummary();
        private readonly long[] _typeRadiusRaw;
        private readonly long[] _typeReachRaw;
        private readonly int[] _typeDamage;
        private readonly int[] _typeHit;
        private readonly int[] _typeInterval;
        private readonly long _maxRadiusRaw;
        private readonly long _maxReachRaw;

        // Scratch, overwritten every run before it is read.
        private SpatialHash? _hash;
        private int[] _strikeTarget = new int[0];
        private int[] _strikeDamage = new int[0];
        private bool[] _engaged = new bool[0];
        private int[] _killer = new int[0];
        private int[] _shooters = new int[0];
        private int[] _candidates = new int[0];
        private bool[] _near = new bool[0];

        /// <summary>
        /// Regiments whose outline is further than this from every enemy can't have a soldier in reach (two of the
        /// largest bodies plus the longest reach, with a metre to spare), so their soldiers skip the swing search.
        /// </summary>
        private readonly long _contactMm;

        public CombatSystem(Ruleset rules, MapData map, Terrain terrain)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _terrain = terrain ?? throw new ArgumentNullException(nameof(terrain));
            var types = rules.UnitTypes;
            _typeRadiusRaw = new long[types.Count];
            _typeReachRaw = new long[types.Count];
            _typeDamage = new int[types.Count];
            _typeHit = new int[types.Count];
            _typeInterval = new int[types.Count];
            for (int t = 0; t < types.Count; t++)
            {
                _typeRadiusRaw[t] = Fix.FromRatio(types[t].RadiusMm, 1000).Raw;
                _maxRadiusRaw = System.Math.Max(_maxRadiusRaw, _typeRadiusRaw[t]);
                var melee = types[t].Melee;
                if (melee == null)
                {
                    _typeReachRaw[t] = -1; // doesn't swing
                    continue;
                }
                _typeReachRaw[t] = Fix.FromRatio(melee.ReachMm, 1000).Raw;
                _typeDamage[t] = melee.Damage;
                _typeHit[t] = melee.HitPermille;
                _typeInterval[t] = melee.IntervalTicks;
                _maxReachRaw = System.Math.Max(_maxReachRaw, _typeReachRaw[t]);
            }
            _contactMm = RegimentSummary.Mm(2 * _maxRadiusRaw + _maxReachRaw) + 1_000;
        }

        public TickStep Step => TickStep.Combat;

        public TickRate Rate => TickRate.EveryTick;

        public void Run(TickContext context)
        {
            var world = context.World;
            int capacity = world.Units.Capacity;
            if (_strikeTarget.Length < capacity)
            {
                _strikeTarget = new int[capacity];
                _strikeDamage = new int[capacity];
                _engaged = new bool[capacity];
                _killer = new int[capacity];
                _candidates = new int[capacity];
            }
            if (_hash == null)
            {
                _hash = new SpatialHash(
                    _map.Width,
                    _map.Depth,
                    MovementSystem.HashCellSize,
                    capacity
                );
            }
            if (_near.Length != world.Regiments.Capacity)
            {
                _near = new bool[world.Regiments.Capacity];
            }
            int highWater = world.Units.HighWater;
            _summary.Gather(world);
            bool contact = _summary.NearEnemy(world, _contactMm, _near);
            bool volleys = AnyVolley(world, context.Tick);
            if (!contact && !volleys)
            {
                return; // nobody in reach and no volley to land or shoot: nothing can be hurt this tick
            }
            _hash.Build(world);
            if (contact)
            {
                context.Runner.For(
                    highWater,
                    ChunkSize,
                    new SwingKernel(this, world, _hash, context.Seed, context.Tick)
                );
            }
            else
            {
                // No melee anywhere: no strikes, and every archer is free to shoot.
                for (int u = 0; u < highWater; u++)
                {
                    _strikeTarget[u] = -1;
                    _engaged[u] = false;
                }
            }

            for (int u = 0; u < highWater; u++)
            {
                _killer[u] = -1;
            }
            for (int u = 0; u < highWater; u++)
            {
                int victim = _strikeTarget[u];
                if (victim >= 0 && world.Units.IsAlive(u))
                {
                    Hurt(world, victim, _strikeDamage[u], world.UnitRegiment[u]);
                }
            }
            Volley(world, context);
            var log = world.Causes;
            for (int u = 0; u < highWater; u++)
            {
                if (_killer[u] < 0 || !world.Units.IsAlive(u))
                {
                    continue;
                }
                int r = world.UnitRegiment[u];
                log?.Add(CauseKind.SoldierKilled, context.Tick, r, _killer[u], CauseLog.None);
                world.DespawnUnit(world.Units.IdOf(u));
                world.Counters.Killed++;
            }
        }

        /// <summary>Whether a volley lands this tick or a regiment is ready to shoot.</summary>
        private bool AnyVolley(World world, int tick)
        {
            var army = world.Army;
            for (int r = 0; r < world.Regiments.HighWater; r++)
            {
                if (
                    world.Regiments.IsAlive(r)
                    && ((army.VolleyLands[r] == tick && tick != 0) || Ready(world, r, tick))
                )
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Takes health off a soldier; the regiment whose blow takes it to 0 is its killer.</summary>
        private void Hurt(World world, int victim, int damage, int byRegiment)
        {
            if (damage <= 0 || world.Health[victim] <= 0)
            {
                return;
            }
            world.Health[victim] -= damage;
            if (world.Health[victim] <= 0)
            {
                _killer[victim] = byRegiment;
            }
        }

        /// <summary>Volleys land (shooter slot order), then loaded regiments shoot (slot order).</summary>
        private void Volley(World world, TickContext context)
        {
            var army = world.Army;
            int tick = context.Tick;
            bool landing = false;
            bool shooting = false;
            for (int r = 0; r < world.Regiments.HighWater; r++)
            {
                if (!world.Regiments.IsAlive(r))
                {
                    continue;
                }
                landing |= army.VolleyLands[r] == tick && army.VolleyLands[r] != 0;
                shooting |= Ready(world, r, tick);
            }
            if (!landing && !shooting)
            {
                return;
            }
            _summary.Gather(world);
            for (int r = 0; r < world.Regiments.HighWater; r++)
            {
                if (
                    world.Regiments.IsAlive(r)
                    && army.VolleyLands[r] == tick
                    && army.VolleyLands[r] != 0
                )
                {
                    Land(world, r, context);
                }
            }
            if (!shooting)
            {
                return;
            }
            if (_shooters.Length < world.Regiments.Capacity)
            {
                _shooters = new int[world.Regiments.Capacity];
            }
            Array.Clear(_shooters, 0, _shooters.Length);
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (
                    world.Units.IsAlive(u)
                    && !_engaged[u]
                    && OnFeet(world.Flags[u])
                    && world.Health[u] > 0
                )
                {
                    _shooters[world.UnitRegiment[u]]++;
                }
            }
            for (int r = 0; r < world.Regiments.HighWater; r++)
            {
                if (Ready(world, r, tick))
                {
                    Shoot(world, r, context);
                }
            }
        }

        private bool Ready(World world, int r, int tick)
        {
            var army = world.Army;
            if (
                army.Order[r] != RegimentOrder.Shoot
                || army.Ammo[r] <= 0
                || army.VolleyLands[r] != 0
            )
            {
                return false;
            }
            int target = army.Target[r];
            return tick >= army.ReloadReady[r]
                && target >= 0
                && world.Regiments.IsAlive(target)
                && world.RegimentAlive[target] > 0
                && _rules.UnitTypes[world.RegimentUnitType[r]].Projectile != null;
        }

        private void Shoot(World world, int r, TickContext context)
        {
            var army = world.Army;
            var p = _rules.UnitTypes[world.RegimentUnitType[r]].Projectile!;
            int target = army.Target[r];
            int shooters = _shooters[r];
            if (shooters == 0 || _summary.Count[target] == 0)
            {
                return;
            }
            var from = _summary.Centroid[r];
            long range = Volleys.RangeMm(p, _map, _terrain, from, _summary.Centroid[target]);
            long gap = _summary.GapFrom(from, target);
            if (range == 0 || gap > range)
            {
                return; // out of reach this moment: wait for the next tick rather than waste the volley
            }
            // Flight time scales with the distance to the target's centre; the aim leads it by its velocity.
            long toCentre = RegimentSummary.Mm((_summary.Centroid[target] - from).Length.Raw);
            // Clamped, so a long height-boosted shot can't push the landing tick past int.
            int flight = (int)
                System.Math.Max(
                    1,
                    System.Math.Min(
                        4L * p.FlightTicksAtRange,
                        (long)p.FlightTicksAtRange * toCentre / p.RangeMm
                    )
                );
            var aim = _summary.Centroid[target] + _summary.MeanVelocity[target] * flight;
            // Hit chance: near up to the close share of range, then linear down to far at full range.
            long near = range * _rules.Combat.CloseRangePermille / 1000;
            long chance =
                gap <= near || range <= near
                    ? p.HitNearPermille
                    : p.HitNearPermille
                        - (p.HitNearPermille - p.HitFarPermille) * (gap - near) / (range - near);
            // × the target's order factor: close order 1,000‰ (open order arrives with M5 item 6).
            long product = shooters * chance;
            int hits = (int)(product / 1000);
            var rng = SimRandom.Stream(context.Seed, VolleySystemId, r, context.Tick);
            if (rng.NextUInt(1000) < (uint)(product % 1000))
            {
                hits++;
            }
            army.Ammo[r]--;
            army.ReloadReady[r] = context.Tick + p.ReloadTicks;
            army.VolleyLands[r] = context.Tick + flight;
            army.VolleyTarget[r] = target;
            army.VolleyAim[r] = aim;
            army.VolleyHits[r] = hits;
        }

        private void Land(World world, int r, TickContext context)
        {
            var army = world.Army;
            int target = army.VolleyTarget[r];
            int hits = army.VolleyHits[r];
            var aim = army.VolleyAim[r];
            army.VolleyLands[r] = 0;
            army.VolleyTarget[r] = -1;
            army.VolleyAim[r] = FixVec2.Zero;
            army.VolleyHits[r] = 0;
            var p = _rules.UnitTypes[world.RegimentUnitType[r]].Projectile;
            if (p == null || target < 0 || hits == 0)
            {
                return;
            }
            int found = _hash!.QueryRadius(
                world,
                aim,
                Fix.FromRatio(_rules.Combat.ImpactRadiusMm, 1000),
                _candidates
            );
            int count = 0;
            for (int k = 0; k < System.Math.Min(found, _candidates.Length); k++)
            {
                int u = _candidates[k];
                if (world.UnitRegiment[u] == target && world.Health[u] > 0)
                {
                    _candidates[count++] = u;
                }
            }
            if (count == 0)
            {
                return; // nobody there: the volley misses
            }
            var rng = SimRandom.Stream(context.Seed, VolleyLandSystemId, r, context.Tick);
            for (int h = 0; h < hits; h++)
            {
                int u = _candidates[rng.NextUInt((uint)count)];
                var taken = _rules.UnitTypes[world.UnitType[u]].ArrowTakenPermille;
                Hurt(world, u, (int)((long)p.Damage * taken / 1000), r);
            }
        }

        /// <summary>On its feet: not swept, stunned, launched or teetering, and not running away.</summary>
        private static bool OnFeet(UnitFlags flags) =>
            (
                flags
                & (
                    UnitFlags.Swept
                    | UnitFlags.Stunned
                    | UnitFlags.Launched
                    | UnitFlags.Teetering
                    | UnitFlags.Routing
                )
            ) == 0;

        /// <summary>A fixed per-slot phase in the swing interval, from the seed (so a regiment doesn't swing in step).</summary>
        private static int Phase(int u, ulong seed, int interval)
        {
            uint h = unchecked((uint)u * 2654435761u ^ (uint)seed ^ (uint)(seed >> 32));
            h ^= h >> 15;
            h = unchecked(h * 2246822519u);
            h ^= h >> 13;
            return (int)(h % (uint)interval);
        }

        private readonly struct SwingKernel : IChunkKernel
        {
            private readonly CombatSystem _s;
            private readonly World _world;
            private readonly SpatialHash _hash;
            private readonly ulong _seed;
            private readonly int _tick;

            public SwingKernel(CombatSystem s, World world, SpatialHash hash, ulong seed, int tick)
            {
                _s = s;
                _world = world;
                _hash = hash;
                _seed = seed;
                _tick = tick;
            }

            public void Execute(int start, int end)
            {
                Span<int> neighbours = stackalloc int[MaxNeighbours];
                var w = _world;
                for (int u = start; u < end; u++)
                {
                    _s._strikeTarget[u] = -1;
                    _s._strikeDamage[u] = 0;
                    _s._engaged[u] = false;
                    if (!w.Units.IsAlive(u))
                    {
                        continue;
                    }
                    int type = w.UnitType[u];
                    long reach = _s._typeReachRaw[type];
                    if (reach < 0 || !OnFeet(w.Flags[u]) || !_s._near[w.UnitRegiment[u]])
                    {
                        continue;
                    }
                    var side = w.RegimentSide[w.UnitRegiment[u]];
                    var p = w.Position[u];
                    long radius = _s._typeRadiusRaw[type];
                    int found = _hash.QueryRadius(
                        w,
                        p,
                        Fix.FromRaw(radius + _s._maxRadiusRaw + reach),
                        neighbours
                    );
                    int count = System.Math.Min(found, MaxNeighbours);
                    int best = -1;
                    long bestSq = long.MaxValue;
                    for (int k = 0; k < count; k++)
                    {
                        int j = neighbours[k];
                        if (j == u || w.RegimentSide[w.UnitRegiment[j]] == side)
                        {
                            continue;
                        }
                        long dx = w.Position[j].X.Raw - p.X.Raw;
                        long dy = w.Position[j].Y.Raw - p.Y.Raw;
                        long within = radius + _s._typeRadiusRaw[w.UnitType[j]] + reach;
                        long dSq = dx * dx + dy * dy;
                        if (dSq <= within * within && (dSq < bestSq || (dSq == bestSq && j < best)))
                        {
                            best = j;
                            bestSq = dSq;
                        }
                    }
                    if (best < 0)
                    {
                        continue;
                    }
                    _s._engaged[u] = true;
                    int interval = _s._typeInterval[type];
                    if ((_tick + Phase(u, _seed, interval)) % interval != 0)
                    {
                        continue;
                    }
                    var rng = SimRandom.Stream(_seed, SwingSystemId, u, _tick);
                    if (rng.NextUInt(1000) >= (uint)_s._typeHit[type])
                    {
                        continue;
                    }
                    int taken = _s._rules.UnitTypes[w.UnitType[best]].MeleeTakenPermille;
                    _s._strikeTarget[u] = best;
                    _s._strikeDamage[u] = (int)((long)_s._typeDamage[type] * taken / 1000);
                }
            }
        }
    }
}
