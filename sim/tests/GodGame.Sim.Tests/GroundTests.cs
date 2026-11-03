using System.Buffers.Binary;
using System.Globalization;
using GodGame.Sim.Hosting;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim.Tests;

[TestFixture, Category("Fast")]
public class GroundTests
{
    private const int Cell = 1_500;

    private static MapData Map(int columns, int rows, string extraJson = "")
    {
        string json = string.Format(
            CultureInfo.InvariantCulture,
            "{{\"version\":0,\"id\":\"ground-test\",\"widthMm\":{0},\"depthMm\":{1},\"cellSizeMm\":{2},\"deployment\":[]{3}}}",
            columns * Cell,
            rows * Cell,
            Cell,
            extraJson
        );
        return MapData.Load(json, new byte[columns * rows * 4]);
    }

    /// <summary>Water and ground on a flat map, with a world for marching soldiers.</summary>
    private static Simulation Run(MapData map, World? world = null, IParallelRunner? runner = null)
    {
        var rules = RepoData.Rules();
        var terrain = new Terrain(map);
        world ??= new World(1, 1);
        world.AttachFields(new FieldStore(rules.Fields, map.Columns, map.Rows));
        var systems = TickOrder.CreateDefault();
        systems[(int)TickStep.Fields] = new FieldSystem(
            new IFieldModel[]
            {
                new WaterModel(terrain, map, rules.Fields.Water!, TickOrder.FieldsRate),
                new GroundModel(terrain, map, rules, TickOrder.FieldsRate),
            }
        );
        return new Simulation(world, 1, runner ?? new SerialRunner(), systems);
    }

    private static FieldGrid Field(Simulation s, string id) => s.World.Fields!.Get(id);

    [Test]
    public void StandingWater_SoaksIn_UntilTheSoilIsFull_ThenLeavesTheRest()
    {
        var simulation = Run(Map(20, 20));
        var water = Field(simulation, WaterModel.Depth);
        var wetness = Field(simulation, GroundModel.Wetness);
        // 100 mm of water over the whole flat map, more than the soil takes (about 28.7 mm from 8,192).
        for (int y = 0; y < 20; y++)
        {
            for (int x = 0; x < 20; x++)
            {
                water.Set(x, y, 100_000);
            }
        }
        long before = water.Values.Sum(v => (long)v);

        simulation.Step(60 * SimConstants.TicksPerSecond);

        Assert.That(
            wetness.Get(10, 10),
            Is.GreaterThanOrEqualTo(GroundModel.Full - 1),
            "soaked full (to the unit: 1 µm is 2 units)"
        );
        long soaked = wetness.Values.Sum(w => (long)w - 8_192) / 2;
        long left = water.Values.Sum(v => (long)v);
        Assert.That(
            left + soaked,
            Is.EqualTo(before).Within(400),
            "water only moved into the soil"
        );
        Assert.That(left, Is.GreaterThan(0), "a flat patch keeps what the soil can't take");
    }

    [Test]
    public void WetGround_DriesToItsNaturalLevel_AndNoFurther()
    {
        var simulation = Run(Map(20, 20));
        var wetness = Field(simulation, GroundModel.Wetness);
        wetness.Set(5, 5, 30_000);

        simulation.Step(30 * SimConstants.TicksPerSecond);
        Assert.That(wetness.Get(5, 5), Is.EqualTo(30_000 - 30 * 110).Within(25), "110 a second");
        simulation.Step(300 * SimConstants.TicksPerSecond);
        Assert.That(wetness.Get(5, 5), Is.EqualTo(8_192), "back to the map's natural moisture");
        Assert.That(wetness.Get(15, 15), Is.EqualTo(8_192), "dry ground was never touched");
    }

