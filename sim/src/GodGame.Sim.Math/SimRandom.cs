using System;
using System.Buffers.Binary;

namespace GodGame.Sim.Math
{
    /// <summary>
    /// Derives independent random streams from (seed, system, entity, tick), so no generator is ever shared
    /// and the order systems or threads ask for randomness can't change the result.
    /// </summary>
    public static class SimRandom
    {
        /// <summary>
        /// xxHash64 of (system, entity, tick) seeded with the battle seed gives the state; adding 0x5EED gives the
        /// sequence. Allocation-free (it runs per unit inside the tick), and bit-identical to hashing the same values
        /// with a <see cref="SimHash"/>.
        /// </summary>
        public static Pcg32 Stream(ulong seed, int systemId, int entityId, int tick)
        {
            Span<byte> input = stackalloc byte[16];
            BinaryPrimitives.WriteInt32LittleEndian(input, systemId);
            BinaryPrimitives.WriteInt32LittleEndian(input.Slice(4), entityId);
            BinaryPrimitives.WriteInt32LittleEndian(input.Slice(8), tick);
            BinaryPrimitives.WriteInt32LittleEndian(input.Slice(12), 0x5EED);
            ulong state = SimHash.HashShort(input.Slice(0, 12), seed);
            ulong sequence = SimHash.HashShort(input, seed);
            return new Pcg32(state, sequence);
        }
    }
}
