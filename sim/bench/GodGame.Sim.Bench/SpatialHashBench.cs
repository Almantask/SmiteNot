using BenchmarkDotNet.Attributes;
using GodGame.Sim.Math;

namespace GodGame.Sim.Bench;

/// <summary>
/// The spatial hash at 10,000 units on the working map (1.2 × 0.8 km): one rebuild per tick, then one
/// separation-sized query (2 m) per unit, which is what movement will do every tick.
/// </summary>
[MemoryDiagnoser]
public class SpatialHashBench
{
    private World _world = null!;
    private SpatialHash _hash = null!;
    private int[] _buffer = null!;

    [Params(10_000)]
    public int Units { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _world = new World(Units, 100);
        var rng = new Pcg32(1, 1);
        for (int r = 0; r < 100; r++)
        {
            var regiment = _world.AddRegiment(r % 2 == 0 ? Side.Attacker : Side.Defender, 0, 100);
            // Regiments of 100 packed into 20 × 20 m blocks, so queries see real crowding.
            var origin = FixVec2.FromInts(rng.NextInt(0, 1180), rng.NextInt(0, 780));
            for (int s = 0; s < Units / 100; s++)
            {
                var offset = new FixVec2(
                    Fix.FromRaw(rng.NextInt(0, 20 << 16)),
                    Fix.FromRaw(rng.NextInt(0, 20 << 16))
                );
                _world.SpawnUnit(regiment, origin + offset, 100, 100);
            }
        }
        _hash = new SpatialHash(Fix.FromInt(1200), Fix.FromInt(800), Fix.FromInt(2), Units);
        _buffer = new int[256];
        _hash.Build(_world);
    }

    [Benchmark]
    public void Build() => _hash.Build(_world);

    [Benchmark]
    public int QueryEveryUnit()
    {
        int total = 0;
        Fix radius = Fix.FromInt(2);
        for (int u = 0; u < _world.Units.HighWater; u++)
        {
            total += _hash.QueryRadius(_world, _world.Position[u], radius, _buffer);
        }
        return total;
    }
}
