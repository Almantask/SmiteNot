namespace GodGame.Sim.Tests;

/// <summary>
/// Hashes pinned by hand in M0, before the golden replay infrastructure exists (M1 replaces this).
/// Change them only together with a SimVersion bump and a line in sim/CHANGELOG-sim.md.
/// </summary>
internal static class PinnedHashes
{
    public const ulong PlaceholderSeed1Units8After60s = 10918359786234566319UL; // SimVersion 22 (the hash includes it)

    /// <summary>Every sine value plus atan2 over a 129 × 129 grid (TrigTests). Pinned 2026-11-01.</summary>
    public const ulong TrigTables = 0xFA3751819458DAD5UL;

    /// <summary>World.AppendTo after WorldTests' seed-7 battle with despawns and a reused slot. Re-pinned 2026-11-15 (SimVersion 20: battle-made blockers and the salvo scatter in the hashed state).</summary>
    public const ulong WorldBattleSeed7 = 914820964043008312UL;

    /// <summary>RulesetHash of every file in data/rules (v0, placeholder values; canonical form without $comment), launches.json included. Re-pinned 2026-11-15 (SimVersion 20: the base types, crews, the siege stone, the wreck stamp).</summary>
    public const ulong RulesV0 = 11644256289627313448UL;

    /// <summary>data/maps/m1-flat (map hash), data/plans/m1-ten-thousand.json (plan hash) and the world it deploys. Pinned 2026-11-01; the plan and world re-pinned 2026-11-15 (SimVersion 20: siege regiments of 4 engines with their crews, infantry of 132).</summary>
    public const ulong MapM1Flat = 0x59885A55C9609ABBUL;
    public const ulong PlanM1TenThousand = 1782868495505118719UL;
    public const ulong WorldM1TenThousandDeployed = 12151633175486177079UL;

    /// <summary>All four cost fields over a seeded 64 × 48 terrain with the repo's movement classes (TerrainCostTests). Pinned 2026-11-02.</summary>
    public const ulong CostFieldsSeed6 = 0x6DB36691903731AFUL;

    /// <summary>Distances and directions of a horse flow field over a seeded 64 × 48 terrain (FlowFieldTests). Pinned 2026-11-02.</summary>
    public const ulong FlowFieldSeed41 = 2360656639287698109UL;
}
