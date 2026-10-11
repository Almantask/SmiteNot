namespace GodGame.Sim
{
    /// <summary>
    /// Bumped whenever battle outcomes change, and only through the golden-replay skill,
    /// with a line in sim/CHANGELOG-sim.md. Replays and golden files record it.
    /// </summary>
    public static class SimVersion
    {
        public const int Current = 1;
    }
}
