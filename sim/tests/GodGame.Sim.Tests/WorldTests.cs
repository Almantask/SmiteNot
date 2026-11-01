using GodGame.Sim.Math;

namespace GodGame.Sim.Tests;

[TestFixture, Category("Fast")]
public class EntityPoolTests
{
    [Test]
    public void Allocate_FillsIndicesInOrder()
    {
        var pool = new EntityPool(4);

        var ids = Enumerable.Range(0, 4).Select(_ => pool.Allocate()).ToArray();

        Assert.That(ids.Select(id => id.Index), Is.EqualTo(new[] { 0, 1, 2, 3 }));
        Assert.That(ids.Select(id => id.Generation), Is.All.EqualTo(1));
        Assert.That(pool.AliveCount, Is.EqualTo(4));
        Assert.Throws<InvalidOperationException>(() => pool.Allocate());
    }

    [Test]
    public void Free_ReusesTheMostRecentlyFreedSlot_WithANewGeneration()
    {
        var pool = new EntityPool(8);
        var ids = Enumerable.Range(0, 8).Select(_ => pool.Allocate()).ToArray();

        pool.Free(ids[3]);
        pool.Free(ids[6]);
        var first = pool.Allocate();
        var second = pool.Allocate();

        Assert.That(first, Is.EqualTo(new EntityId(6, 2)));
        Assert.That(second, Is.EqualTo(new EntityId(3, 2)));
        Assert.That(pool.IsAlive(ids[3]), Is.False, "the old handle must not reach the new owner");
        Assert.That(pool.IsAlive(second), Is.True);
    }

    [Test]
    public void InvalidHandles_AreNeverAlive()
    {
        var pool = new EntityPool(4);
        var id = pool.Allocate();

        Assert.That(pool.IsAlive(EntityId.None), Is.False);
        Assert.That(pool.IsAlive(new EntityId(2, 1)), Is.False, "beyond the high-water mark");
        Assert.That(pool.IsAlive(new EntityId(-1, 1)), Is.False);
        Assert.That(EntityId.None.IsNone, Is.True);
        Assert.That(id.IsNone, Is.False);

        pool.Free(id);
        Assert.Throws<InvalidOperationException>(() => pool.Free(id));
        Assert.Throws<InvalidOperationException>(() => pool.IdOf(id.Index));
    }

    [Test]
    public void SameHistory_SameHash_DifferentHistory_DifferentHash()
    {
        ulong Run(bool swapFrees)
        {
            var pool = new EntityPool(16);
            var ids = Enumerable.Range(0, 10).Select(_ => pool.Allocate()).ToArray();
            pool.Free(ids[swapFrees ? 5 : 2]);
            pool.Free(ids[swapFrees ? 2 : 5]);
            pool.Allocate();
            var hash = new SimHash();
            pool.AppendTo(hash);
            return hash.Digest();
        }

        ulong first = Run(false);
        ulong again = Run(false);

        Assert.That(again, Is.EqualTo(first));
        Assert.That(
            Run(true),
            Is.Not.EqualTo(first),
            "the free order decides future indices, so it is state"
        );
    }
}

[TestFixture, Category("Fast")]
public class WorldTests
{
    private static World BuildBattle(ulong seed, int regiments, int soldiersEach)
    {
        var world = new World(unitCapacity: regiments * soldiersEach, regimentCapacity: regiments);
        for (int r = 0; r < regiments; r++)
        {
            var side = r % 2 == 0 ? Side.Attacker : Side.Defender;
            var regiment = world.AddRegiment(side, unitType: r % 3, morale: 100);
            for (int s = 0; s < soldiersEach; s++)
            {
                var rng = SimRandom.Stream(
                    seed,
                    systemId: 99,
                    entityId: r * soldiersEach + s,
                    tick: 0
                );
                var position = new FixVec2(
                    Fix.FromRaw(rng.NextInt(0, 1200 << 16)),
                    Fix.FromRaw(rng.NextInt(0, 800 << 16))
                );
                world.SpawnUnit(regiment, position, health: 100, morale: 100);
            }
        }
        return world;
    }

