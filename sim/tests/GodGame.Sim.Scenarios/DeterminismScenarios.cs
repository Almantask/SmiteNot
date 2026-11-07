using GodGame.Sim.Hosting;

namespace GodGame.Sim.Scenarios;

/// <summary>
/// Design decisions as executable tests (plan, Appendix B). M1 has the deterministic core but no game systems yet,
/// so this protects the decision every other one depends on.
/// </summary>
[TestFixture, Category("Fast")]
public class DeterminismScenarios
{
    [Test]
    public void Decision_2026_10_08_SamePlanSameBattle_PerSecondHashesMatch()
    {
        string root = RepoFiles.FindRoot(TestContext.CurrentContext.TestDirectory);
        var rules = RepoFiles.LoadRepoRules(root);
        var map = RepoFiles.LoadRepoMap(root, "m1-flat");
        var plan = BattlePlan.Load(
            File.ReadAllText(Path.Combine(root, "data", "plans", "m1-ten-thousand.json")),
            map,
            rules
        );
        var battle = new Battle(rules, map, plan, seed: 2026);

        // 20 s runs every system on 10,000 soldiers; the goldens cover the full three minutes.
        ulong[] first = battle.PerSecondHashes(20, new SerialRunner());
        ulong[] second = battle.PerSecondHashes(20, new ThreadedRunner(3));

        Assert.That(second, Is.EqualTo(first));
    }
}
