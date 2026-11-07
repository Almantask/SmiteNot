using System.Globalization;
using GodGame.Sim.Hosting;
using GodGame.Sim.Rules;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim.Tests;

/// <summary>Terrain pieces v0 (M3 item 8, spec S1 section 9).</summary>
[TestFixture, Category("Fast")]
public class TerrainPieceTests
{
    // A flat 360 × 150 m map; the zones reach 15 m in from the west and east edges, so pieces between x = 115 and
    // 245 m keep the 100 m buffer.
    private const int Columns = 240;
    private const int Rows = 100;

    private static MapData Flat(Func<int, int, int>? height = null) =>
        TestBattles.Map(height ?? ((x, y) => 0), Columns, Rows);

    private static string Plan(string terrain, string army = "") =>
        "{\"version\":0,\"map\":\"test\",\"army\":["
        + army
        + "],\"terrain\":["
        + terrain
        + "],\"casts\":[]}";

    private static string Line(
        string id,
        string piece,
        string extra,
        params (int X, int Y)[] points
    ) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{{\"id\":\"{0}\",\"piece\":\"{1}\",\"points\":[{2}]{3}}}",
            id,
            piece,
            string.Join(",", points.Select(p => "[" + p.X + "," + p.Y + "]")),
            extra
        );

    private static string Point(string id, string piece, int x, int y, string extra = "") =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{{\"id\":\"{0}\",\"piece\":\"{1}\",\"xMm\":{2},\"yMm\":{3}{4}}}",
            id,
            piece,
            x,
            y,
            extra
        );

    private static Battle Load(MapData map, string terrain, string army = "") =>
        new Battle(
            RepoData.Rules(),
            map,
            BattlePlan.Load(Plan(terrain, army), map, RepoData.Rules()),
            1
        );

    private static int Height(Battle battle, int x, int y) =>
        battle.PlayedMap.HeightMm[y * battle.PlayedMap.Columns + x];

    [Test]
    public void AChannel_DigsItsBedAndHalfDepthSides_FollowingTheGround()
    {
        // A sloping map (1 m per 100 m east), a 3 m-wide, 2 m-deep channel along y = 75 m.
        var map = Flat((x, y) => x * 15);
        var battle = Load(
            map,
            Line(
                "c",
                "channel",
                ",\"widthMm\":3000,\"depthMm\":2000",
                (150_000, 75_000),
                (210_000, 75_000)
            )
        );

        Assert.That(
            Height(battle, 120, 50),
            Is.EqualTo(120 * 15 - 2_000),
            "bed: 2 m below the ground there"
        );
        Assert.That(
            Height(battle, 130, 50),
            Is.EqualTo(130 * 15 - 2_000),
            "the bed follows the slope"
        );
        Assert.That(
            Height(battle, 120, 51),
            Is.EqualTo(120 * 15 - 1_000),
            "side cell at half depth"
        );
        Assert.That(Height(battle, 120, 52), Is.EqualTo(120 * 15), "untouched beyond");
        Assert.That(
            battle.Map.HeightMm[50 * Columns + 120],
            Is.EqualTo(120 * 15),
            "the map itself is never changed"
        );
    }

    [Test]
    public void Decision_2026_11_03_WaterAndRainRules_RidgesAndDeepChannelsFunnelTheSiegeTrain()
    {
        // S1 D8: foot and horse cross a ridge and a 2 m channel; wheeled cross neither, but do cross a 0.5 m channel.
        var map = Flat();
        var battle = Load(
            map,
            string.Join(
                ",",
                Line("r", "ridge", ",\"heightMm\":3000", (150_000, 30_000), (210_000, 30_000)),
                Line(
                    "deep",
                    "channel",
                    ",\"widthMm\":3000,\"depthMm\":2000",
                    (150_000, 75_000),
                    (210_000, 75_000)
                ),
                Line(
                    "shallow",
                    "channel",
                    ",\"widthMm\":3000,\"depthMm\":500",
                    (150_000, 120_000),
                    (210_000, 120_000)
                )
            )
        );
        var costs = new CostFields(new Terrain(battle.PlayedMap), battle.Rules, new SerialRunner());

        bool Crosses(MovementClass c, int fromY, int toY) =>
            Enumerable
                .Range(fromY, toY - fromY + 1)
                .All(y => costs.Cost(c, 120, y) != CostFields.Impassable);

        Assert.That(Height(battle, 120, 20), Is.EqualTo(3_000), "ridge crest");
        Assert.That(
            Crosses(MovementClass.Foot, 10, 30) && Crosses(MovementClass.Horse, 10, 30),
            Is.True
        );
        Assert.That(
            Crosses(MovementClass.Wheeled, 10, 30),
            Is.False,
            "400 permille sides stop the siege train"
        );
        Assert.That(
            Crosses(MovementClass.Foot, 45, 55) && Crosses(MovementClass.Horse, 45, 55),
            Is.True
        );
        Assert.That(Crosses(MovementClass.Wheeled, 45, 55), Is.False, "a 2 m channel stops it");
        Assert.That(Crosses(MovementClass.Wheeled, 75, 85), Is.True, "a 0.5 m channel doesn't");
    }

    [Test]
    public void ABoulder_StandsTwoMetresOverTheGround_AndClosesItsCellsToEveryClass()
    {
        var map = Flat((x, y) => y * 10);
        var battle = Load(map, Point("b", "boulder", 180_000, 75_000));
        var costs = new CostFields(new Terrain(battle.PlayedMap), battle.Rules, new SerialRunner());

        foreach (var (x, y) in new[] { (120, 50), (121, 50), (120, 51), (121, 51) })
        {
            Assert.That(
                Height(battle, x, y),
                Is.EqualTo(51 * 10 + 2_000),
                "2 m over the highest ground under it"
            );
            foreach (
                var c in new[] { MovementClass.Foot, MovementClass.Horse, MovementClass.Wheeled }
            )
            {
                Assert.That(costs.Cost(c, x, y), Is.EqualTo(CostFields.Impassable));
            }
        }
        Assert.That(costs.Cost(MovementClass.Foot, 123, 50), Is.Not.EqualTo(CostFields.Impassable));
    }

    [Test]
    public void ASpring_FlowsFromTickZero_AndFillsAHollow()
    {
        var map = Flat();
        var battle = Load(
            map,
            Line("h", "hollow", ",\"depthMm\":2000", (170_000, 75_000), (190_000, 75_000))
                + ","
                + Point("s", "spring", 160_000, 75_000, ",\"litresPerSecond\":3000"),
            "{\"id\":\"d\",\"side\":\"defender\",\"unitType\":\"infantry\",\"soldiers\":50,\"xMm\":352500,\"yMm\":75000,\"facingCentiDegrees\":18000}"
        );
        Assert.That(Height(battle, 120, 50), Is.EqualTo(-2_000), "hollow floor");
        var simulation = battle.Start(new ThreadedRunner(4));
        var depth = simulation.World.Fields!.Get(WaterModel.Depth);

        simulation.Step(60 * SimConstants.TicksPerSecond);

        Assert.That(depth.Get(120, 50), Is.GreaterThan(100_000), "the hollow became a pond");
    }

    [TestCase(114_000, "within")]
    [TestCase(116_500, null)]
    public void Decision_2026_10_08_TerrainPiecesStay100mFromDeploymentZones(int x, string? refused)
    {
        // The attacker zone ends at x = 15 m: a boulder whose west edge is under 100 m from it is refused.
        var map = Flat();
        string plan = Plan(Point("b", "boulder", x, 75_000));
        if (refused == null)
        {
            Assert.That(BattlePlan.Load(plan, map, RepoData.Rules()).Terrain, Has.Count.EqualTo(1));
            return;
        }
        var error = Assert.Throws<DataFormatException>(() =>
            BattlePlan.Load(plan, map, RepoData.Rules())
        );
        Assert.That(error!.Message, Does.Contain(refused).And.Contain("Attacker"));
    }

    [Test]
    public void Decision_2026_10_09_NoPlacementMayFullyTrapEitherArmy_BouldersClosingTheLastGapAreRefused()
    {
        // A wall across the map at x = 180 m with one gap 27 m wide (rows 41 to 58), wide enough for the siege train
        // (routes.json clearance). A boulder in the middle leaves room for foot and horse but closes the siege route.
        var map = Flat((x, y) => x == 120 && (y < 41 || y > 58) ? 8_000 : 0);
        Assert.That(BattlePlan.Load(Plan(""), map, RepoData.Rules()).Terrain, Is.Empty);

        var error = Assert.Throws<DataFormatException>(() =>
            BattlePlan.Load(Plan(Point("b", "boulder", 180_000, 73_500)), map, RepoData.Rules())
        );
        Assert.That(error!.Message, Does.Contain("close the last route").And.Contain("Wheeled"));
        Assert.That(
            BattlePlan
                .Load(Plan(Point("b", "boulder", 150_000, 75_000)), map, RepoData.Rules())
                .Terrain,
            Has.Count.EqualTo(1),
            "a boulder beside the gap is fine"
        );
    }

    [TestCase(
        "{\"id\":\"c\",\"piece\":\"channel\",\"points\":[[150000,75000],[155000,75000]],\"widthMm\":3000,\"depthMm\":1000}",
        "long"
    )]
    [TestCase(
        "{\"id\":\"c\",\"piece\":\"channel\",\"points\":[[150000,75000],[200000,75000]],\"widthMm\":3000,\"depthMm\":2500}",
        "depthMm"
    )]
    [TestCase(
        "{\"id\":\"r\",\"piece\":\"ridge\",\"points\":[[150000,75000]],\"heightMm\":2000}",
        "points"
    )]
    [TestCase(
        "{\"id\":\"s\",\"piece\":\"spring\",\"xMm\":180000,\"yMm\":75000,\"litresPerSecond\":5000}",
        "litresPerSecond"
    )]
    [TestCase("{\"id\":\"w\",\"piece\":\"wall\",\"xMm\":180000,\"yMm\":75000}", "no piece")]
    [TestCase(
        "{\"id\":\"b\",\"piece\":\"boulder\",\"xMm\":180000,\"yMm\":75000},{\"id\":\"b2\",\"piece\":\"boulder\",\"xMm\":181000,\"yMm\":76000}",
        "another boulder"
    )]
    [TestCase(
        "{\"id\":\"b\",\"piece\":\"boulder\",\"xMm\":180000,\"yMm\":75000},{\"id\":\"s\",\"piece\":\"spring\",\"xMm\":181000,\"yMm\":76000,\"litresPerSecond\":1000}",
        "on a boulder"
    )]
    public void APieceThatCantBePlaced_IsRefusedWhilePlanning(string terrain, string why)
    {
        var error = Assert.Throws<DataFormatException>(() =>
            BattlePlan.Load(Plan(terrain), Flat(), RepoData.Rules())
        );
        Assert.That(error!.Message, Does.Contain(why));
    }

    [Test]
    public void PieceOrder_NeverChangesTheGround_AndThePlanHashIgnoresFileOrder()
    {
        // A ridge crossing a channel: channels dig first, ridges raise over them, whatever the plan says.
        string ridge = Line(
            "a-ridge",
            "ridge",
            ",\"heightMm\":2000",
            (180_000, 40_000),
            (180_000, 110_000)
        );
        string channel = Line(
            "z-channel",
            "channel",
            ",\"widthMm\":3000,\"depthMm\":2000",
            (140_000, 75_000),
            (220_000, 75_000)
        );
        var map = Flat();
        var one = Load(map, ridge + "," + channel);
        var two = Load(map, channel + "," + ridge);

        Assert.That(one.PlayedMap.HeightMm, Is.EqualTo(two.PlayedMap.HeightMm));
        Assert.That(one.Plan.Hash, Is.EqualTo(two.Plan.Hash));
        Assert.That(Height(one, 120, 50), Is.EqualTo(2_000), "the ridge crosses the channel");
    }

    [Test]
    public void Pieces_AreTheSameOnAnyRunner_AndAcrossARestore()
    {
        Simulation Make(IParallelRunner runner) =>
            Load(
                    Flat(),
                    string.Join(
                        ",",
                        Line(
                            "c",
                            "channel",
                            ",\"widthMm\":4500,\"depthMm\":1500",
                            (150_000, 20_000),
                            (150_000, 130_000)
                        ),
                        Point("s", "spring", 140_000, 75_000, ",\"litresPerSecond\":3000"),
                        Point("b", "boulder", 200_000, 75_000)
                    ),
                    "{\"id\":\"a\",\"side\":\"attacker\",\"unitType\":\"infantry\",\"soldiers\":100,\"xMm\":7500,\"yMm\":75000,\"facingCentiDegrees\":0}"
                )
                .Start(runner);

        ulong[] serial = Make(new SerialRunner()).RunAndHash(700, 20);
        Assert.That(Make(new ThreadedRunner(4)).RunAndHash(700, 20), Is.EqualTo(serial));
        var original = Make(new ThreadedRunner(4));
        original.Step(613);
        byte[] snapshot = original.CaptureSnapshot();
        var restored = Make(new ThreadedRunner(3));
        restored.Step(41);
        restored.RestoreSnapshot(snapshot);
        restored.Step(700 - 613);
        Assert.That(restored.StateHash(), Is.EqualTo(serial[^1]));
    }

    [Test]
    public void RidgesAndSprings_AreRefusedInTheMapsSettledWater_ChannelsAndBouldersAreNot()
    {
        // m3-exit starts with its river flowing (water.bin); S1 D5 keeps ridges out of it, and springs need dry ground.
        var map = RepoFiles.LoadRepoMap(RepoData.Root, "m3-exit");
        string Across(string piece, string extra) =>
            "{\"id\":\"p\",\"piece\":\"" + piece + "\",\"points\":[[220000,450000],[260000,450000]]" + extra + "}";
        string Plan(string terrain) =>
            "{\"version\":0,\"map\":\"m3-exit\",\"army\":[],\"terrain\":[" + terrain + "],\"casts\":[]}";

        var error = Assert.Throws<DataFormatException>(() =>
            BattlePlan.Load(Plan(Across("ridge", ",\"heightMm\":2000")), map, RepoData.Rules())
        );
        Assert.That(error!.Message, Does.Contain("water"));
        Assert.Throws<DataFormatException>(() =>
            BattlePlan.Load(Plan(Point("s", "spring", 240_000, 450_000, ",\"litresPerSecond\":1000")), map, RepoData.Rules())
        );
        Assert.That(
            BattlePlan.Load(Plan(Across("channel", ",\"widthMm\":3000,\"depthMm\":1000")), map, RepoData.Rules()).Terrain,
            Has.Count.EqualTo(1),
            "cutting into a river is the point of a channel"
        );
    }
}
