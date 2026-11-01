namespace GodGame.Sim.Math
{
    /// <summary>
    /// Sine, cosine and atan2 on binary angles, from tables (plan, section 3.3).
    /// The tables are built once with integer-only series, so every runtime builds the same bits.
    /// </summary>
    public static class Trig
    {
        // Working precision for building the tables: 30 fractional bits. The sine series' largest product
        // (term × x² at x = π/2) is about 4.5 × 10^18, half of long.MaxValue; 31 bits would overflow.
        private const int WorkBits = 30;
        private const long WorkOne = 1L << WorkBits;

        // π × 2^30, rounded to the nearest integer.
        private const long PiWork = 3_373_259_426L;

        /// <summary>Entries per quarter turn of the sine table (one per angle unit).</summary>
        private const int QuarterUnits = Angle.QuarterTurnUnits;

        /// <summary>The atan table covers ratios 0 to 1 in steps of 1/4096.</summary>
        private const int AtanSteps = 4096;

        // Atan results carry 8 extra fractional bits so the interpolation rounds once, at the end.
        private const int AtanExtraBits = 8;

        // Ratios are taken with 20 fractional bits: 12 index the table, 8 interpolate.
        private const int RatioBits = 20;

        /// <summary>sin of 0 to a quarter turn, as raw Fix values (0 to 65,536). Never written after construction.</summary>
        private static readonly int[] QuarterSine = BuildQuarterSine();

        /// <summary>atan(j / 4096) for j = 0 to 4096, in angle units × 2^8 (0 to 8192 × 2^8).</summary>
        private static readonly int[] AtanTable = BuildAtanTable();

        public static Fix Sin(Angle angle)
        {
            int units = angle.Units;
            int quadrant = units >> 14;
            int index = units & (QuarterUnits - 1);
            int value =
                (quadrant & 1) == 0 ? QuarterSine[index] : QuarterSine[QuarterUnits - index];
            return Fix.FromRaw(quadrant < 2 ? value : -value);
        }

        public static Fix Cos(Angle angle) => Sin(angle + Angle.QuarterTurn);

        /// <summary>
        /// The direction of (x, y), 0 along +X, counter-clockwise. (0, 0) gives 0.
        /// Within one unit of the exact angle for any inputs.
        /// </summary>
        public static Angle Atan2(Fix y, Fix x)
        {
            // Magnitudes as ulong so long.MinValue works too.
            ulong ax = Magnitude(x.Raw);
            ulong ay = Magnitude(y.Raw);
            if (ax == 0 && ay == 0)
            {
                return Angle.Zero;
            }

            // The angle within the first octant comes from the smaller over the larger magnitude.
            bool steep = ay > ax;
            ulong small = steep ? ax : ay;
            ulong large = steep ? ay : ax;
            // Keep both under 2^40 so small << 20 fits; the ratio barely changes.
            while (large >= 1UL << 40)
            {
                small >>= 1;
                large >>= 1;
            }
            long ratio = (long)((small << RatioBits) / large); // 0 to 2^20
            int octant = AtanOctant(ratio);
            if (steep)
            {
                octant = Angle.QuarterTurnUnits - octant;
            }

            bool negativeX = x.Raw < 0;
            bool negativeY = y.Raw < 0;
            int units;
            if (!negativeX)
            {
                units = negativeY ? -octant : octant;
            }
            else
            {
                units = negativeY
                    ? Angle.UnitsPerTurn / 2 + octant
                    : Angle.UnitsPerTurn / 2 - octant;
            }
            return Angle.FromUnits(units);
        }

        private static ulong Magnitude(long raw) =>
            raw >= 0 ? (ulong)raw : unchecked((ulong)(-(raw + 1)) + 1);

        /// <summary>atan of a ratio in [0, 1] with 20 fractional bits, in whole angle units (0 to 8192).</summary>
        private static int AtanOctant(long ratio)
        {
            int index = (int)(ratio >> (RatioBits - 12));
            int fraction = (int)(ratio & ((1 << (RatioBits - 12)) - 1));
            int from = AtanTable[index];
            int to = index < AtanSteps ? AtanTable[index + 1] : from;
            long interpolated = from + (((long)(to - from) * fraction) >> (RatioBits - 12));
            return (int)((interpolated + (1 << (AtanExtraBits - 1))) >> AtanExtraBits);
        }

        private static long MulWork(long a, long b) => (a * b + (WorkOne >> 1)) >> WorkBits;

        private static int[] BuildQuarterSine()
        {
            var table = new int[QuarterUnits + 1];
            long halfPi = PiWork / 2;
            for (int i = 0; i <= QuarterUnits; i++)
            {
                // x = i × (π/2) / 16384, rounded, in working precision (at most about 1.571).
                long x = (i * halfPi + QuarterUnits / 2) / QuarterUnits;
                long x2 = MulWork(x, x);
                // sin x = x - x³/3! + x⁵/5! - ...; at x = π/2 the term after x^19 is below 2^-40.
                long term = x;
                long sum = x;
                for (int k = 1; k <= 9; k++)
                {
                    term = -MulWork(term, x2) / ((2 * k) * (2 * k + 1));
                    sum += term;
                }
                // Down to 16 fractional bits, rounded to nearest.
                long value =
                    (sum + (1L << (WorkBits - Fix.FractionalBits - 1)))
                    >> (WorkBits - Fix.FractionalBits);
                table[i] = (int)System.Math.Min(System.Math.Max(value, 0), Fix.OneRaw);
            }
            table[0] = 0;
            table[QuarterUnits] = (int)Fix.OneRaw;
            return table;
        }

        private static int[] BuildAtanTable()
        {
            var table = new int[AtanSteps + 1];
            for (int j = 0; j <= AtanSteps; j++)
            {
                long t = (long)j * WorkOne / AtanSteps; // 0 to 1
                // Halve the angle once so the series converges fast: atan t = 2 atan(t / (1 + sqrt(1 + t²))).
                long onePlusT2 = WorkOne + MulWork(t, t);
                long root = IntMath.Sqrt((ulong)onePlusT2 << WorkBits); // sqrt in working precision
                long u = (t << WorkBits) / (WorkOne + root); // at most tan(π/8) ≈ 0.414
                long u2 = MulWork(u, u);
                // atan u = u - u³/3 + u⁵/5 - ...; 0.414^29 / 29 is below 2^-40.
                long power = u;
                long sum = u;
                for (int k = 1; k <= 14; k++)
                {
                    power = -MulWork(power, u2);
                    sum += power / (2 * k + 1);
                }
                long radians = 2 * sum;
                // Radians to angle units × 2^8: units = rad × 65536 / 2π, so units × 2^8 = rad_work × 2^24 / (2π × 2^30).
                long scaled = radians << 24;
                long denominator = 2 * PiWork;
                table[j] = (int)((scaled + denominator / 2) / denominator);
            }
            return table;
        }
    }
}
