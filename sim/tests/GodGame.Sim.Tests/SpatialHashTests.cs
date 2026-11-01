using GodGame.Sim.Math;

namespace GodGame.Sim.Tests;

[TestFixture, Category("Fast")]
public class SpatialHashTests
{
    private static readonly Fix Width = Fix.FromInt(1200);
    private static readonly Fix Depth = Fix.FromInt(800);

    private static World Scatter(int units, ulong seed, int despawnEvery = 0)
    {
        var world = new World(units, 1);
        var regiment = world.AddRegiment(Side.Attacker, 0, 100);
        var rng = new Pcg32(seed, 1);
        for (int i = 0; i < units; i++)
        {
            var position = new FixVec2(
                Fix.FromRaw(rng.NextInt(-(5 << 16), (1205 << 16))),
                Fix.FromRaw(rng.NextInt(-(5 << 16), (805 << 16)))
            );
            world.SpawnUnit(regiment, position, 100, 100);
        }
        if (despawnEvery > 0)
        {
            for (int u = 0; u < units; u += despawnEvery)
            {
                world.DespawnUnit(world.Units.IdOf(u));
            }
        }
        return world;
    }

    private static int[] BruteForce(World world, FixVec2 center, Fix radius) =>
        Enumerable
            .Range(0, world.Units.HighWater)
            .Where(u =>
            {
                if (!world.Units.IsAlive(u))
                {
                    return false;
                }
                var dx = new System.Numerics.BigInteger(world.Position[u].X.Raw - center.X.Raw);
                var dy = new System.Numerics.BigInteger(world.Position[u].Y.Raw - center.Y.Raw);
                var r = new System.Numerics.BigInteger(radius.Raw);
                return dx * dx + dy * dy <= r * r;
            })
            .ToArray();

    [Test]
    public void Build_PutsEveryLiveUnitInItsCell_InSlotOrder()
    {
        var world = Scatter(5_000, 1, despawnEvery: 7);
        var hash = new SpatialHash(Width, Depth, Fix.FromInt(8), world.Units.Capacity);

        hash.Build(world);

        Assert.That(hash.Count, Is.EqualTo(world.Units.AliveCount));
        var seen = new List<int>();
        for (int cell = 0; cell < hash.CellCount; cell++)
        {
            int[] units = hash.UnitsIn(cell).ToArray();
            Assert.That(units, Is.Ordered, $"cell {cell}");
            Assert.That(units.Select(u => hash.CellOf(world.Position[u])), Is.All.EqualTo(cell));
            seen.AddRange(units);
        }
        Assert.That(
            seen.OrderBy(u => u),
            Is.EqualTo(BruteForce(world, FixVec2.FromInts(600, 400), Fix.FromInt(2000)))
        );
    }

    [Test]
    public void QueryRadius_FindsExactlyTheBruteForceSet()
    {
        var world = Scatter(10_000, 2, despawnEvery: 11);
        var hash = new SpatialHash(Width, Depth, Fix.FromInt(4), world.Units.Capacity);
        hash.Build(world);
        var rng = new Pcg32(2, 2);
        var buffer = new int[10_000];

        for (int i = 0; i < 300; i++)
        {
            var center = new FixVec2(
                Fix.FromRaw(rng.NextInt(-(20 << 16), 1220 << 16)),
                Fix.FromRaw(rng.NextInt(-(20 << 16), 820 << 16))
            );
            var radius = Fix.FromRaw(rng.NextInt(0, 30 << 16));
            int found = hash.QueryRadius(world, center, radius, buffer);

            Assert.That(
                buffer.Take(found).OrderBy(u => u),
                Is.EqualTo(BruteForce(world, center, radius)),
                $"query {i}"
            );
        }
    }

    [Test]
    public void QueryRadius_OrderIsCellsThenSlots_AndRepeatsExactly()
    {
        var world = Scatter(3_000, 3);
        var a = new SpatialHash(Width, Depth, Fix.FromInt(5), world.Units.Capacity);
        var b = new SpatialHash(Width, Depth, Fix.FromInt(5), world.Units.Capacity);
        a.Build(world);
        b.Build(world);
        b.Build(world); // rebuilding must give the same layout
        var first = new int[3_000];
        var second = new int[3_000];

        int n = a.QueryRadius(world, FixVec2.FromInts(600, 400), Fix.FromInt(120), first);
        int m = b.QueryRadius(world, FixVec2.FromInts(600, 400), Fix.FromInt(120), second);

        Assert.That(m, Is.EqualTo(n));
        Assert.That(second.Take(m), Is.EqualTo(first.Take(n)));
        // Row-major cell order means cell indices never decrease; within a cell, slots ascend.
        var keys = first
            .Take(n)
            .Select(u => ((long)a.CellOf(world.Position[u]) << 32) | (uint)u)
            .ToArray();
        Assert.That(n, Is.GreaterThan(50), "the query must cover several cells to test anything");
        Assert.That(keys, Is.Ordered);
    }

