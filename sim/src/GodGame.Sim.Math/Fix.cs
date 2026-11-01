using System;
using System.Diagnostics;
using System.Globalization;

namespace GodGame.Sim.Math
{
    /// <summary>
    /// A 64-bit signed fixed-point number with 16 fractional bits, for positions and velocities (plan, section 3.3).
    /// In metres the resolution is about 0.015 mm. Arithmetic is unchecked; debug builds assert on overflow.
    /// Rounding is part of the contract: products round toward negative infinity (arithmetic shift),
    /// quotients toward zero (integer division).
    /// </summary>
    public readonly struct Fix : IEquatable<Fix>, IComparable<Fix>
    {
        public const int FractionalBits = 16;
        public const long OneRaw = 1L << FractionalBits;

        /// <summary>Whole numbers from <see cref="FromInt"/> must stay inside ±2^47 to fit.</summary>
        public const long MaxWhole = (1L << (63 - FractionalBits)) - 1;

        public static readonly Fix Zero = new Fix(0);
        public static readonly Fix One = new Fix(OneRaw);
        public static readonly Fix Half = new Fix(OneRaw / 2);
        public static readonly Fix MinValue = new Fix(long.MinValue);
        public static readonly Fix MaxValue = new Fix(long.MaxValue);

        public readonly long Raw;

        private Fix(long raw)
        {
            Raw = raw;
        }

        public static Fix FromRaw(long raw) => new Fix(raw);

        public static Fix FromInt(long whole)
        {
            Debug.Assert(whole >= -MaxWhole - 1 && whole <= MaxWhole, "Fix.FromInt overflow");
            return new Fix(whole << FractionalBits);
        }

        /// <summary>numerator / denominator, rounded toward zero. Use for rules data such as "1.25" → (125, 100).</summary>
        public static Fix FromRatio(long numerator, long denominator) =>
            FromInt(numerator) / FromInt(denominator);

        /// <summary>Rounded toward negative infinity.</summary>
        public long FloorToInt() => Raw >> FractionalBits;

        public long CeilingToInt()
        {
            Debug.Assert(Raw <= long.MaxValue - (OneRaw - 1), "Fix.CeilingToInt overflow");
            return unchecked((Raw + (OneRaw - 1)) >> FractionalBits);
        }

        /// <summary>Nearest whole number; halves round up (toward positive infinity).</summary>
        public long RoundToInt()
        {
            Debug.Assert(Raw <= long.MaxValue - OneRaw / 2, "Fix.RoundToInt overflow");
            return unchecked((Raw + OneRaw / 2) >> FractionalBits);
        }

        public static Fix operator +(Fix a, Fix b)
        {
            long sum = unchecked(a.Raw + b.Raw);
            Debug.Assert(((a.Raw ^ sum) & (b.Raw ^ sum)) >= 0, "Fix addition overflow");
            return new Fix(sum);
        }

        public static Fix operator -(Fix a, Fix b)
        {
            long difference = unchecked(a.Raw - b.Raw);
            Debug.Assert(((a.Raw ^ b.Raw) & (a.Raw ^ difference)) >= 0, "Fix subtraction overflow");
            return new Fix(difference);
        }

        public static Fix operator -(Fix a)
        {
            Debug.Assert(a.Raw != long.MinValue, "Fix negation overflow");
            return new Fix(unchecked(-a.Raw));
        }

        /// <summary>A 64-bit multiply and shift: exact while |a × b| stays under 2^31 (about 2.1 × 10^9 m²).</summary>
        public static Fix operator *(Fix a, Fix b)
        {
            long product = unchecked(a.Raw * b.Raw);
            Debug.Assert(
                // The guard comes before the division: long.MinValue / -1 throws.
                a.Raw == 0
                    || (!(a.Raw == -1 && b.Raw == long.MinValue) && product / a.Raw == b.Raw),
                "Fix multiplication overflow"
            );
            return new Fix(product >> FractionalBits);
        }

        /// <summary>Exact while |a| stays under 2^31.</summary>
        public static Fix operator /(Fix a, Fix b)
        {
            if (b.Raw == 0)
            {
                throw new DivideByZeroException();
            }
            Debug.Assert(a.Raw >> (63 - FractionalBits) is 0 or -1, "Fix division overflow");
            long scaled = unchecked(a.Raw << FractionalBits);
            // long.MinValue / -1 throws on .NET and Mono and is undefined in IL2CPP's C++; negate instead.
            if (b.Raw == -1)
            {
                Debug.Assert(scaled != long.MinValue, "Fix division overflow");
                return new Fix(unchecked(-scaled));
            }
            return new Fix(scaled / b.Raw);
        }

        public static Fix operator *(Fix a, int b)
        {
            long product = unchecked(a.Raw * b);
            Debug.Assert(
                b == 0 || (!(b == -1 && a.Raw == long.MinValue) && product / b == a.Raw),
                "Fix multiplication overflow"
            );
            return new Fix(product);
        }

        /// <summary>Rounded toward zero.</summary>
        public static Fix operator /(Fix a, int b)
        {
            if (b == 0)
            {
                throw new DivideByZeroException();
            }
            // See operator /(Fix, Fix): never let long.MinValue / -1 reach the runtime.
            if (b == -1)
            {
                Debug.Assert(a.Raw != long.MinValue, "Fix division overflow");
                return new Fix(unchecked(-a.Raw));
            }
            return new Fix(a.Raw / b);
        }

        public static Fix Abs(Fix a) => a.Raw < 0 ? -a : a;

        public static Fix Min(Fix a, Fix b) => a.Raw <= b.Raw ? a : b;

        public static Fix Max(Fix a, Fix b) => a.Raw >= b.Raw ? a : b;

        public static Fix Clamp(Fix value, Fix min, Fix max) => Max(min, Min(max, value));

        /// <summary>Square root, rounded down to the nearest representable value. Exact for |a| under 2^31.</summary>
        public static Fix Sqrt(Fix a)
        {
            if (a.Raw < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(a), "Square root of a negative Fix.");
            }
            Debug.Assert(a.Raw >> (63 - FractionalBits) == 0, "Fix square root overflow");
            return new Fix(IntMath.Sqrt((ulong)a.Raw << FractionalBits));
        }

