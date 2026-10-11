namespace GodGame.Sim.Scenarios;

/// <summary>
/// Design decisions as executable tests (plan, Appendix B). In M0 only the placeholder simulation exists,
/// so this protects the first decision every other one depends on.
/// </summary>
[TestFixture, Category("Fast")]
public class DeterminismScenarios
{
    [Test]
    public void Decision_2026_10_08_SamePlanSameBattle_PerSecondHashesMatch()
    {
        var setup = new PlaceholderSetup(seed: 2026, unitCount: 1_000);
        ulong[] first = PerSecondHashes(setup, seconds: 180);
        ulong[] second = PerSecondHashes(setup, seconds: 180);

        Assert.That(second, Is.EqualTo(first));
    }

    private static ulong[] PerSecondHashes(PlaceholderSetup setup, int seconds)
    {
        var battle = new PlaceholderBattle(setup);
        var hashes = new ulong[seconds];
        for (int second = 0; second < seconds; second++)
        {
            battle.Step(SimConstants.TicksPerSecond);
            hashes[second] = battle.StateHash();
        }
        return hashes;
    }
}
