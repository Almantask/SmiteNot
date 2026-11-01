using System.Numerics;
using GodGame.Sim.Math;

namespace GodGame.Sim.Tests;

[TestFixture, Category("Fast")]
public class FixTests
{
    private const int PropertyCases = 20_000;

    // Raw values whose magnitude stays under 2^47 (whole part under 2^31), the range the contract covers.
    private static long SafeRaw(ref Pcg32 rng)
    {
        long raw = ((long)rng.NextUInt() << 15) ^ rng.NextUInt();
        return (rng.NextUInt() & 1) == 0 ? raw : -raw;
    }

    // Smaller values for products, so |a × b| stays under 2^31.
    private static long ProductRaw(ref Pcg32 rng) => rng.NextInt(int.MinValue + 1, int.MaxValue);

    private static BigInteger FloorDiv(BigInteger a, BigInteger b)
    {
        BigInteger q = BigInteger.DivRem(a, b, out BigInteger r);
        return r != 0 && (r.Sign < 0) != (b.Sign < 0) ? q - 1 : q;
    }

    [Test]
    public void Constants_HaveTheirValues()
    {
        Assert.That(Fix.One.Raw, Is.EqualTo(65536));
        Assert.That(Fix.Half.Raw, Is.EqualTo(32768));
        Assert.That(Fix.Zero.Raw, Is.EqualTo(0));
        Assert.That(Fix.FromInt(-3).Raw, Is.EqualTo(-3 * 65536));
        Assert.That(Fix.FromRatio(5, 4), Is.EqualTo(Fix.FromInt(1) + Fix.FromRaw(16384)));
    }

    [Test]
    public void Multiply_MatchesExactProductRoundedDown()
    {
        var rng = new Pcg32(11, 1);
        for (int i = 0; i < PropertyCases; i++)
        {
            long a = ProductRaw(ref rng);
            long b = ProductRaw(ref rng);
            var expected = FloorDiv(new BigInteger(a) * b, Fix.OneRaw);

            Assert.That(
                (Fix.FromRaw(a) * Fix.FromRaw(b)).Raw,
                Is.EqualTo((long)expected),
                $"{a} × {b}"
            );
        }
    }

    [Test]
    public void Divide_MatchesExactQuotientRoundedTowardZero()
    {
        var rng = new Pcg32(11, 2);
        for (int i = 0; i < PropertyCases; i++)
        {
            long a = SafeRaw(ref rng);
            long b = SafeRaw(ref rng);
            if (b == 0)
            {
                continue;
            }
            var expected = BigInteger.Divide(new BigInteger(a) * Fix.OneRaw, b);

            Assert.That(
                (Fix.FromRaw(a) / Fix.FromRaw(b)).Raw,
                Is.EqualTo((long)expected),
                $"{a} / {b}"
            );
        }
    }

    [Test]
    public void AddSubtractNegate_AreExact()
    {
        var rng = new Pcg32(11, 3);
        for (int i = 0; i < PropertyCases; i++)
        {
            Fix a = Fix.FromRaw(SafeRaw(ref rng));
            Fix b = Fix.FromRaw(SafeRaw(ref rng));

            Assert.That((a + b).Raw, Is.EqualTo(a.Raw + b.Raw));
            Assert.That((a + b - b), Is.EqualTo(a));
            Assert.That((-a).Raw, Is.EqualTo(-a.Raw));
            Assert.That(a.CompareTo(b), Is.EqualTo(a.Raw.CompareTo(b.Raw)));
        }
    }

    [Test]
    public void IntegerOperators_MatchRawArithmetic()
    {
        Assert.That((Fix.FromRaw(7) * 3).Raw, Is.EqualTo(21));
        Assert.That((Fix.FromRaw(-7) / 2).Raw, Is.EqualTo(-3));
        Assert.Throws<DivideByZeroException>(() => _ = Fix.One / 0);
        Assert.Throws<DivideByZeroException>(() => _ = Fix.One / Fix.Zero);
    }

    [Test]
    public void DivideByMinusOne_NegatesWithoutReachingTheRuntime()
    {
        Fix x = Fix.FromRaw(123_456_789);

        Assert.That(x / -1, Is.EqualTo(-x));
        Assert.That(x / -Fix.FromRaw(1), Is.EqualTo(Fix.FromRaw(-123_456_789L << 16)));
    }

