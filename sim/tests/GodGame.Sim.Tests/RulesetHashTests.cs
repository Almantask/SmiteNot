using System.Text;
using GodGame.Sim.Rules;

namespace GodGame.Sim.Tests;

[TestFixture, Category("Fast")]
public class RulesetHashTests
{
    private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);

    [Test]
    public void SameFiles_SameHash()
    {
        ulong first = RulesetHash.Of(Utf8("{\"a\":1}"), Utf8("{}"));
        ulong second = RulesetHash.Of(Utf8("{\"a\":1}"), Utf8("{}"));

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void ChangedValue_ChangesHash()
    {
        Assert.That(
            RulesetHash.Of(Utf8("{\"a\":1}")),
            Is.Not.EqualTo(RulesetHash.Of(Utf8("{\"a\":2}")))
        );
    }

    [Test]
    public void FileBoundaries_AreHashed()
    {
        Assert.That(
            RulesetHash.Of(Utf8("ab"), Utf8("c")),
            Is.Not.EqualTo(RulesetHash.Of(Utf8("a"), Utf8("bc")))
        );
    }
}
