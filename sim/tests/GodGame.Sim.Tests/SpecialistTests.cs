using System.Globalization;
using GodGame.Sim.Hosting;
using GodGame.Sim.Rules;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim.Tests;

/// <summary>The shared specialist executor, Spademen and Fascine Carriers (M5 item 3, spec S2 section 14).</summary>
[TestFixture, Category("Fast")]
public class SpecialistTests
{
    // A flat 360 × 150 m map with 15 m zones at the west and east edges. x = 180,750 mm is column 120.
    private const int Columns = 240;
    private const int Rows = 100;

    private static MapData Flat(int rows = Rows) => TestBattles.Map((_, _) => 0, Columns, rows);

    private static string Regiment(
        string id,
        string side,
        string type,
        int soldiers,
        int x,
        int y,
        string extra = ""
    ) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{{\"id\":\"{0}\",\"side\":\"{1}\",\"unitType\":\"{2}\",\"soldiers\":{3},\"xMm\":{4},\"yMm\":{5},\"facingCentiDegrees\":0{6}}}",
            id,
            side,
            type,
            soldiers,
            x,
            y,
            extra
        );

    private static string Plan(string army, string terrain = "") =>
        "{\"version\":0,\"map\":\"test\",\"army\":["
        + army
        + "],\"terrain\":["
        + terrain
        + "],\"casts\":[]}";

    private static Battle Load(MapData map, string army, string terrain = "") =>
        new Battle(
            RepoData.Rules(),
            map,
            BattlePlan.Load(Plan(army, terrain), map, RepoData.Rules()),
            1
        );

    private static List<CauseEvent> Events(World world, CauseKind kind) =>
        world.Causes!.Events.Where(e => e.Kind == kind).ToList();

    // The Spademen's line: 30 m north–south at x = 300,750 mm (column 200), 45 m west of the defender zone.
    private const string Ditch = ",\"ditch\":[[300750,60000],[300750,90000]]";

    [Test]
    public void Decision_2026_11_07_ArmiesLoadLaunchesSpecialists_SpademenDigTheLineTheyWereGiven_ADitchAndABankOnTheirSide()
    {
        // S2 14.3, D5: a 1-cell ditch 1,250 mm deep, a 1-cell berm, a 1-cell bank 500 mm high on the regiment's (east)
        // side; 3 m a section, a section per 6 s with the whole regiment: 30 m in about 60 s once they're there.
        var battle = Load(
            Flat(),
            Regiment("s", "defender", "spademen", 100, 352_000, 75_000, Ditch)
        );
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        int firstSection = -1;
        for (int tick = 0; tick < 150 * SimConstants.TicksPerSecond; tick++)
        {
            simulation.Step();
            if (firstSection < 0 && world.Ground.Count > 0)
            {
                firstSection = tick;
            }
        }

        int Height(int x, int y)
        {
            for (int i = 0; i < world.Ground.Count; i++)
            {
                if (world.Ground.CellAt(i) == y * Columns + x)
                {
                    return world.Ground.HeightAt(i);
                }
            }
            return 0;
        }
        for (int y = 41; y <= 59; y++)
        {
            Assert.That(Height(200, y), Is.EqualTo(-1_250), $"ditch at row {y}");
            Assert.That(Height(201, y), Is.EqualTo(0), $"berm at row {y}");
            Assert.That(Height(202, y), Is.EqualTo(500), $"bank at row {y}");
            Assert.That(Height(199, y), Is.EqualTo(0), "nothing on the far side");
        }
        Assert.That(Height(200, 30), Is.EqualTo(0), "only the drawn line");
        Assert.That(Events(world, CauseKind.SpecialistStarted).Single().Subject, Is.EqualTo(0));
        var ended = Events(world, CauseKind.SpecialistEnded).Single();
        // Ten sections of 3 m at 6 s each: the first lands after 6 s of digging, the last 54 s later.
        Assert.That(
            ended.Tick - firstSection,
            Is.InRange(50 * SimConstants.TicksPerSecond, 62 * SimConstants.TicksPerSecond)
        );
        Assert.That(world.Army.Job[0], Is.EqualTo(SpecialistJob.Done));
    }

    [Test]
    public void Decision_2026_11_07_ArmiesLoadLaunchesSpecialists_TheDitchStopsHorseAndWheeled_FootClimbThrough()
    {
        // The ditch's 833‰ step stops horse (700‰) and wheeled (250‰), not foot (1,000‰); the berm keeps the climb from
        // the ditch floor to the bank top within foot's reach.
        var battle = Load(
            Flat(),
            Regiment("s", "defender", "spademen", 100, 352_000, 75_000, Ditch)
        );
        var simulation = battle.Start(new SerialRunner());
        simulation.Step(150 * SimConstants.TicksPerSecond);
        var terrain = new Terrain(battle.PlayedMap);
        var ground = simulation.World.Ground;
        for (int i = 0; i < ground.Count; i++)
        {
            int cell = ground.CellAt(i);
            terrain.SetHeight(cell % Columns, cell / Columns, ground.HeightAt(i));
        }
        var costs = new CostFields(terrain, battle.Rules, new SerialRunner());
        bool Crosses(MovementClass c) =>
            Enumerable.Range(198, 6).All(x => costs.Cost(c, x, 50) != CostFields.Impassable);
        Assert.That(Crosses(MovementClass.Foot), Is.True);
        Assert.That(Crosses(MovementClass.Horse), Is.False);
        Assert.That(Crosses(MovementClass.Wheeled), Is.False);
    }

    [Test]
    public void Decision_2026_11_07_ArmiesLoadLaunchesSpecialists_FascinesFillOneCrossingThroughAChannel_TheEnginesCrossByIt()
    {
        // S2 section 17: the S1 channel (2 m deep, 6 m wide) across the siege route on a 300 m deep map, leaving a gap at
        // the far north end so the plan is legal (the detour stays under the 2× cap). Fascine Carriers on their way east
        // fill one crossing 4.5 m wide in under a minute (2 bundles a second, 400 mm each); the siege train, slowly
        // heading for the far gap, turns for the crossing once it's open.
        string channel =
            "{\"id\":\"c\",\"piece\":\"channel\",\"points\":[[180750,0],[180750,140000]],\"widthMm\":6000,\"depthMm\":2000},"
            + "{\"id\":\"c2\",\"piece\":\"channel\",\"points\":[[180750,135000],[180750,270000]],\"widthMm\":6000,\"depthMm\":2000}";
        string army = string.Join(
            ",",
            Regiment("f", "attacker", "fascine-carriers", 100, 8_000, 40_000),
            Regiment("g", "attacker", "siege", 50, 8_000, 60_000)
        );
        var battle = Load(Flat(rows: 200), army, channel);
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        int firstFill = -1;
        double siegeCrossedAtY = -1;
        int siegeType = battle.Rules.UnitType("siege").Index;
        for (int tick = 0; tick < 600 * SimConstants.TicksPerSecond && siegeCrossedAtY < 0; tick++)
        {
            simulation.Step();
            if (firstFill < 0 && world.Ground.FillCount > 0)
            {
                firstFill = tick;
            }
            foreach (int u in TestBattles.LiveUnits(world))
            {
                if (world.UnitType[u] == siegeType && world.Position[u].X() > 186)
                {
                    siegeCrossedAtY = world.Position[u].Y();
                    break;
                }
            }
        }
        var ended = Events(world, CauseKind.SpecialistEnded);
        Assert.That(firstFill, Is.GreaterThan(0), "the fascines filled something");
        Assert.That(ended, Is.Not.Empty, "the crossing was finished");
        Assert.That(
            ended[0].Tick - firstFill,
            Is.LessThanOrEqualTo(60 * SimConstants.TicksPerSecond),
            "in 60 s or less"
        );
        Assert.That(
            siegeCrossedAtY,
            Is.GreaterThan(0).And.LessThan(100),
            "the engines crossed by the fill, not the far gap"
        );

        // One crossing about 4.5 m wide: filled cells span 3 rows; the rest of the channel stays a channel.
        var rows = Enumerable
            .Range(0, world.Ground.FillCount)
            .Where(i => world.Ground.FillMmAt(i) > 0)
            .Select(i => world.Ground.FillCellAt(i) / Columns)
            .Distinct()
            .ToList();
        Assert.That(rows.Max() - rows.Min() + 1, Is.LessThanOrEqualTo(4));
        Assert.That(battle.PlayedMap.HeightMm[80 * Columns + 120], Is.EqualTo(-2_000));
        Assert.That(world.Ground.Count, Is.LessThan(30));
        Assert.That(
            world.Army.Spent[0],
            Is.GreaterThan(20).And.LessThanOrEqualTo(100),
            "one bundle per living carrier"
        );
    }

    [Test]
    public void Decision_2026_10_10_WorldShapingIsUnitSpecific_ARoutedSpecialistLosesItsCharges()
    {
        var battle = Load(
            Flat(),
            Regiment("s", "defender", "spademen", 100, 352_000, 75_000, Ditch)
        );
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        simulation.Step(40 * SimConstants.TicksPerSecond);
        Assert.That(world.Army.Job[0], Is.EqualTo(SpecialistJob.Active));
        world.RegimentMorale[0] = 100;
        simulation.Step(5 * SimConstants.TicksPerSecond);
        Assert.That(world.Army.Order[0], Is.EqualTo(RegimentOrder.Rout));
        Assert.That(world.Army.Job[0], Is.EqualTo(SpecialistJob.Lost));
        int edits = world.Ground.Count;
        simulation.Step(30 * SimConstants.TicksPerSecond);
        Assert.That(world.Ground.Count, Is.EqualTo(edits), "no more digging, even after a rally");
    }

    [TestCase("attacker", "spademen", "", "can't field")]
    [TestCase("defender", "fascine-carriers", "", "can't field")]
    [TestCase("defender", "infantry", Ditch, "only Spademen")]
    [TestCase(
        "defender",
        "spademen",
        ",\"ditch\":[[300750,50000],[300750,100000]]",
        "dig 30000 mm"
    )]
    [TestCase("defender", "spademen", ",\"ditch\":[[230750,60000],[230750,80000]]", "buffer")]
    [TestCase(
        "defender",
        "spademen",
        ",\"ditch\":[[300750,10000],[300750,30000]]",
        "from the regiment"
    )]
    [TestCase("defender", "spademen", ",\"ditch\":[[300750,60000]]", "2 to 3 points")]
    public void ASpecialistThatCantBeFielded_IsRefusedWhilePlanning(
        string side,
        string type,
        string extra,
        string why
    )
    {
        int x = side == "attacker" ? 8_000 : 352_000;
        var error = Assert.Throws<DataFormatException>(() =>
            BattlePlan.Load(
                Plan(Regiment("r", side, type, 100, x, 75_000, extra)),
                Flat(),
                RepoData.Rules()
            )
        );
        Assert.That(error!.Message, Does.Contain(why));
    }
}

/// <summary>Specialist scenarios that run water for minutes (M5 item 3).</summary>
[TestFixture, Category("Slow")]
public class SpecialistSlowTests
{
    [Test]
    public void Decision_2026_11_07_ArmiesLoadLaunchesSpecialists_SpademensDitchBecomesAMoatInTheRain()
    {
        // S2 section 17: a valley floor sloping 20‰ toward a 30 m Spademen ditch in the defender's buffer (the ground
        // rises again east of it); Rain 100 m upslope at 0:30. The ditch fills past 1,000 mm, lethal for foot (S1).
        static int Valley(int x, int y) => System.Math.Abs(x - 200) * 30;
        var map = TestBattles.Map(Valley, 240, 100);
        string plan =
            "{\"version\":0,\"map\":\"test\",\"army\":[{\"id\":\"s\",\"side\":\"defender\",\"unitType\":\"spademen\",\"soldiers\":100,"
            + "\"xMm\":352000,\"yMm\":75000,\"facingCentiDegrees\":0,\"ditch\":[[300750,60000],[300750,90000]]}],\"terrain\":[],"
            + "\"casts\":[{\"id\":\"rain\",\"power\":\"rain\",\"tick\":600,\"xMm\":150000,\"yMm\":75000,\"radiusMm\":40000}]}";
        var battle = new Battle(
            RepoData.Rules(),
            map,
            BattlePlan.Load(plan, map, RepoData.Rules()),
            1
        );
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        var depth = world.Fields!.Get(WaterModel.Depth);
        int deepest = 0;
        int moatTick = -1;
        for (int tick = 0; tick < 300 * SimConstants.TicksPerSecond && moatTick < 0; tick++)
        {
            simulation.Step();
            if (tick % SimConstants.TicksPerSecond == 0)
            {
                int d = depth.Get(200, 50);
                deepest = System.Math.Max(deepest, d);
                if (d >= 1_000_000 && world.Army.Job[0] == SpecialistJob.Done)
                {
                    moatTick = tick;
                }
            }
        }
        Assert.That(world.Casts!.Status[0], Is.EqualTo(CastStatus.Fired), "the rain fell");
        Assert.That(
            moatTick,
            Is.GreaterThan(0),
            $"the dug ditch held 1,000 mm (deepest {deepest} µm)"
        );
        int codes = world.Fields!.Get(FieldSpeed.FieldId).Get(200, 50);
        Assert.That(
            FieldSpeed.Code(codes, MovementClass.Foot),
            Is.EqualTo(FieldSpeed.Swept),
            "a moat: lethal for foot"
        );
    }

