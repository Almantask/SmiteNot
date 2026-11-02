using BenchmarkDotNet.Attributes;

namespace GodGame.Sim.Bench;

/// <summary>
/// The distance-to-lethal field on the working map with a bridged river (FlowFieldBench.RiverMap): a full build, and
/// the partial rebuild after a one-cell edit (the window the 50 m cap reaches). Part of the navigation rebuild budget
/// after an edit (50 ms recommended, plan section 8).
/// </summary>
[MemoryDiagnoser]
public class LethalDistanceBench
{
    private Terrain _terrain = null!;
    private LethalDistance _field = null!;

    [GlobalSetup]
    public void Setup()
    {
        _terrain = new Terrain(FlowFieldBench.RiverMap());
        _field = new LethalDistance(_terrain, 4_000);
    }

    [Benchmark]
    public void RebuildAll() => _field.Rebuild(_terrain.All);

    [Benchmark]
    public void RebuildAfterOneCellEdit() =>
        _field.Rebuild(_terrain.DirtyAround(380, 267, 380, 267));
}
