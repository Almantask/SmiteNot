namespace GodGame.Sim
{
    /// <summary>
    /// One step of the tick. A system owns one behaviour, reads and writes the world's arrays, and gets everything
    /// else from the context; it keeps no static state and reads no clock.
    /// <para>
    /// Anything that lasts from one tick to the next lives in <see cref="World"/>, where the state hash and
    /// snapshots see it. A system's own fields may only hold scratch buffers that every run overwrites before
    /// reading. Each system instance belongs to one <see cref="Simulation"/>: never share one between a live run
    /// and a preview.
    /// </para>
    /// </summary>
    public interface ISimSystem
    {
        TickStep Step { get; }

        /// <summary>How often the scheduler calls <see cref="Run"/>. Systems that stagger their own entities run every tick and use <see cref="TickRate.IsDueFor"/>.</summary>
        TickRate Rate { get; }

        void Run(TickContext context);
    }

    /// <summary>
    /// A system that keeps data derived from the world between ticks (navigation's costs and flow fields) and must
    /// rebuild it when <see cref="Simulation.RestoreSnapshot"/> puts the world back to another moment.
    /// </summary>
    public interface IRestoreAware
    {
        void AfterRestore(TickContext context);
    }

    /// <summary>What a system gets each time it runs.</summary>
    public sealed class TickContext
    {
        internal TickContext(World world, IParallelRunner runner, ulong seed)
        {
            World = world;
            Runner = runner;
            Seed = seed;
        }

        public World World { get; }

        public IParallelRunner Runner { get; }

        /// <summary>The battle seed, for <see cref="Math.SimRandom.Stream"/>.</summary>
        public ulong Seed { get; }

        /// <summary>The tick being run: 0 for the first.</summary>
        public int Tick { get; internal set; }
    }

    /// <summary>A registered step with no behaviour yet; later milestones replace each one.</summary>
    public sealed class EmptySystem : ISimSystem
    {
        public EmptySystem(TickStep step, TickRate rate)
        {
            Step = step;
            Rate = rate;
        }

        public TickStep Step { get; }

        public TickRate Rate { get; }

        public void Run(TickContext context) { }
    }
}