    [Test]
    public void GroundEditsAreDeterministic_AcrossThreadCountsAndSnapshotsTakenMidFill()
    {
        var map = TestBattles.Map((_, _) => 0, 240, 200);
        string plan =
            "{\"version\":0,\"map\":\"test\",\"army\":["
            + "{\"id\":\"f\",\"side\":\"attacker\",\"unitType\":\"fascine-carriers\",\"soldiers\":100,\"xMm\":8000,\"yMm\":40000,\"facingCentiDegrees\":0},"
            + "{\"id\":\"g\",\"side\":\"attacker\",\"unitType\":\"siege\",\"soldiers\":50,\"xMm\":8000,\"yMm\":60000,\"facingCentiDegrees\":0},"
            + "{\"id\":\"s\",\"side\":\"defender\",\"unitType\":\"spademen\",\"soldiers\":100,\"xMm\":352000,\"yMm\":75000,\"facingCentiDegrees\":0,\"ditch\":[[300750,60000],[300750,90000]]}],"
            + "\"terrain\":[{\"id\":\"c\",\"piece\":\"channel\",\"points\":[[180750,0],[180750,140000]],\"widthMm\":6000,\"depthMm\":2000},"
            + "{\"id\":\"c2\",\"piece\":\"channel\",\"points\":[[180750,135000],[180750,270000]],\"widthMm\":6000,\"depthMm\":2000}],"
            + "\"casts\":[]}";
        var battle = new Battle(
            RepoData.Rules(),
            map,
            BattlePlan.Load(plan, map, RepoData.Rules()),
            5
        );
        ulong[] serial = battle.PerSecondHashes(240, new SerialRunner());
        Assert.That(battle.PerSecondHashes(240, new ThreadedRunner(4)), Is.EqualTo(serial));

        var a = battle.Start(new SerialRunner());
        var snapshots = new List<(int Tick, byte[] Bytes)>();
        for (int second = 1; second <= 240; second++)
        {
            a.Step(SimConstants.TicksPerSecond);
            if (second % 20 == 0)
            {
                snapshots.Add((a.Tick, a.CaptureSnapshot()));
            }
        }
        Assert.That(a.World.Ground.FillCount, Is.GreaterThan(0), "the fill happened in the run");
        Assert.That(
            a.World.Ground.Count,
            Is.GreaterThan(a.World.Ground.FillCount),
            "and the ditch"
        );
        ulong end = a.StateHash();
        foreach (var (tick, bytes) in snapshots)
        {
            var b = battle.Start(new ThreadedRunner(3));
            b.Step(4_321); // run ahead, so the restore must put the ground back as well as forward
            b.RestoreSnapshot(bytes);
            b.Step(240 * SimConstants.TicksPerSecond - tick);
            Assert.That(b.StateHash(), Is.EqualTo(end), $"restored at tick {tick}");
        }
    }

