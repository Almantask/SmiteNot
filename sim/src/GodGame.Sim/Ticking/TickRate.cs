using System;
using System.Globalization;

namespace GodGame.Sim
{
    /// <summary>
    /// How often something runs: on ticks where (tick − phase) is a multiple of the period. A period of 1 runs every
    /// tick. <see cref="IsDueFor"/> staggers entities across the period, so a 4 Hz regiment AI spreads its regiments
    /// over five ticks instead of thinking all at once (plan, section 3.4).
    /// </summary>
    public readonly struct TickRate : IEquatable<TickRate>
    {
        public static readonly TickRate EveryTick = new TickRate(1, 0);

        public readonly int Period;
        public readonly int Phase;

        public TickRate(int period, int phase = 0)
        {
            if (period < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(period));
            }
            if (phase < 0 || phase >= period)
            {
                throw new ArgumentOutOfRangeException(nameof(phase));
            }
            Period = period;
            Phase = phase;
        }

        /// <summary>A rate in hertz; the tick rate must divide evenly by it.</summary>
        public static TickRate FromHertz(int hertz, int phase = 0)
        {
            if (hertz < 1 || SimConstants.TicksPerSecond % hertz != 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(hertz),
                    hertz.ToString(CultureInfo.InvariantCulture)
                        + " Hz does not divide "
                        + SimConstants.TicksPerSecond.ToString(CultureInfo.InvariantCulture)
                        + " ticks per second."
                );
            }
            return new TickRate(SimConstants.TicksPerSecond / hertz, phase);
        }

        // Every term is reduced below Period before subtracting, so nothing overflows for any tick or index.
        public bool IsDue(int tick) => Modulo(tick % Period - Phase) == 0;

        /// <summary>Whether entity <paramref name="index"/> (a regiment or tile index) runs on this tick: each entity gets its own phase.</summary>
        public bool IsDueFor(int tick, int index) =>
            Modulo(tick % Period - Phase - index % Period) == 0;

        private int Modulo(int value)
        {
            int m = value % Period;
            return m < 0 ? m + Period : m;
        }

        public bool Equals(TickRate other) => Period == other.Period && Phase == other.Phase;

        public override bool Equals(object? obj) => obj is TickRate other && Equals(other);

        public override int GetHashCode() => unchecked(Period * 397 ^ Phase);

        public override string ToString() =>
            "every "
            + Period.ToString(CultureInfo.InvariantCulture)
            + " ticks from "
            + Phase.ToString(CultureInfo.InvariantCulture);
    }
}
