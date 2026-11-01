using GodGame.Sim.Math;

namespace GodGame.Sim.Tests;

[TestFixture, Category("Fast")]
public class TrigTests
{
    private static double Radians(int units) => units * (2 * System.Math.PI / Angle.UnitsPerTurn);

    private static long ExactRaw(double value) => (long)System.Math.Round(value * Fix.OneRaw);

    [Test]
    public void SinAndCos_EveryAngle_WithinOneRawUnitOfExact()
    {
        var wrong = new List<string>();
        for (int units = 0; units < Angle.UnitsPerTurn; units++)
        {
            var angle = Angle.FromUnits(units);
            long sin = Trig.Sin(angle).Raw;
            long cos = Trig.Cos(angle).Raw;
            if (System.Math.Abs(sin - ExactRaw(System.Math.Sin(Radians(units)))) > 1)
            {
                wrong.Add($"sin {units}: {sin}");
            }
            if (System.Math.Abs(cos - ExactRaw(System.Math.Cos(Radians(units)))) > 1)
            {
                wrong.Add($"cos {units}: {cos}");
            }
        }

        Assert.That(wrong, Is.Empty);
    }

    [TestCase(0, 0, 65536)]
    [TestCase(16384, 65536, 0)]
    [TestCase(32768, 0, -65536)]
    [TestCase(49152, -65536, 0)]
    public void SinAndCos_QuarterTurns_AreExact(int units, long sin, long cos)
    {
        var angle = Angle.FromUnits(units);

        Assert.That(Trig.Sin(angle).Raw, Is.EqualTo(sin));
        Assert.That(Trig.Cos(angle).Raw, Is.EqualTo(cos));
    }

    [Test]
    public void Atan2_EveryAngleAtMapScale_RoundTripsWithinOneUnit()
    {
        var radius = Fix.FromInt(1000);
        var wrong = new List<string>();
        for (int units = 0; units < Angle.UnitsPerTurn; units++)
        {
            var angle = Angle.FromUnits(units);
            var point = FixVec2.FromAngle(angle, radius);
            int delta = angle.DeltaTo(Trig.Atan2(point.Y, point.X));
            if (System.Math.Abs(delta) > 1)
            {
                wrong.Add($"{units}: off by {delta}");
            }
        }

        Assert.That(wrong, Is.Empty);
    }

    [Test]
    public void Atan2_RandomPoints_WithinOneUnitOfExact()
    {
        var rng = new Pcg32(21, 1);
        var wrong = new List<string>();
        for (int i = 0; i < 50_000; i++)
        {
            // Mix tiny and large magnitudes, including far past map scale.
            int shift = (int)rng.NextUInt(40);
            long x =
                (long)rng.NextInt(int.MinValue + 1, int.MaxValue) << (int)rng.NextUInt(16) >> shift;
            long y =
                (long)rng.NextInt(int.MinValue + 1, int.MaxValue) << (int)rng.NextUInt(16) >> shift;
            if (x == 0 && y == 0)
            {
                continue;
            }
            int exact = (int)
                System.Math.Round(
                    System.Math.Atan2(y, x) * (Angle.UnitsPerTurn / (2 * System.Math.PI))
                );
            int delta = Angle.FromUnits(exact).DeltaTo(Trig.Atan2(Fix.FromRaw(y), Fix.FromRaw(x)));
            if (System.Math.Abs(delta) > 1)
            {
                wrong.Add($"({x}, {y}): off by {delta}");
            }
        }

        Assert.That(wrong, Is.Empty);
    }

    [TestCase(1, 0, 0)]
    [TestCase(0, 1, 16384)]
    [TestCase(-1, 0, 32768)]
    [TestCase(0, -1, 49152)]
    [TestCase(1, 1, 8192)]
    [TestCase(-1, -1, 40960)]
    [TestCase(0, 0, 0)]
    public void Atan2_AxesAndDiagonals_AreExact(long x, long y, int units)
    {
        Assert.That(Trig.Atan2(Fix.FromInt(y), Fix.FromInt(x)).Units, Is.EqualTo(units));
    }

    [Test]
    public void Tables_HashToThePinnedValue()
    {
        // The ±1 tests above would let a rewrite of the table builders change outcomes; this pins every bit.
        var hash = new SimHash();
        for (int units = 0; units < Angle.UnitsPerTurn; units++)
        {
            hash.AppendInt64(Trig.Sin(Angle.FromUnits(units)).Raw);
        }
        for (int y = -64; y <= 64; y++)
        {
            for (int x = -64; x <= 64; x++)
            {
                hash.AppendInt32(
                    Trig.Atan2(Fix.FromRaw(y * 12_345), Fix.FromRaw(x * 12_345)).Units
                );
            }
        }

        Assert.That(hash.Digest(), Is.EqualTo(PinnedHashes.TrigTables));
    }

    [Test]
    public void Atan2_ExtremeInputs_DoNotThrow()
    {
        Assert.That(Trig.Atan2(Fix.MinValue, Fix.MinValue).Units, Is.EqualTo(40960));
        Assert.That(Trig.Atan2(Fix.MaxValue, Fix.MinValue).Units, Is.EqualTo(24576));
        Assert.That(Trig.Atan2(Fix.FromRaw(1), Fix.MaxValue).Units, Is.EqualTo(0));
    }
}

[TestFixture, Category("Fast")]
public class AngleTests
{
    [TestCase(0, 0)]
    [TestCase(9_000, 16384)]
    [TestCase(-9_000, 49152)]
    [TestCase(18_000, 32768)]
    [TestCase(36_000, 0)]
    [TestCase(45_000, 16384)]
    public void FromCentiDegrees_HitsTheQuarterTurns(int centiDegrees, int units)
    {
        Assert.That(Angle.FromCentiDegrees(centiDegrees).Units, Is.EqualTo(units));
    }

    [Test]
    public void CentiDegrees_RoundTripExactly()
    {
        // A unit (about 0.0055°) is finer than a centi-degree, so every centi-degree survives the trip.
        var wrong = new List<int>();
        for (int c = 0; c < 36_000; c++)
        {
            if (Angle.FromCentiDegrees(c).ToCentiDegrees() != c)
            {
                wrong.Add(c);
            }
        }

        Assert.That(wrong, Is.Empty);
    }

    [Test]
    public void Arithmetic_Wraps()
    {
        var almostTurn = Angle.FromUnits(65_000);

        Assert.That((almostTurn + Angle.FromUnits(1_000)).Units, Is.EqualTo(464));
        Assert.That((Angle.Zero - Angle.FromUnits(1)).Units, Is.EqualTo(65_535));
        Assert.That((-Angle.QuarterTurn).Units, Is.EqualTo(49152));
        Assert.That(Angle.FromUnits(-1), Is.EqualTo(Angle.FromUnits(65_535)));
    }

    [TestCase(0, 100, 100)]
    [TestCase(100, 0, -100)]
    [TestCase(65_000, 500, 1_036)]
    [TestCase(500, 65_000, -1_036)]
    [TestCase(0, 32768, -32768)]
    public void DeltaTo_TakesTheShortWay(int from, int to, int delta)
    {
        Assert.That(Angle.FromUnits(from).DeltaTo(Angle.FromUnits(to)), Is.EqualTo(delta));
    }
}
