using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using GodGame.Sim.Hosting;
using GodGame.Sim.Rules;

namespace GodGame.Sim.Bench;

/// <summary>
/// Flow fields on the working map (800 × 534 cells) with gentle hills and a river (a 5 m trench) crossed by three
/// bridges: one full field, the six fields two armies of three classes need, and the repair after a one-cell edit
/// on the main route (toggled each call, so the map never drifts). Budget: the whole navigation rebuild after an edit
/// is 50 ms recommended, 100 ms floor (plan section 8).
/// </summary>
[MemoryDiagnoser]
public class FlowFieldBench
{
    private const int Columns = 800;
    private const int Rows = 534;

    private CostFields _costs = null!;
    private FlowFieldSet _set = null!;
    private FlowField _one = null!;
    private IParallelRunner _runner = null!;
    private bool _raised;

    [Params(1, 4)]
    public int Threads { get; set; }

    internal static MapData RiverMap()
    {
        var height = new byte[Columns * Rows * 4];
        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Columns; x++)
            {
                int hx = x % 100 < 50 ? x % 100 : 100 - x % 100;
                int hy = y % 80 < 40 ? y % 80 : 80 - y % 80;
                int h = 20 * hx + 15 * hy;
                bool river = x >= 395 && x <= 404;
                bool bridge =
                    (y >= 100 && y < 108) || (y >= 263 && y < 271) || (y >= 420 && y < 428);
                if (river && !bridge)
                {
                    h -= 5_000;
                }
                BinaryPrimitives.WriteInt32LittleEndian(height.AsSpan((y * Columns + x) * 4), h);
            }
        }
        string json =
            "{\"version\":0,\"id\":\"bench\",\"widthMm\":1200000,\"depthMm\":800000,\"cellSizeMm\":1500,\"deployment\":["
            + "{\"side\":\"attacker\",\"minXMm\":0,\"minYMm\":0,\"maxXMm\":200000,\"maxYMm\":800000},"
            + "{\"side\":\"defender\",\"minXMm\":1000000,\"minYMm\":0,\"maxXMm\":1200000,\"maxYMm\":800000}]}";
        return MapData.Load(json, height);
    }

    [GlobalSetup]
    public void Setup()
    {
        var rules = RepoFiles.LoadRepoRules(RepoFiles.FindRoot(AppContext.BaseDirectory));
        _runner = Threads == 1 ? new SerialRunner() : new ThreadedRunner(Threads);
        _costs = new CostFields(new Terrain(RiverMap()), rules, _runner);
        _set = new FlowFieldSet(_costs);
        var east = new CellRect(667, 0, 799, Rows - 1);
        var west = new CellRect(0, 0, 133, Rows - 1);
        foreach (var c in new[] { MovementClass.Foot, MovementClass.Horse, MovementClass.Wheeled })
        {
            _set.GetOrAdd(c, east);
            _set.GetOrAdd(c, west);
        }
        _set.BuildPending(_runner);
        _one = new FlowField(_costs, MovementClass.Foot, east);
        _one.Rebuild();
    }

    [Benchmark]
    public void RebuildOneField() => _one.Rebuild();

    [Benchmark]
    public void RebuildSixFields() => _set.RebuildAll(_runner);

    /// <summary>Raises or lowers one cell on the middle bridge's approach: cost fields, then all six fields repaired.</summary>
    [Benchmark]
    public void EditOneCell_RepairSixFields()
    {
        var terrain = _costs.Terrain;
        _raised = !_raised;
        terrain.SetHeight(380, 267, _raised ? 5_000 : terrain.HeightMm(379, 267));
        var dirty = terrain.DirtyAround(380, 267, 380, 267);
        _costs.Rebuild(dirty, _runner);
        _set.Repair(dirty, _runner);
    }
}
