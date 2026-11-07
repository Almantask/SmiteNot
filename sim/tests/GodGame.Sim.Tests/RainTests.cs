using GodGame.Sim.Hosting;
using GodGame.Sim.Rules;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim.Tests;

/// <summary>The casts layer and Rain (M3 item 7, spec S1 section 8).</summary>
[TestFixture, Category("Fast")]
public class RainTests
{
    private const int WindUp = 200;
    private const int Duration = 1_200;

    /// <summary>A flat 450 × 150 m map; the attackers' one regiment stands at its west edge.</summary>
    private static Battle FlatBattle(
        params (string Id, string Power, int Tick, double X, double Y, double Radius)[] casts
    ) =>
        TestBattles.Battle(
            TestBattles.Map((x, y) => 0, 300, 100),
            new[] { ("attacker", "infantry", 50, 7.5, 75.0) },
            1,
            casts
        );

    private static long CellsInCircle(MapData map, double x, double y, double radius)
    {
        long count = 0;
        for (int cy = 0; cy < map.Rows; cy++)
        {
            for (int cx = 0; cx < map.Columns; cx++)
            {
                long dx = (long)cx * 1_500 + 750 - (long)(x * 1000);
                long dy = (long)cy * 1_500 + 750 - (long)(y * 1000);
                long r = (long)(radius * 1000);
                if (dx * dx + dy * dy <= r * r)
                {
                    count++;
                }
            }
        }
        return count;
    }

    /// <summary>Water on the map plus what the soil has soaked up, in µm summed over cells.</summary>
    private static long WaterOnMap(Simulation simulation)
    {
        var fields = simulation.World.Fields!;
        long water = fields.Get(WaterModel.Depth).Values.Sum(v => (long)v);
        long soaked = fields.Get(GroundModel.Wetness).Values.Sum(w => (long)w - 8_192) / 2;
        return water + soaked;
    }

    [Test]
    public void Rain_WaitsOutItsWindUp_ThenAdds3MmASecondForAMinute_AndStops()
    {
        var battle = FlatBattle(("storm", "rain", 100, 300.0, 75.0, 40.0));
        var simulation = battle.Start(new ThreadedRunner(4));
        long cells = CellsInCircle(battle.Map, 300.0, 75.0, 40.0);

        simulation.Step(100 + WindUp);
        Assert.That(simulation.World.Casts!.Status[0], Is.EqualTo(CastStatus.Fired));
        Assert.That(WaterOnMap(simulation), Is.Zero, "nothing falls during the wind-up");

        simulation.Step(Duration);
        long expected = cells * 3_000 * Duration / SimConstants.TicksPerSecond; // 180 mm per cell
        Assert.That(WaterOnMap(simulation), Is.EqualTo(expected).Within(expected / 100));

        long atEnd = WaterOnMap(simulation);
        simulation.Step(200);
        Assert.That(WaterOnMap(simulation), Is.LessThanOrEqualTo(atEnd), "the rain has stopped");
    }

    [Test]
    public void Decision_2026_10_08_GodPowersUseTheSameRangeRule_RainTooCloseToTheArmyFizzles()
    {
        // The regiment's outline reaches to about 12 m; a 40 m storm centred at 100 m comes within about 50 m of it.
        var battle = FlatBattle(
            ("near", "rain", 20, 100.0, 75.0, 40.0),
            ("far", "rain", 20, 300.0, 75.0, 40.0)
        );
        var simulation = battle.Start(new SerialRunner());

        simulation.Step(20 + WindUp + 100);

        var casts = simulation.World.Casts!;
        // Casts keep the plan's id order: "far" before "near".
        Assert.That(casts.Status[1], Is.EqualTo(CastStatus.Fizzled));
        Assert.That(casts.Reasons[1], Is.EqualTo(FizzleReasons.TooCloseToAttacker));
        Assert.That(casts.NearestAttacker[1], Is.Zero);
        Assert.That(casts.AttackerDistanceMm[1], Is.InRange(40_000, 60_000));
        Assert.That(casts.NearestDefender[1], Is.EqualTo(CastStates.NoRegiment));
        Assert.That(casts.Status[0], Is.EqualTo(CastStatus.Fired));
        Assert.That(casts.AttackerDistanceMm[0], Is.GreaterThan(100_000));
        var water = simulation.World.Fields!.Get(WaterModel.Depth);
        Assert.That(water.Get(100 * 2 / 3, 50), Is.Zero, "the fizzled storm wrote nothing");
        Assert.That(water.Get(200, 50), Is.GreaterThan(0), "the far one rains");
    }

