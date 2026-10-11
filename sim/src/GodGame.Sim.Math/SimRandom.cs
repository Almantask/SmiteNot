namespace GodGame.Sim.Math
{
    /// <summary>
    /// Derives independent random streams from (seed, system, entity, tick), so no generator is ever shared
    /// and the order systems or threads ask for randomness can't change the result.
    /// </summary>
    public static class SimRandom
    {
        public static Pcg32 Stream(ulong seed, int systemId, int entityId, int tick)
        {
            var hash = new SimHash(seed);
            hash.AppendInt32(systemId);
            hash.AppendInt32(entityId);
            hash.AppendInt32(tick);
            ulong state = hash.Digest();
            hash.AppendInt32(0x5EED);
            ulong sequence = hash.Digest();
            return new Pcg32(state, sequence);
        }
    }
}
