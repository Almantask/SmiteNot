using System.Buffers.Binary;
using System.Globalization;
using GodGame.Sim.Hosting;
using GodGame.Sim.Rules;

namespace GodGame.Sim.Tests;

[TestFixture, Category("Fast")]
public class WaterTests
{
    private const int Cell = 1_500;

    /// <summary>A map from a height function, with optional water JSON (sources and sinks) and no zones.</summary>
    internal static MapData Map(
        Func<int, int, int> heightMm,
        int columns,
        int rows,
        string waterJson = ""
    )
    {
        string json = string.Format(
            CultureInfo.InvariantCulture,
            "{{\"version\":0,\"id\":\"water-test\",\"widthMm\":{0},\"depthMm\":{1},\"cellSizeMm\":{2},\"deployment\":[]{3}}}",
            columns * Cell,
            rows * Cell,
            Cell,
            waterJson.Length == 0 ? "" : ",\"water\":" + waterJson
        );
        var bytes = new byte[columns * rows * 4];
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(
                    bytes.AsSpan((y * columns + x) * 4),
                    heightMm(x, y)
                );
            }
        }
        return MapData.Load(json, bytes);
    }

    internal static Simulation Run(MapData map, IParallelRunner? runner = null)
    {
        var rules = RepoData.Rules();
        var terrain = new Terrain(map);
        var world = new World(1, 1);
        var fields = new FieldStore(rules.Fields, map.Columns, map.Rows);
        world.AttachFields(fields);
        var model = new WaterModel(terrain, map, rules.Fields.Water!, TickOrder.FieldsRate);
        model.WakeSources(fields);
        var systems = TickOrder.CreateDefault();
        systems[(int)TickStep.Fields] = new FieldSystem(new IFieldModel[] { model });
        return new Simulation(world, 1, runner ?? new SerialRunner(), systems);
    }

    internal static FieldGrid Water(Simulation s) => s.World.Fields!.Get(WaterModel.Depth);

    private static long Volume(Simulation s) => Water(s).Values.Sum(v => (long)v);

    [Test]
    public void AClosedBasin_KeepsEveryMicrometre_AndLevelsOut()
    {
        // A bowl: 2 m higher at the rim. A 1.5 m column of water dropped in the middle.
        var map = Map(
            (x, y) => 2_000 * (System.Math.Abs(x - 20) + System.Math.Abs(y - 15)) / 35,
            40,
            30
        );
        var simulation = Run(map);
        var water = Water(simulation);
        for (int y = 13; y <= 17; y++)
        {
            for (int x = 18; x <= 22; x++)
            {
                water.Set(x, y, 1_500_000);
            }
        }
        long before = Volume(simulation);

        // Deep water sloshes before it settles (a seiche), slowly damped by bed friction.
        simulation.Step(480 * SimConstants.TicksPerSecond);

        Assert.That(Volume(simulation), Is.EqualTo(before), "no water made or lost");
        Assert.That(water.Values.Min(), Is.GreaterThanOrEqualTo(0));
        // Wet cells now share one surface, to within a few mm.
        var terrain = new Terrain(map);
        var surfaces = Enumerable
            .Range(0, map.Columns * map.Rows)
            .Where(i => water.Values[i] > 20_000)
            .Select(i =>
                terrain.HeightMm(i % map.Columns, i / map.Columns) * 1000L + water.Values[i]
            )
            .ToArray();
        Assert.That(
            surfaces.Max() - surfaces.Min(),
            Is.LessThan(10_000),
            "one level, within 10 mm"
        );
    }

    [Test]
    public void StillWater_GoesQuiet_AndItsTilesSleep()
    {
        var map = Map((x, y) => 0, 40, 30);
        var simulation = Run(map);
        var water = Water(simulation);
        water.Set(10, 10, 500_000);
        var tiles = new int[water.TileCount];

        simulation.Step(240 * SimConstants.TicksPerSecond);

        Assert.That(water.AwakeTiles(tiles), Is.Zero, "a pond at rest costs nothing");
    }

    [Test]
    public void WaterRunsDownhill_AndStaysOffHighGround()
    {
        // A slope down to the east: 100 ‰.
        var map = Map((x, y) => 150 * (39 - x), 40, 10);
        var simulation = Run(map);
        var water = Water(simulation);
        for (int y = 0; y < 10; y++)
        {
            water.Set(2, y, 300_000);
        }

        simulation.Step(60 * SimConstants.TicksPerSecond);

        long west = Enumerable.Range(0, 10).Sum(y => (long)water.Get(2, y));
        long east = Enumerable.Range(0, 10).Sum(y => (long)water.Get(39, y));
        Assert.That(east, Is.GreaterThan(west * 10), "it pooled at the low end");
    }

    [Test]
    public void ARiverFromSourceToSink_ReachesASteadyFlow()
    {
        // A channel 1.5 m deep and 9 cells wide running north to south, falling 1 ‰; 2 m³/s in at the north edge,
        // out at the south edge.
        string waterJson =
            "{\"sources\":[{\"minXMm\":15000,\"minYMm\":118500,\"maxXMm\":28500,\"maxYMm\":120000,\"litresPerSecond\":2000}],"
            + "\"sinks\":[{\"minXMm\":15000,\"minYMm\":0,\"maxXMm\":28500,\"maxYMm\":1500}]}";
        var map = Map((x, y) => (x >= 10 && x < 19 ? -1_500 : 0) + y * 15 / 10, 30, 80, waterJson);
        var simulation = Run(map);

        simulation.Step(300 * SimConstants.TicksPerSecond);
        long a = Volume(simulation);
        simulation.Step(30 * SimConstants.TicksPerSecond);
        long b = Volume(simulation);

        var water = Water(simulation);
        Assert.That(
            water.Get(14, 40),
            Is.InRange(100_000, 1_500_000),
            "the channel carries water mid-way"
        );
        Assert.That(water.Get(5, 40), Is.LessThan(5_000), "the banks stay dry");
        WaterModel.Velocity(simulation.World.Fields!, Cell, 14, 40, out int east, out int north);
        Assert.That(-north, Is.InRange(400, 1_200), "it runs south at about S1's 0.5 to 1 m/s");
        Assert.That(System.Math.Abs(east), Is.LessThan(100));
        Assert.That(
            System.Math.Abs(b - a),
            Is.LessThan(a / 50),
            "what comes in goes out: under 2% drift in 30 s"
        );
    }

    [Test]
    public void Water_IsTheSameOnAnyRunner_AndAcrossARestore()
    {
        string waterJson =
            "{\"sources\":[{\"minXMm\":15000,\"minYMm\":88500,\"maxXMm\":28500,\"maxYMm\":90000,\"litresPerSecond\":3000}],"
            + "\"sinks\":[{\"minXMm\":15000,\"minYMm\":0,\"maxXMm\":28500,\"maxYMm\":1500}]}";
        var map = Map(
            (x, y) => (x >= 10 && x < 19 ? -1_000 : 300 * ((x * 7 + y * 3) % 4)) + y * 20,
            60,
            60,
            waterJson
        );

        ulong[] serial = Run(map).RunAndHash(600, 20);
        Assert.That(Run(map, new ThreadedRunner(4)).RunAndHash(600, 20), Is.EqualTo(serial));
        Assert.That(Run(map, new ThreadedRunner(8)).RunAndHash(600, 20), Is.EqualTo(serial));

        var original = Run(map);
        original.Step(300);
        byte[] snapshot = original.CaptureSnapshot();
        var restored = Run(map, new ThreadedRunner(3));
        restored.Step(41);
        restored.RestoreSnapshot(snapshot);
        restored.Step(300);
        Assert.That(restored.StateHash(), Is.EqualTo(serial[^1]));
    }

    [Test]
    public void MapWater_IsReadAndChecked()
    {
        string waterJson =
            "{\"sources\":[{\"minXMm\":0,\"minYMm\":0,\"maxXMm\":3000,\"maxYMm\":1500,\"litresPerSecond\":500}],"
            + "\"sinks\":[{\"minXMm\":0,\"minYMm\":0,\"maxXMm\":1500,\"maxYMm\":15000}]}";
        var map = Map((x, y) => 0, 20, 10, waterJson);

        Assert.That(map.WaterSources, Has.Count.EqualTo(1));
        Assert.That(map.WaterSources[0].LitresPerSecond, Is.EqualTo(500));
        Assert.That(map.WaterSinks, Has.Count.EqualTo(1));
        Assert.Throws<GodGame.Sim.Rules.Json.DataFormatException>(() =>
            Map(
                (x, y) => 0,
                20,
                10,
                "{\"sinks\":[{\"minXMm\":3000,\"minYMm\":3000,\"maxXMm\":6000,\"maxYMm\":6000}]}"
            )
        );
    }
}

