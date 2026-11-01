using GodGame.Sim.Math;

namespace GodGame.Sim
{
    /// <summary>
    /// M1 placeholder for the movement step: every unit walks a seeded straight line at 2 m/s, picks a new heading
    /// every 5 s and bounces off the map edges. It exists so the M1 exit criterion ("10,000 units wandering on seeded
    /// paths") exercises the tick, the runner and the hashes; M2 replaces it with flow-field movement.
    /// </summary>
    public sealed class PlaceholderWanderSystem : ISimSystem
    {
        public const int SystemId = 1;
        public const int HeadingIntervalTicks = 5 * SimConstants.TicksPerSecond;
        public const int ChunkSize = 512;

        /// <summary>2 m/s at 20 ticks per second.</summary>
        public static readonly Fix SpeedPerTick = Fix.FromRatio(1, 10);

        private readonly Fix _width;
        private readonly Fix _depth;

        public PlaceholderWanderSystem(Fix width, Fix depth)
        {
            _width = width;
            _depth = depth;
        }

        public TickStep Step => TickStep.Movement;

        public TickRate Rate => TickRate.EveryTick;

        public void Run(TickContext context)
        {
            var world = context.World;
            context.Runner.For(
                world.Units.HighWater,
                ChunkSize,
                new Kernel(
                    world.Units,
                    world.Position,
                    world.Velocity,
                    context.Seed,
                    context.Tick,
                    _width,
                    _depth
                )
            );
        }

        private readonly struct Kernel : IChunkKernel
        {
            private readonly EntityPool _pool;
            private readonly FixVec2[] _position;
            private readonly FixVec2[] _velocity;
            private readonly ulong _seed;
            private readonly int _tick;
            private readonly Fix _width;
            private readonly Fix _depth;

            public Kernel(
                EntityPool pool,
                FixVec2[] position,
                FixVec2[] velocity,
                ulong seed,
                int tick,
                Fix width,
                Fix depth
            )
            {
                _pool = pool;
                _position = position;
                _velocity = velocity;
                _seed = seed;
                _tick = tick;
                _width = width;
                _depth = depth;
            }

            public void Execute(int start, int end)
            {
                bool newHeading = _tick % HeadingIntervalTicks == 0;
                for (int u = start; u < end; u++)
                {
                    if (!_pool.IsAlive(u))
                    {
                        continue;
                    }
                    FixVec2 v = _velocity[u];
                    if (newHeading)
                    {
                        var rng = SimRandom.Stream(_seed, SystemId, u, _tick);
                        v = FixVec2.FromAngle(
                            Angle.FromUnits((int)rng.NextUInt(Angle.UnitsPerTurn)),
                            SpeedPerTick
                        );
                    }
                    FixVec2 p = _position[u] + v;
                    if (p.X < Fix.Zero || p.X > _width)
                    {
                        v = new FixVec2(-v.X, v.Y);
                        p = new FixVec2(Fix.Clamp(p.X, Fix.Zero, _width), p.Y);
                    }
                    if (p.Y < Fix.Zero || p.Y > _depth)
                    {
                        v = new FixVec2(v.X, -v.Y);
                        p = new FixVec2(p.X, Fix.Clamp(p.Y, Fix.Zero, _depth));
                    }
                    _position[u] = p;
                    _velocity[u] = v;
                }
            }
        }
    }
}
