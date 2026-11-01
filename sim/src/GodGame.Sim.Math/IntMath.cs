namespace GodGame.Sim.Math
{
    /// <summary>Integer helpers that give the same answer on every platform and runtime.</summary>
    public static class IntMath
    {
        /// <summary>Floor of the square root, by the bit-by-bit method (no floating point, no tables).</summary>
        public static uint Sqrt(ulong value)
        {
            ulong remainder = value;
            ulong root = 0;
            ulong bit = 1UL << 62; // the highest power of four a ulong holds
            while (bit > remainder)
            {
                bit >>= 2;
            }
            while (bit != 0)
            {
                if (remainder >= root + bit)
                {
                    remainder -= root + bit;
                    root = (root >> 1) + bit;
                }
                else
                {
                    root >>= 1;
                }
                bit >>= 2;
            }
            return (uint)root;
        }
    }
}
