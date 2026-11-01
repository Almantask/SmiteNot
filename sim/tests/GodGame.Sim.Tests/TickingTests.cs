using GodGame.Sim.Hosting;
using GodGame.Sim.Math;

namespace GodGame.Sim.Tests;

[TestFixture, Category("Fast")]
public class TickOrderTests
{
    [Test]
    public void Default_RegistersEveryStepOnceInOrder()
    {
        var systems = TickOrder.CreateDefault();

        Assert.That(systems.Select(s => s.Step), Is.EqualTo(Enum.GetValues<TickStep>()));
        Assert.That(systems, Has.All.InstanceOf<EmptySystem>());
    }

    [Test]
    public void Validate_RejectsMissingOrMisorderedSteps()
    {
        var systems = TickOrder.CreateDefault().ToList();
        (systems[3], systems[4]) = (systems[4], systems[3]);

        Assert.Throws<ArgumentException>(() => TickOrder.Validate(systems));
        Assert.Throws<ArgumentException>(() =>
            TickOrder.Validate(TickOrder.CreateDefault().Skip(1).ToList())
        );
        var noRate = TickOrder.CreateDefault().ToList();
        noRate[2] = new EmptySystem(TickStep.StructuresAndLoad, default);
        Assert.Throws<ArgumentException>(() => TickOrder.Validate(noRate));
    }

    [Test]
    public void PlanRates_AreFiveAndFourHertz()
    {
        Assert.That(TickOrder.FieldsRate.Period, Is.EqualTo(4));
        Assert.That(TickOrder.RegimentAiRate.Period, Is.EqualTo(5));
    }
}

[TestFixture, Category("Fast")]
public class TickRateTests
{
    [Test]
    public void IsDue_FollowsPeriodAndPhase()
    {
        var rate = new TickRate(4, phase: 1);

        Assert.That(Enumerable.Range(0, 12).Where(rate.IsDue), Is.EqualTo(new[] { 1, 5, 9 }));
        Assert.That(Enumerable.Range(0, 3).All(TickRate.EveryTick.IsDue), Is.True);
    }

    [Test]
    public void IsDueFor_SpreadsEntitiesEvenly()
    {
        var rate = TickRate.FromHertz(4); // every 5 ticks
        for (int regiment = 0; regiment < 23; regiment++)
        {
            var due = Enumerable.Range(0, 20).Where(t => rate.IsDueFor(t, regiment)).ToArray();

            Assert.That(due, Has.Length.EqualTo(4), $"regiment {regiment}");
            Assert.That(due.Zip(due.Skip(1), (a, b) => b - a), Is.All.EqualTo(5));
        }
        int[] perTick = Enumerable
            .Range(0, 5)
            .Select(t => Enumerable.Range(0, 25).Count(r => rate.IsDueFor(t, r)))
            .ToArray();
        Assert.That(perTick, Is.All.EqualTo(5), "25 regiments over 5 ticks: 5 a tick");
    }

    [Test]
    public void IsDueFor_HugeTicksAndIndices_StayCorrect()
    {
        var rate = new TickRate(5, phase: 2);

        Assert.That(
            rate.IsDueFor(int.MaxValue, int.MaxValue),
            Is.EqualTo(((long)int.MaxValue - 2 - int.MaxValue) % 5 == 0)
        );
        Assert.That(rate.IsDueFor(2, 0), Is.True);
        Assert.That(rate.IsDueFor(2 + 5 * 400_000_000, 5 * 400_000_000), Is.True);
        Assert.That(rate.IsDue(int.MaxValue), Is.EqualTo(((long)int.MaxValue - 2) % 5 == 0));
        Assert.That(rate.IsDue(-3), Is.True, "-3 - 2 is a multiple of 5");
    }

    [Test]
    public void FromHertz_RejectsRatesThatDoNotDivideTheTickRate()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TickRate.FromHertz(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => TickRate.FromHertz(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TickRate(4, phase: 4));
        Assert.That(TickRate.FromHertz(20), Is.EqualTo(TickRate.EveryTick));
    }
}

[TestFixture, Category("Fast")]
public class SimulationTests
{
    private sealed class RecordingSystem : ISimSystem
    {
        private readonly List<string> _log;

        public RecordingSystem(TickStep step, TickRate rate, List<string> log)
        {
            Step = step;
            Rate = rate;
            _log = log;
        }

        public TickStep Step { get; }
        public TickRate Rate { get; }

        public void Run(TickContext context) => _log.Add($"{context.Tick}:{Step}");
    }

