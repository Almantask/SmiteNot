using GodGame.Sim.Hosting;
using GodGame.Sim.Math;

namespace GodGame.Sim.Tests;

/// <summary>Melee, volleys, morale, routing, rallying, wiped and fleeing (M5 item 1, spec S2 sections 5 to 7).</summary>
[TestFixture, Category("Fast")]
public class CombatTests
{
    private const int Columns = 120; // 180 m
    private const int Rows = 30; // 45 m

    /// <summary>Flat ground with deployment zones <paramref name="zoneMetres"/> deep from the west and east edges.</summary>
    private static MapData Flat(
        int columns = Columns,
        int rows = Rows,
        string extraJson = "",
        int zoneMetres = 80
    ) => TestBattles.Map((_, _) => 0, columns, rows, zoneMetres, extraJson: extraJson);

    private static List<CauseEvent> Events(World world, CauseKind kind) =>
        world.Causes!.Events.Where(e => e.Kind == kind).ToList();

    [Test]
    public void Decision_2026_10_11_ManyWaysToDie_CombatKillsInMelee_EachDeathNamesTheStrikingRegiment()
    {
        // Attackers march east on the defender zone; the defenders hold and engage within 40 m. Twice their number,
        // the attackers win the melee, and every combat death names a regiment of the other side as its source.
        var battle = TestBattles.Battle(
            Flat(),
            new[]
            {
                ("attacker", "infantry", 100, 60.0, 22.0),
                ("defender", "infantry", 50, 110.0, 22.0),
            }
        );
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        simulation.Step(90 * SimConstants.TicksPerSecond);

        var killed = Events(world, CauseKind.SoldierKilled);
        Assert.That(
            world.Counters.Killed,
            Is.GreaterThan(20),
            "a melee decides within a minute (S2 5)"
        );
        Assert.That(killed, Has.Count.EqualTo(world.Counters.Killed));
        foreach (var e in killed)
        {
            Assert.That(
                world.RegimentSide[e.Detail],
                Is.Not.EqualTo(world.RegimentSide[e.Subject])
            );
        }
        Assert.That(
            killed.Count(e => e.Subject == 1),
            Is.GreaterThan(killed.Count(e => e.Subject == 0))
        );
        Assert.That(
            world.Army.Order[0],
            Is.Not.EqualTo(RegimentOrder.Rout),
            "the larger regiment holds"
        );
    }

    [Test]
    public void Decision_2026_10_08_WindAffectsArrows_TailwindCarriesFurther_HeadwindFallsShort_HeightAddsRange()
    {
        var rules = RepoData.Rules();
        var arrow = rules.Combat.Projectile("light-arrow")!;
        var from = FixVec2.FromInts(20, 20);
        var east = FixVec2.FromInts(150, 20);
        var west = FixVec2.FromInts(-110, 20);
        long Range(string wind, FixVec2 to, Func<int, int, int>? height = null)
        {
            var map = TestBattles.Map(height ?? ((_, _) => 0), Columns, Rows, 80, extraJson: wind);
            return Volleys.RangeMm(arrow, map, new Terrain(map), from, to);
        }

        Assert.That(Range("", east), Is.EqualTo(160_000), "calm");
        // 5 m/s along the shot moves a light arrow's range by 150‰ (S2 6.1: 30‰ per m/s).
        string wind = ",\"wind\":{\"xMmPerS\":5000,\"yMmPerS\":0}";
        Assert.That(Range(wind, east), Is.EqualTo(184_000), "tailwind");
        Assert.That(Range(wind, west), Is.EqualTo(136_000), "headwind");
        // Only the along-shot part counts: a crosswind changes nothing.
        Assert.That(Range(",\"wind\":{\"xMmPerS\":0,\"yMmPerS\":8000}", east), Is.EqualTo(160_000));
        // The clamp: a 15 m/s gale never takes more than 300‰ or adds more than 250‰.
        string gale = ",\"wind\":{\"xMmPerS\":15000,\"yMmPerS\":0}";
        Assert.That(Range(gale, east), Is.EqualTo(200_000));
        Assert.That(Range(gale, west), Is.EqualTo(112_000));
        // A 3 m rise under the shooter adds 12 m.
        Assert.That(Range("", east, (x, _) => x < 20 ? 3_000 : 0), Is.EqualTo(172_000));
    }

