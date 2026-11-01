using System.Numerics;
using GodGame.Sim.Math;

namespace GodGame.Sim.Tests;

[TestFixture, Category("Fast")]
public class FixVecTests
{
    // Components up to about 1.6 km either way: past the 1.2 × 0.8 km working map.
    private static Fix MapCoordinate(ref Pcg32 rng) =>
        Fix.FromRaw(rng.NextInt(-100_000_000, 100_000_001));

    private static long FloorShift(BigInteger value) =>
        (long)(
            value >= 0
                ? value >> Fix.FractionalBits
                : -((-value + Fix.OneRaw - 1) >> Fix.FractionalBits)
        );

    [Test]
    public void DotCrossAndLengthSquared_AtMapScale_MatchFixArithmetic()
    {
        var rng = new Pcg32(31, 1);
        for (int i = 0; i < 20_000; i++)
        {
            var a = new FixVec2(MapCoordinate(ref rng), MapCoordinate(ref rng));
            var b = new FixVec2(MapCoordinate(ref rng), MapCoordinate(ref rng));
            long ax = a.X.Raw,
                ay = a.Y.Raw,
                bx = b.X.Raw,
                by = b.Y.Raw;

            // Each product rounds down on its own, then the sum is exact.
            long dot = FloorShift(new BigInteger(ax) * bx) + FloorShift(new BigInteger(ay) * by);
            long cross = FloorShift(new BigInteger(ax) * by) - FloorShift(new BigInteger(ay) * bx);
            long lengthSquared =
                FloorShift(new BigInteger(ax) * ax) + FloorShift(new BigInteger(ay) * ay);

            Assert.That(FixVec2.Dot(a, b).Raw, Is.EqualTo(dot));
            Assert.That(FixVec2.Cross(a, b).Raw, Is.EqualTo(cross));
            Assert.That(a.LengthSquared.Raw, Is.EqualTo(lengthSquared));
        }
    }

    [Test]
    public void LengthSquared_AcrossTheWholeMap_DoesNotOverflow()
    {
        var corner = FixVec2.FromInts(1200, 800);

        Assert.That(corner.LengthSquared, Is.EqualTo(Fix.FromInt(1200 * 1200 + 800 * 800)));
        Assert.That(FixVec2.FromInts(3, 4).Length, Is.EqualTo(Fix.FromInt(5)));
        Assert.That(FixVec3.FromInts(2, 3, 6).Length, Is.EqualTo(Fix.FromInt(7)));
    }

    [Test]
    public void Rotate_QuarterTurn_IsExact()
    {
        var east = FixVec2.FromInts(5, 0);

        Assert.That(east.Rotate(Angle.QuarterTurn), Is.EqualTo(FixVec2.FromInts(0, 5)));
        Assert.That(east.Rotate(Angle.HalfTurn), Is.EqualTo(FixVec2.FromInts(-5, 0)));
    }

    [Test]
    public void FromAngleAndDirection_RoundTrip()
    {
        var angle = Angle.FromCentiDegrees(3_000);
        var v = FixVec2.FromAngle(angle, Fix.FromInt(50));

        Assert.That(System.Math.Abs(angle.DeltaTo(v.Direction)), Is.LessThanOrEqualTo(1));
        Assert.That(System.Math.Abs((v.Length - Fix.FromInt(50)).Raw), Is.LessThanOrEqualTo(8));
    }

    [Test]
    public void WithLength_KeepsDirectionAndZero()
    {
        var v = FixVec2.FromInts(30, 40).WithLength(Fix.FromInt(10));

        Assert.That(v, Is.EqualTo(FixVec2.FromInts(6, 8)));
        Assert.That(FixVec2.Zero.WithLength(Fix.One), Is.EqualTo(FixVec2.Zero));
    }

    [Test]
    public void ShortVectors_KeepTheirLengthAndDirection()
    {
        // 100 raw units is about 1.5 mm: its Fix square floors to zero, but the length must not.
        var tiny = new FixVec2(Fix.FromRaw(100), Fix.Zero);

        Assert.That(tiny.Length, Is.EqualTo(Fix.FromRaw(100)));
        Assert.That(new FixVec2(Fix.FromRaw(3), Fix.FromRaw(4)).Length, Is.EqualTo(Fix.FromRaw(5)));
        Assert.That(tiny.WithLength(Fix.One), Is.EqualTo(FixVec2.FromInts(1, 0)));
        Assert.That(
            new FixVec3(Fix.FromRaw(2), Fix.FromRaw(3), Fix.FromRaw(6)).Length,
            Is.EqualTo(Fix.FromRaw(7))
        );
    }

    [Test]
    public void Length_IsTheFloorOfTheExactLength()
    {
        var rng = new Pcg32(31, 2);
        for (int i = 0; i < 20_000; i++)
        {
            var v = new FixVec2(MapCoordinate(ref rng), MapCoordinate(ref rng));
            var squared = new BigInteger(v.X.Raw) * v.X.Raw + new BigInteger(v.Y.Raw) * v.Y.Raw;
            long length = v.Length.Raw;

            Assert.That(new BigInteger(length) * length, Is.LessThanOrEqualTo(squared));
            Assert.That(new BigInteger(length + 1) * (length + 1), Is.GreaterThan(squared));
        }
    }

    [Test]
    public void Cross3_OfAxes_IsTheThirdAxis()
    {
        var x = FixVec3.FromInts(1, 0, 0);
        var y = FixVec3.FromInts(0, 1, 0);

        Assert.That(FixVec3.Cross(x, y), Is.EqualTo(FixVec3.FromInts(0, 0, 1)));
        Assert.That(FixVec3.Cross(y, x), Is.EqualTo(FixVec3.FromInts(0, 0, -1)));
        Assert.That(FixVec3.Dot(x, y), Is.EqualTo(Fix.Zero));
    }

    [Test]
    public void Operators_ActPerComponent()
    {
        var a = FixVec3.FromInts(1, -2, 3);
        var b = FixVec3.FromInts(4, 5, -6);

        Assert.That(a + b, Is.EqualTo(FixVec3.FromInts(5, 3, -3)));
        Assert.That(a - b, Is.EqualTo(FixVec3.FromInts(-3, -7, 9)));
        Assert.That(-a, Is.EqualTo(FixVec3.FromInts(-1, 2, -3)));
        Assert.That(a * 2, Is.EqualTo(FixVec3.FromInts(2, -4, 6)));
        Assert.That(
            b / Fix.FromInt(2),
            Is.EqualTo(new FixVec3(Fix.FromInt(2), Fix.FromRatio(5, 2), Fix.FromInt(-3)))
        );
        Assert.That(a.Ground, Is.EqualTo(FixVec2.FromInts(1, -2)));
        Assert.That(new FixVec3(a.Ground, Fix.One).Z, Is.EqualTo(Fix.One));
    }

    [Test]
    public void EqualVectors_HashTheSame()
    {
        var a = new FixVec2(Fix.FromRatio(1, 2), Fix.FromInt(3));
        var b = FixVec2.FromInts(1, 6) / 2;

        Assert.That(a, Is.EqualTo(b));
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }
}
