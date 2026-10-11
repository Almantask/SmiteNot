using System.Text;
using GodGame.Sim.Math;

namespace GodGame.Sim.Tests;

[TestFixture, Category("Fast")]
public class SimHashTests
{
    // Reference values from the xxHash reference implementation (Python xxhash 3.x, XXH64).
    [TestCase("", 0UL, 0xEF46DB3751D8E999UL)]
    [TestCase("a", 0UL, 0xD24EC4F1A98C6E5BUL)]
    [TestCase("abc", 0UL, 0x44BC2CF5AD770999UL)]
    [TestCase("abc", 1UL, 0xBEA9CA8199328908UL)]
    [TestCase("The quick brown fox jumps over the lazy dog", 0UL, 0x0B242D361FDA71BCUL)]
    public void Hash64_MatchesReference(string text, ulong seed, ulong expected)
    {
        Assert.That(SimHash.Hash64(Encoding.ASCII.GetBytes(text), seed), Is.EqualTo(expected));
    }

    [Test]
    public void Hash64_LongInputs_MatchReference()
    {
        byte[] counting256Twice = Enumerable.Range(0, 512).Select(i => (byte)(i % 256)).ToArray();
        byte[] counting100 = Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();

        Assert.That(SimHash.Hash64(counting256Twice), Is.EqualTo(0x7B3BFCAAC0348AC0UL));
        Assert.That(SimHash.Hash64(counting100, 12345), Is.EqualTo(0x028BA1AE2DE4DE27UL));
    }

    [Test]
    public void Append_InAnyChunking_EqualsOneShot([Values(1, 3, 7, 8, 31, 32, 33, 64)] int chunk)
    {
        byte[] data = Enumerable.Range(0, 300).Select(i => (byte)(i * 7 + 3)).ToArray();
        var hash = new SimHash(99);
        for (int offset = 0; offset < data.Length; offset += chunk)
        {
            hash.Append(data.AsSpan(offset, System.Math.Min(chunk, data.Length - offset)));
        }

        Assert.That(hash.Digest(), Is.EqualTo(SimHash.Hash64(data, 99)));
    }

    [Test]
    public void AppendInt32_IsLittleEndian()
    {
        var hash = new SimHash();
        hash.AppendInt32(0x04030201);

        Assert.That(hash.Digest(), Is.EqualTo(SimHash.Hash64(new byte[] { 1, 2, 3, 4 })));
    }

    [Test]
    public void Digest_DoesNotEndTheStream()
    {
        var hash = new SimHash();
        hash.Append("ab"u8);
        _ = hash.Digest();
        hash.Append("c"u8);

        Assert.That(hash.Digest(), Is.EqualTo(0x44BC2CF5AD770999UL));
    }
}
