using GodGame.Sim.Hosting;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim.Tests;

/// <summary>
/// Landings by surface (M5 item 5, spec S2 9.3): one surface for the launch and the landing (a deck, the water's surface
/// on ice, a floating deck or open water, else the ground), hard blockers at water and below cliffs, the map edge, and the
/// rules file's checks.
/// </summary>
[TestFixture, Category("Fast")]
public class LandingSurfaceTests
{
    private const int Columns = 240;

    private static MapData Map(Func<int, int, int> height, int rows = 40, string extra = "") =>
        TestBattles.Map(height, Columns, rows, extraJson: extra);

    private static void Fill(Simulation simulation, Func<int, int, int> depthMm, int rows = 40)
    {
        var water = simulation.World.Fields!.Get(WaterModel.Depth);
        var wetness = simulation.World.Fields!.Get(GroundModel.Wetness);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < Columns; x++)
            {
                int d = depthMm(x, y);
                if (d > 0)
                {
                    water.Set(x, y, d * 1000);
                    wetness.Set(x, y, GroundModel.Full);
                }
            }
        }
    }

    /// <summary>Launches <paramref name="u"/> from where he stands, with exact speeds, and returns his landing.</summary>
    private static CauseEvent Throw(
        Simulation simulation,
        Battle battle,
        int u,
        Angle heading,
        int horizontalMmPerS,
        int verticalMmPerS = 6_000
    )
    {
        var world = simulation.World;
        var terrain = new Terrain(battle.PlayedMap);
        terrain.CellOf(world.Position[u], out int x, out int y);
        int launched = Ballistics.LaunchWith(
            world,
            u,
            heading,
            horizontalMmPerS,
            verticalMmPerS,
            0,
            CauseLog.None,
            simulation.Tick,
            Ballistics.SurfaceMm(terrain, world, x, y)
        );
        return LaunchTests.StepUntilLanded(simulation, launched);
    }

    [Test]
    public void Decision_2026_10_08_ColdFreezesWater_LandingOnIntactIceOverDeepWaterIsAsGround()
    {
        // S2 9.3: intact ice is as ground. The pond is 2.5 m deep under 250 mm ice; the ice sits at the water's surface,
        // level with the bank he was thrown from, so it is no fall.
        var map = Map(
            (x, _) => x >= 148 && x <= 227 ? -2_500 : 0,
            extra: ",\"frozenPonds\":[{\"minXMm\":222000,\"minYMm\":0,\"maxXMm\":342000,\"maxYMm\":60000,\"thicknessMm\":250}]"
        );
        var battle = LaunchTests.Load(map, LaunchTests.Plan(LaunchTests.Defenders()));
        var simulation = battle.Start(new SerialRunner());
        Fill(simulation, (x, _) => x >= 148 && x <= 227 ? 2_500 : 0);
        simulation.Step(60);
        int u = LaunchTests.Westmost(simulation.World);
        int h = (int)((simulation.World.Position[u].X() - 335.0) * 1000 / 3);
        var landed = Throw(simulation, battle, u, Angle.HalfTurn, h);

        Assert.That(Landing.Outcome(landed.Detail), Is.EqualTo(LandingOutcome.Ice));
        Assert.That(simulation.World.Health[u], Is.EqualTo(90));
    }

    [Test]
    public void Decision_2026_10_08_TerrainCanIncludeBuiltPieces_LandingOnAStandingDeckIsAsGround()
    {
        // A wooden bridge over a dry gorge 6 m deep: thrown from the bank onto the deck, he lands on the deck, not 6 m
        // down on the gorge floor.
        var map = Map((x, _) => x >= 118 && x <= 122 ? -6_000 : 0, 100);
        string bridge =
            "{\"id\":\"b\",\"piece\":\"wooden-bridge\",\"points\":[[175500,75750],[186000,75750]],\"widthMm\":4500}";
        var battle = LaunchTests.Load(
            map,
            LaunchTests.Plan(LaunchTests.Defenders(50, 75_000), bridge)
        );
        var simulation = battle.Start(new SerialRunner());
        simulation.Step(20);
        int u = LaunchTests.Westmost(simulation.World);
        simulation.World.Position[u] = new FixVec2(
            Fix.FromRatio(174_000, 1000),
            Fix.FromRatio(75_750, 1000)
        );
        var landed = Throw(simulation, battle, u, Angle.Zero, 2_000);

        Assert.That(
            simulation.World.Position[u].X(),
            Is.EqualTo(180.0).Within(0.01),
            "on the span"
        );
        Assert.That(Landing.Outcome(landed.Detail), Is.EqualTo(LandingOutcome.Ground));
        Assert.That(simulation.World.Units.IsAlive(u), Is.True);
    }

    [Test]
    public void Decision_2026_10_09_ExaggeratedPhysics_ThrownOffADeckIntoAFordHeLandsInShallowWater()
    {
        // A bridge over a channel 2.5 m deep holding 0.8 m of still water (a ford foot can wade): thrown off the deck, he
        // falls from the deck to the water's surface (1.7 m), not to the bed, so it's a shallow-water stun, no damage.
        var map = Map((x, _) => x >= 120 && x <= 130 ? -2_500 : 0, 100);
        string bridge =
            "{\"id\":\"b\",\"piece\":\"wooden-bridge\",\"points\":[[178500,75750],[198000,75750]],\"widthMm\":4500}";
        var battle = LaunchTests.Load(
            map,
            LaunchTests.Plan(LaunchTests.Defenders(50, 30_000), bridge)
        );
        var simulation = battle.Start(new SerialRunner());
        Fill(simulation, (x, _) => x >= 120 && x <= 130 ? 800 : 0, 100);
        simulation.Step(60);
        int u = LaunchTests.Westmost(simulation.World);
        simulation.World.Position[u] = new FixVec2(
            Fix.FromRatio(187_500, 1000),
            Fix.FromRatio(75_750, 1000)
        );
        var landed = Throw(simulation, battle, u, Angle.QuarterTurn, 3_000);

        Assert.That(
            simulation.World.Position[u].Y(),
            Is.GreaterThan(78.0),
            "off the 4.5 m span, falling past its level to the water"
        );
        Assert.That(Landing.Outcome(landed.Detail), Is.EqualTo(LandingOutcome.ShallowWater));
        Assert.That(simulation.World.Health[u], Is.EqualTo(100));
    }

    [Test]
    public void ThrownOffTheMap_HeIsClampedToItsEdge_AndLandsAsOnGround()
    {
        // S2 9.3: being thrown off the map never counts as fleeing.
        var battle = LaunchTests.Load(Map((_, _) => 0), LaunchTests.Plan(LaunchTests.Defenders()));
        var simulation = battle.Start(new SerialRunner());
        simulation.Step(20);
        var world = simulation.World;
        int u = TestBattles
            .LiveUnits(world)
            .OrderByDescending(k => world.Position[k].X.Raw)
            .First();
        var landed = Throw(simulation, battle, u, Angle.Zero, 5_000);

        Assert.That(Landing.Outcome(landed.Detail), Is.EqualTo(LandingOutcome.Ground));
        Assert.That(world.Units.IsAlive(u), Is.True);
        Assert.That(world.Position[u].X(), Is.GreaterThan(359.99).And.LessThan(360.0));
        Assert.That(world.Counters.Fled, Is.Zero);
    }

    [Test]
    public void Decision_2026_10_09_ExaggeratedPhysics_ABoulderInAPoolSendsHimBackIntoTheWater_NotOntoIt()
    {
        // S2 9.3: after the setback from a hard blocker he lands by the normal rules. The boulder stands in a pool 1.5 m
        // deep: back along his arc is deep water, so he is swept, without the blocker's blow.
        var map = Map((x, _) => x >= 140 && x <= 170 ? -1_500 : 0);
        string boulder = "{\"id\":\"b\",\"piece\":\"boulder\",\"xMm\":230000,\"yMm\":30000}";
        var battle = LaunchTests.Load(map, LaunchTests.Plan(LaunchTests.Defenders(), boulder));
        var simulation = battle.Start(new SerialRunner());
        var terrain = new Terrain(battle.PlayedMap);
        Fill(
            simulation,
            (x, y) => x >= 140 && x <= 170 && !terrain.IsBlocked(terrain.Index(x, y)) ? 1_500 : 0
        );
        simulation.Step(60);
        int u = Centre(simulation.World);
        // The boulder's top stands 0.5 m above the water: he comes down on it at the 59th tick.
        int h = (int)((simulation.World.Position[u].X() - 230.0) * 1000 * 20 / 59);
        var landed = Throw(simulation, battle, u, Angle.HalfTurn, h);

        Assert.That(Landing.Outcome(landed.Detail), Is.EqualTo(LandingOutcome.Swept));
        Assert.That(simulation.World.Health[u], Is.EqualTo(100));
        Assert.That(
            simulation.World.Position[u].X(),
            Is.GreaterThan(231.0),
            "back east of the boulder"
        );
    }

    [Test]
    public void Decision_2026_10_09_ExaggeratedPhysics_ABoulderBelowACliffSendsHimBackOverTheDrop()
    {
        // The boulder stands on ground 5 m below where he was thrown from: his setback lands him on that ground, so he
        // fell, whatever the boulder's own height. Without the boulder he'd have come down 4 m further west.
        var map = Map((x, _) => x >= 140 && x <= 220 ? -5_000 : 0);
        string boulder = "{\"id\":\"b\",\"piece\":\"boulder\",\"xMm\":230000,\"yMm\":30000}";
        var battle = LaunchTests.Load(map, LaunchTests.Plan(LaunchTests.Defenders(), boulder));
        var simulation = battle.Start(new SerialRunner());
        simulation.Step(20);
        int u = Centre(simulation.World);
        int h = (int)((simulation.World.Position[u].X() - 226.0) * 1000 * 20 / 74);
        var landed = Throw(simulation, battle, u, Angle.HalfTurn, h);

        Assert.That(Landing.Outcome(landed.Detail), Is.EqualTo(LandingOutcome.Fell));
        Assert.That(simulation.World.Units.IsAlive(u), Is.False);
        Assert.That(simulation.World.Counters.Fell, Is.EqualTo(1));
        var terrain = new Terrain(battle.PlayedMap);
        int cell = Landing.Cell(landed.Detail);
        Assert.That(terrain.IsBlocked(cell), Is.False);
        Assert.That(
            cell % Columns * 1.5,
            Is.GreaterThan(229.5),
            "set back east, onto the low ground"
        );
    }

    /// <summary>The soldier nearest y = 30 m, the westmost of those: in line with the boulder.</summary>
    private static int Centre(World world)
    {
        int u = TestBattles
            .LiveUnits(world)
            .OrderBy(k => System.Math.Abs(world.Position[k].Y() - 30))
            .ThenBy(k => world.Position[k].X())
            .First();
        Assert.That(world.Position[u].Y(), Is.EqualTo(30.0).Within(0.6));
        return u;
    }

    [TestCase("\"collapse\"", "\"slump\"", "collapse")]
    [TestCase(
        "\"hardBlocker\": {",
        "\"burning\": { \"damage\": 0, \"stunTicks\": 1 }, \"hardBlocker\": {",
        "burning"
    )]
    [TestCase("\"gravityMmPerS2\": 4000", "\"gravityMmPerS2\": 0", "gravityMmPerS2")]
    public void ABadLaunchesFile_IsRejected(string from, string to, string why)
    {
        var files = RepoData
            .RuleFiles()
            .Select(f =>
                f.Key == "launches.json" ? KeyValuePair.Create(f.Key, f.Value.Replace(from, to)) : f
            )
            .ToArray();
        Assert.That(files.Single(f => f.Key == "launches.json").Value, Does.Contain(to));

        var error = Assert.Throws<DataFormatException>(() => RulesetLoader.Load(files));
        Assert.That(error!.Message, Does.Contain(why));
    }
}
