namespace GodGame.Sim.Math
{
    /// <summary>
    /// PCG32 (XSH RR 64/32), O'Neill 2014. A small, fast generator whose output is identical on every platform.
    /// Get streams from <see cref="SimRandom"/>; never share one generator between systems or threads.
    /// </summary>
    public struct Pcg32
    {
        private const ulong Multiplier = 6364136223846793005UL;

        private ulong _state;
        private readonly ulong _increment;

        /// <summary>Same seeding as the reference pcg32_srandom_r(initState, initSequence).</summary>
        public Pcg32(ulong initState, ulong initSequence)
        {
            _state = 0;
            _increment = (initSequence << 1) | 1UL;
            NextUInt();
            unchecked
            {
                _state += initState;
            }
            NextUInt();
        }

        public uint NextUInt()
        {
            unchecked
            {
                ulong old = _state;
                _state = old * Multiplier + _increment;
                uint xorShifted = (uint)(((old >> 18) ^ old) >> 27);
                int rotation = (int)(old >> 59);
                return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
            }
        }

        /// <summary>Uniform in [0, bound), without modulo bias. Bound must be at least 1.</summary>
        public uint NextUInt(uint bound)
        {
            if (bound == 0)
            {
                throw new System.ArgumentOutOfRangeException(
                    nameof(bound),
                    "Bound must be at least 1."
                );
            }
            unchecked
            {
                uint threshold = (0U - bound) % bound;
                while (true)
                {
                    uint r = NextUInt();
                    if (r >= threshold)
                    {
                        return r % bound;
                    }
                }
            }
        }

        /// <summary>Uniform in [minInclusive, maxExclusive).</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            unchecked
            {
                uint range = (uint)(maxExclusive - minInclusive);
                return minInclusive + (int)NextUInt(range);
            }
        }
    }
}
