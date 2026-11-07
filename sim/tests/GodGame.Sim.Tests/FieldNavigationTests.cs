using GodGame.Sim.Hosting;
using GodGame.Sim.Rules;

namespace GodGame.Sim.Tests;

/// <summary>Units read the fields (M3 item 5): wading and mud slow each class, deep or fast water closes to it.</summary>
[TestFixture, Category("Fast")]
public class FieldNavigationTests
{
    private const int PondX = 40;
    private const int PondY = 20;

    /// <summary>A bowl 1.8 m deep in the middle, sloping gently (about 200 permille), 8 cells across each way.</summary>
    private static int PondHeight(int x, int y)
    {
        int distance = System.Math.Max(System.Math.Abs(x - PondX), System.Math.Abs(y - PondY));
        return -System.Math.Max(0, 1_800 - distance * 300);
    }

    /// <summary>Fills the bowl to the brim (surface at 0 m): still water, up to 1.8 m deep.</summary>
    private static void FillPond(Simulation simulation, int columns, int rows)
    {
        var water = simulation.World.Fields!.Get(WaterModel.Depth);
        var wetness = simulation.World.Fields!.Get(GroundModel.Wetness);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                int depthMm = -PondHeight(x, y);
                if (depthMm > 0)
                {
                    water.Set(x, y, depthMm * 1_000);
                    wetness.Set(x, y, GroundModel.Full); // already soaked: the pond keeps its level
                }
            }
        }
    }

    private static int FootCode(Simulation simulation, int x, int y) =>
        FieldSpeed.Code(
            simulation.World.Fields!.Get(FieldSpeed.FieldId).Get(x, y),
            MovementClass.Foot
        );

    [Test]
    public void SpeedCodes_FollowSpecS1_ForEachClass()
    {
        var rules = RepoData.Rules();
        var foot = rules.Movement(MovementClass.Foot);
        var horse = rules.Movement(MovementClass.Horse);
        var wheeled = rules.Movement(MovementClass.Wheeled);

        Assert.That(FieldSpeed.CodeFor(foot, 0, 0, 0), Is.EqualTo(FieldSpeed.AllDryCode));
        Assert.That(FieldSpeed.CodeFor(foot, 300, 0, 0), Is.EqualTo(800 / 4), "knee-deep: 80 %");
        Assert.That(FieldSpeed.CodeFor(foot, 450, 0, 0), Is.EqualTo(675 / 4), "between points");
        Assert.That(
            FieldSpeed.CodeFor(foot, 1_000, 0, 0),
            Is.EqualTo(FieldSpeed.Swept),
            "1 m sweeps"
        );
        Assert.That(
            FieldSpeed.CodeFor(foot, 400, 1_500, 0),
            Is.EqualTo(FieldSpeed.Swept),
            "0.4 m at 1.5 m/s is 0.6 m²/s"
        );
        Assert.That(
            FieldSpeed.CodeFor(foot, 250, 5_000, 0),
            Is.Not.EqualTo(FieldSpeed.Swept),
            "too shallow to sweep"
        );
        Assert.That(
            FieldSpeed.CodeFor(horse, 1_000, 0, 0),
            Is.EqualTo(500 / 4),
            "horses wade deeper"
        );
        Assert.That(
            FieldSpeed.CodeFor(wheeled, 600, 0, 0),
            Is.EqualTo(FieldSpeed.Closed),
            "carts can't"
        );
        Assert.That(
            FieldSpeed.CodeFor(foot, 0, 0, 65_535),
            Is.EqualTo(500 / 4),
            "deep mud halves foot"
        );
        Assert.That(
            FieldSpeed.CodeFor(wheeled, 0, 0, 65_535),
            Is.EqualTo(150 / 4),
            "and nearly stops carts"
        );
        Assert.That(
            FieldSpeed.CodeFor(horse, 0, 0, 32_767),
            Is.LessThan(FieldSpeed.CodeFor(foot, 0, 0, 32_767)),
            "horses suffer more in mud than foot"
        );
    }

    [Test]
    public void Decision_2026_10_09_SoldiersNeverWalkIntoLethalHazards_AColumnMarchesAroundADeepPond()
    {
        // The pond sits on the straight line from the attackers to the defenders' zone; its slopes alone are gentle
        // enough to walk, so only the water closes it.
        var map = TestBattles.Map(PondHeight, 80, 40);
        var battle = TestBattles.Battle(
            map,
            new[] { ("attacker", "infantry", 60, 8.0, PondY * 1.5) }
        );
        var simulation = battle.Start(new SerialRunner());
        FillPond(simulation, 80, 40);
        var world = simulation.World;
        var terrain = new Terrain(map);
        int wading = 0;

        for (int tick = 0; tick < 150 * SimConstants.TicksPerSecond; tick++)
        {
            simulation.Step();
            if (tick % 5 != 0)
            {
                continue;
            }
            foreach (int u in TestBattles.LiveUnits(world))
            {
                terrain.CellOf(world.Position[u], out int cx, out int cy);
                int code = FootCode(simulation, cx, cy);
                Assert.That(
                    code,
                    Is.GreaterThan(FieldSpeed.Closed),
                    $"unit {u} at tick {tick} in deep water"
                );
                if (code < FieldSpeed.AllDryCode)
                {
                    wading++;
                }
            }
        }

        Assert.That(
            FootCode(simulation, PondX, PondY),
            Is.EqualTo(FieldSpeed.Swept),
            "the middle is deep"
        );
        int crossed = TestBattles
            .LiveUnits(world)
            .Count(u => world.Position[u].X() > (PondX + 8) * 1.5);
        Assert.That(crossed, Is.EqualTo(60), "the column went around and on");
        Assert.That(wading, Is.GreaterThan(0), "some waded through the pond's shallow rim");
    }

    [Test]
    public void DeepWater_IsLethal_InTheDistanceField()
    {
        var map = TestBattles.Map(PondHeight, 80, 40);
        var battle = TestBattles.Battle(map, new[] { ("attacker", "infantry", 50, 8.0, 8.0) });
        var simulation = battle.Start(new SerialRunner());
        var navigation = new Navigation(map, battle.Rules, new SerialRunner());
        int dryDistance = navigation.Lethal.DistanceMm(PondX, PondY);
        FillPond(simulation, 80, 40);

        simulation.Step(SimConstants.TicksPerSecond + 1); // past the next refresh
        navigation.RebuildFromWorld(simulation.World, new SerialRunner());

        Assert.That(dryDistance, Is.GreaterThan(10_000), "a dry bowl is safe");
        Assert.That(navigation.Lethal.IsLethal(PondX, PondY), Is.True);
        Assert.That(navigation.Lethal.DistanceMm(PondX, PondY), Is.Zero);
        Assert.That(
            navigation.Lethal.DistanceMm(PondX, PondY + 6),
            Is.InRange(1, 4 * TestBattles.CellMm),
            "the shallow rim is near the deep water"
        );
        Assert.That(
            navigation.Costs.Cost(MovementClass.Foot, PondX, PondY),
            Is.EqualTo(CostFields.Impassable)
        );
    }

    [Test]
    public void Infantry_WadeAndSquelch_SlowerThanOnDryGround()
    {
        // Two columns march east; the second's field has a band 20 m wide of knee-deep water on soaked ground.
        double TimeToCross(bool wet)
        {
            var map = TestBattles.Map((x, y) => 0, 60, 16);
            var battle = TestBattles.Battle(map, new[] { ("attacker", "infantry", 50, 6.0, 12.0) });
            var simulation = battle.Start(new SerialRunner());
            if (wet)
            {
                var water = simulation.World.Fields!.Get(WaterModel.Depth);
                var wetness = simulation.World.Fields!.Get(GroundModel.Wetness);
                for (int y = 0; y < 16; y++)
                {
                    for (int x = 20; x < 34; x++)
                    {
                        water.Set(x, y, 300_000);
                        wetness.Set(x, y, GroundModel.Full);
                    }
                }
            }
            var world = simulation.World;
            for (int tick = 1; tick < 120 * SimConstants.TicksPerSecond; tick++)
            {
                simulation.Step();
                if (TestBattles.LiveUnits(world).Average(u => world.Position[u].X()) > 36 * 1.5)
                {
                    return tick / (double)SimConstants.TicksPerSecond;
                }
            }
            return double.PositiveInfinity;
        }

        double dry = TimeToCross(wet: false);
        double wet = TimeToCross(wet: true);
        // About 20 m at well under 80 % (wading, spreading water and soft mud) costs several seconds.
        Assert.That(wet - dry, Is.InRange(4.0, 30.0), $"dry {dry} s, wet {wet} s");
    }

    [Test]
    public void FieldCosts_AreTheSameOnAnyRunner_AndAcrossARestore()
    {
        // A pond in the column's path, and water poured beside it that runs down into it, so the speed codes, costs and
        // flow fields keep changing.
        Simulation Make(IParallelRunner runner, bool pond, bool pour)
        {
            var map = TestBattles.Map(PondHeight, 80, 40);
            var battle = TestBattles.Battle(
                map,
                new[]
                {
                    ("attacker", "infantry", 80, 8.0, PondY * 1.5),
                    ("attacker", "cavalry", 50, 9.0, 12.0),
                }
            );
            var simulation = battle.Start(runner);
            if (pond)
            {
                FillPond(simulation, 80, 40);
            }
            if (pour)
            {
                var water = simulation.World.Fields!.Get(WaterModel.Depth);
                for (int y = 10; y < 30; y++)
                {
                    water.Set(PondX - 9, y, 600_000);
                }
            }
            return simulation;
        }

        ulong[] serial = Make(new SerialRunner(), true, true).RunAndHash(800, 20);
        Assert.That(
            Make(new ThreadedRunner(4), true, true).RunAndHash(800, 20),
            Is.EqualTo(serial)
        );
        foreach (int at in new[] { 313 }) // between two refreshes
        {
            var original = Make(new ThreadedRunner(4), true, true);
            original.Step(at);
            byte[] snapshot = original.CaptureSnapshot();
            var restored = Make(new ThreadedRunner(3), true, true);
            restored.Step(517);
            restored.RestoreSnapshot(snapshot);
            restored.Step(800 - at);
            Assert.That(restored.StateHash(), Is.EqualTo(serial[^1]), $"restored at {at}");
        }

        // Restored into a battle whose navigation was built for dry ground: the still pond's codes never change
        // again, so only the rebuild after the restore puts the pond back into the costs.
        ulong[] still = Make(new SerialRunner(), true, false).RunAndHash(1_200, 20);
        var wet = Make(new ThreadedRunner(4), true, false);
        wet.Step(60);
        var dry = Make(new ThreadedRunner(3), false, false);
        dry.Step(300);
        dry.RestoreSnapshot(wet.CaptureSnapshot());
        dry.Step(1_140);
        Assert.That(dry.StateHash(), Is.EqualTo(still[^1]));
    }

    [Test]
    public void ACurveWhoseXDoesNotRise_IsRefused()
    {
        string json = RepoData
            .Read("data", "rules", "movement-classes.json")
            .Replace("[[0, 1000], [32767, 800], [65535, 500]]", "[[0, 1000], [0, 800]]");
        Assert.That(json, Does.Contain("[[0, 1000], [0, 800]]"));
        var error = Assert.Throws<GodGame.Sim.Rules.Json.DataFormatException>(() =>
            RulesetLoader.Load(
                RepoData
                    .RuleFiles()
                    .Select(f =>
                        f.Key == "movement-classes.json" ? KeyValuePair.Create(f.Key, json) : f
                    )
                    .ToArray()
            )
        );
        Assert.That(error!.Message, Does.Contain("x must rise"));
    }
}
