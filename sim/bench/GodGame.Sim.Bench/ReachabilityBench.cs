using BenchmarkDotNet.Attributes;
using GodGame.Sim.Hosting;

namespace GodGame.Sim.Bench;

/// <summary>
/// The reachability check on the working map with a bridged river (FlowFieldBench.RiverMap, zones 800 m apart): what
/// one placement drag costs, a one-cell edit on the main route (toggled), the cost-field rebuild, the coarse-grid
/// update and the whole check (12 routes). Budget: 5 ms recommended, 10 ms floor (plan section 8).
/// </summary>
[MemoryDiagnoser]
public class ReachabilityBench
{
    private CostFields _costs = null!;
    private Reachability _reach = null!;
    private IParallelRunner _runner = null!;
    private bool _raised;

    [Params(1, 4)]
    public int Threads { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var rules = RepoFiles.LoadRepoRules(RepoFiles.FindRoot(AppContext.BaseDirectory));
        var map = FlowFieldBench.RiverMap();
        _runner = Threads == 1 ? new SerialRunner() : new ThreadedRunner(Threads);
        _costs = new CostFields(new Terrain(map), rules, _runner);
        _reach = new Reachability(_costs, map, rules.Routes);
    }

    [Benchmark]
    public ReachabilityResult DragOneCell()
    {
        var terrain = _costs.Terrain;
        _raised = !_raised;
        terrain.SetHeight(380, 267, _raised ? 5_000 : terrain.HeightMm(379, 267));
        var dirty = terrain.DirtyAround(380, 267, 380, 267);
        _costs.Rebuild(dirty, _runner);
        _reach.Update(dirty);
        return _reach.Check(_runner);
    }
}
