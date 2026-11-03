using System.Buffers.Binary;
using BenchmarkDotNet.Attributes;
using GodGame.Sim.Hosting;

namespace GodGame.Sim.Bench;

/// <summary>
/// Water on the working map (800 × 534 cells): a 20 m channel 1.5 m deep running north to south down a 1 ‰ valley,
/// 10 m³/s in at the north edge and out at the south (spec S1's M3 river), warmed up for two minutes so the river
/// flows end to end. One field step (two water substeps over every awake tile) and one whole tick with water and
/// nothing else. Budget: the field step must fit the tick budget with water active (M3 exit criterion).
/// </summary>
[MemoryDiagnoser]
public class WaterBench
{
    private const int Columns = 800;
    private const int Rows = 534;

    private Simulation _simulation = null!;
    private IParallelRunner _runner = null!;

    [Params(1, 4)]
    public int Threads { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var height = new byte[Columns * Rows * 4];
        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Columns; x++)
            {
                bool channel = x >= 393 && x < 406;
                int valley = System.Math.Abs(x - 400) * 40 * 1_500 / 1000; // 40 ‰ sides
                int h = (channel ? -1_500 : valley) + y * 15 / 10;
                BinaryPrimitives.WriteInt32LittleEndian(height.AsSpan((y * Columns + x) * 4), h);
            }
        }
        string json =
            "{\"version\":0,\"id\":\"water-bench\",\"widthMm\":1200000,\"depthMm\":801000,\"cellSizeMm\":1500,\"deployment\":[],"
            + "\"water\":{\"sources\":[{\"minXMm\":589500,\"minYMm\":799500,\"maxXMm\":609000,\"maxYMm\":801000,\"litresPerSecond\":10000}],"
            + "\"sinks\":[{\"minXMm\":589500,\"minYMm\":0,\"maxXMm\":609000,\"maxYMm\":1500}]}}";
        var map = MapData.Load(json, height);
        var rules = RepoFiles.LoadRepoRules(RepoFiles.FindRoot(AppContext.BaseDirectory));
        _runner = Threads == 1 ? new SerialRunner() : new ThreadedRunner(Threads);
        var world = new World(1, 1);
        var fields = new FieldStore(rules.Fields, map.Columns, map.Rows);
        world.AttachFields(fields);
        var model = new WaterModel(
            new Terrain(map),
            map,
            rules.Fields.Water!,
            TickOrder.FieldsRate
        );
        model.WakeSources(fields);
        var systems = TickOrder.CreateDefault();
        systems[(int)TickStep.Fields] = new FieldSystem(new IFieldModel[] { model });
        _simulation = new Simulation(world, 1, _runner, systems);
        _simulation.Step(120 * SimConstants.TicksPerSecond);
        var tiles = new int[fields.Get(WaterModel.Depth).TileCount];
        Console.WriteLine(
            "awake water tiles "
                + fields.Get(WaterModel.Depth).AwakeTiles(tiles)
                + " of "
                + tiles.Length
        );
    }

    /// <summary>One 5 Hz period: three empty ticks and the one that steps the water.</summary>
    [Benchmark]
    public void FourTicks() => _simulation.Step(4);
}