    private static ulong Hash(World world)
    {
        var hash = new SimHash();
        world.AppendTo(hash);
        return hash.Digest();
    }

    [Test]
    public void SpawnUnit_CopiesTheRegimentTypeAndCountsIt()
    {
        var world = new World(unitCapacity: 10, regimentCapacity: 2);
        var regiment = world.AddRegiment(Side.Defender, unitType: 4, morale: 80);

        var unit = world.SpawnUnit(regiment, FixVec2.FromInts(3, 4), health: 50, morale: 70);

        Assert.That(world.UnitType[unit.Index], Is.EqualTo(4));
        Assert.That(world.UnitRegiment[unit.Index], Is.EqualTo(regiment.Index));
        Assert.That(world.Position[unit.Index], Is.EqualTo(FixVec2.FromInts(3, 4)));
        Assert.That(world.Velocity[unit.Index], Is.EqualTo(FixVec2.Zero));
        Assert.That(world.Flags[unit.Index], Is.EqualTo(UnitFlags.None));
        Assert.That(world.RegimentStartingSize[regiment.Index], Is.EqualTo(1));
        Assert.That(world.RegimentAlive[regiment.Index], Is.EqualTo(1));
        Assert.That(world.RegimentSide[regiment.Index], Is.EqualTo(Side.Defender));
    }

    [Test]
    public void DespawnUnit_KeepsTheStartingSize()
    {
        var world = new World(unitCapacity: 10, regimentCapacity: 1);
        var regiment = world.AddRegiment(Side.Attacker, unitType: 0, morale: 100);
        var units = Enumerable
            .Range(0, 10)
            .Select(i => world.SpawnUnit(regiment, FixVec2.FromInts(i, 0), 100, 100))
            .ToArray();

        world.DespawnUnit(units[2]);
        world.DespawnUnit(units[7]);

        Assert.That(world.RegimentAlive[regiment.Index], Is.EqualTo(8));
        Assert.That(world.RegimentStartingSize[regiment.Index], Is.EqualTo(10));
        Assert.That(world.Units.IsAlive(units[2]), Is.False);
        Assert.Throws<InvalidOperationException>(() => world.DespawnUnit(units[2]));
        Assert.Throws<InvalidOperationException>(() => world.RemoveRegiment(regiment));
    }

    [Test]
    public void RemoveRegiment_OnlyWhenEmpty_AndOldHandlesStopWorking()
    {
        var world = new World(unitCapacity: 2, regimentCapacity: 1);
        var regiment = world.AddRegiment(Side.Attacker, 0, 100);
        var unit = world.SpawnUnit(regiment, FixVec2.Zero, 100, 100);

        world.DespawnUnit(unit);
        world.RemoveRegiment(regiment);

        Assert.Throws<InvalidOperationException>(() =>
            world.SpawnUnit(regiment, FixVec2.Zero, 100, 100)
        );
        var reused = world.AddRegiment(Side.Defender, 1, 100);
        Assert.That(reused.Index, Is.EqualTo(regiment.Index));
        Assert.That(reused.Generation, Is.EqualTo(regiment.Generation + 1));
    }

    [Test]
    public void SameBattle_SameHash()
    {
        ulong first = Hash(BuildBattle(7, 20, 50));
        ulong again = Hash(BuildBattle(7, 20, 50));

        Assert.That(again, Is.EqualTo(first));
        Assert.That(Hash(BuildBattle(8, 20, 50)), Is.Not.EqualTo(first));
    }

