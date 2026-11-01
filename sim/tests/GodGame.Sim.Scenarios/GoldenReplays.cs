using GodGame.Sim.Hosting;

namespace GodGame.Sim.Scenarios;

/// <summary>
/// Every golden replay in sim/tests/golden/ must still produce its recorded per-second hashes. A failure means
/// outcomes changed: explain it, then bump SimVersion and regenerate through /golden-replay.
/// </summary>
[TestFixture, Category("Golden")]
public class GoldenReplays
{
    private static readonly Goldens Files = new(
        RepoFiles.FindRoot(TestContext.CurrentContext.TestDirectory)
    );

    private static IEnumerable<string> Names() =>
        new Goldens(RepoFiles.FindRoot(AppContext.BaseDirectory)).Names();

    [Test]
    public void AtLeastOneGoldenExists() => Assert.That(Names(), Is.Not.Empty);

    [TestCaseSource(nameof(Names))]
    public void Golden_StillMatches(string name)
    {
        var result = Files.Verify(name, new ThreadedRunner(4));

        Assert.That(result.Passed, Is.True, result.ToString());
    }
}