    [Test]
    public void Decision_2026_10_08_WindAffectsArrows_ArchersShootVolleysBeforeContact_AndRunOutOfAmmunition()
    {
        // Defending archers hold the east; an attacking regiment walks in from 100 m away. Volleys kill attackers
        // before any melee, each death naming the archers, and every volley costs one of the twenty.
        var battle = TestBattles.Battle(
            Flat(),
            new[]
            {
                ("attacker", "infantry", 100, 30.0, 22.0),
                ("defender", "ranged", 60, 165.0, 22.0),
            }
        );
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        // About ten hits of 21 a volley spread over 100 soldiers of 100 health: deaths come after a few volleys, and the
        // attackers are still well outside the 25 m at which the archers would fall back.
        simulation.Step(75 * SimConstants.TicksPerSecond);

        var killed = Events(world, CauseKind.SoldierKilled);
        Assert.That(
            killed.Count(e => e.Subject == 0 && e.Detail == 1),
            Is.GreaterThan(0),
            "arrows killed attackers"
        );
        Assert.That(world.Army.Ammo[1], Is.LessThan(20));
        Assert.That(world.Army.Ammo[0], Is.EqualTo(0), "infantry carry no arrows");
    }

    [Test]
    public void Decision_2026_10_09_PvpRoundsAreWonByWipingOutTheDefender_RoutedRegimentsRallyOnce_ASecondRoutRunsOffTheMap()
    {
        // A lone regiment far from any enemy and from the map edge: broken morale routs it toward its retreat edge (west);
        // after 20 s with no enemy within 80 m it rallies at 450. Broken again, it routs for good and runs off the map,
        // and once fewer than 10% are left it counts as wiped.
        var map = Flat(columns: 200, rows: 80, zoneMetres: 145); // 300 m by 120 m
        var battle = TestBattles.Battle(map, new[] { ("attacker", "infantry", 60, 140.0, 60.0) });
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        world.RegimentMorale[0] = 100;
        simulation.Step(TickOrder.RegimentAiRate.Period);
        Assert.That(world.Army.Order[0], Is.EqualTo(RegimentOrder.Rout));
        Assert.That(
            TestBattles.LiveUnits(world).All(u => (world.Flags[u] & UnitFlags.Routing) != 0),
            Is.True
        );
        Assert.That(
            Events(world, CauseKind.RegimentRouted).Single().Detail,
            Is.EqualTo(1),
            "it may rally"
        );

        simulation.Step(25 * SimConstants.TicksPerSecond);
        var rallied = Events(world, CauseKind.RegimentRallied);
        Assert.That(rallied, Has.Count.EqualTo(1));
        Assert.That(world.Army.Order[0], Is.Not.EqualTo(RegimentOrder.Rout));
        Assert.That(
            TestBattles.LiveUnits(world).Any(u => (world.Flags[u] & UnitFlags.Routing) != 0),
            Is.False
        );
        Assert.That(world.Counters.Fled, Is.EqualTo(0));

        world.RegimentMorale[0] = 100;
        simulation.Step(120 * SimConstants.TicksPerSecond);
        var routs = Events(world, CauseKind.RegimentRouted);
        Assert.That(routs, Has.Count.EqualTo(2));
        Assert.That(routs[1].Detail, Is.EqualTo(2), "the second rout is final");
        Assert.That(Events(world, CauseKind.RegimentRallied), Has.Count.EqualTo(1), "only once");
        Assert.That(world.Counters.Fled, Is.EqualTo(60), "everyone ran off the map");
        Assert.That(Events(world, CauseKind.SoldierFled), Has.Count.EqualTo(60));
        Assert.That(world.Army.Wiped[0], Is.True, "fled soldiers count as lost");
        Assert.That(Events(world, CauseKind.RegimentWiped), Has.Count.EqualTo(1));
    }

    [Test]
    public void Decision_2026_10_09_PvpRoundsAreWonByWipingOutTheDefender_ALosingRegimentRouts_AndTheVictorsRideItDown()
    {
        // A small defending regiment against one four times its size: losses and the shock rout it before it is
        // destroyed (morale breaks first), and the attackers keep it as their target while it runs.
        var battle = TestBattles.Battle(
            Flat(),
            new[]
            {
                ("attacker", "infantry", 200, 70.0, 22.0),
                ("defender", "infantry", 50, 110.0, 22.0),
            }
        );
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        simulation.Step(90 * SimConstants.TicksPerSecond);

        var routed = Events(world, CauseKind.RegimentRouted);
        Assert.That(routed.Any(e => e.Subject == 1), Is.True, "the defenders broke");
        int routTick = routed.First(e => e.Subject == 1).Tick;
        int diedBeforeRout = Events(world, CauseKind.SoldierKilled)
            .Count(e => e.Subject == 1 && e.Tick <= routTick);
        Assert.That(
            diedBeforeRout,
            Is.LessThan(45),
            "morale breaks before the regiment is destroyed"
        );
    }

