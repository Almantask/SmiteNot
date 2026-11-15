using System.Globalization;
using GodGame.Sim.Hosting;

namespace GodGame.Sim.Tests;

/// <summary>Cavalry charges and trampling (M5 item 7; spec S2 section 10, D7, D8).</summary>
[TestFixture, Category("Fast")]
public class ChargeTests
{
    // A flat 300 × 90 m map with 15 m zones: the riders start in the west, the infantry stand in the east.
    private const int Columns = 200;
    private const int Rows = 60;

    private static MapData Flat(string extraJson = "") =>
        TestBattles.Map((_, _) => 0, Columns, Rows, extraJson: extraJson);

    private static string Regiment(
        string id,
        string side,
        string type,
        int soldiers,
        int x,
        int y,
        int facing,
        string extra = ""
    ) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{{\"id\":\"{0}\",\"side\":\"{1}\",\"unitType\":\"{2}\",\"soldiers\":{3},\"xMm\":{4},\"yMm\":{5},\"facingCentiDegrees\":{6}{7}}}",
            id,
            side,
            type,
            soldiers,
            x,
            y,
            facing,
            extra
        );

    private static Battle Load(MapData map, string army)
    {
        var rules = RepoData.Rules();
        string plan =
            "{\"version\":0,\"map\":\"test\",\"army\":[" + army + "],\"terrain\":[],\"casts\":[]}";
        return new Battle(rules, map, BattlePlan.Load(plan, map, rules), 1);
    }

    private static string Riders => Regiment("a", "attacker", "cavalry", 60, 8_000, 45_000, 0);

    private static string Line(string extra = "") =>
        Regiment("d", "defender", "infantry", 200, 290_000, 45_000, 18_000, extra);

    private static List<CauseEvent> Events(World world, CauseKind kind) =>
        world.Causes!.Events.Where(e => e.Kind == kind).ToList();

    [Test]
    public void Decision_2026_10_11_ManyWaysToDie_AChargeLaunchesAndTramplesTheFrontRanks_ButNeverChains()
    {
        // S2 10.3: each charging rider takes at most 2 soldiers in front of him, launched (500 per mille) or trampled;
        // every launch is the charge's, never a landing's.
        var battle = Load(Flat(), Riders + "," + Line());
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        simulation.Step(120 * SimConstants.TicksPerSecond);
        var events = world.Causes!.Events.ToList();
        var started = Events(world, CauseKind.ChargeStarted);
        Assert.That(started, Is.Not.Empty, "the riders charge");
        var launched = events.Where(e => e.Kind == CauseKind.SoldierLaunched).ToList();
        int chargeSource = battle.Rules.Launches.Source("charge")!.Index;
        var chargeEvents = started.Select(e => events.IndexOf(e)).ToHashSet();
        TestContext.Out.WriteLine(
            $"charges {started.Count}, launched {launched.Count}, trampled to death {events.Count(e => e.Kind == CauseKind.SoldierTrampled)}"
        );
        Assert.That(launched, Is.Not.Empty, "the charge throws the front ranks");
        Assert.That(
            launched.All(e =>
                Ballistics.SourceIndexOf(e.Detail) == chargeSource && chargeEvents.Contains(e.Cause)
            ),
            Is.True,
            "every launch comes from a rider's contact"
        );
        Assert.That(
            launched.Count,
            Is.LessThanOrEqualTo(2 * 60 * started.Count),
            "at most two per rider per charge"
        );
    }

    [Test]
    public void Decision_2026_11_07_DeepMudStopsACharge_ARiderInDeepGroundIsThrownAndTheChargeEnds()
    {
        // D7: a charging rider whose cell has mud of three quarters of deep stops dead, stunned, unhurt; the charge ends.
        var battle = Load(Flat(), Riders + "," + Line());
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        int guard = 0;
        while (
            Events(world, CauseKind.ChargeStarted).Count == 0
            && guard++ < 200 * SimConstants.TicksPerSecond
        )
        {
            simulation.Step();
        }
        Assert.That(Events(world, CauseKind.ChargeStarted), Has.Count.EqualTo(1));
        // A band of deep mud right ahead of the riders, laid after the charge began (so the line was open).
        const int riders = 0; // regiment slots follow plan id order: "a" first
        double front = TestBattles
            .LiveUnits(world)
            .Where(u => world.UnitRegiment[u] == riders)
            .Max(u => world.Position[u].X());
        var mud = world.Fields!.Get(GroundModel.Mud).Values;
        var wet = world.Fields.Get(GroundModel.Wetness).Values;
        int from = (int)(front / 1.5) + 15; // 22 m on: they are at charging speed by then
        for (int y = 0; y < Rows; y++)
        {
            for (int x = from; x < from + 6; x++)
            {
                mud[y * Columns + x] = 65_535;
                wet[y * Columns + x] = 65_535;
            }
        }
        simulation.Step(10 * SimConstants.TicksPerSecond);
        var thrown = Events(world, CauseKind.RiderThrown);
        Assert.That(thrown, Is.Not.Empty, "riders thrown in the mud");
        Assert.That(world.Army.ChargeSince[riders], Is.EqualTo(0), "the charge ended");
        Assert.That(world.Units.IsAlive(thrown[0].Detail), Is.True, "no damage");
    }

    [Test]
    public void Decision_2026_11_07_NoChargeOnIceOrThroughMud_ACavalryRegimentDoesNotChargeAcrossDeepGround()
    {
        // S2 10.1, D8: the straight line must hold no mud of at least 32,767, water of 300 mm or ice.
        var battle = Load(Flat(), Riders + "," + Line());
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        var mud = world.Fields!.Get(GroundModel.Mud).Values;
        var wet = world.Fields.Get(GroundModel.Wetness).Values;
        // A strip of mud across the whole field, between the two armies, from the start.
        for (int y = 0; y < Rows; y++)
        {
            for (int x = 150; x < 154; x++)
            {
                mud[y * Columns + x] = 40_000;
                wet[y * Columns + x] = 65_535;
            }
        }
        int guard = 0;
        // The riders walk across the strip (it slows but doesn't close horse), so they charge only once past it.
        while (
            guard++ < 120 * SimConstants.TicksPerSecond
            && Events(world, CauseKind.ChargeStarted).Count == 0
        )
        {
            simulation.Step();
        }
        var started = Events(world, CauseKind.ChargeStarted);
        Assert.That(started, Is.Not.Empty);
        double front = TestBattles
            .LiveUnits(world)
            .Where(u => world.UnitRegiment[u] == 0)
            .Average(u => world.Position[u].X());
        Assert.That(front, Is.GreaterThan(154 * 1.5 - 5), "not before the strip lay behind them");
    }

    [Test]
    public void ChargesAreDeterministic_AcrossThreadCountsAndARestore()
    {
        var battle = Load(Flat(), Riders + "," + Line());
        ulong[] serial = battle.PerSecondHashes(90, new SerialRunner());
        Assert.That(battle.PerSecondHashes(90, new ThreadedRunner(4)), Is.EqualTo(serial));
        var a = battle.Start(new SerialRunner());
        a.Step(60 * SimConstants.TicksPerSecond);
        var snapshot = a.CaptureSnapshot();
        a.Step(30 * SimConstants.TicksPerSecond);
        var b = battle.Start(new ThreadedRunner(3));
        b.Step(100);
        b.RestoreSnapshot(snapshot);
        b.Step(30 * SimConstants.TicksPerSecond);
        Assert.That(b.StateHash(), Is.EqualTo(a.StateHash()));
    }
}
