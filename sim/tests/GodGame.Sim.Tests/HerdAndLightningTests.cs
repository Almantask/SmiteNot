using System.Globalization;
using GodGame.Sim.Hosting;
using GodGame.Sim.Math;

namespace GodGame.Sim.Tests;

/// <summary>Herds, Lightning v0 and fire v0 (M5 item 7; spec S2 sections 11, 12.1–12.2, 13.2).</summary>
[TestFixture, Category("Fast")]
public class HerdAndLightningTests
{
    // A flat 450 × 210 m map with 15 m zones at the west and east edges; pieces stand between x = 115 and 335 m.
    private const int Columns = 300;
    private const int Rows = 140;

    private static MapData Flat(int zoneMetres = 15) =>
        TestBattles.Map((_, _) => 0, Columns, Rows, zoneMetres);

    private const string Armies =
        "{\"id\":\"a\",\"side\":\"attacker\",\"unitType\":\"infantry\",\"soldiers\":50,\"xMm\":7000,\"yMm\":20000,\"facingCentiDegrees\":0},"
        + "{\"id\":\"d\",\"side\":\"defender\",\"unitType\":\"infantry\",\"soldiers\":50,\"xMm\":443000,\"yMm\":20000,\"facingCentiDegrees\":18000}";

    internal static string Herd(int xMm, int yMm, int radiusMm = 15_000, int animals = 60) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{{\"id\":\"herd\",\"piece\":\"herd\",\"xMm\":{0},\"yMm\":{1},\"radiusMm\":{2},\"animals\":{3}}}",
            xMm,
            yMm,
            radiusMm,
            animals
        );

    internal static string Grove(int xMm, int yMm, int radiusMm = 10_000) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{{\"id\":\"grove\",\"piece\":\"grove\",\"xMm\":{0},\"yMm\":{1},\"radiusMm\":{2}}}",
            xMm,
            yMm,
            radiusMm
        );

    internal static string Lightning(int tick, int xMm, int yMm, int headingCd) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{{\"id\":\"bolt\",\"power\":\"lightning\",\"tick\":{0},\"xMm\":{1},\"yMm\":{2},\"radiusMm\":30000,\"headingCd\":{3}}}",
            tick,
            xMm,
            yMm,
            headingCd
        );

    private static string Rain(int tick, int xMm, int yMm, int radiusMm) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{{\"id\":\"rain\",\"power\":\"rain\",\"tick\":{0},\"xMm\":{1},\"yMm\":{2},\"radiusMm\":{3}}}",
            tick,
            xMm,
            yMm,
            radiusMm
        );

    internal static Battle Load(
        string terrain,
        string casts,
        string army = Armies,
        int zoneMetres = 15
    )
    {
        var map = Flat(zoneMetres);
        var rules = RepoData.Rules();
        string plan =
            "{\"version\":0,\"map\":\"test\",\"army\":["
            + army
            + "],\"terrain\":["
            + terrain
            + "],\"casts\":["
            + casts
            + "]}";
        return new Battle(rules, map, BattlePlan.Load(plan, map, rules), 1);
    }

    private static List<CauseEvent> Events(World world, CauseKind kind) =>
        world.Causes!.Events.Where(e => e.Kind == kind).ToList();

    private static int HerdRegiment(World world) => world.Herds.Regiment[0];

    private static (double X, double Y) Centroid(World world, int regiment)
    {
        var units = TestBattles
            .LiveUnits(world)
            .Where(u => world.UnitRegiment[u] == regiment)
            .ToList();
        return (
            units.Average(u => world.Position[u].X()),
            units.Average(u => world.Position[u].Y())
        );
    }

    [Test]
    public void Decision_2026_10_08_HerdsRoamAndBlock_AGrazingHerdWandersInsideItsCircle()
    {
        var battle = Load(Herd(200_000, 100_000), "");
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        int herd = HerdRegiment(world);
        var start = TestBattles
            .LiveUnits(world)
            .Where(u => world.UnitRegiment[u] == herd)
            .ToDictionary(u => u, u => world.Position[u]);
        simulation.Step(60 * SimConstants.TicksPerSecond);
        int moved = 0;
        foreach (var (u, p) in start)
        {
            Assert.That(world.Units.IsAlive(u), Is.True, "animals never die");
            double dx = world.Position[u].X() - 200;
            double dy = world.Position[u].Y() - 100;
            Assert.That(
                System.Math.Sqrt(dx * dx + dy * dy),
                Is.LessThan(17.0),
                "inside the grazing circle"
            );
            if ((world.Position[u] - p).Length > Fix.One)
            {
                moved++;
            }
        }
        Assert.That(moved, Is.GreaterThan(start.Count / 2), "they graze from waypoint to waypoint");
        Assert.That(world.Herds.Mode[0], Is.EqualTo(HerdMode.Grazing));
    }

    [Test]
    public void Decision_2026_11_07_LightningV0_FellsTheTallestTreeAlongTheHeading_AndLightsDryFuel_WhichBurnsOut()
    {
        // S2 12.1: 3 s wind-up, the fall lands 2 s later; the fire burns 30 s and never spreads.
        var battle = Load(Grove(250_000, 100_000), Lightning(20, 250_000, 100_000, 9_000));
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        simulation.Step(20 + 60 + 1);
        Assert.That(Events(world, CauseKind.LightningStruck), Has.Count.EqualTo(1));
        var fires = Events(world, CauseKind.FireStarted);
        Assert.That(fires, Has.Count.EqualTo(1), "dry fuel catches");
        Assert.That(Events(world, CauseKind.TreeFelled), Is.Empty, "the tree is still falling");
        simulation.Step(40);
        var felled = Events(world, CauseKind.TreeFelled);
        Assert.That(felled, Has.Count.EqualTo(1));
        int blocker = felled[0].Detail;
        Assert.That(world.Blockers.Kind(blocker), Is.EqualTo(BlockerKind.FelledTree));
        // The trunk lies north of its stump (heading 90°): its cells share the stump's column, further north.
        var tree = battle.PlayedMap.Trees.Single(t => t.Index == felled[0].Subject);
        var cells = world.Blockers.Cells(blocker);
        Assert.That(cells.Length, Is.GreaterThan(tree.HeightMm / 1_500 - 1));
        Assert.That(cells.Max(c => c / Columns), Is.GreaterThan(tree.Cell / Columns + 6));
        Assert.That(
            battle
                .PlayedMap.Trees.Where(t =>
                {
                    long dx = (t.Cell % Columns) * 1500L + 750 - 250_000;
                    long dy = (t.Cell / Columns) * 1500L + 750 - 100_000;
                    return dx * dx + dy * dy <= 8_000L * 8_000;
                })
                .Max(t => t.HeightMm),
            Is.EqualTo(tree.HeightMm),
            "the tallest tree within 8 m"
        );
        var trace = world.Causes!.Trace(world.Causes.Events.ToList().IndexOf(felled[0]));
        Assert.That(trace.Any(e => e.Kind == CauseKind.CastFired), Is.True, "traced to the cast");

        simulation.Step(30 * SimConstants.TicksPerSecond);
        Assert.That(world.Strikes.FireOut(0), Is.True, "burned out after 30 s");
        Assert.That(world.Blockers.IsCleared(fires[0].Detail), Is.True, "its cells open again");
    }

    [Test]
    public void Decision_2026_11_07_LightningV0_RainBeforehandWetsTheFuel_SoNoFireCatches_ButTheTreeStillFalls()
    {
        var battle = Load(
            Grove(250_000, 100_000),
            Rain(0, 250_000, 100_000, 40_000) + "," + Lightning(600, 250_000, 100_000, 9_000)
        );
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        simulation.Step(600 + 60 + 40 + 1);
        Assert.That(Events(world, CauseKind.LightningStruck), Has.Count.EqualTo(1));
        Assert.That(Events(world, CauseKind.FireStarted), Is.Empty);
        Assert.That(Events(world, CauseKind.TreeFelled), Has.Count.EqualTo(1));
    }

    [Test]
    public void Decision_2026_10_08_HerdsStampede_FireNearAHerdStampedesItAwayFromTheFire()
    {
        // S2 11.2–11.3: the fire's fear (1,000 within 50 m) passes the threshold; 3 s agitated, then the run, heading away.
        var battle = Load(
            Herd(200_000, 100_000) + "," + Grove(250_000, 100_000),
            Lightning(20, 250_000, 100_000, 9_000)
        );
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        int herd = HerdRegiment(world);
        var before = Centroid(world, herd);
        simulation.Step(20 + 60 + 2 * SimConstants.TicksPerSecond);
        Assert.That(Events(world, CauseKind.HerdAgitated), Has.Count.EqualTo(1));
        simulation.Step(10 * SimConstants.TicksPerSecond);
        var stampeded = Events(world, CauseKind.HerdStampeded);
        Assert.That(stampeded, Has.Count.EqualTo(1));
        var trace = world.Causes!.Trace(world.Causes.Events.ToList().IndexOf(stampeded[0]));
        Assert.That(trace.Any(e => e.Kind == CauseKind.FireStarted), Is.True, "← fire started");
        Assert.That(trace.Any(e => e.Kind == CauseKind.CastFired), Is.True, "← the Lightning cast");
        var after = Centroid(world, herd);
        TestContext.Out.WriteLine(
            $"herd from ({before.X:F1}, {before.Y:F1}) to ({after.X:F1}, {after.Y:F1})"
        );
        Assert.That(after.X, Is.LessThan(before.X - 30), "away from the fire, to the west");
        Assert.That(System.Math.Abs(after.Y - before.Y), Is.LessThan(25), "straight away from it");
    }

    [Test]
    public void Decision_2026_11_07_LightningV0_ThunderAloneOnlyMakesAHerdMill_ItNeverStampedes()
    {
        // S2 11.2: a strike's own fear is 500, under the 700 threshold; with no fuel there is no fire.
        var battle = Load(Herd(200_000, 100_000), Lightning(20, 250_000, 100_000, 9_000));
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        simulation.Step(30 * SimConstants.TicksPerSecond);
        Assert.That(Events(world, CauseKind.LightningStruck), Has.Count.EqualTo(1));
        Assert.That(Events(world, CauseKind.HerdAgitated), Is.Empty);
        Assert.That(world.Herds.Mode[0], Is.EqualTo(HerdMode.Grazing));
    }

    [Test]
    public void Decision_2026_10_11_ManyWaysToDie_AStampedeLaunchesAndTramplesTheColumnItMeets_ButNeverChains()
    {
        // S2 11.4: an animal at speed in contact with a soldier launches him (600 per mille) or tramples him (60 damage,
        // stunned 3 s). Every launch is the stampede's: landings only knock down (9.3).
        string column =
            "{\"id\":\"a\",\"side\":\"attacker\",\"unitType\":\"infantry\",\"soldiers\":200,\"xMm\":8000,\"yMm\":100000,\"facingCentiDegrees\":0},"
            + "{\"id\":\"d\",\"side\":\"defender\",\"unitType\":\"infantry\",\"soldiers\":50,\"xMm\":443000,\"yMm\":20000,\"facingCentiDegrees\":18000}";
        var battle = Load(
            Herd(200_000, 100_000) + "," + Grove(250_000, 100_000),
            Lightning(20, 250_000, 100_000, 9_000),
            column
        );
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        simulation.Step(60 * SimConstants.TicksPerSecond);
        var events = world.Causes!.Events.ToList();
        var stampede = events.FindIndex(e => e.Kind == CauseKind.HerdStampeded);
        Assert.That(stampede, Is.GreaterThanOrEqualTo(0));
        var launched = events.Where(e => e.Kind == CauseKind.SoldierLaunched).ToList();
        var stampedeSource = battle.Rules.Launches.Source("stampede")!.Index;
        int trampled = events.Count(e => e.Kind == CauseKind.SoldierTrampled);
        int deaths = events.Count(e =>
            e.Kind is CauseKind.SoldierTrampled or CauseKind.SoldierFell or CauseKind.SoldierKilled
        );
        TestContext.Out.WriteLine(
            $"launched {launched.Count}, trampled to death {trampled}, deaths {deaths}, killed {world.Counters.Killed}"
        );
        Assert.That(launched, Is.Not.Empty, "the stampede throws soldiers");
        Assert.That(
            launched.All(e =>
                Ballistics.SourceIndexOf(e.Detail) == stampedeSource && e.Cause == stampede
            ),
            Is.True,
            "every launch is the stampede's: none chains from a landing"
        );
        Assert.That(world.Units.HighWater, Is.GreaterThan(0));
        Assert.That(
            TestBattles
                .LiveUnits(world)
                .Count(u => world.UnitRegiment[u] == world.Herds.Regiment[0]),
            Is.EqualTo(60),
            "animals never die"
        );
    }

    internal static string Reg(
        string id,
        string side,
        string type,
        int soldiers,
        int x,
        int y,
        int facing = 0
    ) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{{\"id\":\"{0}\",\"side\":\"{1}\",\"unitType\":\"{2}\",\"soldiers\":{3},\"xMm\":{4},\"yMm\":{5},\"facingCentiDegrees\":{6}}}",
            id,
            side,
            type,
            soldiers,
            x,
            y,
            facing
        );

    [Test]
    public void Decision_2026_10_11_TheFirstSetIncludesSpecialists_DroversWalkAHerdOffTheRoad_AndNeverStampedeIt()
    {
        // S2 14.8: a herd grazing on the army's path ahead; the attacker's drovers walk it until every animal is 50 m from
        // the path, at the driven pace (fear 400, under the 700 threshold).
        string army =
            Reg("a", "attacker", "infantry", 100, 8_000, 100_000)
            + ","
            + Reg("b", "attacker", "drovers", 15, 95_000, 80_000)
            + ","
            + Reg("d", "defender", "infantry", 50, 443_000, 100_000, 18_000);
        // A 100 m zone: the drovers set out 87 m ahead of the column (a herd 100 m beyond the zone edge).
        var battle = Load(Herd(230_000, 100_000), "", army, zoneMetres: 100);
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        int herd = HerdRegiment(world);
        int vanguardArrives = -1;
        double clearAtArrival = -1;
        for (int second = 0; second < 240 && vanguardArrives < 0; second++)
        {
            simulation.Step(SimConstants.TicksPerSecond);
            double front = TestBattles
                .LiveUnits(world)
                .Where(u => world.UnitRegiment[u] == 0)
                .Max(u => world.Position[u].X());
            if (front >= 200)
            {
                vanguardArrives = second;
                clearAtArrival = TestBattles
                    .LiveUnits(world)
                    .Where(u => world.UnitRegiment[u] == herd)
                    .Min(u => System.Math.Abs(world.Position[u].Y() - 100));
            }
        }
        TestContext.Out.WriteLine(
            $"vanguard at the herd's ground at {vanguardArrives} s; nearest animal {clearAtArrival:F1} m from the road"
        );
        Assert.That(vanguardArrives, Is.GreaterThan(0));
        Assert.That(
            Events(world, CauseKind.HerdAgitated),
            Is.Empty,
            "drovers never start a stampede"
        );
        Assert.That(
            clearAtArrival,
            Is.GreaterThanOrEqualTo(50.0),
            "walked off the road before the vanguard came (S2 17: 50 m from the path)"
        );
    }

    [Test]
    public void Decision_2026_11_07_ClearingFelledTrees_EngineersClearATreeLightningFelledAcrossTheRoad_In30Seconds()
    {
        // S2 13.2, 14.7: Engineers clear a felled tree on their path in 600 ticks.
        string army =
            Reg("a", "attacker", "infantry", 100, 8_000, 60_000)
            + ","
            + Reg("e", "attacker", "engineers", 40, 8_000, 100_000)
            + ","
            + Reg("d", "defender", "infantry", 50, 443_000, 100_000, 18_000);
        var battle = Load(
            Grove(250_000, 112_000, 6_000),
            Lightning(20, 250_000, 112_000, 27_000),
            army
        );
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        const int engineers = 2; // slots follow plan ids in ordinal order: a, d, e
        int onSite = -1;
        for (int tick = 0; tick < 200 * SimConstants.TicksPerSecond; tick++)
        {
            simulation.Step();
            if (onSite < 0 && world.Army.WorkProgress[engineers] > 0)
            {
                onSite = tick;
            }
        }
        var felled = Events(world, CauseKind.TreeFelled);
        Assert.That(felled, Has.Count.EqualTo(1));
        var cleared = Events(world, CauseKind.BlockerCleared);
        TestContext.Out.WriteLine(
            string.Join(
                "; ",
                world
                    .Causes!.Events.Where(e =>
                        e.Kind
                            is CauseKind.SpecialistStarted
                                or CauseKind.SpecialistEnded
                                or CauseKind.BlockerCleared
                    )
                    .Select(e => $"{e.Kind} r{e.Subject} d{e.Detail} @{e.Tick / 20.0:F1}s")
            )
        );
        Assert.That(cleared, Has.Count.EqualTo(1), "the engineers clear it");
        Assert.That(cleared[0].Detail, Is.EqualTo(felled[0].Detail), "that tree");
        Assert.That(world.Blockers.IsCleared(felled[0].Detail), Is.True);
        Assert.That(cleared[0].Subject, Is.EqualTo(engineers));
        Assert.That(
            cleared[0].Tick - onSite,
            Is.InRange(600 - 10, 600),
            "600 ticks of work once there (the first update's 5 ticks already count)"
        );
    }

    [Test]
    public void HerdsAndLightningAreDeterministic_AcrossThreadCountsAndARestore()
    {
        var battle = Load(
            Herd(200_000, 100_000) + "," + Grove(250_000, 100_000),
            Lightning(20, 250_000, 100_000, 9_000)
        );
        ulong[] serial = battle.PerSecondHashes(40, new SerialRunner());
        Assert.That(battle.PerSecondHashes(40, new ThreadedRunner(4)), Is.EqualTo(serial));
        var a = battle.Start(new SerialRunner());
        a.Step(150); // the fall is in the air, the fire burning, the herd agitated
        var snapshot = a.CaptureSnapshot();
        a.Step(500);
        var b = battle.Start(new ThreadedRunner(3));
        b.Step(30);
        b.RestoreSnapshot(snapshot);
        b.Step(500);
        Assert.That(b.StateHash(), Is.EqualTo(a.StateHash()));
    }
}

/// <summary>Herds and charges across several movement chunks (M5 item 7): a minute of runs, so Slow, not Fast.</summary>
[TestFixture, Category("Slow")]
public class HerdAndChargeDeterminismTests
{
    [Test]
    public void StampedesAndChargesAreDeterministic_AcrossSeveralChunks_ThreadCountsAndRestores()
    {
        // About 920 units, so movement runs in two chunks (512 each): the stampede runs through three regiments of
        // 200 in the attacker zone while their cavalry charges the defenders across the field.
        string army =
            HerdAndLightningTests.Reg("a1", "attacker", "infantry", 200, 8_000, 80_000)
            + ","
            + HerdAndLightningTests.Reg("a2", "attacker", "infantry", 200, 8_000, 100_000)
            + ","
            + HerdAndLightningTests.Reg("a3", "attacker", "infantry", 200, 8_000, 120_000)
            + ","
            + HerdAndLightningTests.Reg("c", "attacker", "cavalry", 60, 8_000, 40_000)
            + ","
            + HerdAndLightningTests.Reg("d", "defender", "infantry", 200, 443_000, 40_000, 18_000);
        var battle = HerdAndLightningTests.Load(
            HerdAndLightningTests.Herd(200_000, 100_000)
                + ","
                + HerdAndLightningTests.Grove(250_000, 100_000),
            HerdAndLightningTests.Lightning(20, 250_000, 100_000, 9_000),
            army
        );
        ulong[] serial = battle.PerSecondHashes(150, new SerialRunner());
        Assert.That(battle.PerSecondHashes(150, new ThreadedRunner(3)), Is.EqualTo(serial));
        Assert.That(battle.PerSecondHashes(150, new ThreadedRunner(8)), Is.EqualTo(serial));
        var reference = battle.Start(new SerialRunner());
        reference.Step(150 * SimConstants.TicksPerSecond);
        var world = reference.World;
        Assert.That(world.Causes!.Events.Any(e => e.Kind == CauseKind.SoldierLaunched), Is.True);
        Assert.That(world.Causes.Events.Any(e => e.Kind == CauseKind.ChargeStarted), Is.True);
        foreach (int at in new[] { 400, 1_200, 2_200 })
        {
            var a = battle.Start(new SerialRunner());
            a.Step(at);
            var snapshot = a.CaptureSnapshot();
            var b = battle.Start(new ThreadedRunner(8));
            b.Step(50);
            b.RestoreSnapshot(snapshot);
            b.Step(150 * SimConstants.TicksPerSecond - at);
            Assert.That(b.StateHash(), Is.EqualTo(reference.StateHash()), "restored at tick " + at);
        }
    }
}