    [Test]
    public void CombatIsDeterministic_AcrossThreadCounts_AndSnapshots()
    {
        var battle = TestBattles.Battle(
            Flat(),
            new[]
            {
                ("attacker", "infantry", 150, 50.0, 15.0),
                ("attacker", "cavalry", 60, 50.0, 32.0),
                ("defender", "infantry", 80, 120.0, 15.0),
                ("defender", "ranged", 60, 160.0, 30.0),
            },
            seed: 7
        );
        ulong[] serial = battle.PerSecondHashes(45, new SerialRunner());
        ulong[] threaded = battle.PerSecondHashes(45, new ThreadedRunner(4));
        Assert.That(threaded, Is.EqualTo(serial));

        var a = battle.Start(new SerialRunner());
        a.Step(30 * SimConstants.TicksPerSecond);
        Assert.That(a.World.Counters.Killed, Is.GreaterThan(0), "the fight has started");
        var snapshot = a.CaptureSnapshot();
        a.Step(15 * SimConstants.TicksPerSecond);
        var b = battle.Start(new SerialRunner());
        b.RestoreSnapshot(snapshot);
        b.Step(15 * SimConstants.TicksPerSecond);
        Assert.That(b.StateHash(), Is.EqualTo(a.StateHash()));
    }
}

/// <summary>Combat determinism at a size that splits every parallel pass into several chunks (the audit's ask).</summary>
[TestFixture, Category("Slow")]
public class CombatDeterminismSlowTests
{
    private static Battle Melee()
    {
        // Twelve regiments of 200 a side-by-side line each, 40 m apart: they meet within seconds, rout from about
        // 30 s and flee off the map later. 2,400 soldiers make five chunks of 512.
        var map = TestBattles.Map((_, _) => 0, 200, 120, 140);
        var regiments = new List<(string, string, int, double, double)>();
        for (int i = 0; i < 6; i++)
        {
            double y = 20 + i * 28;
            regiments.Add(("attacker", i % 3 == 2 ? "cavalry" : "infantry", 200, 130.0, y));
            regiments.Add(("defender", i % 3 == 1 ? "ranged" : "infantry", 200, 170.0, y));
        }
        return TestBattles.Battle(map, regiments, seed: 11);
    }

    [Test]
    public void CombatIsDeterministic_OneThreeAndEightThreads()
    {
        var battle = Melee();
        ulong[] one = battle.PerSecondHashes(90, new SerialRunner());
        Assert.That(battle.PerSecondHashes(90, new ThreadedRunner(3)), Is.EqualTo(one));
        Assert.That(battle.PerSecondHashes(90, new ThreadedRunner(8)), Is.EqualTo(one));
    }

    [Test]
    public void CombatIsDeterministic_AcrossSnapshotsDuringMeleeRoutsAndFlight()
    {
        var battle = Melee();
        var a = battle.Start(new SerialRunner());
        var snapshots = new List<(int Tick, byte[] Bytes)>();
        for (int second = 1; second <= 90; second++)
        {
            a.Step(SimConstants.TicksPerSecond);
            if (second % 15 == 0)
            {
                snapshots.Add((a.Tick, a.CaptureSnapshot()));
            }
        }
        var world = a.World;
        Assert.That(world.Counters.Killed, Is.GreaterThan(100));
        Assert.That(world.Causes!.Events.Any(e => e.Kind == CauseKind.RegimentRouted), Is.True);
        ulong end = a.StateHash();
        foreach (var (tick, bytes) in snapshots)
        {
            var b = battle.Start(new ThreadedRunner(3));
            b.RestoreSnapshot(bytes);
            b.Step(90 * SimConstants.TicksPerSecond - tick);
            Assert.That(b.StateHash(), Is.EqualTo(end), $"restored at tick {tick}");
        }
    }
}
