using System;
using GodGame.Sim.Math;

namespace GodGame.Sim
{
    /// <summary>Inputs for <see cref="PlaceholderBattle"/>.</summary>
    public sealed class PlaceholderSetup
    {
        public PlaceholderSetup(
            ulong seed = 1,
            int unitCount = 8,
            int arenaWidthMm = 200_000,
            int arenaDepthMm = 100_000,
            int maxSpeedMmPerTick = 100
        )
        {
            if (unitCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(unitCount));
            }
            if (arenaWidthMm <= 2 * maxSpeedMmPerTick || arenaDepthMm <= 2 * maxSpeedMmPerTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(arenaWidthMm),
                    "The arena must be wider than two ticks of movement."
                );
            }
            if (maxSpeedMmPerTick < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSpeedMmPerTick));
            }
            Seed = seed;
            UnitCount = unitCount;
            ArenaWidthMm = arenaWidthMm;
            ArenaDepthMm = arenaDepthMm;
            MaxSpeedMmPerTick = maxSpeedMmPerTick;
        }

        public ulong Seed { get; }
        public int UnitCount { get; }
        public int ArenaWidthMm { get; }
        public int ArenaDepthMm { get; }

        /// <summary>Per velocity component. 100 mm per tick is 2 m/s at 20 ticks per second.</summary>
        public int MaxSpeedMmPerTick { get; }
    }

    /// <summary>
    /// The M0 placeholder simulation: a tick counter and a few units wandering on seeded paths inside a box,
    /// with a state hash. It proves the toolchain (tests, CI hash comparison, Unity driving a cube) and is
    /// replaced by the real world state and tick scheduler in M1.
    /// </summary>
    public sealed class PlaceholderBattle
    {
        /// <summary>System id for the random streams that pick new headings.</summary>
        public const int WanderSystemId = 1;

        /// <summary>Units pick a new seeded velocity this often.</summary>
        public const int WanderIntervalTicks = 5 * SimConstants.TicksPerSecond;

        private readonly PlaceholderSetup _setup;
        private readonly int[] _xMm;
        private readonly int[] _yMm;
        private readonly int[] _vxMm;
        private readonly int[] _vyMm;
        private readonly SimHash _hash = new SimHash();

        public PlaceholderBattle(PlaceholderSetup setup)
        {
            _setup = setup ?? throw new ArgumentNullException(nameof(setup));
            int count = setup.UnitCount;
            _xMm = new int[count];
            _yMm = new int[count];
            _vxMm = new int[count];
            _vyMm = new int[count];

            for (int unit = 0; unit < count; unit++)
            {
                Pcg32 rng = SimRandom.Stream(setup.Seed, WanderSystemId, unit, -1);
                _xMm[unit] = rng.NextInt(0, setup.ArenaWidthMm);
                _yMm[unit] = rng.NextInt(0, setup.ArenaDepthMm);
                PickVelocity(unit, 0);
            }
        }

        /// <summary>Ticks completed since the battle started.</summary>
        public int Tick { get; private set; }

        public int UnitCount => _setup.UnitCount;
        public PlaceholderSetup Setup => _setup;

        public ReadOnlySpan<int> PositionXMm => _xMm;
        public ReadOnlySpan<int> PositionYMm => _yMm;

        public void Step()
        {
            Tick++;
            int width = _setup.ArenaWidthMm;
            int depth = _setup.ArenaDepthMm;
            bool wander = Tick % WanderIntervalTicks == 0;

            for (int unit = 0; unit < _xMm.Length; unit++)
            {
                if (wander)
                {
                    PickVelocity(unit, Tick);
                }
                Move(ref _xMm[unit], ref _vxMm[unit], width);
                Move(ref _yMm[unit], ref _vyMm[unit], depth);
            }
        }

        public void Step(int ticks)
        {
            for (int i = 0; i < ticks; i++)
            {
                Step();
            }
        }

        /// <summary>xxHash64 over the version, tick and every unit's state, in index order.</summary>
        public ulong StateHash()
        {
            _hash.Reset();
            _hash.AppendInt32(SimVersion.Current);
            _hash.AppendInt32(Tick);
            _hash.AppendInt32(_xMm.Length);
            _hash.AppendInt32s(_xMm);
            _hash.AppendInt32s(_yMm);
            _hash.AppendInt32s(_vxMm);
            _hash.AppendInt32s(_vyMm);
            return _hash.Digest();
        }

        private void PickVelocity(int unit, int tick)
        {
            int max = _setup.MaxSpeedMmPerTick;
            Pcg32 rng = SimRandom.Stream(_setup.Seed, WanderSystemId, unit, tick);
            _vxMm[unit] = rng.NextInt(-max, max + 1);
            _vyMm[unit] = rng.NextInt(-max, max + 1);
        }

        /// <summary>Moves along one axis and bounces off the arena walls.</summary>
        private static void Move(ref int position, ref int velocity, int size)
        {
            position += velocity;
            if (position < 0)
            {
                position = -position;
                velocity = -velocity;
            }
            else if (position >= size)
            {
                position = 2 * (size - 1) - position;
                velocity = -velocity;
            }
        }
    }
}
