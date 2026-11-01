using System;
using System.Collections.Generic;
using GodGame.Sim.Math;

namespace GodGame.Sim
{
    /// <summary>
    /// Runs the tick: the systems from <see cref="TickOrder"/>, in order, each when its rate is due. The host
    /// supplies the parallel runner, so the same battle can run on one thread in Unity's preview and on many in
    /// the CLI with identical results.
    /// </summary>
    public sealed class Simulation
    {
        private readonly ISimSystem[] _systems;
        private readonly TickContext _context;
        private readonly SimHash _hash = new SimHash();

        public Simulation(
            World world,
            ulong seed,
            IParallelRunner runner,
            IReadOnlyList<ISimSystem>? systems = null,
            ulong battleId = 0
        )
        {
            BattleId = battleId;
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
            if (runner == null)
            {
                throw new ArgumentNullException(nameof(runner));
            }
            _systems = systems == null ? TickOrder.CreateDefault() : Copy(systems);
            TickOrder.Validate(_systems);
            _context = new TickContext(world, runner, seed);
        }

        public World World => _context.World;

        public ulong Seed => _context.Seed;

        /// <summary>Which battle this is (Battle.Id); snapshots carry it, so one can't restore into another battle. 0 for simulations built by hand.</summary>
        public ulong BattleId { get; }

        /// <summary>Ticks completed; also the number of the tick <see cref="Step()"/> runs next.</summary>
        public int Tick { get; private set; }

        public IReadOnlyList<ISimSystem> Systems => _systems;

        public void Step()
        {
            _context.Tick = Tick;
            for (int i = 0; i < _systems.Length; i++)
            {
                if (_systems[i].Rate.IsDue(Tick))
                {
                    _systems[i].Run(_context);
                }
            }
            Tick++;
        }

        public void Step(int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                Step();
            }
        }

        /// <summary>A snapshot of the whole simulation; see <see cref="Snapshot"/>.</summary>
        public byte[] CaptureSnapshot() => Snapshot.Write(BattleId, Tick, Seed, World);

        /// <summary>
        /// Puts this simulation back to a snapshot's moment. The world must have the same capacities and the seed must
        /// match: a snapshot belongs to one battle. Systems keep no state between ticks, so nothing else needs restoring.
        /// </summary>
        public void RestoreSnapshot(byte[] snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            // Read into a scratch world first, so a bad snapshot leaves this simulation untouched.
            var scratch = new World(World.Units.Capacity, World.Regiments.Capacity);
            var (battleId, tick, seed) = Snapshot.Read(snapshot, scratch);
            if (battleId != BattleId)
            {
                throw new FormatException(
                    "The snapshot belongs to another battle (rules, map or plan differ)."
                );
            }
            if (seed != Seed)
            {
                throw new FormatException("The snapshot belongs to a battle with another seed.");
            }
            Snapshot.Read(snapshot, World);
            Tick = tick;
        }

        /// <summary>
        /// Steps <paramref name="ticks"/> ticks and records the state hash after every <paramref name="every"/>th
        /// (plan, section 3.9: hashes every N ticks; 20 gives the per-second hashes goldens and CI compare).
        /// </summary>
        public ulong[] RunAndHash(int ticks, int every)
        {
            if (ticks < 0 || every < 1)
            {
                throw new ArgumentOutOfRangeException(ticks < 0 ? nameof(ticks) : nameof(every));
            }
            var hashes = new ulong[ticks / every];
            for (int i = 0; i < hashes.Length; i++)
            {
                Step(every);
                hashes[i] = StateHash();
            }
            Step(ticks - hashes.Length * every);
            return hashes;
        }

        /// <summary>xxHash64 over the version, the tick, the seed and the whole world.</summary>
        public ulong StateHash()
        {
            _hash.Reset();
            _hash.AppendInt32(SimVersion.Current);
            _hash.AppendInt32(Tick);
            _hash.AppendUInt64(Seed);
            World.AppendTo(_hash);
            return _hash.Digest();
        }

        private static ISimSystem[] Copy(IReadOnlyList<ISimSystem> systems)
        {
            var copy = new ISimSystem[systems.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = systems[i];
            }
            return copy;
        }
    }
}
