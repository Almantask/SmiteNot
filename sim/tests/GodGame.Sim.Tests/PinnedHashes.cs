namespace GodGame.Sim.Tests;

/// <summary>
/// Hashes pinned by hand in M0, before the golden replay infrastructure exists (M1 replaces this).
/// Change them only together with a SimVersion bump and a line in sim/CHANGELOG-sim.md.
/// </summary>
internal static class PinnedHashes
{
    public const ulong PlaceholderSeed1Units8After60s = 13820591963978316701UL; // SimVersion 11 (the hash includes it)

    /// <summary>Every sine value plus atan2 over a 129 × 129 grid (TrigTests). Pinned 2026-11-01.</summary>
    public const ulong TrigTables = 0xFA3751819458DAD5UL;

    /// <summary>World.AppendTo after WorldTests' seed-7 battle with despawns and a reused slot. Pinned 2026-11-01.</summary>
    public const ulong WorldBattleSeed7 = 1631372241034031227UL;

    /// <summary>RulesetHash of data/rules (unit-types.json with speed, radius and formation, movement-classes.json, hazards.json, routes.json and fields.json, v0, placeholder values; canonical form without $comment). Pinned 2026-11-02.</summary>
    public const ulong RulesV0 = 18088109343337982458UL;

    /// <summary>data/maps/m1-flat (map hash), data/plans/m1-ten-thousand.json (plan hash) and the world it deploys. Pinned 2026-11-01.</summary>
    public const ulong MapM1Flat = 0x59885A55C9609ABBUL;
    public const ulong PlanM1TenThousand = 0xE6CC8D05245963B5UL;
    public const ulong WorldM1TenThousandDeployed = 11205715549797829735UL;

    /// <summary>All four cost fields over a seeded 64 × 48 terrain with the repo's movement classes (TerrainCostTests). Pinned 2026-11-02.</summary>
    public const ulong CostFieldsSeed6 = 0x6DB36691903731AFUL;

    /// <summary>Distances and directions of a horse flow field over a seeded 64 × 48 terrain (FlowFieldTests). Pinned 2026-11-02.</summary>
    public const ulong FlowFieldSeed41 = 2360656639287698109UL;
}