    [Test]
    public void Step_RunsDueSystemsInOrder()
    {
        var log = new List<string>();
        var systems = Enum.GetValues<TickStep>()
            .Select(step =>
                (ISimSystem)
                    new RecordingSystem(
                        step,
                        step == TickStep.Fields ? new TickRate(4, phase: 1) : TickRate.EveryTick,
                        log
                    )
            )
            .ToList();
        var simulation = new Simulation(new World(1, 1), seed: 1, new SerialRunner(), systems);

        simulation.Step(6);

        Assert.That(simulation.Tick, Is.EqualTo(6));
        Assert.That(
            log.Where(e => e.EndsWith(":Fields")),
            Is.EqualTo(new[] { "1:Fields", "5:Fields" })
        );
        var tickTwo = log.Where(e => e.StartsWith("2:")).ToArray();
        Assert.That(tickTwo.First(), Is.EqualTo("2:PlanEvents"));
        Assert.That(tickTwo.Last(), Is.EqualTo("2:Bookkeeping"));
        Assert.That(tickTwo, Has.Length.EqualTo(10), "fields skip tick 2");
    }

    [Test]
    public void StateHash_ChangesWithTickAndSeed()
    {
        var a = new Simulation(new World(4, 1), seed: 1, new SerialRunner());
        var b = new Simulation(new World(4, 1), seed: 1, new SerialRunner());
        var c = new Simulation(new World(4, 1), seed: 2, new SerialRunner());
        ulong start = a.StateHash();

        Assert.That(b.StateHash(), Is.EqualTo(start));
        Assert.That(c.StateHash(), Is.Not.EqualTo(start));
        a.Step();
        Assert.That(a.StateHash(), Is.Not.EqualTo(start));
    }
}

[TestFixture, Category("Fast")]
public class ParallelRunnerTests
{
    private struct MarkKernel : IChunkKernel
    {
        public int[] Visits;
        public int[] ChunkStart;

        public void Execute(int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                Visits[i]++;
                ChunkStart[i] = start;
            }
        }
    }

    // Per-chunk partial sums, reduced afterwards in chunk order: the pattern for anything that adds up.
    private struct SumKernel : IChunkKernel
    {
        public long[] Values;
        public long[] Partials;
        public int ChunkSize;

        public void Execute(int start, int end)
        {
            long sum = 0;
            for (int i = start; i < end; i++)
            {
                sum = unchecked(sum * 31 + Values[i]); // order-sensitive on purpose
            }
            Partials[start / ChunkSize] = sum;
        }
    }

    private static IEnumerable<IParallelRunner> Runners() =>
        new IParallelRunner[]
        {
            new SerialRunner(),
            new ThreadedRunner(1),
            new ThreadedRunner(2),
            new ThreadedRunner(8),
        };

    [TestCaseSource(nameof(Runners))]
    public void For_VisitsEveryIndexOnce_InFixedChunks(IParallelRunner runner)
    {
        const int Count = 10_007;
        const int ChunkSize = 256;
        var kernel = new MarkKernel { Visits = new int[Count], ChunkStart = new int[Count] };

        runner.For(Count, ChunkSize, kernel);

        Assert.That(kernel.Visits, Is.All.EqualTo(1));
        Assert.That(
            Enumerable.Range(0, Count).All(i => kernel.ChunkStart[i] == i / ChunkSize * ChunkSize),
            Is.True
        );
    }

    // Breaks the rule on purpose: it changes its own field. Every runner must give each chunk a fresh copy.
    private struct CountingKernel : IChunkKernel
    {
        public int[] SeenCount;
        public int Calls;

        public void Execute(int start, int end)
        {
            Calls++;
            for (int i = start; i < end; i++)
            {
                SeenCount[i] = Calls;
            }
        }
    }

    private struct ThrowingKernel : IChunkKernel
    {
        public int ChunkSize;

        public void Execute(int start, int end)
        {
            int chunk = start / ChunkSize;
            if (chunk == 3 || chunk == 9)
            {
                throw new InvalidOperationException("chunk " + chunk);
            }
        }
    }

    [TestCaseSource(nameof(Runners))]
    public void For_GivesEveryChunkAFreshKernelCopy(IParallelRunner runner)
    {
        var kernel = new CountingKernel { SeenCount = new int[5_000] };

        runner.For(5_000, 100, kernel);

        Assert.That(kernel.SeenCount, Is.All.EqualTo(1));
    }

    [TestCaseSource(nameof(Runners))]
    public void For_RethrowsTheLowestFailingChunk(IParallelRunner runner)
    {
        var kernel = new ThrowingKernel { ChunkSize = 10 };

        var error = Assert.Throws<InvalidOperationException>(() => runner.For(200, 10, kernel));

        Assert.That(error!.Message, Is.EqualTo("chunk 3"));
    }

    [Test]
    public void Chunking_DoesNotOverflowNearIntMax()
    {
        Assert.That(ParallelChunks.Count(int.MaxValue, int.MaxValue), Is.EqualTo(1));
        Assert.That(ParallelChunks.Count(int.MaxValue, 1 << 30), Is.EqualTo(2));
        Assert.That(ParallelChunks.End(1, int.MaxValue, 1 << 30), Is.EqualTo(int.MaxValue));
    }

    [Test]
    public void ChunkedReduction_MatchesAcrossThreadCounts()
    {
        var rng = new Pcg32(5, 5);
        var values = Enumerable.Range(0, 50_000).Select(_ => (long)rng.NextUInt()).ToArray();
        long Reduce(IParallelRunner runner)
        {
            const int ChunkSize = 1000;
            var kernel = new SumKernel
            {
                Values = values,
                Partials = new long[ParallelChunks.Count(values.Length, ChunkSize)],
                ChunkSize = ChunkSize,
            };
            runner.For(values.Length, ChunkSize, kernel);
            long total = 0;
            foreach (long partial in kernel.Partials)
            {
                total = unchecked(total * 17 + partial);
            }
            return total;
        }

        long serial = Reduce(new SerialRunner());

        Assert.That(Runners().Select(Reduce), Is.All.EqualTo(serial));
    }

    [Test]
    public void For_RejectsBadArguments_AndAcceptsZeroCount()
    {
        var kernel = new MarkKernel
        {
            Visits = Array.Empty<int>(),
            ChunkStart = Array.Empty<int>(),
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => new SerialRunner().For(10, 0, kernel));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ThreadedRunner(4).For(-1, 8, kernel));
        Assert.DoesNotThrow(() => new ThreadedRunner(4).For(0, 8, kernel));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ThreadedRunner(0));
    }
}

