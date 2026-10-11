using BenchmarkDotNet.Attributes;

namespace GodGame.Sim.Bench;

/// <summary>
/// M0 baseline: one tick of the placeholder simulation. M1 records its real baseline in budgets.json
/// ("tick_ms" at 10,000 units); the perf-check skill compares results with that file.
/// </summary>
[MemoryDiagnoser]
public class PlaceholderTickBench
{
    private PlaceholderBattle _battle = null!;

    [Params(1_000, 10_000)]
    public int Units { get; set; }

    [GlobalSetup]
    public void Setup() =>
        _battle = new PlaceholderBattle(new PlaceholderSetup(seed: 1, unitCount: Units));

    [Benchmark]
    public void Tick() => _battle.Step();

    [Benchmark]
    public ulong StateHash() => _battle.StateHash();
}
