using GodGame.Sim.Hosting;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim.Tests;

/// <summary>A test-only field model: each stepped cell moves halfway toward the mean of its four neighbours.</summary>
internal sealed class DiffusionModel : IFieldModel
{
    public DiffusionModel(string fieldId, TickRate rate, bool staggered)
    {
        FieldId = fieldId;
        Rate = rate;
        Staggered = staggered;
    }

    public string FieldId { get; }

    public IReadOnlyList<string> AlsoWrites => Array.Empty<string>();

    public void BeforeStep(FieldStepContext context) { }

    public TickRate Rate { get; }

    public bool Staggered { get; }

    public int Steps { get; private set; }

    public int MostTilesInAStep { get; private set; }

    public void Step(FieldStepContext context, FieldGrid grid, ReadOnlySpan<int> tiles)
    {
        Steps++;
        MostTilesInAStep = System.Math.Max(MostTilesInAStep, tiles.Length);
        context.Runner.For(tiles.Length, 3, new Kernel(grid, tiles.ToArray()));
    }

    private readonly struct Kernel : IChunkKernel
    {
        private readonly FieldGrid _grid;
        private readonly int[] _tiles;

        public Kernel(FieldGrid grid, int[] tiles)
        {
            _grid = grid;
            _tiles = tiles;
        }

        public void Execute(int start, int end)
        {
            var v = _grid.Values;
            for (int k = start; k < end; k++)
            {
                var r = _grid.TileRect(_tiles[k]);
                for (int y = r.MinY; y <= r.MaxY; y++)
                {
                    for (int x = r.MinX; x <= r.MaxX; x++)
                    {
                        int c = v[y * _grid.Columns + x];
                        long sum =
                            (long)Get(x - 1, y, c)
                            + Get(x + 1, y, c)
                            + Get(x, y - 1, c)
                            + Get(x, y + 1, c);
                        int next = (int)((4L * c + sum) / 8);
                        _grid.Next[y * _grid.Columns + x] = next;
                    }
                }
            }
        }

        private int Get(int x, int y, int fallback) =>
            x < 0 || y < 0 || x >= _grid.Columns || y >= _grid.Rows ? fallback : _grid.Get(x, y);
    }
}

/// <summary>A careless test model: raises every other cell far past the field's maximum and leaves the rest alone.</summary>
internal sealed class CarelessModel : IFieldModel
{
    public string FieldId => "water";

    public IReadOnlyList<string> AlsoWrites => Array.Empty<string>();

    public void BeforeStep(FieldStepContext context) { }

    public TickRate Rate => TickRate.EveryTick;

    public bool Staggered => false;

    public void Step(FieldStepContext context, FieldGrid grid, ReadOnlySpan<int> tiles)
    {
        foreach (int tile in tiles)
        {
            var r = grid.TileRect(tile);
            for (int y = r.MinY; y <= r.MaxY; y++)
            {
                for (int x = r.MinX + (y & 1); x <= r.MaxX; x += 2)
                {
                    grid.Next[y * grid.Columns + x] = grid.Get(x, y) + 70_000;
                }
            }
        }
    }
}

[TestFixture, Category("Fast")]
public class FieldTests
{
    private static FieldRules Rules() =>
        new FieldRules(
            8,
            3,
            new[]
            {
                new FieldDefinition(0, "water", "mm", 0, 100_000, 0),
                new FieldDefinition(1, "wetness", "scale", 0, 65_535, 100),
            },
            Array.Empty<FieldInteraction>()
        );

    private static (Simulation, DiffusionModel) Run(IParallelRunner runner, bool staggered = false)
    {
        var world = new World(1, 1);
        world.AttachFields(new FieldStore(Rules(), 60, 40));
        var model = new DiffusionModel("water", TickRate.FromHertz(5), staggered);
        var systems = TickOrder.CreateDefault();
        systems[(int)TickStep.Fields] = new FieldSystem(new IFieldModel[] { model });
        return (new Simulation(world, 3, runner, systems), model);
    }

