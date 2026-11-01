using BenchmarkDotNet.Attributes;
using GodGame.Sim.Hosting;

namespace GodGame.Sim.Bench;

/// <summary>
/// One tick of the M1 battle (data/plans/m1-ten-thousand.json: 10,000 units on m1-flat) with the M1 systems: the
/// empty steps plus the placeholder wander movement. The M1 exit criterion asks for at least 15× real time, a tick
/// under 3.3 ms; budgets.json records the baseline.
/// </summary>
[MemoryDiagnoser]
public class BattleTickBench
{
    private Simulation _simulation = null!;

    [Params(1, 4)]
    public int Threads { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        string root = RepoFiles.FindRoot(AppContext.BaseDirectory);
        var rules = RepoFiles.LoadRepoRules(root);
        var map = RepoFiles.LoadRepoMap(root, "m1-flat");
        var plan = BattlePlan.Load(
            File.ReadAllText(Path.Combine(root, "data", "plans", "m1-ten-thousand.json")),
            map,
            rules
        );
        IParallelRunner runner = Threads == 1 ? new SerialRunner() : new ThreadedRunner(Threads);
        _simulation = new Battle(rules, map, plan, seed: 1).Start(runner);
    }

    [Benchmark]
    public void Tick() => _simulation.Step();

    [Benchmark]
    public ulong StateHash() => _simulation.StateHash();
}