/// <summary>Water over fifteen simulated minutes: runs with the slow tests.</summary>
[TestFixture, Category("Slow")]
public class WaterSlowTests
{
    [Test]
    public void ASourceTile_StaysAwakeEvenWhenItsRiverIsInExactBalance()
    {
        // A trickle (20 L/s) settles into a balance where the source cell's depth stops changing to the µm; its
        // tile must still step every field step, or the source would stop.
        string waterJson =
            "{\"sources\":[{\"minXMm\":15000,\"minYMm\":118500,\"maxXMm\":28500,\"maxYMm\":120000,\"litresPerSecond\":20}],"
            + "\"sinks\":[{\"minXMm\":15000,\"minYMm\":0,\"maxXMm\":28500,\"maxYMm\":1500}]}";
        var map = WaterTests.Map(
            (x, y) => (x >= 10 && x < 19 ? -1_500 : 0) + y * 15 / 10,
            30,
            80,
            waterJson
        );
        var simulation = WaterTests.Run(map);
        var water = WaterTests.Water(simulation);
        int sourceTile = water.TileOf(14, 79);

        simulation.Step(900 * SimConstants.TicksPerSecond);
        for (int t = 0; t < 60 * SimConstants.TicksPerSecond; t++)
        {
            simulation.Step();
            Assert.That(water.IsAwake(sourceTile), Is.True, $"tick {simulation.Tick}");
        }
    }
}
