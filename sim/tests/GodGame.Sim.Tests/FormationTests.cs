using GodGame.Sim.Hosting;
using GodGame.Sim.Math;

namespace GodGame.Sim.Tests;

[TestFixture, Category("Fast")]
public class FormationTests
{
    private const int Wall = 5_000;

    private static double SlotError(World world, int u)
    {
        int r = world.UnitRegiment[u];
        int anchor = world.RegimentAnchor[r];
        var rules = RepoData.Rules().UnitTypes[world.UnitType[u]].Formation;
        Fix fileSpacing = Fix.FromRatio(rules.FileSpacingMm, 1000);
        Fix rankSpacing = Fix.FromRatio(rules.RankSpacingMm, 1000);
        var origin =
            world.Position[anchor]
            - Formation.SlotOffset(
                world.FormationSlot[anchor],
                world.RegimentFiles[r],
                fileSpacing,
                rankSpacing,
                world.RegimentFacing[r]
            );
        var slot =
            origin
            + Formation.SlotOffset(
                world.FormationSlot[u],
                world.RegimentFiles[r],
                Fix.FromRatio(rules.FileSpacingMm, 1000),
                Fix.FromRatio(rules.RankSpacingMm, 1000),
                world.RegimentFacing[r]
            );
        double dx = slot.X() - world.Position[u].X();
        double dy = slot.Y() - world.Position[u].Y();
        return System.Math.Sqrt(dx * dx + dy * dy);
    }

    [Test]
    public void AMarchingRegiment_KeepsItsRanksBehindTheOfficer()
    {
        var map = TestBattles.Map((x, y) => 0, 160, 40);
        var simulation = TestBattles
            .Battle(map, new[] { ("attacker", "infantry", 120, 7.5, 30.0) })
            .Start(new SerialRunner());
        var world = simulation.World;

        simulation.Step(40 * SimConstants.TicksPerSecond);

        var errors = TestBattles
            .LiveUnits(world)
            .Where(u => u != world.RegimentAnchor[0])
            .Select(u => SlotError(world, u))
            .OrderBy(e => e)
            .ToArray();
        Assert.That(
            world.Position[world.RegimentAnchor[0]].X(),
            Is.GreaterThan(60),
            "on the march"
        );
        Assert.That(
            errors[errors.Length / 2],
            Is.LessThan(0.3),
            "most soldiers stand in their slot"
        );
        Assert.That(errors[^1], Is.LessThan(1.5));
        Assert.That(
            TestBattles
                .LiveUnits(world)
                .All(u =>
                    world.Position[u].X() <= world.Position[world.RegimentAnchor[0]].X() + 0.5
                ),
            Is.True,
            "the officer leads"
        );
    }

    [Test]
    public void WhenTheOfficerFalls_TheLowestSlotTakesTheStandard()
    {
        var map = TestBattles.Map((x, y) => 0, 80, 40);
        var simulation = TestBattles
            .Battle(map, new[] { ("attacker", "infantry", 50, 7.5, 30.0) })
            .Start(new SerialRunner());
        var world = simulation.World;
        simulation.Step(20);

        world.DespawnUnit(world.Units.IdOf(world.RegimentAnchor[0]));
        Assert.That(world.RegimentAnchor[0], Is.EqualTo(-1));
        simulation.Step();

        Assert.That(world.RegimentAnchor[0], Is.EqualTo(1));
        Assert.That(world.FormationSlot[world.RegimentAnchor[0]], Is.EqualTo(1));
        // The formation forms on where the old officer stood: nobody's slot jumped.
        Assert.That(
            TestBattles.LiveUnits(world).Where(u => u != 1).Max(u => SlotError(world, u)),
            Is.LessThan(1.0)
        );
    }

    [Test]
    public void FormationsWiderOrDeeperThanAKilometre_AreRefused()
    {
        string wide = RepoData
            .Read("data", "rules", "unit-types.json")
            .Replace(
                "\"files\": 20, \"fileSpacingMm\": 1000",
                "\"files\": 1000, \"fileSpacingMm\": 1100"
            );
        string deep = RepoData
            .Read("data", "rules", "unit-types.json")
            .Replace(
                "\"files\": 20, \"fileSpacingMm\": 1000, \"rankSpacingMm\": 1200",
                "\"files\": 1, \"fileSpacingMm\": 1000, \"rankSpacingMm\": 6000"
            );

        Assert.Throws<GodGame.Sim.Rules.Json.DataFormatException>(() =>
            RepoData.RulesWithUnitTypes(wide)
        );
        Assert.Throws<GodGame.Sim.Rules.Json.DataFormatException>(() =>
            RepoData.RulesWithUnitTypes(deep)
        );
    }

