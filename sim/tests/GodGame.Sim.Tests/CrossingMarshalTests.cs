using System.Globalization;
using GodGame.Sim.Hosting;
using GodGame.Sim.Rules;

namespace GodGame.Sim.Tests;

/// <summary>Crossing Marshals (M5 item 4, spec S2 14.6), open order (S2 2.2) and small regiments under the range rule (D15).</summary>
[TestFixture, Category("Fast")]
public class CrossingMarshalTests
{
    // LoadTests' map: 360 × 150 m, a gorge 6 m deep at columns 118–122 that only a wooden bridge (4.5 m wide, at y
    // 75.75 m) crosses; here with 70 m zones, so the column deploys about 110 m short of the bridge.
    private const int Columns = 240;
    private const int Rows = 100;

    private static int Gorge(int x, int y) => x >= 118 && x <= 122 ? -6_000 : 0;

    private const string Bridge =
        "{\"id\":\"b\",\"piece\":\"wooden-bridge\",\"points\":[[175500,75750],[186000,75750]],\"widthMm\":4500}";

    private static string Regiment(string id, string type, int soldiers, int x, int y) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{{\"id\":\"{0}\",\"side\":\"attacker\",\"unitType\":\"{1}\",\"soldiers\":{2},\"xMm\":{3},\"yMm\":{4},\"facingCentiDegrees\":0}}",
            id,
            type,
            soldiers,
            x,
            y
        );

    /// <summary>Marshals (slot 0; plans order regiments by id) and three regiments of 50 infantry (slots 1–3), all bound over the one bridge.</summary>
    private static Battle Column(bool marshals = true)
    {
        var map = TestBattles.Map(Gorge, Columns, Rows, zoneMetres: 70);
        var army = new List<string>();
        if (marshals)
        {
            army.Add(Regiment("a", "crossing-marshals", 24, 65_000, 70_000));
        }
        army.Add(Regiment("b", "infantry", 50, 65_000, 40_000));
        army.Add(Regiment("c", "infantry", 50, 65_000, 85_000));
        army.Add(Regiment("d", "infantry", 50, 65_000, 115_000));
        string plan =
            "{\"version\":0,\"map\":\"test\",\"army\":["
            + string.Join(",", army)
            + "],\"terrain\":["
            + Bridge
            + "],\"casts\":[]}";
        return new Battle(RepoData.Rules(), map, BattlePlan.Load(plan, map, RepoData.Rules()), 1);
    }

    private static int[] DeckCells(Battle battle) =>
        battle.PlayedMap.Decks!.Segments.SelectMany(s => s.Cells).ToArray();

    /// <summary>The regiments (by slot) with soldiers standing on the bridge.</summary>
    private static List<int> OnBridge(World world, Terrain terrain, HashSet<int> deck)
    {
        var on = new SortedSet<int>();
        foreach (int u in TestBattles.LiveUnits(world))
        {
            terrain.CellOf(world.Position[u], out int x, out int y);
            if (deck.Contains(terrain.Index(x, y)))
            {
                on.Add(world.UnitRegiment[u]);
            }
        }
        return on.ToList();
    }

    [Test]
    public void Decision_2026_11_14_MarshalsIdleLimitAndOwnOrder_OneRegimentAtATime_NarrowedToTheSpan()
    {
        // S2 14.6 as decided on 2026-11-14: one regiment on the span at a time, in its own (close) order but narrowed to
        // as many files as the span is wide (4.5 m ÷ (2 × 1 m) = 2); the rest wait short of it. The clock counts idle
        // time only, so the crossing runs until every regiment is across and then ends, keeping the second charge.
        var battle = Column();
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        var terrain = new Terrain(battle.PlayedMap);
        var deck = DeckCells(battle).ToHashSet();
        int started = -1;
        int firstOnBridge = -1;
        int mostHeld = 0;
        var crossed = new HashSet<int>();
        int allAcross = int.MaxValue;
        for (int tick = 0; tick < 900 * SimConstants.TicksPerSecond; tick++)
        {
            simulation.Step();
            if (started < 0 && world.Causes!.Events.Any(e => e.Kind == CauseKind.SpecialistStarted))
            {
                started = simulation.Tick;
            }
            if (tick % 10 != 0)
            {
                continue;
            }
            var on = OnBridge(world, terrain, deck).Where(r => r != 0).ToList();
            Assert.That(
                on.Count,
                Is.LessThanOrEqualTo(1),
                $"tick {simulation.Tick}: {string.Join(",", on)} on the bridge"
            );
            foreach (int r in on)
            {
                firstOnBridge = firstOnBridge < 0 ? simulation.Tick : firstOnBridge;
                crossed.Add(r);
                Assert.That(world.Army.Open[r], Is.False, $"regiment {r} keeps its own order");
                if (world.Army.JobRef[0] == r)
                {
                    Assert.That(world.RegimentFiles[r], Is.EqualTo(2), "two files on a 4.5 m span");
                }
            }
            if (
                allAcross == int.MaxValue
                && TestBattles
                    .LiveUnits(world)
                    .Where(u => world.UnitRegiment[u] != 0)
                    .All(u => world.Position[u].X() > 184.5)
            )
            {
                allAcross = simulation.Tick;
            }
            mostHeld = System.Math.Max(
                mostHeld,
                Enumerable.Range(0, 4).Count(r => world.Army.Order[r] == RegimentOrder.Hold)
            );
            if (
                world.Army.Job[0] != SpecialistJob.Active
                && Enumerable
                    .Range(1, 3)
                    .All(r =>
                        TestBattles
                            .LiveUnits(world)
                            .Where(u => world.UnitRegiment[u] == r)
                            .All(u => world.Position[u].X() > 190)
                    )
            )
            {
                break;
            }
        }
        var ended = world
            .Causes!.Events.Where(e =>
                e.Kind == CauseKind.SpecialistEnded && e.Detail == SpecialistExecutor.MeterAction
            )
            .ToList();
        var starts = world
            .Causes.Events.Where(e =>
                e.Kind == CauseKind.SpecialistStarted && e.Detail == SpecialistExecutor.MeterAction
            )
            .ToList();
        Assert.That(started, Is.GreaterThan(0), "the marshals took the crossing");
        Assert.That(started, Is.LessThan(firstOnBridge), "before anyone reached it");
        Assert.That(mostHeld, Is.GreaterThanOrEqualTo(2), "regiments waited while one crossed");
        Assert.That(crossed, Is.EquivalentTo(new[] { 1, 2, 3 }), "every regiment crossed");
        Assert.That(ended, Has.Count.EqualTo(1), "one crossing, for all three");
        Assert.That(starts, Has.Count.EqualTo(1));
        Assert.That(
            ended[0].Tick,
            Is.GreaterThanOrEqualTo(allAcross),
            "it ended only once every soldier was across"
        );
        Assert.That(world.Army.Spent[0], Is.EqualTo(1));
        Assert.That(
            world.Army.Job[0],
            Is.EqualTo(SpecialistJob.Idle),
            "a charge left for the next crossing"
        );
        for (int r = 1; r <= 3; r++)
        {
            Assert.That(world.RegimentFiles[r], Is.EqualTo(20), "back to its usual files");
            Assert.That(world.Army.Order[r], Is.Not.EqualTo(RegimentOrder.Hold));
        }
        Assert.That(world.Counters.SegmentsFailed, Is.Zero);
    }

    [Test]
    public void Decision_2026_10_11_TheFirstSetIncludesSpecialists_WhenTheMarshalsRout_TheQueueSurgesOnAtOnce()
    {
        var battle = Column();
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        int guard = 0;
        while (Enumerable.Range(1, 3).All(r => world.Army.Order[r] != RegimentOrder.Hold))
        {
            simulation.Step(5);
            Assert.That(++guard, Is.LessThan(4_000), "a regiment was held");
        }
        int crossing = world.Army.JobRef[0];
        Assert.That(crossing, Is.InRange(1, 3));
        world.Army.Order[0] = RegimentOrder.Rout; // as the morale step routs it
        simulation.Step(TickOrder.RegimentAiRate.Period);
        Assert.That(world.Army.Job[0], Is.EqualTo(SpecialistJob.Lost));
        for (int r = 1; r <= 3; r++)
        {
            Assert.That(
                world.Army.Order[r],
                Is.Not.EqualTo(RegimentOrder.Hold),
                $"regiment {r} goes on"
            );
            Assert.That(world.Army.Open[r], Is.False);
            Assert.That(world.RegimentFiles[r], Is.EqualTo(20));
        }
    }

    [Test]
    public void TheGateIsDeterministic_AcrossThreadCountsAndRestores()
    {
        var battle = Column();
        ulong[] serial = battle.PerSecondHashes(300, new SerialRunner());
        Assert.That(battle.PerSecondHashes(300, new ThreadedRunner(4)), Is.EqualTo(serial));
        var a = battle.Start(new SerialRunner());
        var snapshots = new List<(int Tick, byte[] Bytes)>();
        while (a.Tick < 300 * SimConstants.TicksPerSecond)
        {
            a.Step(1);
            if (a.Tick % 997 == 0)
            {
                snapshots.Add((a.Tick, a.CaptureSnapshot()));
            }
        }
        Assert.That(
            a.World.Causes!.Events.Any(e =>
                e.Kind == CauseKind.SpecialistStarted && e.Detail == SpecialistExecutor.MeterAction
            ),
            Is.True,
            "the gate ran"
        );
        ulong end = a.StateHash();
        foreach (var (tick, bytes) in snapshots)
        {
            var b = battle.Start(new ThreadedRunner(3));
            b.Step(1_777);
            b.RestoreSnapshot(bytes);
            b.Step(300 * SimConstants.TicksPerSecond - tick);
            Assert.That(b.StateHash(), Is.EqualTo(end), $"restored at tick {tick}");
        }
    }

    [Test]
    public void Decision_2026_11_07_SmallRegimentsDontBlockCasts_ButAFullRegimentDoes()
    {
        // S2 D15: a regiment that deploys with fewer than 25 soldiers (Crossing Marshals) is no shield; Rot 60 m from
        // the marshals fires, and 60 m from infantry it fizzles under the 100 m rule.
        string Cast(string regiment) =>
            "{\"version\":0,\"map\":\"test\",\"army\":["
            + regiment
            + "],\"terrain\":[],\"casts\":[{\"id\":\"rot\",\"power\":\"rot\",\"tick\":20,\"xMm\":70000,\"yMm\":75000,\"radiusMm\":10000}]}";
        var map = TestBattles.Map(Gorge, Columns, Rows);
        CauseKind Outcome(string regiment)
        {
            var battle = new Battle(
                RepoData.Rules(),
                map,
                BattlePlan.Load(Cast(regiment), map, RepoData.Rules()),
                1
            );
            var simulation = battle.Start(new SerialRunner());
            simulation.Step(2 * SimConstants.TicksPerSecond);
            return simulation
                .World.Causes!.Events.First(e =>
                    e.Kind == CauseKind.CastFired || e.Kind == CauseKind.CastFizzled
                )
                .Kind;
        }
        Assert.That(
            Outcome(Regiment("m", "crossing-marshals", 24, 10_000, 75_000)),
            Is.EqualTo(CauseKind.CastFired)
        );
        Assert.That(
            Outcome(Regiment("i", "infantry", 60, 10_000, 75_000)),
            Is.EqualTo(CauseKind.CastFizzled)
        );
    }

    [Test]
    public void OpenOrder_TakesFewerArrows()
    {
        // S2 2.2: a volley at a regiment in open order lands half its hits (and its soldiers stand further apart).
        long Damage(bool open)
        {
            var battle = TestBattles.Battle(
                TestBattles.Map((_, _) => 0, 140, 30, 80),
                new[]
                {
                    ("attacker", "infantry", 100, 30.0, 22.0),
                    ("defender", "ranged", 60, 165.0, 22.0),
                }
            );
            var simulation = battle.Start(new SerialRunner());
            var world = simulation.World;
            world.Army.Open[0] = open;
            simulation.Step(75 * SimConstants.TicksPerSecond);
            long damage = 0;
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (world.UnitRegiment[u] == 0)
                {
                    damage += world.Units.IsAlive(u) ? 100 - world.Health[u] : 100;
                }
            }
            return damage;
        }
        long close = Damage(false);
        Assert.That(close, Is.GreaterThan(0));
        Assert.That(Damage(true), Is.LessThan(close * 3 / 4), "about half");
    }
}