    [Test]
    public void RainBetweenBothArmies_FizzlesForBoth()
    {
        var map = TestBattles.Map((x, y) => 0, 100, 100);
        var battle = TestBattles.Battle(
            map,
            new[]
            {
                ("attacker", "infantry", 50, 7.5, 75.0),
                ("defender", "infantry", 50, 142.5, 75.0),
            },
            1,
            new[] { ("mid", "rain", 0, 75.0, 75.0, 40.0) }
        );
        var simulation = battle.Start(new SerialRunner());

        simulation.Step(1);

        var casts = simulation.World.Casts!;
        Assert.That(casts.Status[0], Is.EqualTo(CastStatus.Fizzled));
        Assert.That(
            casts.Reasons[0],
            Is.EqualTo(FizzleReasons.TooCloseToAttacker | FizzleReasons.TooCloseToDefender)
        );
        Assert.That(casts.NearestDefender[0], Is.EqualTo(1));
    }

    [Test]
    public void ALoneStraggler_DoesNotBlockACast_ButAFormationDoes()
    {
        // One soldier wanders to 30 m from the storm's edge; his regiment stands far away. A lone soldier in a coarse
        // cell isn't an outline (brief, 2026-10-08: ignoring isolated stragglers).
        var battle = FlatBattle(("storm", "rain", 1, 300.0, 75.0, 40.0));
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        int straggler = TestBattles.LiveUnits(world).Last();
        world.Position[straggler] = GodGame.Sim.Math.FixVec2.FromInts(230, 75);

        simulation.Step(2);

        Assert.That(world.Casts!.Status[0], Is.EqualTo(CastStatus.Fired));
        Assert.That(world.Casts.AttackerDistanceMm[0], Is.GreaterThan(100_000));
    }

    [TestCase("\"power\":\"rain\"", "\"power\":\"hail\"", "no power")]
    [TestCase("\"radiusMm\":40000", "\"radiusMm\":30000", "radiusMm")]
    [TestCase("\"xMm\":300000", "\"xMm\":430000", "xMm")]
    [TestCase("\"tick\":100", "\"tick\":18001", "tick")]
    public void ACastThatCantBePlaced_IsRefusedWhilePlanning(string from, string to, string why)
    {
        var map = TestBattles.Map((x, y) => 0, 300, 100);
        string plan =
            "{\"version\":0,\"map\":\"test\",\"army\":[],\"terrain\":[],\"casts\":["
            + "{\"id\":\"storm\",\"power\":\"rain\",\"tick\":100,\"xMm\":300000,\"yMm\":75000,\"radiusMm\":40000}]}";
        Assert.That(BattlePlan.Load(plan, map, RepoData.Rules()).Casts, Has.Count.EqualTo(1));

        var error = Assert.Throws<DataFormatException>(() =>
            BattlePlan.Load(plan.Replace(from, to), map, RepoData.Rules())
        );
        Assert.That(error!.Message, Does.Contain(why));
    }

    [Test]
    public void CastsRun_InIdOrder_WhateverTheFileOrder_AndHashTheSame()
    {
        var map = TestBattles.Map((x, y) => 0, 300, 100);
        string a =
            "{\"id\":\"a\",\"power\":\"rain\",\"tick\":5,\"xMm\":300000,\"yMm\":75000,\"radiusMm\":40000}";
        string b =
            "{\"id\":\"b\",\"power\":\"rain\",\"tick\":9,\"xMm\":250000,\"yMm\":75000,\"radiusMm\":50000}";
        string Plan(string casts) =>
            "{\"version\":0,\"map\":\"test\",\"army\":[],\"terrain\":[],\"casts\":[" + casts + "]}";

        var ab = BattlePlan.Load(Plan(a + "," + b), map, RepoData.Rules());
        var ba = BattlePlan.Load(Plan(b + "," + a), map, RepoData.Rules());

        Assert.That(ba.Casts.Select(c => c.Id), Is.EqualTo(new[] { "a", "b" }));
        Assert.That(ba.Hash, Is.EqualTo(ab.Hash));
        Assert.Throws<DataFormatException>(() =>
            BattlePlan.Load(Plan(a + "," + a), map, RepoData.Rules())
        );
    }

