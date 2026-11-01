using System.Buffers.Binary;
using GodGame.Sim.Hosting;
using GodGame.Sim.Math;

namespace GodGame.Sim.Tests;

[TestFixture, Category("Fast")]
public class SnapshotIntegrityTests
{
    // Header: magic, format, SimVersion (3 × 4), battle id (8), tick (4), seed (8).
    private const int HeaderBytes = 32;

    private static byte[] WithFreshChecksum(byte[] snapshot)
    {
        var copy = (byte[])snapshot.Clone();
        var hash = new SimHash();
        hash.Append(new ReadOnlySpan<byte>(copy, 0, copy.Length - 8));
        BinaryPrimitives.WriteUInt64LittleEndian(copy.AsSpan(copy.Length - 8), hash.Digest());
        return copy;
    }

    private static Simulation ThreeSoldiersOneDespawned()
    {
        var world = new World(3, 1);
        var regiment = world.AddRegiment(Side.Attacker, 0, 100);
        for (int i = 0; i < 3; i++)
        {
            world.SpawnUnit(regiment, FixVec2.FromInts(i, i), 100, 100);
        }
        world.DespawnUnit(world.Units.IdOf(1));
        return new Simulation(world, 5, new SerialRunner());
    }

    [Test]
    public void DamagedBytes_FailTheChecksum()
    {
        var simulation = ThreeSoldiersOneDespawned();
        byte[] snapshot = simulation.CaptureSnapshot();
        snapshot[HeaderBytes + 40] ^= 0x01;

        var error = Assert.Throws<FormatException>(() => simulation.RestoreSnapshot(snapshot));
        Assert.That(error!.Message, Does.Contain("checksum"));
    }

    [Test]
    public void BrokenPoolInvariants_AreRefusedEvenWithAValidChecksum()
    {
        var simulation = ThreeSoldiersOneDespawned();
        byte[] snapshot = simulation.CaptureSnapshot();
        // Unit pool: 4 counts (16 bytes), generations (4 + 3 × 4), free stack (4 + 3 × 4), then one alive byte per slot.
        int aliveFlagOfSlot1 = HeaderBytes + 16 + 16 + 16 + 1;
        Assert.That(snapshot[aliveFlagOfSlot1], Is.EqualTo(0), "slot 1 was despawned");
        snapshot[aliveFlagOfSlot1] = 1; // now alive and on the free stack at once
        snapshot = WithFreshChecksum(snapshot);

        Assert.Throws<FormatException>(() => simulation.RestoreSnapshot(snapshot));
        Assert.That(simulation.World.Units.IsAlive(1), Is.False, "the live world is untouched");
    }

    [Test]
    public void SnapshotsFromAnotherBattle_AreRefused_EvenWithTheSameSeedAndSize()
    {
        var battle = RepoBattle.TenThousand(seed: 42);
        var map = battle.Map;
        var otherPlan = BattlePlan.Load(
            RepoData
                .Read("data", "plans", "m1-ten-thousand.json")
                .Replace("\"xMm\": 20000", "\"xMm\": 21000"),
            map,
            battle.Rules
        );
        var other = new Battle(battle.Rules, map, otherPlan, 42);

        byte[] snapshot = other.Start(new SerialRunner()).CaptureSnapshot();

        Assert.That(other.Id, Is.Not.EqualTo(battle.Id));
        Assert.Throws<FormatException>(() =>
            battle.Start(new SerialRunner()).RestoreSnapshot(snapshot)
        );
    }

    [Test]
    public void RestoreIntoASimulationThatMovedOn_MatchesByteForByte_OnAnyRunner()
    {
        var battle = RepoBattle.TenThousand();
        var original = battle.Start(new SerialRunner());
        original.Step(120);
        byte[] at120 = original.CaptureSnapshot();
        original.Step(80);
        byte[] at200 = original.CaptureSnapshot();

        var other = battle.Start(new ThreadedRunner(4));
        other.Step(333); // somewhere else entirely
        other.RestoreSnapshot(at120);
        other.Step(80);

        Assert.That(other.CaptureSnapshot(), Is.EqualTo(at200));
    }
}