        public static bool operator ==(Fix a, Fix b) => a.Raw == b.Raw;

        public static bool operator !=(Fix a, Fix b) => a.Raw != b.Raw;

        public static bool operator <(Fix a, Fix b) => a.Raw < b.Raw;

        public static bool operator >(Fix a, Fix b) => a.Raw > b.Raw;

        public static bool operator <=(Fix a, Fix b) => a.Raw <= b.Raw;

        public static bool operator >=(Fix a, Fix b) => a.Raw >= b.Raw;

        public bool Equals(Fix other) => Raw == other.Raw;

        public override bool Equals(object? obj) => obj is Fix other && Raw == other.Raw;

        /// <summary>For .NET collections only; state hashes feed <see cref="Raw"/> to <see cref="SimHash"/>.</summary>
        public override int GetHashCode() => unchecked((int)Raw ^ (int)(Raw >> 32));

        public int CompareTo(Fix other) => Raw.CompareTo(other.Raw);

        /// <summary>The exact decimal value (16 fractional bits never need more than 16 digits), invariant culture.</summary>
        public override string ToString()
        {
            // Work on the magnitude as a ulong so long.MinValue formats too.
            bool negative = Raw < 0;
            ulong magnitude = negative ? unchecked((ulong)(-(Raw + 1)) + 1) : (ulong)Raw;
            ulong whole = magnitude >> FractionalBits;
            ulong fraction = magnitude & (OneRaw - 1);
            string text = whole.ToString(CultureInfo.InvariantCulture);
            if (fraction != 0)
            {
                // fraction / 2^16 = fraction × 5^16 / 10^16, and 5^16 × 2^16 fits a ulong.
                const ulong FivePow16 = 152587890625UL;
                string digits = (fraction * FivePow16)
                    .ToString(CultureInfo.InvariantCulture)
                    .PadLeft(16, '0')
                    .TrimEnd('0');
                text += "." + digits;
            }
            return negative ? "-" + text : text;
        }
    }
}
