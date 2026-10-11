namespace GodGame.Sim.Tests;

[TestFixture, Category("Fast")]
public class PlaceholderBattleTests
{
    [Test]
    public void Step_AdvancesTheTickCounter()
    {
        var battle = new PlaceholderBattle(new PlaceholderSetup());
        battle.Step(37);

        Assert.That(battle.Tick, Is.EqualTo(37));
    }

    [Test]
    public void Units_StayInsideTheArena()
    {
        var setup = new PlaceholderSetup(
            seed: 5,
            unitCount: 200,
            arenaWidthMm: 5_000,
            arenaDepthMm: 3_000
        );
        var battle = new PlaceholderBattle(setup);

        for (int tick = 0; tick < 2_000; tick++)
        {
            battle.Step();
            for (int unit = 0; unit < battle.UnitCount; unit++)
            {
                int x = battle.PositionXMm[unit];
                int y = battle.PositionYMm[unit];
                if (x < 0 || x >= setup.ArenaWidthMm || y < 0 || y >= setup.ArenaDepthMm)
                {
                    Assert.Fail($"Unit {unit} left the arena at tick {battle.Tick}: ({x}, {y}).");
                }
            }
        }
    }

    [Test]
    public void Units_Move()
    {
        var battle = new PlaceholderBattle(new PlaceholderSetup(unitCount: 50));
        int[] startX = battle.PositionXMm.ToArray();
        battle.Step(SimConstants.TicksPerSecond * 10);

        Assert.That(battle.PositionXMm.ToArray(), Is.Not.EqualTo(startX));
    }

    [Test]
    public void SameSetup_SameHashEveryTick()
    {
        var a = new PlaceholderBattle(new PlaceholderSetup(seed: 11, unitCount: 100));
        var b = new PlaceholderBattle(new PlaceholderSetup(seed: 11, unitCount: 100));

        for (int tick = 0; tick < 600; tick++)
        {
            a.Step();
            b.Step();
            Assert.That(a.StateHash(), Is.EqualTo(b.StateHash()), $"Diverged at tick {a.Tick}.");
        }
    }

    [Test]
    public void DifferentSeed_DifferentHash()
    {
        var a = new PlaceholderBattle(new PlaceholderSetup(seed: 1));
        var b = new PlaceholderBattle(new PlaceholderSetup(seed: 2));

        Assert.That(a.StateHash(), Is.Not.EqualTo(b.StateHash()));
    }

    [Test]
    public void StateHash_ChangesEveryTick()
    {
        var battle = new PlaceholderBattle(new PlaceholderSetup());
        ulong before = battle.StateHash();
        battle.Step();

        Assert.That(battle.StateHash(), Is.Not.EqualTo(before));
    }

    [Test]
    public void StateHash_IsStableAcrossBuildsAndPlatforms()
    {
        // Pinned value: if this changes, battle outcomes changed. That needs a SimVersion bump (golden-replay skill).
        var battle = new PlaceholderBattle(new PlaceholderSetup(seed: 1, unitCount: 8));
        battle.Step(SimConstants.TicksPerSecond * 60);

        Assert.That(battle.StateHash(), Is.EqualTo(PinnedHashes.PlaceholderSeed1Units8After60s));
    }
}