    /// <summary>
    /// A 360 × 240 m valley with an 18 m river (1.2 m banks, 0.5 permille fall) fed 10 m³/s at the north edge and
    /// drained at the south, the defenders holding both edges of the map, far from the river.
    /// </summary>
    internal static Battle ValleyBattle(bool rain, int castTick = 4_800)
    {
        int Height(int x, int y)
        {
            int d = System.Math.Abs(x - 120);
            int sink =
                d < 6 ? 1_200
                : d < 10 ? 1_200 * (10 - d) / 4
                : 0;
            return y * 1_500 / 2_000 - sink;
        }

        string water =
            ",\"water\":{\"sources\":[{\"minXMm\":165000,\"minYMm\":238500,\"maxXMm\":195000,\"maxYMm\":240000,\"litresPerSecond\":10000}],"
            + "\"sinks\":[{\"minXMm\":165000,\"minYMm\":0,\"maxXMm\":195000,\"maxYMm\":1500}]}";
        var map = TestBattles.Map(Height, 240, 160, 15, "valley", water);
        var casts = rain
            ? new[] { ("storm", "rain", castTick, 180.0, 180.0, 40.0) }
            : Array.Empty<(string, string, int, double, double, double)>();
        return TestBattles.Battle(
            map,
            new[] { ("defender", "infantry", 50, 352.5, 120.0) },
            1,
            casts
        );
    }

    [Test]
    public void Rain_IsTheSameOnAnyRunner_AndAcrossARestore()
    {
        ulong[] serial = ValleyBattle(rain: true, castTick: 20)
            .Start(new SerialRunner())
            .RunAndHash(700, 20);
        Assert.That(
            ValleyBattle(rain: true, castTick: 20)
                .Start(new ThreadedRunner(4))
                .RunAndHash(700, 20),
            Is.EqualTo(serial)
        );
        foreach (int at in new[] { 613 }) // mid-rain between field steps (cast at tick 20)
        {
            var original = ValleyBattle(rain: true, castTick: 20).Start(new ThreadedRunner(4));
            original.Step(at);
            byte[] snapshot = original.CaptureSnapshot();
            var restored = ValleyBattle(rain: true, castTick: 20).Start(new ThreadedRunner(3));
            restored.Step(37);
            restored.RestoreSnapshot(snapshot);
            restored.Step(700 - at);
            Assert.That(restored.StateHash(), Is.EqualTo(serial[^1]), $"restored at {at}");
        }
    }
}

/// <summary>Rain on a steady river: minutes of simulated water, so it runs with the slow tests.</summary>
[TestFixture, Category("Slow")]
public class RainSlowTests
{
    [Test]
    public void Decision_2026_11_03_WaterAndRainRules_RainUpstreamRaisesTheRiverDownstream()
    {
        // The river runs steady after four minutes; then a 40 m storm falls on it 120 m upstream of the gauge (S1 D3:
        // no hidden multiplier, the river carries what falls on it and what runs off its banks).
        int GaugePeakMm(bool rain, out CastStatus status)
        {
            var simulation = RainTests.ValleyBattle(rain).Start(new ThreadedRunner(4));
            var depth = simulation.World.Fields!.Get(WaterModel.Depth);
            simulation.Step(4_800);
            int peak = 0;
            for (int second = 0; second < 150; second++)
            {
                simulation.Step(SimConstants.TicksPerSecond);
                peak = System.Math.Max(peak, depth.Get(120, 40) / 1000);
            }
            status =
                simulation.World.Casts!.Count > 0
                    ? simulation.World.Casts.Status[0]
                    : CastStatus.Pending;
            return peak;
        }

        int dry = GaugePeakMm(rain: false, out _);
        int wet = GaugePeakMm(rain: true, out var status);

        Assert.That(status, Is.EqualTo(CastStatus.Fired), "100 m clear of both armies at the cast");
        Assert.That(
            wet,
            Is.GreaterThan(dry + 100),
            $"the river rose: {dry} mm without rain, {wet} mm with"
        );
    }
}