    [Test]
    public void OverflowChecks_NeverThrowFromTheirOwnArithmetic()
    {
        // These overflow by design; in Debug the assert reports it, but the check itself must not divide
        // long.MinValue by -1. Debug.Assert fails through a listener, so only look for exceptions here.
        var listeners = System.Diagnostics.Trace.Listeners;
        var saved = new System.Diagnostics.TraceListener[listeners.Count];
        listeners.CopyTo(saved, 0);
        listeners.Clear();
        try
        {
            Assert.That(() => _ = Fix.FromRaw(-1) * Fix.MinValue, Throws.Nothing);
            Assert.That(() => _ = Fix.MinValue * -1, Throws.Nothing);
            Assert.That(() => _ = Fix.MinValue / -1, Throws.Nothing);
            Assert.That(() => _ = Fix.FromRaw(-(1L << 47)) / Fix.FromRaw(-1), Throws.Nothing);
        }
        finally
        {
            listeners.AddRange(saved);
        }
    }

    [TestCase(0, 0, 0, 0)]
    [TestCase(65536, 1, 1, 1)]
    [TestCase(98304, 1, 2, 2)] // 1.5: halves round up
    [TestCase(-98304, -2, -1, -1)] // -1.5
    [TestCase(-1, -1, 0, 0)]
    [TestCase(32767, 0, 1, 0)]
    public void Rounding_FloorCeilingNearest(long raw, long floor, long ceiling, long nearest)
    {
        var x = Fix.FromRaw(raw);

        Assert.That(x.FloorToInt(), Is.EqualTo(floor));
        Assert.That(x.CeilingToInt(), Is.EqualTo(ceiling));
        Assert.That(x.RoundToInt(), Is.EqualTo(nearest));
    }

    [Test]
    public void Sqrt_IsTheLargestValueWhoseSquareFits()
    {
        var rng = new Pcg32(11, 4);
        for (int i = 0; i < PropertyCases; i++)
        {
            long a = System.Math.Abs(SafeRaw(ref rng));
            long root = Fix.Sqrt(Fix.FromRaw(a)).Raw;
            var scaled = new BigInteger(a) * Fix.OneRaw;

            Assert.That(new BigInteger(root) * root, Is.LessThanOrEqualTo(scaled), $"sqrt({a})");
            Assert.That(
                new BigInteger(root + 1) * (root + 1),
                Is.GreaterThan(scaled),
                $"sqrt({a})"
            );
        }
        Assert.That(Fix.Sqrt(Fix.FromInt(9)), Is.EqualTo(Fix.FromInt(3)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Fix.Sqrt(-Fix.One));
    }

    [Test]
    public void IntSqrt_ExhaustiveBelowTwoToTheTwenty()
    {
        uint root = 0;
        var wrong = new List<ulong>();
        for (ulong n = 0; n < 1 << 20; n++)
        {
            if ((ulong)(root + 1) * (root + 1) <= n)
            {
                root++;
            }
            if (IntMath.Sqrt(n) != root)
            {
                wrong.Add(n);
            }
        }

        Assert.That(wrong, Is.Empty);
    }

    [TestCase(ulong.MaxValue, 4294967295u)]
    [TestCase(18446744065119617025UL, 4294967295u)] // (2^32 - 1)^2
    [TestCase(18446744065119617024UL, 4294967294u)]
    [TestCase(1UL << 62, 2147483648u)]
    public void IntSqrt_Edges(ulong n, uint expected)
    {
        Assert.That(IntMath.Sqrt(n), Is.EqualTo(expected));
    }

    [TestCase(0L, "0")]
    [TestCase(65536L, "1")]
    [TestCase(98304L, "1.5")]
    [TestCase(-98304L, "-1.5")]
    [TestCase(1L, "0.0000152587890625")]
    [TestCase(long.MinValue, "-140737488355328")]
    public void ToString_IsExactAndInvariant(long raw, string expected)
    {
        Assert.That(Fix.FromRaw(raw).ToString(), Is.EqualTo(expected));
    }

    [Test]
    public void Equality_FollowsRaw()
    {
        Fix five = Fix.FromRaw(5);
        Fix alsoFive = Fix.FromInt(5) / 65536;

        Assert.That(five, Is.EqualTo(alsoFive));
        Assert.That(five == alsoFive, Is.True);
        Assert.That(Fix.FromRaw(5) != Fix.FromRaw(6), Is.True);
        Assert.That(five.GetHashCode(), Is.EqualTo(alsoFive.GetHashCode()));
        Assert.That(Fix.Clamp(Fix.FromInt(5), Fix.Zero, Fix.One), Is.EqualTo(Fix.One));
        Assert.That(Fix.Abs(Fix.FromInt(-2)), Is.EqualTo(Fix.FromInt(2)));
    }
}
