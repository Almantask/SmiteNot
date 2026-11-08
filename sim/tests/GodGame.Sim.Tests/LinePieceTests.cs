using System.Globalization;
using GodGame.Sim.Hosting;
using GodGame.Sim.Rules;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim.Tests;

/// <summary>Hedgerows and drystone walls as navigation blockers (M5 item 2, spec S2 section 13.3).</summary>
[TestFixture, Category("Fast")]
public class LinePieceTests
{
    // A flat 360 × 150 m map with 15 m zones at the west and east edges: pieces between x = 115 and 245 m keep the
    // 100 m buffer. Lines run through cell centres (x = 180,750 mm is column 120); a line on a cell boundary takes
    // both cells beside it.
    private const int Columns = 240;
    private const int Rows = 100;

    private static MapData Flat() => TestBattles.Map((_, _) => 0, Columns, Rows);

    private static string Plan(string terrain, string army = "") =>
        "{\"version\":0,\"map\":\"test\",\"army\":["
        + army
        + "],\"terrain\":["
        + terrain
        + "],\"casts\":[]}";

    private static string Line(string id, string piece, params (int X, int Y)[] points) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{{\"id\":\"{0}\",\"piece\":\"{1}\",\"points\":[{2}]}}",
            id,
            piece,
            string.Join(",", points.Select(p => "[" + p.X + "," + p.Y + "]"))
        );

    private static Battle Load(MapData map, string terrain, string army = "") =>
        new Battle(
            RepoData.Rules(),
            map,
            BattlePlan.Load(Plan(terrain, army), map, RepoData.Rules()),
            1
        );

    private static CostFields Costs(Battle battle) =>
        new CostFields(new Terrain(battle.PlayedMap), battle.Rules, new SerialRunner());

    [Test]
    public void Decision_2026_11_07_ArmiesLoadLaunchesSpecialists_HedgesAndWallsLetFootClimbOver_HorseAndWheeledNeedAGap()
    {
        // S2 13.3: foot cross a hedgerow at 250‰ and a drystone wall at 300‰; horse and wheeled can't cross either.
        // A hedge runs north from the map's south edge, a wall south from its north edge, with the gap between.
        var battle = Load(
            Flat(),
            string.Join(
                ",",
                Line("h", "hedgerow", (150_750, 0), (150_750, 60_000)),
                Line("w", "drystone-wall", (210_750, 149_000), (210_750, 90_000))
            )
        );
        var costs = Costs(battle);
        var open = Costs(
            new Battle(
                RepoData.Rules(),
                Flat(),
                BattlePlan.Load(Plan(""), Flat(), RepoData.Rules()),
                1
            )
        );
        int hedge = 100; // x = 150 m
        int wall = 140; // x = 210 m

        Assert.That(
            costs.Cost(MovementClass.Foot, hedge, 20),
            Is.EqualTo(open.Cost(MovementClass.Foot, hedge, 20) * 1000 / 248)
        );
        Assert.That(
            costs.Cost(MovementClass.Foot, wall, 80),
            Is.EqualTo(open.Cost(MovementClass.Foot, wall, 80) * 1000 / 300)
        );
        foreach (var c in new[] { MovementClass.Horse, MovementClass.Wheeled })
        {
            Assert.That(
                costs.Cost(c, hedge, 20),
                Is.EqualTo(CostFields.Impassable),
                $"{c} at the hedge"
            );
            Assert.That(
                costs.Cost(c, wall, 80),
                Is.EqualTo(CostFields.Impassable),
                $"{c} at the wall"
            );
            Assert.That(
                costs.Cost(c, hedge, 70),
                Is.Not.EqualTo(CostFields.Impassable),
                $"{c} past the hedge's end"
            );
        }
        // One cell wide along the grid, and no height edit.
        Assert.That(
            costs.Cost(MovementClass.Horse, hedge - 1, 20),
            Is.Not.EqualTo(CostFields.Impassable)
        );
        Assert.That(
            costs.Cost(MovementClass.Horse, hedge + 1, 20),
            Is.Not.EqualTo(CostFields.Impassable)
        );
        Assert.That(battle.PlayedMap.HeightMm.All(h => h == 0), Is.True);
    }

    [Test]
    public void ADiagonalLine_LeavesNoCornerAHorseCouldSlipThrough()
    {
        var battle = Load(Flat(), Line("h", "hedgerow", (140_000, 20_000), (215_000, 130_000)));
        var costs = Costs(battle);
        bool Closed(int x, int y) => costs.Cost(MovementClass.Horse, x, y) == CostFields.Impassable;
        int checkedPairs = 0;
        for (int y = 0; y + 1 < Rows; y++)
        {
            for (int x = 0; x + 1 < Columns; x++)
            {
                // Two closed cells touching only at a corner need one of the two cells beside that corner closed too.
                if (Closed(x, y) && Closed(x + 1, y + 1) && !Closed(x + 1, y) && !Closed(x, y + 1))
                {
                    Assert.Fail($"a corner gap between ({x},{y}) and ({x + 1},{y + 1})");
                }
                if (Closed(x + 1, y) && Closed(x, y + 1) && !Closed(x, y) && !Closed(x + 1, y + 1))
                {
                    Assert.Fail($"a corner gap between ({x + 1},{y}) and ({x},{y + 1})");
                }
                checkedPairs += Closed(x, y) ? 1 : 0;
            }
        }
        Assert.That(checkedPairs, Is.GreaterThan(80), "the line was stamped");
    }

    [Test]
    public void Decision_2026_10_09_NoPlacementMayFullyTrapEitherArmy_AHedgeAcrossTheWholeMapIsRefused()
    {
        // Foot could climb it, but horse and wheeled would have no way through: refused, naming the class.
        var map = Flat();
        var error = Assert.Throws<DataFormatException>(() =>
            BattlePlan.Load(
                Plan(Line("h", "hedgerow", (180_000, 0), (180_000, 149_999))),
                map,
                RepoData.Rules()
            )
        );
        Assert.That(error!.Message, Does.Contain("close the last route").And.Contain("Horse"));
        Assert.That(
            BattlePlan
                .Load(
                    Plan(Line("h", "hedgerow", (180_750, 0), (180_750, 110_000))),
                    map,
                    RepoData.Rules()
                )
                .Terrain,
            Has.Count.EqualTo(1),
            "with a 40 m gap at the north end it stands"
        );
    }

    [TestCase(
        "{\"id\":\"h\",\"piece\":\"hedgerow\",\"points\":[[150000,75000],[155000,75000]]}",
        "long"
    )]
    [TestCase("{\"id\":\"h\",\"piece\":\"hedgerow\",\"points\":[[150000,75000]]}", "points")]
    [TestCase(
        "{\"id\":\"h\",\"piece\":\"hedgerow\",\"points\":[[150000,75000],[200000,75000]],\"heightMm\":2000}",
        "heightMm"
    )]
    [TestCase(
        "{\"id\":\"b\",\"piece\":\"boulder\",\"xMm\":180000,\"yMm\":75000},{\"id\":\"w\",\"piece\":\"drystone-wall\",\"points\":[[170000,76000],[200000,76000]]}",
        "on a boulder"
    )]
    [TestCase(
        "{\"id\":\"w\",\"piece\":\"drystone-wall\",\"points\":[[20000,75000],[60000,75000]]}",
        "deployment zone"
    )]
    public void ALineThatCantBePlaced_IsRefusedWhilePlanning(string terrain, string why)
    {
        var error = Assert.Throws<DataFormatException>(() =>
            BattlePlan.Load(Plan(terrain), Flat(), RepoData.Rules())
        );
        Assert.That(error!.Message, Does.Contain(why));
    }

    [Test]
    public void Decision_2026_11_07_ArmiesLoadLaunchesSpecialists_InfantryClimbTheWall_CavalryRideRoundThroughTheGap()
    {
        // A wall from the south edge to y = 110 m at x = 180 m, a 40 m gap north of it. Attacking infantry from the
        // south-west climb straight over (slowly); attacking cavalry beside them must ride round through the gap.
        string wall = Line("w", "drystone-wall", (180_750, 0), (180_750, 110_000));
        string army =
            "{\"id\":\"i\",\"side\":\"attacker\",\"unitType\":\"infantry\",\"soldiers\":50,\"xMm\":8000,\"yMm\":30000,\"facingCentiDegrees\":0},"
            + "{\"id\":\"c\",\"side\":\"attacker\",\"unitType\":\"cavalry\",\"soldiers\":50,\"xMm\":8000,\"yMm\":60000,\"facingCentiDegrees\":0}";
        var battle = Load(Flat(), wall, army);
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        var terrain = new Terrain(battle.PlayedMap);
        int infantryType = battle.Rules.UnitType("infantry").Index; // the plan orders regiments by id
        double infantryCrossedAtY = -1;
        double cavalryCrossedAtY = -1;
        int infantryOnWall = 0;
        for (
            int tick = 0;
            tick < 300 * SimConstants.TicksPerSecond
                && (infantryCrossedAtY < 0 || cavalryCrossedAtY < 0);
            tick++
        )
        {
            simulation.Step();
            foreach (int u in TestBattles.LiveUnits(world))
            {
                terrain.CellOf(world.Position[u], out int x, out int y);
                bool infantry = world.UnitType[u] == infantryType;
                if (x == 120 && infantry)
                {
                    infantryOnWall++;
                }
                if (x > 120 && world.FormationSlot[u] == 0)
                {
                    if (infantry && infantryCrossedAtY < 0)
                    {
                        infantryCrossedAtY = world.Position[u].Y();
                    }
                    else if (!infantry && cavalryCrossedAtY < 0)
                    {
                        cavalryCrossedAtY = world.Position[u].Y();
                    }
                }
            }
        }
        Assert.That(
            infantryCrossedAtY,
            Is.GreaterThan(0).And.LessThan(105),
            "the infantry went over the wall"
        );
        Assert.That(infantryOnWall, Is.GreaterThan(0));
        Assert.That(cavalryCrossedAtY, Is.GreaterThan(110), "the cavalry used the gap");
        foreach (
            int u in TestBattles.LiveUnits(world).Where(u => world.UnitType[u] != infantryType)
        )
        {
            terrain.CellOf(world.Position[u], out int x, out int y);
            Assert.That(x == 120 && y * 1.5 < 110, Is.False, "no rider stands on the wall");
        }
    }
}

/// <summary>A battle with crossing lines on any thread count and across a restore (the audit's ask).</summary>
[TestFixture, Category("Slow")]
public class LinePieceSlowTests
{
    [Test]
    public void LinesAreDeterministic_AcrossThreadCountsPieceOrderAndSnapshots()
    {
        var map = TestBattles.Map((_, _) => 0, 240, 100, 60);
        string Pieces(bool reversed)
        {
            var lines = new[]
            {
                "{\"id\":\"a\",\"piece\":\"drystone-wall\",\"points\":[[180750,0],[180750,100000]]}",
                "{\"id\":\"b\",\"piece\":\"hedgerow\",\"points\":[[165000,30000],[190000,60000],[195000,90000]]}",
                "{\"id\":\"c\",\"piece\":\"hedgerow\",\"points\":[[170000,40000],[195000,45000]]}",
            };
            return string.Join(",", reversed ? lines.Reverse() : lines);
        }
        string army = string.Join(
            ",",
            Enumerable
                .Range(0, 4)
                .Select(i =>
                    "{\"id\":\"a"
                    + i
                    + "\",\"side\":\"attacker\",\"unitType\":\""
                    + (i == 3 ? "cavalry" : "infantry")
                    + "\",\"soldiers\":200,\"xMm\":40000,\"yMm\":"
                    + (20000 + i * 35000)
                    + ",\"facingCentiDegrees\":0}"
                )
                .Concat(
                    Enumerable
                        .Range(0, 3)
                        .Select(i =>
                            "{\"id\":\"d"
                            + i
                            + "\",\"side\":\"defender\",\"unitType\":\""
                            + (i == 1 ? "ranged" : "infantry")
                            + "\",\"soldiers\":200,\"xMm\":320000,\"yMm\":"
                            + (30000 + i * 45000)
                            + ",\"facingCentiDegrees\":18000}"
                        )
                )
        );
        Battle Make(bool reversed) =>
            new Battle(
                RepoData.Rules(),
                map,
                BattlePlan.Load(
                    "{\"version\":0,\"map\":\"test\",\"army\":["
                        + army
                        + "],\"terrain\":["
                        + Pieces(reversed)
                        + "],\"casts\":[]}",
                    map,
                    RepoData.Rules()
                ),
                3
            );
        var battle = Make(false);
        Assert.That(Make(true).Id, Is.EqualTo(battle.Id), "piece order is not part of the battle");
        Assert.That(Make(true).PlayedMap.PieceSpeed, Is.EqualTo(battle.PlayedMap.PieceSpeed));

        ulong[] serial = battle.PerSecondHashes(150, new SerialRunner());
        Assert.That(battle.PerSecondHashes(150, new ThreadedRunner(4)), Is.EqualTo(serial));

        var a = battle.Start(new SerialRunner());
        var snapshots = new List<(int Tick, byte[] Bytes)>();
        for (int second = 1; second <= 150; second++)
        {
            a.Step(SimConstants.TicksPerSecond);
            if (second % 30 == 0)
            {
                snapshots.Add((a.Tick, a.CaptureSnapshot()));
            }
        }
        ulong end = a.StateHash();
        foreach (var (tick, bytes) in snapshots)
        {
            var b = battle.Start(new ThreadedRunner(3));
            b.Step(517);
            b.RestoreSnapshot(bytes);
            b.Step(150 * SimConstants.TicksPerSecond - tick);
            Assert.That(b.StateHash(), Is.EqualTo(end), $"restored at tick {tick}");
        }
    }
}
