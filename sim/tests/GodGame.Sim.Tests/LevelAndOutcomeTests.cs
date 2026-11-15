using System.Globalization;
using GodGame.Sim.Rules;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim.Tests;

[TestFixture, Category("Fast"), Parallelizable(ParallelScope.Children)]
public class LevelAndOutcomeTests
{
    private static readonly MapData FlatMap = TestBattles.Map(
        (_, _) => 0,
        80,
        60,
        zoneMetres: 30,
        id: "flat-level"
    );

    private static string LevelJson(
        string extraOutcomeJson = "\"variant\":\"survival\",\"sunsetTick\":1200",
        string extraPowers = ""
    )
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{{"
                + "\"version\":0,"
                + "\"id\":\"test-level\","
                + "\"chapter\":\"chapter-0\","
                + "\"title\":\"Test Crossing\","
                + "\"map\":\"{0}\","
                + "\"plan\":\"plans/test.json\","
                + "\"seed\":42,"
                + "\"allowedPieces\":[\"wooden-bridge\",\"hedgerow\"],"
                + "\"hand\":[{{\"power\":\"rain\",\"charges\":2}}],"
                + "{1}"
                + "\"outcome\":{{{2}}},"
                + "\"sideQuests\":[{{\"id\":\"keep-bridge\",\"description\":\"Hold the span\"}}],"
                + "\"teachingFlags\":[\"strain\"]"
                + "}}",
            FlatMap.Id,
            extraPowers,
            extraOutcomeJson
        );
    }

    [Test]
    public void LevelData_LoadsSurvivalAndObjectiveWithPendingPowers()
    {
        var rules = RepoData.Rules();
        var pending =
            "\"pendingPowers\":[{\"power\":\"lightning\",\"charges\":2,\"note\":\"M5 item 7\"}],";
        var objective =
            "\"variant\":\"objective\",\"sunsetTick\":3600,\"armyRoutPermille\":700,"
            + "\"objectivePolygonMm\":[[60000,15000],[105000,15000],[105000,45000],[60000,45000]],"
            + "\"objectiveMinSoldiers\":25,\"objectiveHoldTicks\":100";

        var level = LevelData.Load(LevelJson(objective, pending), FlatMap, rules);

        Assert.That(level.Id, Is.EqualTo("test-level"));
        Assert.That(level.Hand.Count, Is.EqualTo(1));
        Assert.That(level.Hand[0].PowerId, Is.EqualTo("rain"));
        Assert.That(level.PendingPowers.Count, Is.EqualTo(1));
        Assert.That(level.PendingPowers[0].PowerId, Is.EqualTo("lightning"));
        Assert.That(level.PendingPowers[0].Pending, Is.True);
        Assert.That(level.Outcome.Variant, Is.EqualTo(LevelOutcomeVariant.Objective));
        Assert.That(level.Outcome.ObjectivePointCount, Is.EqualTo(4));
        Assert.That(level.Outcome.ObjectiveHoldTicks, Is.EqualTo(100));
        Assert.That(level.SideQuests[0].Id, Is.EqualTo("keep-bridge"));
        Assert.That(level.TeachingFlags[0], Is.EqualTo("strain"));
    }

    [Test]
    public void LevelData_RejectsUnknownActivePowerOrBadOutcomePolygon()
    {
        var rules = RepoData.Rules();
        string badPower = LevelJson("\"variant\":\"survival\"").Replace("\"rain\"", "\"tornado\"");
        Assert.Throws<DataFormatException>(() => LevelData.Load(badPower, FlatMap, rules));

        string badPoly = LevelJson(
            "\"variant\":\"objective\",\"objectivePolygonMm\":[[1000,1000],[2000,2000]]"
        );
        Assert.Throws<DataFormatException>(() => LevelData.Load(badPoly, FlatMap, rules));
    }

    [Test]
    public void OutcomeEvaluator_DetectsDefenderRoutAndSunsetWins_AndObjectiveCaptureLoss()
    {
        var rules = RepoData.Rules();
        var smallMap = TestBattles.Map((_, _) => 0, 40, 20, zoneMetres: 15, id: "outcome-box");

        // Objective polygon squarely covers defender zone east of x = 45 m.
        var spec = new LevelOutcomeSpec(
            LevelOutcomeVariant.Objective,
            sunsetTick: 600,
            armyRoutPermille: 700,
            objectivePolygonMm: new[]
            {
                42_000,
                3_000,
                58_000,
                3_000,
                58_000,
                27_000,
                42_000,
                27_000,
            },
            objectiveMinSoldiers: 30,
            objectiveHoldTicks: 40
        );

        // 1) Survival hold: defenders stand far south so attackers don't rout them by tick 200.
        var holdBattle = TestBattles.Battle(
            smallMap,
            new[]
            {
                ("attacker", "infantry", 50, 6.0, 3.0),
                ("defender", "infantry", 50, 50.0, 25.0),
            }
        );
        var sim = holdBattle.Start(new Hosting.ThreadedRunner(2));
        var holdTrack = new PlaybackTrack(smallMap.Columns, smallMap.Rows, 16);
        for (int t = 0; t <= 600; t++)
        {
            if (t > 0)
            {
                sim.Step(1);
            }
            if (t % 4 == 0)
            {
                holdTrack.RecordUnits(sim.Tick, sim.World);
                holdTrack.RecordRegiments(sim.Tick, sim.World);
            }
        }
        var midResult = BattleOutcomeEvaluator.Evaluate(
            holdTrack,
            LevelOutcomeSpec.DefaultSurvival(600),
            maxTick: 200
        );
        Assert.That(midResult.Kind, Is.EqualTo(BattleOutcomeKind.Ongoing));

        var sunsetResult = BattleOutcomeEvaluator.Evaluate(
            holdTrack,
            LevelOutcomeSpec.DefaultSurvival(600)
        );
        Assert.That(sunsetResult.Kind, Is.EqualTo(BattleOutcomeKind.DefenderVictory));
        Assert.That(sunsetResult.Reason, Is.EqualTo(BattleOutcomeReason.HeldUntilSunset));

        // 2) Objective capture: spawn attackers directly inside the objective polygon and hold >= 40 ticks.
        var capBattle = TestBattles.Battle(
            smallMap,
            new[]
            {
                ("attacker", "infantry", 60, 10.0, 15.0),
                ("defender", "infantry", 50, 55.0, 15.0),
            }
        );
        var capSim = capBattle.Start(new Hosting.ThreadedRunner(2));
        var capTrack = new PlaybackTrack(smallMap.Columns, smallMap.Rows, 16);
        // Move attackers cleanly into the objective box in this synthetic track evaluation test.
        for (int t = 0; t <= 80; t += 4)
        {
            for (int u = 0; u < 60; u++)
            {
                capSim.World.Position[u] = new Math.FixVec2(
                    Math.Fix.FromInt(50),
                    Math.Fix.FromInt(15)
                );
            }
            capTrack.RecordUnits(t, capSim.World);
            capTrack.RecordRegiments(t, capSim.World);
        }
        var captured = BattleOutcomeEvaluator.Evaluate(capTrack, spec);
        Assert.That(captured.Kind, Is.EqualTo(BattleOutcomeKind.AttackerVictory));
        Assert.That(captured.Reason, Is.EqualTo(BattleOutcomeReason.ObjectiveCaptured));
        Assert.That(captured.DecidedTick, Is.LessThanOrEqualTo(80));
    }
}
