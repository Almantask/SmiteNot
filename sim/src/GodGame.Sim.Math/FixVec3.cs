using System;
using System.Diagnostics;

namespace GodGame.Sim.Math
{
    /// <summary>A 3D vector of <see cref="Fix"/>: X east, Y north, Z up, in metres (launched soldiers, boulders).</summary>
    public readonly struct FixVec3 : IEquatable<FixVec3>
    {
        public static readonly FixVec3 Zero = new FixVec3(Fix.Zero, Fix.Zero, Fix.Zero);

        public readonly Fix X;
        public readonly Fix Y;
        public readonly Fix Z;

        public FixVec3(Fix x, Fix y, Fix z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public FixVec3(FixVec2 ground, Fix z)
            : this(ground.X, ground.Y, z) { }

        public static FixVec3 FromInts(long x, long y, long z) =>
            new FixVec3(Fix.FromInt(x), Fix.FromInt(y), Fix.FromInt(z));

        /// <summary>The ground-plane part (X, Y).</summary>
        public FixVec2 Ground => new FixVec2(X, Y);

        /// <summary>Exact while each product stays under 2^31 and their sum fits (components up to about 26 km).</summary>
        public Fix LengthSquared => X * X + Y * Y + Z * Z;

        /// <summary>The floor of the true length, in raw units, from unrounded squares (see <see cref="FixVec2.Length"/>). Components up to 32 km.</summary>
        public Fix Length => Fix.FromRaw(IntMath.Sqrt(RawLengthSquared()));

        public static Fix Dot(FixVec3 a, FixVec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static FixVec3 Cross(FixVec3 a, FixVec3 b) =>
            new FixVec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

        public static Fix DistanceSquared(FixVec3 a, FixVec3 b) => (a - b).LengthSquared;

        public static FixVec3 operator +(FixVec3 a, FixVec3 b) =>
            new FixVec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static FixVec3 operator -(FixVec3 a, FixVec3 b) =>
            new FixVec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static FixVec3 operator -(FixVec3 a) => new FixVec3(-a.X, -a.Y, -a.Z);

        public static FixVec3 operator *(FixVec3 a, Fix s) =>
            new FixVec3(a.X * s, a.Y * s, a.Z * s);

        public static FixVec3 operator *(FixVec3 a, int s) =>
            new FixVec3(a.X * s, a.Y * s, a.Z * s);

        public static FixVec3 operator /(FixVec3 a, Fix s) =>
            new FixVec3(a.X / s, a.Y / s, a.Z / s);

        public static FixVec3 operator /(FixVec3 a, int s) =>
            new FixVec3(a.X / s, a.Y / s, a.Z / s);

        public static bool operator ==(FixVec3 a, FixVec3 b) =>
            a.X == b.X && a.Y == b.Y && a.Z == b.Z;

        public static bool operator !=(FixVec3 a, FixVec3 b) => !(a == b);

        public bool Equals(FixVec3 other) => this == other;

        public override bool Equals(object? obj) => obj is FixVec3 other && this == other;

        /// <summary>For .NET collections only; state hashes feed the raw values to <see cref="SimHash"/>.</summary>
        public override int GetHashCode() =>
            unchecked((int)Fold((X.Raw * 31 + Y.Raw) * 31 + Z.Raw));

        private ulong RawLengthSquared()
        {
            ulong x = Magnitude(X.Raw);
            ulong y = Magnitude(Y.Raw);
            ulong z = Magnitude(Z.Raw);
            // Each square stays under 2^62, so three of them fit a ulong.
            Debug.Assert(
                x <= MaxRawComponent && y <= MaxRawComponent && z <= MaxRawComponent,
                "FixVec3 length overflow"
            );
            return x * x + y * y + z * z;
        }

        private const ulong MaxRawComponent = 1UL << 31;

        private static ulong Magnitude(long raw) =>
            raw >= 0 ? (ulong)raw : unchecked((ulong)(-(raw + 1)) + 1);

        private static long Fold(long value) => value ^ (value >> 32);

        public override string ToString() => "(" + X + ", " + Y + ", " + Z + ")";
    }
}
