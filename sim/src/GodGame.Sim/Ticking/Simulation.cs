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
            IReadOnlyList<ISimSystem>? systems = null
        )
        {
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
