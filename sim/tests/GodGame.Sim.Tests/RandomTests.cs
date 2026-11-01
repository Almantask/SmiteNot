using GodGame.Sim.Math;

namespace GodGame.Sim.Tests;

[TestFixture, Category("Fast")]
public class RandomTests
{
    [Test]
    public void Pcg32_MatchesReferenceDemo()
    {
        // pcg32-demo from the reference C implementation, seeded with pcg32_srandom(42, 54).
        var rng = new Pcg32(42, 54);
        uint[] expected =
        {
            0xA15C02B7,
            0x7B47F409,
            0xBA1D3330,
            0x83D2F293,
            0xBFA4784B,
            0xCBED606E,
        };

        Assert.That(Enumerable.Range(0, 6).Select(_ => rng.NextUInt()), Is.EqualTo(expected));
    }

    [Test]
    public void NextUInt_Bounded_StaysInRange(
        [Values(1u, 2u, 3u, 7u, 1000u, 0x80000001u)] uint bound
    )
    {
        var rng = new Pcg32(1, 2);
        for (int i = 0; i < 1000; i++)
        {
            Assert.That(rng.NextUInt(bound), Is.LessThan(bound));
        }
    }

    [Test]
    public void NextInt_CoversTheWholeRange()
    {
        var rng = new Pcg32(3, 4);
        var seen = new SortedSet<int>();
        for (int i = 0; i < 1000; i++)
        {
            seen.Add(rng.NextInt(-2, 3));
        }

        Assert.That(seen, Is.EqualTo(new[] { -2, -1, 0, 1, 2 }));
    }

    [Test]
    public void Stream_MatchesHashingWithASimHash()
    {
        // The allocation-free path must give exactly what M0's SimHash-based streams gave.
        var rng = new Pcg32(9, 9);
        for (int i = 0; i < 2_000; i++)
        {
            ulong seed = ((ulong)rng.NextUInt() << 32) | rng.NextUInt();
            int system = (int)rng.NextUInt(),
                entity = (int)rng.NextUInt(),
                tick = (int)rng.NextUInt();
            var hash = new SimHash(seed);
            hash.AppendInt32(system);
            hash.AppendInt32(entity);
            hash.AppendInt32(tick);
            ulong state = hash.Digest();
            hash.AppendInt32(0x5EED);
            var expected = new Pcg32(state, hash.Digest());

            var actual = SimRandom.Stream(seed, system, entity, tick);

            Assert.That(actual.NextUInt(), Is.EqualTo(expected.NextUInt()));
            Assert.That(actual.NextUInt(), Is.EqualTo(expected.NextUInt()));
        }
    }

    [Test]
    public void HashShort_MatchesSimHashForEveryLengthUnder32()
    {
        var bytes = Enumerable.Range(0, 31).Select(i => (byte)(i * 37 + 11)).ToArray();
        for (int length = 0; length < 32; length++)
        {
            var hash = new SimHash(77);
            hash.Append(bytes.AsSpan(0, length));

            Assert.That(
                SimHash.HashShort(bytes.AsSpan(0, length), 77),
                Is.EqualTo(hash.Digest()),
                $"length {length}"
            );
        }
    }

    [Test]
    public void Stream_SameInputs_SameSequence()
    {
        var a = SimRandom.Stream(7, 1, 2, 3);
        var b = SimRandom.Stream(7, 1, 2, 3);

        for (int i = 0; i < 100; i++)
        {
            Assert.That(a.NextUInt(), Is.EqualTo(b.NextUInt()));
        }
    }

    [Test]
    public void Stream_AnyInputDiffers_SequenceDiffers()
    {
        uint First(Pcg32 rng) => rng.NextUInt();
        uint baseline = First(SimRandom.Stream(7, 1, 2, 3));

        Assert.Multiple(() =>
        {
            Assert.That(First(SimRandom.Stream(8, 1, 2, 3)), Is.Not.EqualTo(baseline));
            Assert.That(First(SimRandom.Stream(7, 9, 2, 3)), Is.Not.EqualTo(baseline));
            Assert.That(First(SimRandom.Stream(7, 1, 9, 3)), Is.Not.EqualTo(baseline));
            Assert.That(First(SimRandom.Stream(7, 1, 2, 9)), Is.Not.EqualTo(baseline));
        });
    }
}
