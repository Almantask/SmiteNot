using System;
using System.Globalization;

namespace GodGame.Sim.Math
{
    /// <summary>
    /// A 16-bit binary angle: 65,536 units per turn, wrapping on overflow (plan, section 3.3).
    /// 0 points along +X and angles grow counter-clockwise toward +Y. One unit is about 0.0055°.
    /// </summary>
    public readonly struct Angle : IEquatable<Angle>
    {
        public const int UnitsPerTurn = 1 << 16;
        public const int QuarterTurnUnits = UnitsPerTurn / 4;

        public static readonly Angle Zero = new Angle(0);
        public static readonly Angle QuarterTurn = new Angle(QuarterTurnUnits);
        public static readonly Angle HalfTurn = new Angle(UnitsPerTurn / 2);

        public readonly ushort Units;

        private Angle(ushort units)
        {
            Units = units;
        }

        /// <summary>Any whole number of units; it wraps into one turn.</summary>
        public static Angle FromUnits(int units) => new Angle(unchecked((ushort)units));

        /// <summary>Hundredths of a degree (rules data and fields speak these), rounded to the nearest unit.</summary>
        public static Angle FromCentiDegrees(int centiDegrees)
        {
            // 36,000 centi-degrees per turn; round half away from zero so mirrored data stays mirrored.
            long scaled = (long)centiDegrees * UnitsPerTurn;
            long units = scaled >= 0 ? (scaled + 18_000) / 36_000 : (scaled - 18_000) / 36_000;
            return FromUnits(unchecked((int)units));
        }

        /// <summary>The same direction in hundredths of a degree, 0 to 35,999, rounded to the nearest.</summary>
        public int ToCentiDegrees() =>
            (int)(((long)Units * 36_000 + UnitsPerTurn / 2) / UnitsPerTurn) % 36_000;

        /// <summary>
        /// The signed difference in units, -32,768 to 32,767: how far to turn from this angle to reach <paramref name="target"/>.
        /// Exactly half a turn always gives -32,768 (clockwise), so mirrored scenarios need their own tie-break there.
        /// </summary>
        public int DeltaTo(Angle target) => unchecked((short)(target.Units - Units));

        public static Angle operator +(Angle a, Angle b) => FromUnits(a.Units + b.Units);

        public static Angle operator -(Angle a, Angle b) => FromUnits(a.Units - b.Units);

        public static Angle operator -(Angle a) => FromUnits(-a.Units);

        public static bool operator ==(Angle a, Angle b) => a.Units == b.Units;

        public static bool operator !=(Angle a, Angle b) => a.Units != b.Units;

        public bool Equals(Angle other) => Units == other.Units;

        public override bool Equals(object? obj) => obj is Angle other && Units == other.Units;

        public override int GetHashCode() => Units;

        public override string ToString() => Units.ToString(CultureInfo.InvariantCulture) + "u";
    }
}
