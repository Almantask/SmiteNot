using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using GodGame.Sim.Hosting;
using GodGame.Sim.Math;

namespace GodGame.Sim.Bench;

/// <summary>
/// Cost fields on the working map (1.2 × 0.8 km at 1.5 m: 800 × 534 cells, about 430,000) with rolling ground: a full
/// rebuild, and the incremental rebuild after a one-cell edit. The navigation rebuild budget after an edit is 50 ms
/// (recommended) and 100 ms (floor), plan section 8; flow fields add to it in item 2.
/// </summary>
[MemoryDiagnoser]
public class CostFieldsBench
{
    private CostFields _costs = null!;
    private IParallelRunner _runner = null!;

    [Params(1, 4)]
    public int Threads { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        const int Columns = 800;
        const int Rows = 534;
        var rng = new Pcg32(3, 3);
        var height = new byte[Columns * Rows * 4];
        for (int i = 0; i < Columns * Rows; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(height.AsSpan(i * 4), rng.NextInt(0, 2_000));
        }
        string json =
            "{\"version\":0,\"id\":\"bench\",\"widthMm\":1200000,\"depthMm\":800000,\"cellSizeMm\":1500,\"deployment\":[]}";
        var map = MapData.Load(json, height);
        var rules = RepoFiles.LoadRepoRules(RepoFiles.FindRoot(AppContext.BaseDirectory));
        _runner = Threads == 1 ? new SerialRunner() : new ThreadedRunner(Threads);
        _costs = new CostFields(new Terrain(map), rules, _runner);
    }

    [Benchmark]
    public void RebuildAll() => _costs.RebuildAll(_runner);

    [Benchmark]
    public void RebuildAfterOneCellEdit() =>
        _costs.Rebuild(_costs.Terrain.DirtyAround(400, 267, 400, 267), _runner);
}