    [Test]
    public void QueryRadius_FillsTheBufferInOrder_AndCountsTheRest()
    {
        var world = new World(10, 1);
        var regiment = world.AddRegiment(Side.Defender, 0, 100);
        for (int i = 0; i < 10; i++)
        {
            world.SpawnUnit(regiment, FixVec2.FromInts(100, 100), 100, 100);
        }
        var hash = new SpatialHash(Width, Depth, Fix.FromInt(4), 10);
        hash.Build(world);
        var buffer = new int[4];

        Assert.That(
            hash.QueryRadius(world, FixVec2.FromInts(100, 100), Fix.One, buffer),
            Is.EqualTo(10)
        );
        Assert.That(buffer, Is.EqualTo(new[] { 0, 1, 2, 3 }));
    }

    [Test]
    public void FarOffMapUnitsAndCentres_NeitherOverflowNorMatch()
    {
        var world = new World(4, 1);
        var regiment = world.AddRegiment(Side.Attacker, 0, 100);
        world.SpawnUnit(regiment, FixVec2.FromInts(-100_000, 10), 100, 100); // flung 100 km west
        world.SpawnUnit(regiment, FixVec2.FromInts(1, 10), 100, 100);
        world.SpawnUnit(regiment, FixVec2.FromInts(100_000, 100_000), 100, 100);
        world.SpawnUnit(regiment, FixVec2.FromInts(-4, -4), 100, 100); // exactly one cell below zero
        var hash = new SpatialHash(Width, Depth, Fix.FromInt(4), 4);
        hash.Build(world);
        var buffer = new int[4];

        Assert.That(
            hash.QueryRadius(world, FixVec2.FromInts(0, 10), Fix.FromInt(5), buffer),
            Is.EqualTo(1)
        );
        Assert.That(buffer[0], Is.EqualTo(1));
        Assert.That(
            hash.QueryRadius(world, FixVec2.FromInts(-100_000, 10), Fix.One, buffer),
            Is.EqualTo(1)
        );
        Assert.That(buffer[0], Is.EqualTo(0));
        Assert.That(
            hash.QueryRadius(world, FixVec2.FromInts(-3, -3), Fix.FromInt(2), buffer),
            Is.EqualTo(1)
        );
        Assert.That(buffer[0], Is.EqualTo(3));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            hash.QueryRadius(world, FixVec2.Zero, Fix.FromInt(1001), buffer)
        );
    }

    [Test]
    public void QueryRadius_ReturnsTheTotalWhenTheBufferIsShort()
    {
        var world = Scatter(2_000, 4);
        var hash = new SpatialHash(Width, Depth, Fix.FromInt(4), world.Units.Capacity);
        hash.Build(world);
        var big = new int[2_000];
        var small = new int[3];

        int total = hash.QueryRadius(world, FixVec2.FromInts(600, 400), Fix.FromInt(100), big);
        int reported = hash.QueryRadius(world, FixVec2.FromInts(600, 400), Fix.FromInt(100), small);

        Assert.That(total, Is.GreaterThan(3));
        Assert.That(reported, Is.EqualTo(total));
        Assert.That(small, Is.EqualTo(big.Take(3)));
    }

    [Test]
    public void TinyCells_AreRejectedRatherThanOverflowing()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new SpatialHash(Width, Depth, Fix.FromRaw(1), 10)
        );
    }

    [Test]
    public void PositionsOffTheMap_ClampToTheEdgeCells()
    {
        var hash = new SpatialHash(Width, Depth, Fix.FromInt(10), 1);

        Assert.That(hash.Columns, Is.EqualTo(120));
        Assert.That(hash.Rows, Is.EqualTo(80));
        Assert.That(hash.CellOf(FixVec2.FromInts(-50, -50)), Is.EqualTo(0));
        Assert.That(hash.CellOf(FixVec2.FromInts(5000, 5000)), Is.EqualTo(hash.CellCount - 1));
        Assert.That(hash.CellOf(new FixVec2(Fix.FromRaw(-1), Fix.Zero)), Is.EqualTo(0));
    }
}
