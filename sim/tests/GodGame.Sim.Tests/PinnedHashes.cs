namespace GodGame.Sim.Tests;

/// <summary>
/// Hashes pinned by hand in M0, before the golden replay infrastructure exists (M1 replaces this).
/// Change them only together with a SimVersion bump and a line in sim/CHANGELOG-sim.md.
/// </summary>
internal static class PinnedHashes
{
    public const ulong PlaceholderSeed1Units8After60s = 0xADB4E108642D52BCUL;

    /// <summary>Every sine value plus atan2 over a 129 × 129 grid (TrigTests). Pinned 2026-11-01.</summary>
    public const ulong TrigTables = 0xFA3751819458DAD5UL;
}
