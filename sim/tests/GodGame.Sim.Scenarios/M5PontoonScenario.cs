using GodGame.Sim.Hosting;
using GodGame.Sim.Rules;

namespace GodGame.Sim.Scenarios;

/// <summary>
/// S2's pontoon exit scenario (spec S2 section 17) on data/maps/m3-exit with data/plans/m5-pontoon.json: the M3 march and
/// storm, and a Pontoon Train of 8 boats with the column. The train bridges the river where the army's foot path meets
/// it and the storm's water breaks it loose. (S2 also wants regiments on it before the crest: a timing for its section 17
/// tuning, below.)
/// </summary>
[TestFixture, Category("Slow")]
public class M5PontoonScenario
{
    private static Battle Load()
    {
        string root = RepoFiles.FindRoot(TestContext.CurrentContext.TestDirectory);
        var rules = RepoFiles.LoadRepoRules(root);
        var map = RepoFiles.LoadRepoMap(root, "m3-exit");
        var plan = BattlePlan.Load(
            File.ReadAllText(Path.Combine(root, "data", "plans", "m5-pontoon.json")),
            map,
            rules
        );
        return new Battle(rules, map, plan, seed: 1);
    }

    [Test]
    public void Decision_2026_10_11_TheFirstSetIncludesSpecialists_APontoonSpansTheFloodedRiver_AndTheSurgeBreaksItLoose()
    {
        var battle = Load();
        var simulation = battle.Start(new ThreadedRunner(4));
        var world = simulation.World;
        var terrain = new Terrain(battle.PlayedMap);
        int onPontoon = 0;
        int firstLaid = -1;
        for (int second = 1; second <= 420; second++)
        {
            simulation.Step(SimConstants.TicksPerSecond);
            var structures = world.Structures!;
            if (firstLaid < 0 && structures.Count > structures.StaticCount)
            {
                firstLaid = simulation.Tick;
            }
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (!world.Units.IsAlive(u) || world.UnitRegiment[u] == 0)
                {
                    continue;
                }
                terrain.CellOf(world.Position[u], out int x, out int y);
                int cell = terrain.Index(x, y);
                for (int s = structures.StaticCount; s < structures.Count; s++)
                {
                    if (!structures.Broken[s] && structures.BattleCells[s]!.Contains(cell))
                    {
                        onPontoon++;
                    }
                }
            }
        }
        var states = world.Structures!;
        TestContext.Out.WriteLine(
            $"laid at {firstLaid / 20} s: {states.Count - states.StaticCount} segments; soldier-seconds on it {onPontoon}; failed {world.Counters.SegmentsFailed}; swept {world.Counters.Swept}"
        );
        Assert.That(
            states.Count - states.StaticCount,
            Is.GreaterThan(3),
            "a bridge of several segments"
        );
        Assert.That(
            battle.Rules.Structures.Kinds[states.BattleKind[states.StaticCount]].Id,
            Is.EqualTo("pontoon")
        );
        // Not yet asserted: "regiments use it before the crest". On m3-exit the train reaches the river at about 6 min, when
        // the flood is already over the 1.8 m/s surge line, so the bridge breaks loose as it is laid; S2 sets the timings
        // to be tuned in its section 17 (the ford's flow when the train arrives, the crest's, the train's pace).
        var failed = world.Causes!.Events.Where(e => e.Kind == CauseKind.SegmentFailed).ToList();
        Assert.That(failed, Is.Not.Empty, "the crest broke it loose");
        var events = world.Causes.Events.ToList();
        Assert.That(
            world.Causes.Trace(events.IndexOf(failed[0])).Any(e => e.Kind == CauseKind.CastFired),
            Is.True,
            "traced to the storm"
        );
    }

    [Test]
    public void PontoonsAreDeterministic_AcrossThreadCountsAndARestore()
    {
        var battle = Load();
        ulong[] serial = battle.PerSecondHashes(380, new SerialRunner());
        Assert.That(battle.PerSecondHashes(380, new ThreadedRunner(4)), Is.EqualTo(serial));
        var a = battle.Start(new SerialRunner());
        a.Step(7_340); // just before the surge breaks the first segments (about 7,344)
        var snapshot = a.CaptureSnapshot();
        a.Step(380 * SimConstants.TicksPerSecond - 7_340);
        var b = battle.Start(new ThreadedRunner(3));
        b.Step(7_500);
        b.RestoreSnapshot(snapshot);
        b.Step(380 * SimConstants.TicksPerSecond - 7_340);
        Assert.That(b.StateHash(), Is.EqualTo(a.StateHash()));
    }
}