    [Test]
    public void EveryUnitField_ChangesTheHash()
    {
        var world = BuildBattle(7, 2, 5);
        ulong before = Hash(world);
        var changes = new (string Field, Action Change, Action Undo)[]
        {
            (
                "position",
                () => world.Position[3] += FixVec2.FromInts(0, 1),
                () => world.Position[3] -= FixVec2.FromInts(0, 1)
            ),
            (
                "velocity",
                () => world.Velocity[3] = FixVec2.FromInts(1, 0),
                () => world.Velocity[3] = FixVec2.Zero
            ),
            ("type", () => world.UnitType[3]++, () => world.UnitType[3]--),
            ("regiment", () => world.UnitRegiment[3]++, () => world.UnitRegiment[3]--),
            ("health", () => world.Health[3]--, () => world.Health[3]++),
            ("morale", () => world.Morale[3]--, () => world.Morale[3]++),
            (
                "flags",
                () => world.Flags[3] = UnitFlags.Stunned,
                () => world.Flags[3] = UnitFlags.None
            ),
            (
                "regiment side",
                () => world.RegimentSide[1] = Side.Attacker,
                () => world.RegimentSide[1] = Side.Defender
            ),
            ("regiment type", () => world.RegimentUnitType[1]++, () => world.RegimentUnitType[1]--),
            (
                "regiment size",
                () => world.RegimentStartingSize[1]++,
                () => world.RegimentStartingSize[1]--
            ),
            ("regiment morale", () => world.RegimentMorale[1]--, () => world.RegimentMorale[1]++),
            ("regiment alive", () => world.RegimentAlive[1]--, () => world.RegimentAlive[1]++),
        };

        foreach (var (field, change, undo) in changes)
        {
            change();
            Assert.That(Hash(world), Is.Not.EqualTo(before), field);
            undo();
            Assert.That(Hash(world), Is.EqualTo(before), field + " (undone)");
        }
    }

    [Test]
    public void FreedSlots_AreResetToZero()
    {
        var world = new World(unitCapacity: 3, regimentCapacity: 2);
        var regiment = world.AddRegiment(Side.Defender, unitType: 5, morale: 90);
        var unit = world.SpawnUnit(regiment, FixVec2.FromInts(7, 9), health: 40, morale: 60);
        world.Velocity[unit.Index] = FixVec2.FromInts(1, 1);
        world.Flags[unit.Index] = UnitFlags.Burning | UnitFlags.Routing;

        world.DespawnUnit(unit);
        world.RemoveRegiment(regiment);

        int u = unit.Index;
        Assert.That(world.Position[u], Is.EqualTo(FixVec2.Zero));
        Assert.That(world.Velocity[u], Is.EqualTo(FixVec2.Zero));
        Assert.That(
            new[] { world.UnitType[u], world.UnitRegiment[u], world.Health[u], world.Morale[u] },
            Is.All.EqualTo(0)
        );
        Assert.That(world.Flags[u], Is.EqualTo(UnitFlags.None));
        int r = regiment.Index;
        Assert.That(world.RegimentSide[r], Is.EqualTo(Side.Attacker));
        Assert.That(
            new[]
            {
                world.RegimentUnitType[r],
                world.RegimentStartingSize[r],
                world.RegimentAlive[r],
                world.RegimentMorale[r],
            },
            Is.All.EqualTo(0)
        );
    }

    [Test]
    public void BattleHash_MatchesThePinnedValue()
    {
        // Pins the hash layout and byte order, so a change shows up on every runtime, not only in comparisons.
        var world = BuildBattle(7, 20, 50);
        for (int u = 0; u < 1000; u += 7)
        {
            world.DespawnUnit(world.Units.IdOf(u));
        }
        var regiment = world.Regiments.IdOf(3);
        world.SpawnUnit(regiment, FixVec2.FromInts(600, 400), health: 75, morale: 50);

        Assert.That(Hash(world), Is.EqualTo(PinnedHashes.WorldBattleSeed7));
    }

    [Test]
    public void TenThousandUnits_FitAndHashDeterministically()
    {
        var a = BuildBattle(3, 100, 100);
        var b = BuildBattle(3, 100, 100);

        Assert.That(a.Units.AliveCount, Is.EqualTo(10_000));
        Assert.That(Hash(a), Is.EqualTo(Hash(b)));
    }
}