    [Test]
    public void TheFormationTurnsTowardTheOfficersHeading_AtALimitedRate()
    {
        // Deployed facing east, the goal is east too; a wall makes the officer swing north around it.
        var map = TestBattles.Map((x, y) => x == 30 && y < 30 ? Wall : 0, 80, 40);
        var simulation = TestBattles
            .Battle(map, new[] { ("attacker", "infantry", 60, 7.5, 15.0) })
            .Start(new SerialRunner());
        var world = simulation.World;
        Angle previous = world.RegimentFacing[0];
        int largestTurn = 0;
        bool turned = false;

        for (int t = 0; t < 60 * SimConstants.TicksPerSecond; t++)
        {
            simulation.Step();
            int turn = System.Math.Abs(previous.DeltaTo(world.RegimentFacing[0]));
            largestTurn = System.Math.Max(largestTurn, turn);
            turned |=
                System.Math.Abs(Angle.Zero.DeltaTo(world.RegimentFacing[0]))
                > Angle.UnitsPerTurn / 16;
            previous = world.RegimentFacing[0];
        }

        Assert.That(turned, Is.True, "it wheeled more than 22.5° to pass the wall");
        Assert.That(largestTurn, Is.LessThanOrEqualTo(MovementSystem.MaxTurnUnitsPerTick));
    }

    [Test]
    public void ARegimentSqueezesThroughAGapAndReforms()
    {
        // The 20-file rank (19 m) can't pass a 7.5 m gap: the outer files follow the flow through it, then reform.
        var map = TestBattles.Map((x, y) => x == 40 && (y < 17 || y > 23) ? Wall : 0, 160, 40);
        var simulation = TestBattles
            .Battle(map, new[] { ("attacker", "infantry", 100, 7.5, 30.0) })
            .Start(new SerialRunner());
        var world = simulation.World;

        simulation.Step(110 * SimConstants.TicksPerSecond);

        Assert.That(
            TestBattles.LiveUnits(world).All(u => world.Position[u].X() > 62),
            Is.True,
            "all through"
        );
        var errors = TestBattles
            .LiveUnits(world)
            .Where(u => u != world.RegimentAnchor[0])
            .Select(u => SlotError(world, u))
            .OrderBy(e => e)
            .ToArray();
        Assert.That(errors[errors.Length / 2], Is.LessThan(0.5), "back in formation");
    }
}

[TestFixture, Category("Slow")]
public class FormationCrowdTests
{
    [Test]
    public void OfficersFallingMidBattle_AreTheSameOnAnyThreadCount_AndAcrossARestoreWithoutOfficers()
    {
        var map = TestBattles.Map((x, y) => x == 60 && (y < 25 || y > 35) ? 5_000 : 0, 160, 60);
        var regiments = Enumerable
            .Range(0, 60)
            .Select(i =>
                (
                    "attacker",
                    i % 4 == 1 ? "cavalry" : "infantry",
                    50,
                    4.0 + (i % 3) * 4.0,
                    5.0 + (i / 3) * 4.0
                )
            );
        var battle = TestBattles.Battle(map, regiments);

        static void KillEveryOtherOfficer(World world)
        {
            for (int r = 0; r < world.Regiments.HighWater; r += 2)
            {
                if (world.RegimentAnchor[r] >= 0)
                {
                    world.DespawnUnit(world.Units.IdOf(world.RegimentAnchor[r]));
                }
            }
        }

        ulong Run(IParallelRunner runner)
        {
            var simulation = battle.Start(runner);
            simulation.Step(100);
            KillEveryOtherOfficer(simulation.World);
            simulation.Step(60);
            KillEveryOtherOfficer(simulation.World);
            simulation.Step(100);
            return simulation.StateHash();
        }

        ulong serial = Run(new SerialRunner());
        Assert.That(Run(new ThreadedRunner(8)), Is.EqualTo(serial));

        var original = battle.Start(new ThreadedRunner(4));
        original.Step(100);
        KillEveryOtherOfficer(original.World);
        byte[] leaderless = original.CaptureSnapshot(); // regiments without an officer, before promotion
        var restored = battle.Start(new ThreadedRunner(3));
        restored.RestoreSnapshot(leaderless);
        restored.Step(60);
        KillEveryOtherOfficer(restored.World);
        restored.Step(100);
        Assert.That(restored.StateHash(), Is.EqualTo(serial));
    }
}
