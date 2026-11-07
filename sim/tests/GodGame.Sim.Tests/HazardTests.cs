using GodGame.Sim.Hosting;
using GodGame.Sim.Rules;

namespace GodGame.Sim.Tests;

/// <summary>Swept, drowning, bank exit and teetering (M3 item 6, spec S1 section 4).</summary>
[TestFixture, Category("Fast")]
public class HazardTests
{
    private const int Columns = 60;
    private const int Rows = 30;

    /// <summary>A basin 1.5 m deep with steep sides: cells 44 to 58 by 4 to 26 (66 to 88.5 m by 6 to 40 m).</summary>
    private static bool InBasin(int x, int y) => x >= 44 && x <= 58 && y >= 4 && y <= 26;

    private static int BasinHeight(int x, int y) => InBasin(x, y) ? -1_500 : 0;

    private static void Flood(Simulation simulation, int depthUm)
    {
        var water = simulation.World.Fields!.Get(WaterModel.Depth);
        var wetness = simulation.World.Fields!.Get(GroundModel.Wetness);
        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Columns; x++)
            {
                if (InBasin(x, y))
                {
                    water.Set(x, y, depthUm);
                    wetness.Set(x, y, GroundModel.Full);
                }
            }
        }
    }

    [Test]
    public void Decision_2026_10_11_ManyWaysToDie_WaterRisingAroundSoldiersSweepsThem_TheNearBankSurvive_TheFarDrown()
    {
        // A defending regiment stands on the dry floor of a basin, its front rank 5.5 m from the east rim; the basin fills to 1.4 m in a moment. Soldiers
        // within a paddle of the rim (S1: 0.4 m/s for 15 s, about 6 m) climb out stunned; the rest drown.
        var map = TestBattles.Map(BasinHeight, Columns, Rows);
        var battle = TestBattles.Battle(map, new[] { ("defender", "infantry", 50, 83.0, 22.0) });
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        Assert.That(world.Units.AliveCount, Is.EqualTo(50));
        Flood(simulation, 1_400_000);

        simulation.Step(1);
        Assert.That(world.Counters.Swept, Is.EqualTo(50), "the water rose around all of them");
        Assert.That(
            TestBattles.LiveUnits(world).All(u => (world.Flags[u] & UnitFlags.Swept) != 0),
            Is.True
        );

        bool sawStunned = false;
        for (int tick = 0; tick < 30 * SimConstants.TicksPerSecond; tick++)
        {
            simulation.Step();
            sawStunned |= TestBattles
                .LiveUnits(world)
                .Any(u => (world.Flags[u] & UnitFlags.Stunned) != 0);
        }

        var counters = world.Counters;
        Assert.That(
            counters.Drowned,
            Is.GreaterThan(0),
            "the middle of the basin is too far to paddle"
        );
        Assert.That(
            counters.ReachedBank,
            Is.GreaterThan(0),
            "the edge of the regiment reached the rim"
        );
        Assert.That(sawStunned, Is.True, "out of the water, stunned first");
        Assert.That(counters.Drowned + counters.ReachedBank, Is.EqualTo(50));
        Assert.That(world.Units.AliveCount, Is.EqualTo(50 - counters.Drowned));
        Assert.That(world.RegimentAlive[0], Is.EqualTo(world.Units.AliveCount));
        var terrain = new Terrain(map);
        foreach (int u in TestBattles.LiveUnits(world))
        {
            Assert.That(
                world.Flags[u] & (UnitFlags.Swept | UnitFlags.Stunned),
                Is.EqualTo(UnitFlags.None)
            );
            terrain.CellOf(world.Position[u], out int x, out int y);
            Assert.That(InBasin(x, y), Is.False, $"unit {u} back on dry ground");
        }
    }

    [Test]
    public void ShallowWater_NeverSweeps_AndStillMoatWaterDrownsOnlyFarFromTheEdge()
    {
        var map = TestBattles.Map(BasinHeight, Columns, Rows);
        var battle = TestBattles.Battle(map, new[] { ("defender", "infantry", 50, 82.0, 22.0) });
        var simulation = battle.Start(new SerialRunner());
        Flood(simulation, 250_000); // knee-deep and still

        simulation.Step(10 * SimConstants.TicksPerSecond);

        Assert.That(simulation.World.Counters.Swept, Is.Zero);
        Assert.That(simulation.World.Units.AliveCount, Is.EqualTo(50));
    }

    [Test]
    public void Decision_2026_10_09_SoldiersNeverWalkIntoLethalHazards_ButTheCrowdBehindPushesTheFrontRanksOver()
    {
        // A deep moat across the map (cells 38 to 42, still water 1.5 m deep) with one causeway 6 m wide (rows 18 to
        // 21). Four regiments march for it; at its mouth the crowd presses the front ranks against the water.
        var map = TestBattles.Map(Moat, 80, 40);
        var regiments = Enumerable
            .Range(0, 4)
            .Select(i => ("attacker", "infantry", 150, 6.0, 8.0 + 14.0 * i));
        var battle = TestBattles.Battle(map, regiments);
        var simulation = battle.Start(new ThreadedRunner(4));
        FillMoat(simulation);
        var world = simulation.World;
        int teetered = 0;
        int sweptBeforeAnyTeeter = -1;

        for (int tick = 0; tick < 240 * SimConstants.TicksPerSecond; tick++)
        {
            simulation.Step();
            int teetering = TestBattles
                .LiveUnits(world)
                .Count(u => (world.Flags[u] & UnitFlags.Teetering) != 0);
            teetered = System.Math.Max(teetered, teetering);
            if (sweptBeforeAnyTeeter < 0 && world.Counters.Swept > 0)
            {
                sweptBeforeAnyTeeter = teetered;
            }
        }

        var counters = world.Counters;
        Assert.That(teetered, Is.GreaterThan(0), "soldiers teetered at the water's edge");
        Assert.That(counters.PushedOver, Is.GreaterThan(0), "and the crowd pushed some of them in");
        Assert.That(
            counters.Swept,
            Is.EqualTo(counters.PushedOver),
            "nobody walked in; every one was pushed"
        );
        Assert.That(
            sweptBeforeAnyTeeter,
            Is.Not.Zero,
            "every push into the water came after a teeter"
        );
        Assert.That(
            TestBattles.LiveUnits(world).Count(u => world.Position[u].X() > 43 * 1.5)
                + counters.Drowned,
            Is.GreaterThan(400),
            "most of the column still crossed"
        );
    }

    private static int Moat(int x, int y) => x >= 38 && x <= 42 && (y < 18 || y > 21) ? -1_500 : 0;

    private static void FillMoat(Simulation simulation)
    {
        var water = simulation.World.Fields!.Get(WaterModel.Depth);
        var wetness = simulation.World.Fields!.Get(GroundModel.Wetness);
        for (int y = 0; y < 40; y++)
        {
            for (int x = 38; x <= 42; x++)
            {
                if (Moat(x, y) < 0)
                {
                    water.Set(x, y, 1_500_000);
                    wetness.Set(x, y, GroundModel.Full);
                }
            }
        }
    }

    [Test]
    public void Hazards_AreTheSameOnAnyRunner_AndAcrossARestore()
    {
        Simulation Make(IParallelRunner runner)
        {
            var map = TestBattles.Map(Moat, 80, 40);
            var regiments = Enumerable
                .Range(0, 4)
                .Select(i => ("attacker", "infantry", 150, 6.0, 8.0 + 14.0 * i));
            var simulation = TestBattles.Battle(map, regiments).Start(runner);
            FillMoat(simulation);
            return simulation;
        }

        // 75 s: the column reaches the moat, teeters and is pushed in (the restore lands among swept soldiers).
        ulong[] serial = Make(new SerialRunner()).RunAndHash(1_500, 20);
        Assert.That(Make(new ThreadedRunner(8)).RunAndHash(1_500, 20), Is.EqualTo(serial));
        foreach (int at in new[] { 1_213 })
        {
            var original = Make(new ThreadedRunner(4));
            original.Step(at);
            byte[] snapshot = original.CaptureSnapshot();
            var restored = Make(new ThreadedRunner(3));
            restored.Step(200);
            restored.RestoreSnapshot(snapshot);
            restored.Step(1_500 - at);
            Assert.That(restored.StateHash(), Is.EqualTo(serial[^1]), $"restored at {at}");
        }
    }
}