    [Test]
    public void RepoFields_Load_InFileOrder()
    {
        var fields = RepoData.Rules().Fields;

        Assert.That(
            fields.Fields.Select(f => f.Id),
            Is.EqualTo(
                new[]
                {
                    "water",
                    "water_out_e",
                    "water_out_n",
                    "water_out_w",
                    "water_out_s",
                    "wetness",
                    "mud",
                    "nav_speed",
                }
            )
        );
        Assert.That(fields.Fields[0].Unit, Is.EqualTo("um"));
        Assert.That(fields.TileCells, Is.EqualTo(16));
        Assert.That(fields.IndexOf("mud"), Is.EqualTo(6));
        Assert.That(fields.IndexOf("lava"), Is.EqualTo(-1));
    }

    [TestCase("\"unit\": \"um\"", "\"unit\": \"metres\"", "unit must be")]
    [TestCase("\"max\": 65535", "\"max\": 70000", "outside")]
    [TestCase(
        "\"interactions\": []",
        "\"interactions\": [{ \"id\": \"soak\", \"source\": \"rain\", \"target\": \"mud\", \"rate\": 1 }]",
        "source and target"
    )]
    [TestCase("{ \"id\": \"mud\"", "{ \"id\": \"water\"", "duplicate field")]
    public void BadFieldRules_AreRefused(string from, string to, string why)
    {
        string json = RepoData.Read("data", "rules", "fields.json");
        Assert.That(json, Does.Contain(from));
        var files = RepoData
            .RuleFiles()
            .Select(f =>
                f.Key == "fields.json" ? KeyValuePair.Create(f.Key, json.Replace(from, to)) : f
            )
            .ToArray();

        var error = Assert.Throws<DataFormatException>(() => RulesetLoader.Load(files));
        Assert.That(error!.Message, Does.Contain(why));
    }

    [Test]
    public void AFieldStartsAsleepAtItsInitialValue_AndAWriteWakesTheTilesAround()
    {
        var grid = new FieldStore(Rules(), 60, 40)[1];
        var tiles = new int[grid.TileCount];

        Assert.That(grid.Values, Is.All.EqualTo(100));
        Assert.That(grid.AwakeTiles(tiles), Is.Zero);

        grid.Set(20, 20, 99_999); // clamped to the scale
        Assert.That(grid.Get(20, 20), Is.EqualTo(65_535));
        int awake = grid.AwakeTiles(tiles);
        Assert.That(awake, Is.EqualTo(9), "its tile and the eight around it");
        Assert.That(tiles.Take(awake), Does.Contain(grid.TileOf(20, 20)));
    }

    [Test]
    public void ChangesSpreadAcrossTiles_AndQuietTilesFallAsleep()
    {
        var (simulation, model) = Run(new SerialRunner());
        var water = simulation.World.Fields!.Get("water");
        water.Set(30, 20, 80_000);
        var tiles = new int[water.TileCount];

        simulation.Step(20 * SimConstants.TicksPerSecond);
        int spreading = water.AwakeTiles(tiles);
        Assert.That(model.Steps, Is.EqualTo(5 * 20), "5 Hz for 20 s");
        Assert.That(water.Get(38, 20), Is.GreaterThan(0), "water reached the next tile");
        Assert.That(spreading, Is.GreaterThan(9));

        // Flatten the field: nothing changes, so every tile sleeps after three quiet steps.
        for (int y = 0; y < 40; y++)
        {
            for (int x = 0; x < 60; x++)
            {
                water.Set(x, y, 500);
            }
        }
        simulation.Step(SimConstants.TicksPerSecond);
        Assert.That(water.AwakeTiles(tiles), Is.Zero);
        int stepsAsleep = model.Steps;
        simulation.Step(SimConstants.TicksPerSecond);
        Assert.That(model.Steps, Is.EqualTo(stepsAsleep), "a sleeping field costs nothing");
    }

    [Test]
    public void StaggeredTiles_StepOnTheirOwnPhase()
    {
        var (simulation, model) = Run(new SerialRunner(), staggered: true);
        var water = simulation.World.Fields!.Get("water");
        water.Set(30, 20, 80_000);

        simulation.Step(4); // one 5 Hz period: each awake tile on its own tick of the four

        Assert.That(
            model.Steps,
            Is.GreaterThan(1),
            "the nine awake tiles are spread over several ticks"
        );
        Assert.That(model.MostTilesInAStep, Is.LessThan(9), "never all nine at once");
    }