    [Test]
    public void SoakedGround_IsSoftMudByItself_AndMudFollowsTheCurves()
    {
        var simulation = Run(Map(20, 20));
        var wetness = Field(simulation, GroundModel.Wetness);
        var mud = Field(simulation, GroundModel.Mud);
        wetness.Set(3, 3, 65_535);
        wetness.Set(6, 6, 30_000);
        wetness.Set(9, 9, 15_000);

        simulation.Step(4); // one field step

        Assert.That(
            mud.Get(3, 3),
            Is.EqualTo(65_535 - 32_768 - 0).Within(110 + 50),
            "saturated: soft, by itself"
        );
        Assert.That(mud.Get(6, 6), Is.Zero, "too dry to be mud untrodden");
        Assert.That(mud.Get(9, 9), Is.Zero);
    }

    [Test]
    public void Decision_PowerPalette_MudWorsensAsManyFeetChurnIt_TheArmyRutsItsOwnRoad()
    {
        // A soaked field, half of it a firm road. 400 soldiers mill about on each half.
        var map = Map(
            40,
            20,
            ",\"soils\":[{\"soil\":\"road\",\"minXMm\":30000,\"minYMm\":0,\"maxXMm\":60000,\"maxYMm\":30000}]"
        );
        var rules = RepoData.Rules();
        var world = new World(800, 2);
        var earth = world.AddRegiment(Side.Attacker, 0, 100);
        var road = world.AddRegiment(Side.Attacker, 0, 100);
        for (int i = 0; i < 400; i++)
        {
            int u1 = world
                .SpawnUnit(earth, FixVec2.FromInts(5 + i % 20, 5 + i / 20), 100, 100)
                .Index;
            int u2 = world
                .SpawnUnit(road, FixVec2.FromInts(35 + i % 20, 5 + i / 20), 100, 100)
                .Index;
            world.Velocity[u1] = FixVec2.FromInts(0, 1) / 20; // marching, so they churn
            world.Velocity[u2] = FixVec2.FromInts(0, 1) / 20;
        }
        var simulation = Run(map, world);
        var wetness = Field(simulation, GroundModel.Wetness);
        var mud = Field(simulation, GroundModel.Mud);
        for (int y = 0; y < 20; y++)
        {
            for (int x = 0; x < 40; x++)
            {
                wetness.Set(x, y, 65_535);
            }
        }
        simulation.Step(4);
        int softEarth = mud.Get(8, 8);

        // The soldiers stand still in the world (no movement system here) but count as marching. The soaked ground
        // dries a little over the minute, which lowers every curve, so compare churned ground with untrodden.
        simulation.Step(60 * SimConstants.TicksPerSecond);
        int churned = mud.Get(8, 8);
        int untrodden = mud.Get(8, 18);
        int cap = (wetness.Get(8, 8) - 16_384) * 4 / 3;

        Assert.That(softEarth, Is.InRange(32_000, 33_500), "saturated earth starts soft");
        Assert.That(
            churned,
            Is.EqualTo(cap).Within(50),
            "churned to as deep as its wetness allows"
        );
        Assert.That(
            churned,
            Is.GreaterThan(untrodden * 3 / 2),
            "far deeper than the ground beside the column"
        );
        Assert.That(mud.Get(28, 8), Is.LessThanOrEqualTo(cap / 2), "a road gets soft, never deep");
        Assert.That(
            mud.Get(28, 8),
            Is.LessThan(churned * 2 / 3),
            "the same march, far shallower ruts on the road"
        );
    }

    [Test]
    public void Ground_IsTheSameOnAnyRunner()
    {
        ulong[] Hashes(IParallelRunner runner)
        {
            var simulation = Run(Map(60, 40), null, runner);
            var water = Field(simulation, WaterModel.Depth);
            for (int y = 10; y < 30; y++)
            {
                water.Set(20 + y % 7, y, 50_000 + y * 1_000);
            }
            return simulation.RunAndHash(400, 20);
        }

        var serial = Hashes(new SerialRunner());
        Assert.That(Hashes(new ThreadedRunner(4)), Is.EqualTo(serial));
        Assert.That(Hashes(new ThreadedRunner(8)), Is.EqualTo(serial));
    }