[TestFixture, Category("Fast")]
public class ThreadCountDeterminismTests
{
    /// <summary>A test-only movement system: each unit wanders on a seeded path inside a 1.2 × 0.8 km box.</summary>
    private sealed class WanderSystem : ISimSystem
    {
        public TickStep Step => TickStep.Movement;
        public TickRate Rate => TickRate.EveryTick;

        public void Run(TickContext context)
        {
            var world = context.World;
            context.Runner.For(
                world.Units.HighWater,
                chunkSize: 512,
                new WanderKernel
                {
                    Pool = world.Units,
                    Position = world.Position,
                    Velocity = world.Velocity,
                    Seed = context.Seed,
                    Tick = context.Tick,
                }
            );
        }
    }

    private struct WanderKernel : IChunkKernel
    {
        public EntityPool Pool;
        public FixVec2[] Position;
        public FixVec2[] Velocity;
        public ulong Seed;
        public int Tick;

        private static readonly Fix Width = Fix.FromInt(1200);
        private static readonly Fix Depth = Fix.FromInt(800);

        public void Execute(int start, int end)
        {
            for (int u = start; u < end; u++)
            {
                if (!Pool.IsAlive(u))
                {
                    continue;
                }
                if (Tick % 100 == 0)
                {
                    var rng = SimRandom.Stream(Seed, systemId: 1, entityId: u, tick: Tick);
                    var heading = Angle.FromUnits((int)rng.NextUInt(Angle.UnitsPerTurn));
                    Velocity[u] = FixVec2.FromAngle(heading, Fix.FromRatio(1, 10)); // 2 m/s
                }
                var p = Position[u] + Velocity[u];
                var v = Velocity[u];
                if (p.X < Fix.Zero || p.X > Width)
                {
                    v = new FixVec2(-v.X, v.Y);
                    p = new FixVec2(Fix.Clamp(p.X, Fix.Zero, Width), p.Y);
                }
                if (p.Y < Fix.Zero || p.Y > Depth)
                {
                    v = new FixVec2(v.X, -v.Y);
                    p = new FixVec2(p.X, Fix.Clamp(p.Y, Fix.Zero, Depth));
                }
                Position[u] = p;
                Velocity[u] = v;
            }
        }
    }

    private static ulong[] RunBattle(IParallelRunner runner)
    {
        var world = new World(unitCapacity: 10_000, regimentCapacity: 100);
        for (int r = 0; r < 100; r++)
        {
            var regiment = world.AddRegiment(r % 2 == 0 ? Side.Attacker : Side.Defender, 0, 100);
            for (int s = 0; s < 100; s++)
            {
                var rng = SimRandom.Stream(42, systemId: 2, entityId: r * 100 + s, tick: 0);
                var position = new FixVec2(
                    Fix.FromRaw(rng.NextInt(0, 1200 << 16)),
                    Fix.FromRaw(rng.NextInt(0, 800 << 16))
                );
                world.SpawnUnit(regiment, position, 100, 100);
            }
        }
        var systems = TickOrder.CreateDefault();
        systems[(int)TickStep.Movement] = new WanderSystem();
        var simulation = new Simulation(world, seed: 42, runner, systems);

        var hashes = new ulong[10];
        for (int second = 0; second < hashes.Length; second++)
        {
            simulation.Step(SimConstants.TicksPerSecond);
            hashes[second] = simulation.StateHash();
        }
        return hashes;
    }

    [Test]
    public void TenThousandUnits_OneTwoAndEightThreads_HashIdentically()
    {
        ulong[] serial = RunBattle(new SerialRunner());

        Assert.That(RunBattle(new ThreadedRunner(1)), Is.EqualTo(serial));
        Assert.That(RunBattle(new ThreadedRunner(2)), Is.EqualTo(serial));
        Assert.That(RunBattle(new ThreadedRunner(8)), Is.EqualTo(serial));
        Assert.That(
            serial.Distinct().Count(),
            Is.EqualTo(serial.Length),
            "units must actually move"
        );
    }
}
