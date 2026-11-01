using System;
using System.Diagnostics;

namespace GodGame.Sim.Math
{
    /// <summary>A 2D vector of <see cref="Fix"/> on the ground plane: X east, Y north, in metres.</summary>
    public readonly struct FixVec2 : IEquatable<FixVec2>
    {
        public static readonly FixVec2 Zero = new FixVec2(Fix.Zero, Fix.Zero);

        public readonly Fix X;
        public readonly Fix Y;

        public FixVec2(Fix x, Fix y)
        {
            X = x;
            Y = y;
        }

        public static FixVec2 FromInts(long x, long y) =>
            new FixVec2(Fix.FromInt(x), Fix.FromInt(y));

        /// <summary>A vector of the given length pointing along <paramref name="angle"/>.</summary>
        public static FixVec2 FromAngle(Angle angle, Fix length) =>
            new FixVec2(Trig.Cos(angle) * length, Trig.Sin(angle) * length);

        /// <summary>Exact while each product stays under 2^31 (components up to about 32 km).</summary>
        public Fix LengthSquared => X * X + Y * Y;

        /// <summary>
        /// The floor of the true length, in raw units, from unrounded squares: short vectors keep their length
        /// (squaring each Fix component would floor anything under 4 mm to zero). Components up to about 46 km.
        /// </summary>
        public Fix Length => Fix.FromRaw(IntMath.Sqrt(RawLengthSquared()));

        /// <summary>The direction this vector points; the zero vector gives 0.</summary>
        public Angle Direction => Trig.Atan2(Y, X);

        public static Fix Dot(FixVec2 a, FixVec2 b) => a.X * b.X + a.Y * b.Y;

        /// <summary>The z of the 3D cross product: positive when <paramref name="b"/> lies counter-clockwise of <paramref name="a"/>.</summary>
        public static Fix Cross(FixVec2 a, FixVec2 b) => a.X * b.Y - a.Y * b.X;

        public static Fix DistanceSquared(FixVec2 a, FixVec2 b) => (a - b).LengthSquared;

        /// <summary>Rotated counter-clockwise by <paramref name="angle"/>.</summary>
        public FixVec2 Rotate(Angle angle)
        {
            Fix cos = Trig.Cos(angle);
            Fix sin = Trig.Sin(angle);
            return new FixVec2(X * cos - Y * sin, X * sin + Y * cos);
        }

        /// <summary>
        /// The same direction with the given length; the zero vector stays zero.
        /// Each component is the exact quotient rounded toward zero; prefer squared lengths in hot loops.
        /// </summary>
        public FixVec2 WithLength(Fix length)
        {
            long current = IntMath.Sqrt(RawLengthSquared());
            if (current == 0)
            {
                return Zero;
            }
            // Scale the raw components directly; exact while |component| × |length| stays under 2^31 m².
            return new FixVec2(
                Fix.FromRaw(X.Raw * length.Raw / current),
                Fix.FromRaw(Y.Raw * length.Raw / current)
            );
        }

        public static FixVec2 operator +(FixVec2 a, FixVec2 b) => new FixVec2(a.X + b.X, a.Y + b.Y);

        public static FixVec2 operator -(FixVec2 a, FixVec2 b) => new FixVec2(a.X - b.X, a.Y - b.Y);

        public static FixVec2 operator -(FixVec2 a) => new FixVec2(-a.X, -a.Y);

        public static FixVec2 operator *(FixVec2 a, Fix s) => new FixVec2(a.X * s, a.Y * s);

        public static FixVec2 operator *(FixVec2 a, int s) => new FixVec2(a.X * s, a.Y * s);

        public static FixVec2 operator /(FixVec2 a, Fix s) => new FixVec2(a.X / s, a.Y / s);

        public static FixVec2 operator /(FixVec2 a, int s) => new FixVec2(a.X / s, a.Y / s);

        public static bool operator ==(FixVec2 a, FixVec2 b) => a.X == b.X && a.Y == b.Y;

        public static bool operator !=(FixVec2 a, FixVec2 b) => !(a == b);

        public bool Equals(FixVec2 other) => this == other;

        public override bool Equals(object? obj) => obj is FixVec2 other && this == other;

        /// <summary>For .NET collections only; state hashes feed the raw values to <see cref="SimHash"/>.</summary>
        public override int GetHashCode() => unchecked((int)Fold(X.Raw * 31 + Y.Raw));

        private ulong RawLengthSquared()
        {
            ulong x = Magnitude(X.Raw);
            ulong y = Magnitude(Y.Raw);
            // Each square stays under 2^63, so the sum fits a ulong.
            Debug.Assert(x <= MaxRawComponent && y <= MaxRawComponent, "FixVec2 length overflow");
            return x * x + y * y;
        }

        /// <summary>floor(sqrt(2^63)): the largest raw component whose square leaves room for a second one.</summary>
        private const ulong MaxRawComponent = 3_037_000_499UL;

        private static ulong Magnitude(long raw) =>
            raw >= 0 ? (ulong)raw : unchecked((ulong)(-(raw + 1)) + 1);

        private static long Fold(long value) => value ^ (value >> 32);

        public override string ToString() => "(" + X + ", " + Y + ")";
    }
}
