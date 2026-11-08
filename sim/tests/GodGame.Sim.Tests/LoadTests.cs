using System.Globalization;
using GodGame.Sim.Hosting;
using GodGame.Sim.Rules;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim.Tests;

/// <summary>Decks, load, strain, collapse and Rot v0 (M5 item 4, spec S2 sections 8, 12.3 and 13.4–13.6).</summary>
[TestFixture, Category("Fast")]
public class LoadTests
{
    // 360 × 150 m, 15 m zones at the west and east edges. A gorge 6 m deep at columns 118–122 (x 177–184.5 m) that
    // nothing climbs: only a bridge crosses it.
    private const int Columns = 240;
    private const int Rows = 100;

    private static int Gorge(int x, int y) => x >= 118 && x <= 122 ? -6_000 : 0;

    private static string Plan(string army, string terrain = "", string casts = "") =>
        "{\"version\":0,\"map\":\"test\",\"army\":["
        + army
        + "],\"terrain\":["
        + terrain
        + "],\"casts\":["
        + casts
        + "]}";

    private static string Infantry(int soldiers, int x, int y, string id = "i") =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{{\"id\":\"{0}\",\"side\":\"attacker\",\"unitType\":\"infantry\",\"soldiers\":{1},\"xMm\":{2},\"yMm\":{3},\"facingCentiDegrees\":0}}",
            id,
            soldiers,
            x,
            y
        );

    private const string Bridge =
        "{\"id\":\"b\",\"piece\":\"wooden-bridge\",\"points\":[[175500,75750],[186000,75750]],\"widthMm\":4500}";

    private const string RotTheBridge =
        "{\"id\":\"rot\",\"power\":\"rot\",\"tick\":100,\"xMm\":180750,\"yMm\":75750,\"radiusMm\":10000}";

    private static Battle Load(MapData map, string plan) =>
        new Battle(RepoData.Rules(), map, BattlePlan.Load(plan, map, RepoData.Rules()), 1);

    [Test]
    public void ABridgeIsADeck_FlatForTheClassesItCarries_AFootbridgeCarriesOnlyFoot()
    {
        var map = TestBattles.Map(Gorge, Columns, Rows);
        string foot =
            "{\"id\":\"f\",\"piece\":\"footbridge\",\"points\":[[175500,30750],[186000,30750]],\"widthMm\":3000}";
        var battle = Load(map, Plan(Infantry(50, 8_000, 75_000), Bridge + "," + foot));
        var costs = new CostFields(new Terrain(battle.PlayedMap), battle.Rules, new SerialRunner());
        foreach (var c in new[] { MovementClass.Foot, MovementClass.Horse, MovementClass.Wheeled })
        {
            Assert.That(
                costs.Cost(c, 120, 50),
                Is.Not.EqualTo(CostFields.Impassable),
                $"{c} on the wooden bridge"
            );
            Assert.That(
                costs.Cost(c, 118, 60),
                Is.EqualTo(CostFields.Impassable),
                $"{c} at the gorge's wall (its floor is flat)"
            );
        }
        Assert.That(costs.Cost(MovementClass.Foot, 120, 20), Is.Not.EqualTo(CostFields.Impassable));
        Assert.That(
            costs.Cost(MovementClass.Horse, 120, 20),
            Is.EqualTo(CostFields.Impassable),
            "a footbridge is foot only"
        );
        var decks = battle.PlayedMap.Decks!;
        // 4.5 m wide (3 cells) and 10.5 m long: 2-cell segments, the last taking the odd cell; strength = area × 300 kg/m².
        Assert.That(decks.Segments[0].Cells, Has.Length.EqualTo(6));
        Assert.That(decks.Segments[0].MaxStrengthKg, Is.EqualTo(6 * 2_250_000L * 300 / 1_000_000));
    }

    [Test]
    public void Decision_2026_10_08_PowersCanKillThroughConsequences_ARottedBridgeFailsUnderAJamThatASoundOneCarries()
    {
        // S2 8.1: a jam of 2–3 soldiers per m² takes a sound wooden bridge (300 kg/m²) to 60–90%: it creaks and holds; a
        // rotted one (half strength) fails under it, naming the Rot. The jam is put on the deck directly (a holding
        // regiment, far from its formation slots, stays where it is put): how dense a column packs on its own waits for
        // the steering calibration (S2 4.2).
        var map = TestBattles.Map(Gorge, Columns, Rows);
        string army =
            "{\"id\":\"d\",\"side\":\"defender\",\"unitType\":\"infantry\",\"soldiers\":120,\"xMm\":352000,\"yMm\":75000,\"facingCentiDegrees\":0}";
        (int Failed, int PeakStage, List<CauseEvent> Events, int Stunned) Run(string casts)
        {
            var battle = Load(map, Plan(army, Bridge, casts));
            var simulation = battle.Start(new SerialRunner());
            var world = simulation.World;
            simulation.Step(45 * SimConstants.TicksPerSecond); // any Rot is done by now
            var decks = battle.PlayedMap.Decks!;
            var deck = decks.Segments.SelectMany(seg => seg.Cells).OrderBy(c => c).ToArray();
            var soldiers = TestBattles.LiveUnits(world).ToArray();
            for (int k = 0; k < soldiers.Length; k++)
            {
                int cell = deck[k % deck.Length];
                int round = k / deck.Length;
                world.Position[soldiers[k]] = new GodGame.Sim.Math.FixVec2(
                    GodGame.Sim.Math.Fix.FromRatio(
                        (cell % Columns) * 1_500 + 300 + (round % 3) * 450,
                        1000
                    ),
                    GodGame.Sim.Math.Fix.FromRatio(
                        (cell / Columns) * 1_500 + 300 + (round / 3) * 450,
                        1000
                    )
                );
            }
            int peak = 0;
            for (int t = 0; t < 10 * SimConstants.TicksPerSecond; t++)
            {
                simulation.Step();
                for (int s = 0; s < world.Structures!.Count; s++)
                {
                    peak = System.Math.Max(peak, (int)world.Structures.Stage[s]);
                }
            }
            int stunned = TestBattles
                .LiveUnits(world)
                .Count(u => (world.Flags[u] & UnitFlags.Stunned) != 0);
            return (world.Counters.SegmentsFailed, peak, world.Causes!.Events.ToList(), stunned);
        }

        var sound = Run("");
        Assert.That(sound.Failed, Is.Zero, "a sound bridge carries the jam");
        Assert.That(
            sound.PeakStage,
            Is.GreaterThanOrEqualTo((int)StrainStage.Creak),
            "and creaks under it"
        );

        var rotted = Run(RotTheBridge);
        Assert.That(rotted.Failed, Is.GreaterThan(0), "a rotted bridge fails under it");
        var failed = rotted.Events.First(e => e.Kind == CauseKind.SegmentFailed);
        Assert.That(failed.Detail, Is.EqualTo(1));
        Assert.That(
            rotted.Events[failed.Cause].Kind,
            Is.EqualTo(CauseKind.CastFired),
            "the Rot is the cause"
        );
    }

    [TestCase(60, true)]
    [TestCase(250, false)]
    public void Decision_2026_10_08_WeakIceBreaksUnderCrowds_ThinIceBreaksUnderAColumn_ThickIceCarriesIt(
        int thicknessMm,
        bool breaks
    )
    {
        // S2 8.1: a 6 × 6 m patch holds 1 kg/m² per mm. A marching infantry column (about 75 kg/m²) breaks 60 mm ice and
        // crosses 250 mm; the soldiers on a broken patch are swept at once.
        string pond = string.Format(
            CultureInfo.InvariantCulture,
            ",\"frozenPonds\":[{{\"minXMm\":165000,\"minYMm\":0,\"maxXMm\":195000,\"maxYMm\":150000,\"thicknessMm\":{0}}}]",
            thicknessMm
        );
        var map = TestBattles.Map((_, _) => 0, Columns, Rows, extraJson: pond);
        var battle = Load(map, Plan(Infantry(200, 8_000, 75_000)));
        var simulation = battle.Start(new SerialRunner());
        simulation.Step(240 * SimConstants.TicksPerSecond);
        var world = simulation.World;
        if (breaks)
        {
            Assert.That(world.Counters.SegmentsFailed, Is.GreaterThan(0));
            Assert.That(
                world.Causes!.Events.Any(e => e.Kind == CauseKind.SegmentFailed && e.Detail == 2),
                Is.True
            );
            Assert.That(world.Counters.Swept, Is.GreaterThan(0), "into the hole");
        }
        else
        {
            Assert.That(world.Counters.SegmentsFailed, Is.Zero);
            Assert.That(
                TestBattles.LiveUnits(world).Count(u => world.Position[u].X() > 200),
                Is.GreaterThan(150)
            );
        }
    }

    [Test]
    public void RotV0_WeakensWoodTo500_InThirtySeconds_AndASecondRotStacksDownTo300()
    {
        var map = TestBattles.Map(Gorge, Columns, Rows);
        string second =
            "{\"id\":\"rot2\",\"power\":\"rot\",\"tick\":1200,\"xMm\":180750,\"yMm\":75750,\"radiusMm\":10000}";
        var battle = Load(
            map,
            Plan(
                "{\"id\":\"d\",\"side\":\"defender\",\"unitType\":\"infantry\",\"soldiers\":50,\"xMm\":352000,\"yMm\":20000,\"facingCentiDegrees\":0}",
                Bridge,
                RotTheBridge + "," + second
            )
        );
        var simulation = battle.Start(new SerialRunner());
        var states = simulation.World.Structures!;
        simulation.Step(300); // the wind-up ends at tick 300
        Assert.That(states.Strength[0], Is.EqualTo(1000));
        simulation.Step(300);
        Assert.That(states.Strength[0], Is.InRange(740, 760), "halfway through the 30 s");
        simulation.Step(400);
        Assert.That(states.Strength, Is.All.EqualTo(500));
        simulation.Step(1400);
        Assert.That(states.Strength, Is.All.EqualTo(300), "500 less 200, the stack");
    }

    [TestCase(
        "{\"id\":\"b\",\"piece\":\"wooden-bridge\",\"points\":[[178000,75750],[182000,75750]],\"widthMm\":4500}",
        "spans"
    )]
    [TestCase(
        "{\"id\":\"b\",\"piece\":\"wooden-bridge\",\"points\":[[175500,75750],[186000,75750]],\"widthMm\":8000}",
        "widthMm"
    )]
    [TestCase(
        "{\"id\":\"b\",\"piece\":\"footbridge\",\"points\":[[175500,75750],[186000,75750]],\"widthMm\":4500}",
        "widthMm"
    )]
    [TestCase(
        "{\"id\":\"b\",\"piece\":\"wooden-bridge\",\"points\":[[175500,75750]],\"widthMm\":4500}",
        "two bank points"
    )]
    [TestCase(
        "{\"id\":\"b\",\"piece\":\"wooden-bridge\",\"points\":[[20000,75750],[30000,75750]],\"widthMm\":4500}",
        "deployment zone"
    )]
    public void ABridgeThatCantBePlaced_IsRefusedWhilePlanning(string terrain, string why)
    {
        var map = TestBattles.Map(Gorge, Columns, Rows);
        var error = Assert.Throws<DataFormatException>(() =>
            BattlePlan.Load(Plan(Infantry(50, 8_000, 75_000), terrain), map, RepoData.Rules())
        );
        Assert.That(error!.Message, Does.Contain(why));
    }
}

/// <summary>Collapses across thread counts and snapshots (M5 item 4).</summary>
[TestFixture, Category("Slow")]
public class LoadSlowTests
{
    [Test]
    public void CollapsesAreDeterministic_AcrossThreadCountsAndSnapshots()
    {
        var map = TestBattles.Map((x, y) => x >= 118 && x <= 122 ? -6_000 : 0, 240, 100);
        // Seven infantry regiments and the siege train (1,450 units: three movement chunks) on a rotted bridge.
        string army = string.Join(
            ",",
            Enumerable
                .Range(0, 7)
                .Select(k =>
                    "{\"id\":\"i"
                    + k
                    + "\",\"side\":\"attacker\",\"unitType\":\"infantry\",\"soldiers\":200,\"xMm\":8000,\"yMm\":"
                    + (10_000 + k * 20_000)
                    + ",\"facingCentiDegrees\":0}"
                )
                .Append(
                    "{\"id\":\"j\",\"side\":\"attacker\",\"unitType\":\"siege\",\"soldiers\":50,\"xMm\":8000,\"yMm\":145000,\"facingCentiDegrees\":0}"
                )
        );
        string plan =
            "{\"version\":0,\"map\":\"test\",\"army\":["
            + army
            + "],"
            + "\"terrain\":[{\"id\":\"b\",\"piece\":\"wooden-bridge\",\"points\":[[175500,75750],[186000,75750]],\"widthMm\":4500}],"
            + "\"casts\":[{\"id\":\"rot\",\"power\":\"rot\",\"tick\":100,\"xMm\":180750,\"yMm\":75750,\"radiusMm\":10000}]}";
        var battle = new Battle(
            RepoData.Rules(),
            map,
            BattlePlan.Load(plan, map, RepoData.Rules()),
            3
        );
        ulong[] serial = battle.PerSecondHashes(240, new SerialRunner());
        Assert.That(battle.PerSecondHashes(240, new ThreadedRunner(4)), Is.EqualTo(serial));

        var a = battle.Start(new SerialRunner());
        var snapshots = new List<(int Tick, byte[] Bytes)>();
        // Every 571 ticks: off the 4-tick load checks as well as on them.
        while (a.Tick < 240 * SimConstants.TicksPerSecond)
        {
            a.Step(1);
            if (a.Tick % 571 == 0)
            {
                snapshots.Add((a.Tick, a.CaptureSnapshot()));
            }
        }
        Assert.That(
            a.World.Counters.SegmentsFailed,
            Is.GreaterThan(0),
            "the rotted bridge failed in the run"
        );
        ulong end = a.StateHash();
        foreach (var (tick, bytes) in snapshots)
        {
            var b = battle.Start(new ThreadedRunner(3));
            b.Step(4_321);
            b.RestoreSnapshot(bytes);
            b.Step(240 * SimConstants.TicksPerSecond - tick);
            Assert.That(b.StateHash(), Is.EqualTo(end), $"restored at tick {tick}");
        }
    }
}
