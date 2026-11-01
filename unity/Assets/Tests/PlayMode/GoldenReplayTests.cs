using System.Collections.Generic;
using GodGame.Bridge;
using NUnit.Framework;

namespace GodGame.Tests.PlayMode
{
    /// <summary>
    /// The goldens replayed in Play Mode under the editor's Mono runtime must give the hashes .NET recorded
    /// (plan, M1 exit criterion: .NET vs the Unity Mono editor vs the IL2CPP player).
    /// </summary>
    public class GoldenReplayTests
    {
        private static IEnumerable<string> Names() => GoldenRunner.GoldenNames(GoldenRunner.EditorRepoRoot);

        [Test]
        public void AtLeastOneGoldenExists()
        {
            Assert.That(Names(), Is.Not.Empty);
        }

        [TestCaseSource(nameof(Names))]
        public void Golden_MatchesUnderMono(string name)
        {
            Assert.That(GoldenRunner.Verify(GoldenRunner.EditorRepoRoot, name), Is.Null);
        }
    }
}