    [Test]
    public void FieldSteps_AreTheSameOnAnyRunner()
    {
        ulong[] Hashes(IParallelRunner runner, bool staggered)
        {
            var (simulation, _) = Run(runner, staggered);
            var water = simulation.World.Fields!.Get("water");
            water.Set(30, 20, 80_000);
            water.Set(5, 35, 40_000);
            return simulation.RunAndHash(200, 20);
        }

        foreach (bool staggered in new[] { false, true })
        {
            var serial = Hashes(new SerialRunner(), staggered);
            Assert.That(Hashes(new ThreadedRunner(4), staggered), Is.EqualTo(serial));
            Assert.That(Hashes(new ThreadedRunner(8), staggered), Is.EqualTo(serial));
        }
    }

    [Test]
    public void FieldsAreHashed_ValuesAndTileActivity()
    {
        var world = new World(1, 1);
        world.AttachFields(new FieldStore(Rules(), 60, 40));
        var hash = new SimHash();
        ulong Hash()
        {
            hash.Reset();
            world.AppendTo(hash);
            return hash.Digest();
        }

        ulong before = Hash();
        world.Fields!.Get("wetness").Set(3, 3, 100); // the same value, but the tiles wake
        ulong woken = Hash();
        world.Fields!.Get("wetness").Set(3, 3, 101);

        Assert.That(woken, Is.Not.EqualTo(before));
        Assert.That(Hash(), Is.Not.EqualTo(woken));
    }

    [Test]
    public void Snapshots_CarryFields_SparselyAndExactly()
    {
        var (simulation, _) = Run(new SerialRunner());
        byte[] untouched = simulation.CaptureSnapshot();
        simulation.World.Fields!.Get("water").Set(30, 20, 80_000);
        simulation.Step(60);
        byte[] snapshot = simulation.CaptureSnapshot();
        ulong[] expected = simulation.RunAndHash(100, 20);

        var (restored, _) = Run(new ThreadedRunner(3));
        restored.Step(13);
        restored.RestoreSnapshot(snapshot);

        Assert.That(restored.CaptureSnapshot(), Is.EqualTo(snapshot));
        Assert.That(restored.RunAndHash(100, 20), Is.EqualTo(expected));
        Assert.That(
            untouched.Length,
            Is.LessThan(2 * 60 * 40),
            "an untouched field costs two bytes a tile, not four a cell"
        );
    }

    [Test]
    public void TheRepoBattle_HasItsFields_WithinTheKeyframeBudget()
    {
        var simulation = RepoBattle.TenThousand().Start(new SerialRunner());
        var fields = simulation.World.Fields!;

        Assert.That(fields.Count, Is.EqualTo(8));
        Assert.That(fields.Columns * fields.Rows, Is.EqualTo(800 * 534));
        Assert.That(simulation.CaptureSnapshot().Length, Is.LessThan(4 * 1024 * 1024));
    }

    [Test]
    public void AModelThatSkipsCellsAndOvershoots_IsClampedAndRestoresExactly()
    {
        Simulation Make()
        {
            var world = new World(1, 1);
            world.AttachFields(new FieldStore(Rules(), 60, 40));
            var systems = TickOrder.CreateDefault();
            systems[(int)TickStep.Fields] = new FieldSystem(
                new IFieldModel[] { new CarelessModel() }
            );
            return new Simulation(world, 3, new SerialRunner(), systems);
        }

        var live = Make();
        var water = live.World.Fields!.Get("water");
        water.Set(30, 20, 10);
        live.Step(3);
        byte[] snapshot = live.CaptureSnapshot();
        live.Step(3);

        var restored = Make();
        restored.Step(5); // the next buffer now holds other leftovers
        restored.RestoreSnapshot(snapshot);
        restored.Step(3);

        Assert.That(water.Values.Max(), Is.EqualTo(100_000), "clamped to the field's maximum");
        Assert.That(water.Get(31, 20), Is.Zero, "a cell the model skipped kept its value");
        Assert.That(restored.StateHash(), Is.EqualTo(live.StateHash()));
    }
}