    [Test]
    public void AMapNamingAnUnknownSoil_IsRefused()
    {
        var map = Map(
            20,
            20,
            ",\"soils\":[{\"soil\":\"lava\",\"minXMm\":0,\"minYMm\":0,\"maxXMm\":3000,\"maxYMm\":3000}]"
        );

        Assert.Throws<GodGame.Sim.Rules.Json.DataFormatException>(() =>
            new GroundModel(new Terrain(map), map, RepoData.Rules(), TickOrder.FieldsRate)
        );
    }

    [Test]
    public void WaterSleepsOnItsOwnSchedule_WhenTheGroundModelAlsoWritesIt()
    {
        // A pond on soil that is already full: the ground takes no water, so both runs should send the pond's tiles
        // to sleep on the same tick.
        int SleepTick(bool withGround)
        {
            var map = Map(30, 20);
            var rules = RepoData.Rules();
            var terrain = new Terrain(map);
            var world = new World(1, 1);
            world.AttachFields(new FieldStore(rules.Fields, map.Columns, map.Rows));
            var models = new List<IFieldModel>
            {
                new WaterModel(terrain, map, rules.Fields.Water!, TickOrder.FieldsRate),
            };
            if (withGround)
            {
                models.Add(new GroundModel(terrain, map, rules, TickOrder.FieldsRate));
            }
            var systems = TickOrder.CreateDefault();
            systems[(int)TickStep.Fields] = new FieldSystem(models);
            var simulation = new Simulation(world, 1, new SerialRunner(), systems);
            var water = world.Fields!.Get(WaterModel.Depth);
            var wetness = world.Fields!.Get(GroundModel.Wetness);
            for (int y = 0; y < 20; y++)
            {
                for (int x = 0; x < 30; x++)
                {
                    wetness.Set(x, y, GroundModel.Full);
                    water.Set(x, y, 200_000);
                }
            }
            var tiles = new int[water.TileCount];
            while (water.AwakeTiles(tiles) > 0)
            {
                simulation.Step();
                Assert.That(simulation.Tick, Is.LessThan(2_000), "the pond never went quiet");
            }
            return simulation.Tick;
        }

        Assert.That(SleepTick(withGround: true), Is.EqualTo(SleepTick(withGround: false)));
    }

    [Test]
    public void Churn_IsTheSameOnAnyRunner_AndAcrossARestore()
    {
        Simulation Make(IParallelRunner runner)
        {
            var map = Map(60, 40);
            var world = new World(1_500, 3);
            for (int r = 0; r < 3; r++)
            {
                var regiment = world.AddRegiment(Side.Attacker, r == 1 ? 1 : 0, 100);
                for (int i = 0; i < 500; i++)
                {
                    int u = world
                        .SpawnUnit(
                            regiment,
                            FixVec2.FromInts(3 + (i % 50) + r, 3 + i / 50 + r * 10),
                            100,
                            100
                        )
                        .Index;
                    world.Velocity[u] = FixVec2.FromInts(1, 0) / 20;
                }
            }
            var simulation = Run(map, world, runner);
            var wetness = world.Fields!.Get(GroundModel.Wetness);
            for (int y = 0; y < 40; y++)
            {
                for (int x = 0; x < 60; x++)
                {
                    wetness.Set(x, y, 40_000 + 300 * ((x + y) % 50));
                }
            }
            return simulation;
        }

        ulong[] serial = Make(new SerialRunner()).RunAndHash(200, 20);
        Assert.That(Make(new ThreadedRunner(4)).RunAndHash(200, 20), Is.EqualTo(serial));

        var original = Make(new ThreadedRunner(4));
        original.Step(100);
        byte[] snapshot = original.CaptureSnapshot();
        var restored = Make(new ThreadedRunner(3));
        restored.Step(17);
        restored.RestoreSnapshot(snapshot);
        restored.Step(100);
        Assert.That(restored.StateHash(), Is.EqualTo(serial[^1]));
    }
}
