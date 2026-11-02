using GodGame.Sim.Hosting;
using GodGame.Sim.Rules;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim.Tests;

internal static class RepoBattle
{
    public static Battle TenThousand(ulong seed = 42)
    {
        var rules = RepoData.Rules();
        var map = MapData.Load(RepoData.Read("data", "maps", "m1-flat", "map.json"));
        var plan = BattlePlan.Load(
            RepoData.Read("data", "plans", "m1-ten-thousand.json"),
            map,
            rules
        );
        return new Battle(rules, map, plan, seed);
    }
}

[TestFixture, Category("Fast")]
public class SnapshotTests
{
    [Test]
    public void RestoreMidBattle_ContinuesExactlyAsTheOriginal()
    {
        var battle = RepoBattle.TenThousand();
        var original = battle.Start(new SerialRunner());
        original.Step(150); // past a heading change at tick 100
        byte[] snapshot = original.CaptureSnapshot();
        ulong[] expected = original.RunAndHash(200, 20);

        var restored = battle.Start(new SerialRunner());
        restored.RestoreSnapshot(snapshot);

        Assert.That(restored.Tick, Is.EqualTo(150));
        Assert.That(restored.RunAndHash(200, 20), Is.EqualTo(expected));
    }

    [Test]
    public void SnapshotRoundTrip_IsByteIdentical_AndUnderTheKeyframeBudget()
    {
        var simulation = RepoBattle.TenThousand().Start(new SerialRunner());
        simulation.Step(30);
        byte[] first = simulation.CaptureSnapshot();
        var copy = RepoBattle.TenThousand().Start(new SerialRunner());
        copy.RestoreSnapshot(first);

        Assert.That(copy.CaptureSnapshot(), Is.EqualTo(first));
        Assert.That(copy.StateHash(), Is.EqualTo(simulation.StateHash()));
        Assert.That(
            first.Length,
            Is.LessThan(4 * 1024 * 1024),
            "plan section 8: keyframes ≤ 4 MB before delta"
        );
    }

    [Test]
    public void BadSnapshots_AreRefused_AndLeaveTheSimulationUntouched()
    {
        var simulation = RepoBattle.TenThousand().Start(new SerialRunner());
        simulation.Step(10);
        byte[] good = simulation.CaptureSnapshot();
        ulong before = simulation.StateHash();

        var truncated = good.Take(good.Length - 1).ToArray();
        var otherSeed = RepoBattle.TenThousand(seed: 7).Start(new SerialRunner()).CaptureSnapshot();
        var wrongMagic = (byte[])good.Clone();
        wrongMagic[0] ^= 0xFF;

        Assert.Throws<FormatException>(() => simulation.RestoreSnapshot(truncated));
        Assert.Throws<FormatException>(() => simulation.RestoreSnapshot(otherSeed));
        Assert.Throws<FormatException>(() => simulation.RestoreSnapshot(wrongMagic));
        Assert.Throws<FormatException>(() =>
            new Simulation(new World(5, 1), 42, new SerialRunner()).RestoreSnapshot(good)
        );
        Assert.That(simulation.StateHash(), Is.EqualTo(before));
    }

    [Test]
    public void RunAndHash_RecordsEveryNthTick()
    {
        var a = RepoBattle.TenThousand().Start(new SerialRunner());
        var b = RepoBattle.TenThousand().Start(new SerialRunner());

        ulong[] hashes = a.RunAndHash(45, 20);
        b.Step(20);
        ulong first = b.StateHash();

        Assert.That(hashes, Has.Length.EqualTo(2));
        Assert.That(hashes[0], Is.EqualTo(first));
        Assert.That(a.Tick, Is.EqualTo(45));
    }
}

[TestFixture, Category("Fast")]
public class ReplayTests
{
    [Test]
    public void Replay_RoundTripsAndRebuildsTheSameBattle()
    {
        var battle = RepoBattle.TenThousand();
        ulong[] hashes = battle.PerSecondHashes(5, new SerialRunner());
        string json = battle.ToReplay(5, hashes).ToJson();

        var replay = Replay.Parse(json);
        var rebuilt = replay.ToBattle(battle.Rules, battle.Map);

        Assert.That(replay.ToJson(), Is.EqualTo(json));
        Assert.That(replay.Seed, Is.EqualTo(42UL));
        Assert.That(rebuilt.Plan.Hash, Is.EqualTo(battle.Plan.Hash));
        Assert.That(
            rebuilt.PerSecondHashes(5, new ThreadedRunner(4)),
            Is.EqualTo(replay.PerSecondHashes)
        );
    }

    [Test]
    public void Replay_RefusesOtherRulesMapsAndVersions()
    {
        var battle = RepoBattle.TenThousand();
        var replay = battle.ToReplay(3);
        var otherRules = RepoData.RulesWithUnitTypes(
            RepoData
                .Read("data", "rules", "unit-types.json")
                .Replace("\"health\": 100", "\"health\": 101")
        );
        var otherMap = MapData.Load(
            RepoData.Read("data", "maps", "m1-flat", "map.json").Replace("200000", "199000")
        );
        string oldVersion = replay
            .ToJson()
            .Replace("\"simVersion\": 1", "\"simVersion\": 0")
            .Replace("\"simVersion\": " + SimVersion.Current, "\"simVersion\": 9999");

        Assert.Throws<DataFormatException>(() => replay.ToBattle(otherRules, battle.Map));
        Assert.Throws<DataFormatException>(() => replay.ToBattle(battle.Rules, otherMap));
        Assert.Throws<DataFormatException>(() =>
            Replay.Parse(oldVersion).ToBattle(battle.Rules, battle.Map)
        );
    }

    [Test]
    public void PerSecondHashes_ChangeEverySecond_AndWithTheSeed()
    {
        ulong[] a = RepoBattle.TenThousand(seed: 1).PerSecondHashes(3, new SerialRunner());
        ulong[] b = RepoBattle.TenThousand(seed: 2).PerSecondHashes(3, new SerialRunner());

        Assert.That(
            a.Distinct().Count(),
            Is.EqualTo(3),
            "the wander system moves units every tick"
        );
        Assert.That(b, Is.Not.EqualTo(a));
    }
}