    private static Battle FascineBattle()
    {
        var map = TestBattles.Map((_, _) => 0, 240, 200);
        string plan =
            "{\"version\":0,\"map\":\"test\",\"army\":["
            + "{\"id\":\"f\",\"side\":\"attacker\",\"unitType\":\"fascine-carriers\",\"soldiers\":100,\"xMm\":8000,\"yMm\":40000,\"facingCentiDegrees\":0}],"
            + "\"terrain\":[{\"id\":\"c\",\"piece\":\"channel\",\"points\":[[180750,0],[180750,140000]],\"widthMm\":6000,\"depthMm\":2000},"
            + "{\"id\":\"c2\",\"piece\":\"channel\",\"points\":[[180750,135000],[180750,270000]],\"widthMm\":6000,\"depthMm\":2000}],"
            + "\"casts\":[]}";
        return new Battle(RepoData.Rules(), map, BattlePlan.Load(plan, map, RepoData.Rules()), 5);
    }

    [Test]
    public void ARestoreRightAtTheFillsTrigger_PlaysOnLikeTheLiveRun()
    {
        // The audit's H1: the regiment AI step reads each regiment's flow field before navigation runs, so a restore
        // must rebuild the assignment too. Snapshots every AI tick around the trigger, each restored into a fresh run.
        var battle = FascineBattle();
        var live = battle.Start(new SerialRunner());
        var snapshots = new List<(int Tick, byte[] Bytes)>();
        int trigger = -1;
        while (live.Tick < 400 * SimConstants.TicksPerSecond && trigger < 0)
        {
            live.Step();
            trigger = live
                .World.Causes!.Events.Where(e => e.Kind == CauseKind.SpecialistStarted)
                .Select(e => e.Tick)
                .DefaultIfEmpty(-1)
                .First();
        }
        Assert.That(trigger, Is.GreaterThan(0), "the carriers found the channel");
        var replay = battle.Start(new SerialRunner());
        while (replay.Tick < trigger + 25)
        {
            if (replay.Tick >= trigger - 25 && replay.Tick % TickOrder.RegimentAiRate.Period == 0)
            {
                snapshots.Add((replay.Tick, replay.CaptureSnapshot()));
            }
            replay.Step();
        }
        int end = trigger + 60 * SimConstants.TicksPerSecond;
        replay.Step(end - replay.Tick);
        ulong expected = replay.StateHash();
        foreach (var (tick, bytes) in snapshots)
        {
            var b = battle.Start(new SerialRunner());
            b.RestoreSnapshot(bytes);
            b.Step(end - tick);
            Assert.That(
                b.StateHash(),
                Is.EqualTo(expected),
                $"restored at tick {tick} (trigger {trigger})"
            );
        }
    }

