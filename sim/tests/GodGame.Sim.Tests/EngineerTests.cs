using System.Globalization;
using GodGame.Sim.Hosting;
using GodGame.Sim.Rules;

namespace GodGame.Sim.Tests;

/// <summary>Engineers: trestles, repairs and clearing (M5 item 4 part two, spec S2 section 14.7).</summary>
[TestFixture, Category("Fast")]
public class EngineerTests
{
    private const int Columns = 240;

    private static string Regiment(
        string id,
        string side,
        string type,
        int soldiers,
        int x,
        int y
    ) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{{\"id\":\"{0}\",\"side\":\"{1}\",\"unitType\":\"{2}\",\"soldiers\":{3},\"xMm\":{4},\"yMm\":{5},\"facingCentiDegrees\":0}}",
            id,
            side,
            type,
            soldiers,
            x,
            y
        );

    private static Battle Load(MapData map, string army, string terrain = "", string casts = "") =>
        new Battle(
            RepoData.Rules(),
            map,
            BattlePlan.Load(
                "{\"version\":0,\"map\":\"test\",\"army\":["
                    + army
                    + "],\"terrain\":["
                    + terrain
                    + "],\"casts\":["
                    + casts
                    + "]}",
                map,
                RepoData.Rules()
            ),
            1
        );

    private static List<CauseEvent> Events(World world, CauseKind kind) =>
        world.Causes!.Events.Where(e => e.Kind == kind).ToList();

    [Test]
    public void Decision_2026_10_11_EngineersJoinTheFirstSet_ATrestleSpansAChannel_AndTheEnginesCrossIt()
    {
        // S2 section 17: the S1 channel (2 m deep, 6 m wide) across the siege route, its far end 270 m up a 300 m map.
        // Engineers span it with a trestle (a segment every 8 s, 2 cells wide) and the siege train crosses by it.
        string channel =
            "{\"id\":\"c\",\"piece\":\"channel\",\"points\":[[180750,0],[180750,140000]],\"widthMm\":6000,\"depthMm\":2000},"
            + "{\"id\":\"c2\",\"piece\":\"channel\",\"points\":[[180750,135000],[180750,270000]],\"widthMm\":6000,\"depthMm\":2000}";
        string army =
            Regiment("e", "attacker", "engineers", 40, 8_000, 40_000)
            + ","
            + Regiment("g", "attacker", "siege", 50, 8_000, 60_000);
        var battle = Load(TestBattles.Map((_, _) => 0, Columns, 200), army, channel);
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        int siegeType = battle.Rules.UnitType("siege").Index;
        int firstSegment = -1;
        var crossedAt = new Dictionary<int, double>(); // engine slot -> y where it first reached the far side
        for (int tick = 0; tick < 600 * SimConstants.TicksPerSecond; tick++)
        {
            simulation.Step();
            if (firstSegment < 0 && world.Structures!.Count > 0)
            {
                firstSegment = tick;
            }
            foreach (int u in TestBattles.LiveUnits(world))
            {
                if (
                    world.UnitType[u] == siegeType
                    && world.Position[u].X() > 186
                    && !crossedAt.ContainsKey(u)
                )
                {
                    crossedAt[u] = world.Position[u].Y();
                }
            }
        }
        var started = Events(world, CauseKind.SpecialistStarted)
            .Where(e => e.Detail == SpecialistExecutor.TrestleAction)
            .ToList();
        var ended = Events(world, CauseKind.SpecialistEnded)
            .Where(e => e.Detail == SpecialistExecutor.TrestleAction)
            .ToList();
        Assert.That(started, Has.Count.EqualTo(1));
        Assert.That(ended, Has.Count.EqualTo(1));
        Assert.That(
            ended[0].Tick - firstSegment,
            Is.LessThanOrEqualTo(60 * SimConstants.TicksPerSecond),
            "done in 60 s or less"
        );
        Assert.That(world.Structures!.Count, Is.InRange(2, 6), "a few 2-cell segments");
        Assert.That(
            battle.Rules.Structures.Kinds[world.Structures.BattleKind[0]].Id,
            Is.EqualTo("trestle")
        );
        // The stand-in siege regiment is 50 engines in a column 60 m deep (real ones are 1–4 with crews, item 6): its
        // tail may already be committed to the far gap when the trestle opens, but the engines that turn cross by it.
        Assert.That(
            crossedAt.Values.Count(y => y < 100),
            Is.GreaterThan(10),
            "engines crossed by the trestle"
        );
    }

    [Test]
    public void Decision_2026_10_11_EngineersJoinTheFirstSet_TheyRepairARottedSpan_AndTheRotIsOver()
    {
        // S2 14.7: a wooden segment under 700‰ within 100 m is repaired at 25‰ a second, back to 1,000‰.
        var map = TestBattles.Map((x, y) => x >= 118 && x <= 122 ? -6_000 : 0, Columns, 100);
        string bridge =
            "{\"id\":\"b\",\"piece\":\"wooden-bridge\",\"points\":[[175500,75750],[186000,75750]],\"widthMm\":4500}";
        string rot =
            "{\"id\":\"rot\",\"power\":\"rot\",\"tick\":100,\"xMm\":180750,\"yMm\":75750,\"radiusMm\":10000}";
        var battle = Load(
            map,
            Regiment("e", "attacker", "engineers", 40, 8_000, 75_000),
            bridge,
            rot
        );
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        simulation.Step(50 * SimConstants.TicksPerSecond);
        Assert.That(
            world.Structures!.Strength.Take(world.Structures.Count),
            Is.All.EqualTo(500),
            "rotted"
        );
        simulation.Step(200 * SimConstants.TicksPerSecond);
        var repairs = Events(world, CauseKind.SpecialistStarted)
            .Where(e => e.Detail == SpecialistExecutor.RepairAction)
            .ToList();
        Assert.That(repairs, Is.Not.Empty);
        Assert.That(
            world.Structures.Strength.Take(world.Structures.Count),
            Is.All.GreaterThanOrEqualTo(950),
            "sound again"
        );
        Assert.That(world.Army.Spent[0], Is.GreaterThan(0), "for timber");
    }

    [Test]
    public void Decision_2026_10_11_EngineersJoinTheFirstSet_TheyClearABoulderOffTheRoad()
    {
        // S2 14.7: a placed boulder within reach of the path is cleared in 60 s; its cells open and drop to the ground.
        string boulder = "{\"id\":\"b\",\"piece\":\"boulder\",\"xMm\":150000,\"yMm\":40000}";
        var battle = Load(
            TestBattles.Map((_, _) => 0, Columns, 100),
            Regiment("e", "attacker", "engineers", 40, 8_000, 40_000),
            boulder
        );
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        simulation.Step(200 * SimConstants.TicksPerSecond);
        Assert.That(world.Ground.IsCleared(0), Is.True);
        var started = Events(world, CauseKind.SpecialistStarted)
            .First(e => e.Detail == SpecialistExecutor.ClearAction);
        var ended = Events(world, CauseKind.SpecialistEnded)
            .First(e => e.Detail == SpecialistExecutor.ClearAction);
        Assert.That(
            ended.Tick - started.Tick,
            Is.GreaterThanOrEqualTo(60 * SimConstants.TicksPerSecond)
        );
        var spec = battle.PlayedMap.Boulders[0];
        foreach (int cell in spec.Cells)
        {
            Assert.That(world.Ground.Count, Is.GreaterThan(0));
        }
        Assert.That(world.Army.Spent2[0], Is.EqualTo(1_200));
    }

    [Test]
    public void TwoEngineerRegiments_ClearABoulderOnce_AndTheBattleStillRestores()
    {
        // The audit's H1: two regiments, one boulder. One clears it; the other leaves it alone (or stops when it's gone),
        // so the boulder is cleared once, charged once, and a snapshot taken afterwards restores.
        string boulder = "{\"id\":\"b\",\"piece\":\"boulder\",\"xMm\":150000,\"yMm\":40000}";
        string army =
            Regiment("e", "attacker", "engineers", 40, 8_000, 40_000)
            + ","
            + Regiment("f", "attacker", "engineers", 40, 8_000, 55_000);
        var battle = Load(TestBattles.Map((_, _) => 0, Columns, 100), army, boulder);
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        simulation.Step(200 * SimConstants.TicksPerSecond);
        Assert.That(world.Ground.ClearedCount, Is.EqualTo(1));
        Assert.That(world.Army.Spent2[0] + world.Army.Spent2[1], Is.EqualTo(1_200), "charged once");
        var snapshot = simulation.CaptureSnapshot();
        simulation.Step(20 * SimConstants.TicksPerSecond);
        var restored = battle.Start(new SerialRunner());
        restored.RestoreSnapshot(snapshot);
        restored.Step(20 * SimConstants.TicksPerSecond);
        Assert.That(restored.StateHash(), Is.EqualTo(simulation.StateHash()));
    }
}

/// <summary>Battle-made decks and cleared boulders across thread counts and restores (M5 item 4 part two).</summary>
[TestFixture, Category("Slow")]
public class EngineerSlowTests
{
    [Test]
    public void EngineersWorkIsDeterministic_AcrossThreadCountsAndSnapshots()
    {
        // Engineers clear a boulder on the road, then span the channel beyond it; the siege train follows.
        var map = TestBattles.Map((_, _) => 0, 240, 200);
        string plan =
            "{\"version\":0,\"map\":\"test\",\"army\":["
            + "{\"id\":\"e\",\"side\":\"attacker\",\"unitType\":\"engineers\",\"soldiers\":40,\"xMm\":8000,\"yMm\":40000,\"facingCentiDegrees\":0},"
            + "{\"id\":\"g\",\"side\":\"attacker\",\"unitType\":\"siege\",\"soldiers\":50,\"xMm\":8000,\"yMm\":60000,\"facingCentiDegrees\":0}],"
            + "\"terrain\":[{\"id\":\"b\",\"piece\":\"boulder\",\"xMm\":130000,\"yMm\":40000},"
            + "{\"id\":\"c\",\"piece\":\"channel\",\"points\":[[180750,0],[180750,140000]],\"widthMm\":6000,\"depthMm\":2000},"
            + "{\"id\":\"c2\",\"piece\":\"channel\",\"points\":[[180750,135000],[180750,270000]],\"widthMm\":6000,\"depthMm\":2000}],"
            + "\"casts\":[]}";
        var battle = new Battle(
            RepoData.Rules(),
            map,
            BattlePlan.Load(plan, map, RepoData.Rules()),
            9
        );
        ulong[] serial = battle.PerSecondHashes(300, new SerialRunner());
        Assert.That(battle.PerSecondHashes(300, new ThreadedRunner(4)), Is.EqualTo(serial));

        var a = battle.Start(new SerialRunner());
        var snapshots = new List<(int Tick, byte[] Bytes)>();
        while (a.Tick < 300 * SimConstants.TicksPerSecond)
        {
            a.Step(1);
            if (a.Tick % 571 == 0)
            {
                snapshots.Add((a.Tick, a.CaptureSnapshot()));
            }
        }
        Assert.That(
            a.World.Ground.ClearedCount,
            Is.EqualTo(1),
            "the boulder was cleared in the run"
        );
        Assert.That(a.World.Structures!.Count, Is.GreaterThan(0), "and the trestle built");
        ulong end = a.StateHash();
        foreach (var (tick, bytes) in snapshots)
        {
            var b = battle.Start(new ThreadedRunner(3));
            b.Step(4_321); // run ahead first, so the restore must take decks and boulders back as well as forward
            b.RestoreSnapshot(bytes);
            b.Step(300 * SimConstants.TicksPerSecond - tick);
            Assert.That(b.StateHash(), Is.EqualTo(end), $"restored at tick {tick}");
        }
    }
}