    [Test]
    public void MovingTheDitchInTheTimeline_ReplaysTheBattleFromTheStart()
    {
        // The audit's H3: a ditch-only edit is an army-layer change.
        var map = TestBattles.Map((_, _) => 0, 240, 100);
        Battle With(string ditch) =>
            new Battle(
                RepoData.Rules(),
                map,
                BattlePlan.Load(
                    "{\"version\":0,\"map\":\"test\",\"army\":[{\"id\":\"s\",\"side\":\"defender\",\"unitType\":\"spademen\",\"soldiers\":100,"
                        + "\"xMm\":352000,\"yMm\":75000,\"facingCentiDegrees\":0,\"ditch\":"
                        + ditch
                        + "}],\"terrain\":[],\"casts\":[]}",
                    map,
                    RepoData.Rules()
                ),
                1
            );
        var first = With("[[300750,60000],[300750,90000]]");
        var moved = With("[[306750,60000],[306750,90000]]");
        Assert.That(Timeline.EarliestChange(first.Plan, moved.Plan), Is.Zero);
        var timeline = new Timeline(first, new SerialRunner(), new KeyframeStore(long.MaxValue));
        timeline.AdvanceTo(90 * SimConstants.TicksPerSecond);
        Assert.That(timeline.Edit(moved.Plan), Is.Zero);
        timeline.AdvanceTo(150 * SimConstants.TicksPerSecond);
        var fresh = moved.Start(new SerialRunner());
        fresh.Step(150 * SimConstants.TicksPerSecond);
        Assert.That(timeline.Simulation.StateHash(), Is.EqualTo(fresh.StateHash()));
    }
}
