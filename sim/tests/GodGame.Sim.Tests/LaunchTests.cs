using System.Globalization;
using GodGame.Sim.Hosting;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim.Tests;

/// <summary>Launches: ballistic bodies, the seeded spread, landings and knock-downs (M5 item 5, spec S2 section 9).</summary>
[TestFixture, Category("Fast")]
public class LaunchTests
{
    // 360 × 60 m with 15 m zones at each edge; a defending regiment of 50 holds at x = 352 m. Whatever the case puts on
    // the ground lies west of x = 342 m (columns 0–227), where its launched soldiers come down.
    private const int Columns = 240;
    private const int Rows = 40;
    private const int FeatureEndColumn = 227;

    internal static string Plan(string army, string terrain = "", string casts = "") =>
        "{\"version\":0,\"map\":\"test\",\"army\":["
        + army
        + "],\"terrain\":["
        + terrain
        + "],\"casts\":["
        + casts
        + "]}";

    internal static string Defenders(int soldiers = 50, int yMm = 30_000, string id = "d") =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{{\"id\":\"{0}\",\"side\":\"defender\",\"unitType\":\"infantry\",\"soldiers\":{1},\"xMm\":352000,\"yMm\":{2},\"facingCentiDegrees\":0}}",
            id,
            soldiers,
            yMm
        );

    internal static Battle Load(MapData map, string plan) =>
        new Battle(RepoData.Rules(), map, BattlePlan.Load(plan, map, RepoData.Rules()), 1);

    /// <summary>A map whose columns up to <see cref="FeatureEndColumn"/> sit at <paramref name="floorMm"/>.</summary>
    private static MapData Floor(int floorMm) =>
        TestBattles.Map((x, _) => x <= FeatureEndColumn && x >= 150 ? floorMm : 0, Columns, Rows);

    /// <summary>Fills the hollow west of the regiment with still water to the brim (as the moat tests do).</summary>
    private static void Flood(Simulation simulation, int depthMm)
    {
        var water = simulation.World.Fields!.Get(WaterModel.Depth);
        var wetness = simulation.World.Fields!.Get(GroundModel.Wetness);
        for (int y = 0; y < Rows; y++)
        {
            for (int x = 150; x <= FeatureEndColumn; x++)
            {
                water.Set(x, y, depthMm * 1000);
                wetness.Set(x, y, GroundModel.Full);
            }
        }
    }

    internal static int Westmost(World world) =>
        TestBattles.LiveUnits(world).OrderBy(u => world.Position[u].X.Raw).ThenBy(u => u).First();

    internal static CauseEvent? LandingOf(World world, int launched) =>
        world
            .Causes!.Events.Where(e => e.Kind == CauseKind.SoldierLanded && e.Cause == launched)
            .Cast<CauseEvent?>()
            .FirstOrDefault();

    /// <summary>Steps until the launch has landed (at most 30 s) and returns the landing.</summary>
    internal static CauseEvent StepUntilLanded(Simulation simulation, int launched)
    {
        for (int t = 0; t < 600; t++)
        {
            simulation.Step();
            var landed = LandingOf(simulation.World, launched);
            if (landed != null)
            {
                return landed.Value;
            }
        }
        Assert.Fail("never landed");
        return default;
    }

    [Test]
    public void AnArc_FliesTwiceItsVerticalSpeedOverGravity_AndItsHorizontalSpeedTimesThat()
    {
        // S2 9.2: flight = 2 × vertical / gravity; distance = horizontal × flight. A charge's throw: 6 m/s up, 5 m/s along,
        // under 4 m/s²: 3 s (60 ticks) and 15 m on level ground.
        var battle = Load(Floor(0), Plan(Defenders()));
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        simulation.Step(20);
        int u = Westmost(world);
        var start = world.Position[u];
        int launched = Ballistics.LaunchWith(
            world,
            u,
            Angle.HalfTurn,
            5_000,
            6_000,
            0,
            CauseLog.None,
            simulation.Tick,
            0
        );
        int launchTick = simulation.Tick;
        Assert.That((world.Flags[u] & UnitFlags.Launched) != 0, Is.True);
        simulation.Step(30);
        Assert.That(
            world.Ballistic.HeightMm[u],
            Is.EqualTo(4_500),
            "the top of the arc at 1.5 s: 6 × 1.5 − 2 × 1.5² m"
        );
        var landed = StepUntilLanded(simulation, launched);

        Assert.That(landed.Tick - launchTick + 1, Is.EqualTo(60), "3 s in the air");
        Assert.That(start.X() - world.Position[u].X(), Is.EqualTo(15.0).Within(0.001));
        Assert.That(world.Position[u].Y(), Is.EqualTo(start.Y()).Within(0.001));
        Assert.That(Landing.Outcome(landed.Detail), Is.EqualTo(LandingOutcome.Ground));
        Assert.That(world.Health[u], Is.EqualTo(100 - battle.Rules.Launches.Ground.Damage));
        Assert.That(
            world.Flags[u] & (UnitFlags.Stunned | UnitFlags.Launched),
            Is.EqualTo(UnitFlags.Stunned)
        );
        Assert.That(world.Ballistic.HeightMm[u], Is.Zero, "a landed soldier has no arc");
    }

    [Test]
    public void Decision_2026_10_09_ThePreviewShowsWhereSoldiersWillLand_TheSpreadIsSeededPerSourceAndSoldier_NotByWhoIsNear()
    {
        // S2 9.2: heading ± 25°, speed ± 20%, seeded by (source event, soldier), never by contact geometry: the same soldier
        // thrown by the same source flies the same way twice, whoever else stands near him.
        (FixVec2 Velocity, int Vertical, int Spin)[] Throw(bool crowd)
        {
            string army = Defenders() + (crowd ? "," + Defenders(200, 31_000, "z") : "");
            var battle = Load(Floor(0), Plan(army));
            var simulation = battle.Start(new SerialRunner());
            var world = simulation.World;
            simulation.Step(20);
            var charge = battle.Rules.Launches.Source("charge")!;
            var result = new (FixVec2, int, int)[50];
            for (int u = 0; u < 50; u++)
            {
                Ballistics.Launch(
                    world,
                    battle.Rules.Launches,
                    u,
                    charge,
                    7, // the rider's regiment, say
                    Angle.HalfTurn,
                    CauseLog.None,
                    simulation.Tick,
                    simulation.Seed,
                    0
                );
                result[u] = (
                    world.Velocity[u],
                    world.Ballistic.VerticalMmPerS[u],
                    world.Ballistic.SpinCdPerS[u]
                );
            }
            return result;
        }

        var alone = Throw(false);
        Assert.That(Throw(false), Is.EqualTo(alone), "the same throw twice");
        Assert.That(Throw(true), Is.EqualTo(alone), "a crowd beside him changes nothing");
        Assert.That(
            alone.Select(t => t.Velocity).Distinct().Count(),
            Is.EqualTo(50),
            "but each soldier has his own"
        );
        var rules = RepoData.Rules().Launches;
        foreach (var (velocity, vertical, spin) in alone)
        {
            double degrees = System.Math.Atan2(velocity.Y(), -velocity.X()) * 180 / System.Math.PI;
            Assert.That(degrees, Is.InRange(-25.01, 25.01), "within 25° of the rider's heading");
            Assert.That(vertical, Is.InRange(6_000 * 0.8, 6_000 * 1.2));
            double along =
                System.Math.Sqrt(velocity.X() * velocity.X() + velocity.Y() * velocity.Y())
                * 20_000;
            Assert.That(
                along / vertical,
                Is.EqualTo(5.0 / 6.0).Within(0.01),
                "the spread scales both speeds"
            );
            Assert.That(spin, Is.InRange(rules.SpinMinCdPerS, rules.SpinMaxCdPerS));
        }
    }

    public enum Ground
    {
        Flat,
        Cliff,
        Drop,
        Shallow,
        Deep,
        DeepNearTheBank,
    }

    [TestCase(Ground.Flat, LandingOutcome.Ground, 10, 60)]
    [TestCase(Ground.Drop, LandingOutcome.HardFall, 40, 80)]
    [TestCase(Ground.Shallow, LandingOutcome.ShallowWater, 0, 60)]
    [TestCase(Ground.DeepNearTheBank, LandingOutcome.Fringe, 0, 60)]
    public void Decision_2026_10_09_ExaggeratedPhysics_MostLaunchedSoldiersSurviveStunned(
        Ground ground,
        LandingOutcome expected,
        int damage,
        int stunTicks
    )
    {
        // S2 9.3: shape first, kill second. Ground, a 3 m drop, shallow water and deep water within 2 m of footing all
        // leave the soldier alive, hurt by the table's damage and stunned for its time.
        var (simulation, u, landed) = LandAt(ground);
        var world = simulation.World;

        Assert.That(Landing.Outcome(landed.Detail), Is.EqualTo(expected));
        Assert.That(world.Units.IsAlive(u), Is.True);
        Assert.That(world.Health[u], Is.EqualTo(100 - damage));
        Assert.That(
            world.Flags[u] & (UnitFlags.Stunned | UnitFlags.Swept),
            Is.EqualTo(UnitFlags.Stunned)
        );
        Assert.That(
            world.HazardTimer[u],
            Is.EqualTo(stunTicks - 1),
            "stunned, and the hazard step counted one tick"
        );
        if (ground == Ground.DeepNearTheBank)
        {
            Assert.That(
                world.Position[u].X(),
                Is.GreaterThan((FeatureEndColumn + 1) * 1.5),
                "moved out to the bank"
            );
        }
    }

    [Test]
    public void Decision_2026_10_09_ExaggeratedPhysics_LandingOffACliffKills_AndTheTraceNamesTheLaunch()
    {
        // S2 9.3: ground 4 m or more below where he was launched from kills him (cause fell), linked to the launch.
        var (simulation, u, landed) = LandAt(Ground.Cliff);
        var log = simulation.World.Causes!;

        Assert.That(Landing.Outcome(landed.Detail), Is.EqualTo(LandingOutcome.Fell));
        Assert.That(Landing.IsLethal(LandingOutcome.Fell), Is.True);
        Assert.That(simulation.World.Units.IsAlive(u), Is.False);
        int index = log.Events.ToList().FindIndex(e => e.Kind == CauseKind.SoldierFell);
        Assert.That(index, Is.GreaterThanOrEqualTo(0));
        Assert.That(
            log.Trace(index).Select(e => e.Kind),
            Is.EqualTo(
                new[] { CauseKind.SoldierFell, CauseKind.SoldierLanded, CauseKind.SoldierLaunched }
            )
        );
    }

    [Test]
    public void Decision_2026_10_09_ExaggeratedPhysics_LandingInDeepWaterBeyondTheFringeSweepsHim()
    {
        // S2 9.3 and S1: deep water more than 2 m from footing hands him to the swept state with the class's drowning
        // timer; his sweep links to the landing, so a drowning traces back to the launch.
        var (simulation, u, landed) = LandAt(Ground.Deep);
        var world = simulation.World;

        Assert.That(Landing.Outcome(landed.Detail), Is.EqualTo(LandingOutcome.Swept));
        Assert.That(world.Health[u], Is.EqualTo(100), "water does no damage");
        Assert.That(world.Flags[u] & UnitFlags.Swept, Is.EqualTo(UnitFlags.Swept));
        Assert.That(
            world.HazardTimer[u],
            Is.EqualTo(300 - 1),
            "S1's foot drowning timer, one tick spent"
        );
        Assert.That(
            world.Causes!.UnitSwept[u],
            Is.EqualTo(world.Causes.Events.ToList().IndexOf(landed))
        );
    }

    /// <summary>
    /// Launches the westmost soldier 6 m/s up and on west so he comes down at x = 335 m (deep water near the bank: 341 m)
    /// over the case's ground, and returns his landing.
    /// </summary>
    private static (Simulation Simulation, int Unit, CauseEvent Landed) LandAt(Ground ground)
    {
        int floor = ground switch
        {
            Ground.Cliff => -5_000,
            Ground.Drop => -3_000,
            Ground.Shallow => -300,
            Ground.Deep or Ground.DeepNearTheBank => -1_500,
            _ => 0,
        };
        var battle = Load(Floor(floor), Plan(Defenders()));
        var simulation = battle.Start(new SerialRunner());
        if (ground == Ground.Shallow)
        {
            Flood(simulation, 300);
        }
        else if (ground == Ground.Deep || ground == Ground.DeepNearTheBank)
        {
            Flood(simulation, 1_500);
        }
        simulation.Step(60); // the speed codes catch up with the water
        var world = simulation.World;
        int u = Westmost(world);
        double target = ground == Ground.DeepNearTheBank ? 341.0 : 335.0;
        int horizontal = (int)((world.Position[u].X() - target) * 1000 / 3);
        int launched = Ballistics.LaunchWith(
            world,
            u,
            Angle.HalfTurn,
            horizontal,
            6_000,
            0,
            CauseLog.None,
            simulation.Tick,
            0
        );
        return (simulation, u, StepUntilLanded(simulation, launched));
    }

    [Test]
    public void Decision_2026_10_09_ExaggeratedPhysics_ThrownIntoABoulderHeComesBackAlongHisArc()
    {
        // S2 9.3: a hard blocker moves him back along the arc to the nearest open cell, hurt 20 and stunned 60 ticks.
        string boulder = "{\"id\":\"b\",\"piece\":\"boulder\",\"xMm\":230000,\"yMm\":30000}";
        var battle = Load(TestBattles.Map((_, _) => 0, Columns, Rows), Plan(Defenders(), boulder));
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        simulation.Step(20);
        // The officer stands at the regiment's point; the boulder's top (2 m) is reached on the way down at tick 53.
        int u = TestBattles
            .LiveUnits(world)
            .OrderBy(k => System.Math.Abs(world.Position[k].Y() - 30))
            .ThenBy(k => world.Position[k].X())
            .First();
        double y = world.Position[u].Y();
        Assume.That(y, Is.EqualTo(30.0).Within(0.6));
        int horizontal = (int)((world.Position[u].X() - 230.0) * 1000 * 20 / 53);
        int launched = Ballistics.LaunchWith(
            world,
            u,
            Angle.HalfTurn,
            horizontal,
            6_000,
            0,
            CauseLog.None,
            simulation.Tick,
            0
        );
        var landed = StepUntilLanded(simulation, launched);

        Assert.That(Landing.Outcome(landed.Detail), Is.EqualTo(LandingOutcome.Blocked));
        Assert.That(world.Health[u], Is.EqualTo(80));
        Assert.That(world.Position[u].X(), Is.GreaterThan(230.0), "back toward where he came from");
        var terrain = new Terrain(battle.PlayedMap);
        terrain.CellOf(world.Position[u], out int cx, out int cy);
        Assert.That(terrain.IsBlocked(terrain.Index(cx, cy)), Is.False, "on an open cell");
    }

    [Test]
    public void Decision_2026_10_09_ExaggeratedPhysics_ALandingKnocksNeighboursDown_NeverHurtsThem_AndNeverChains()
    {
        // S2 9.3: everyone on his feet within 1 m of a landing is down for 20 ticks, unhurt and never launched; the
        // knocked-down knock nobody down, so only the lander's own neighbours fall.
        var battle = Load(Floor(0), Plan(Defenders(200)));
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        simulation.Step(200); // formed up
        int lander = TestBattles
            .LiveUnits(world)
            .OrderBy(k =>
                System.Math.Abs(world.Position[k].Y() - 30)
                + System.Math.Abs(world.Position[k].X() - 349)
            )
            .First();
        int launched = Ballistics.LaunchWith(
            world,
            lander,
            Angle.Zero,
            0,
            2_000,
            0,
            CauseLog.None,
            simulation.Tick,
            0
        );
        var landed = StepUntilLanded(simulation, launched);
        var at = world.Position[lander];
        var down = TestBattles
            .LiveUnits(world)
            .Where(k => k != lander && (world.Flags[k] & UnitFlags.Stunned) != 0)
            .ToList();

        Assert.That(Landing.Outcome(landed.Detail), Is.EqualTo(LandingOutcome.Ground));
        Assert.That(down, Is.Not.Empty, "his neighbours went down");
        foreach (int k in down)
        {
            double dx = world.Position[k].X() - at.X();
            double dy = world.Position[k].Y() - at.Y();
            Assert.That(
                System.Math.Sqrt(dx * dx + dy * dy),
                Is.LessThanOrEqualTo(1.0 + 1e-3),
                "only within 1 m"
            );
            Assert.That(world.HazardTimer[k], Is.EqualTo(19), "down for 20 ticks");
        }
        Assert.That(TestBattles.LiveUnits(world).Count(), Is.EqualTo(200), "nobody died");
        Assert.That(
            TestBattles.LiveUnits(world).Where(k => k != lander).All(k => world.Health[k] == 100),
            Is.True,
            "nobody else was hurt"
        );
        Assert.That(
            TestBattles.LiveUnits(world).Any(k => (world.Flags[k] & UnitFlags.Launched) != 0),
            Is.False,
            "nobody was launched"
        );
    }

    [Test]
    public void Decision_2026_10_09_ThePreviewShowsWhereSoldiersWillLand_LaunchedSoldiersStillBlockACast()
    {
        // Brief 2026-10-09: launched and stunned soldiers still count toward their regiment's outline for the range rule,
        // so scattering a regiment can't bring casts closer. The regiment is thrown west, high, 5 s before a Rain 60 m from
        // where it is then (and 122 m from where it stood): the cast fizzles.
        string rain =
            "{\"id\":\"r\",\"power\":\"rain\",\"tick\":120,\"xMm\":150000,\"yMm\":75000,\"radiusMm\":40000}";
        CauseKind Fire(bool launch)
        {
            var battle = Load(
                TestBattles.Map((_, _) => 0, Columns, 100),
                Plan(Defenders(50, 75_000), "", rain)
            );
            var simulation = battle.Start(new SerialRunner());
            var world = simulation.World;
            simulation.Step(20);
            if (launch)
            {
                foreach (int u in TestBattles.LiveUnits(world).ToList())
                {
                    Ballistics.LaunchWith(
                        world,
                        u,
                        Angle.HalfTurn,
                        20_000,
                        20_000,
                        0,
                        CauseLog.None,
                        simulation.Tick,
                        0
                    );
                }
            }
            simulation.Step(110);
            if (launch)
            {
                Assert.That(
                    TestBattles
                        .LiveUnits(world)
                        .All(u => (world.Flags[u] & UnitFlags.Launched) != 0),
                    Is.True,
                    "still in the air when the cast fires"
                );
            }
            return world
                .Causes!.Events.First(e =>
                    e.Kind == CauseKind.CastFired || e.Kind == CauseKind.CastFizzled
                )
                .Kind;
        }

        Assert.That(
            Fire(false),
            Is.EqualTo(CauseKind.CastFired),
            "far enough from where they stood"
        );
        Assert.That(Fire(true), Is.EqualTo(CauseKind.CastFizzled), "but they flew closer");
    }

    [Test]
    public void Decision_2026_10_08_PowersCanKillThroughConsequences_ACollapsedBridgeLaunchesItsCrowdIntoTheRiver()
    {
        // S2 8.4: units on a failed bridge segment are launched with the collapse source (a 1 m hop), then land on what is
        // below: deep water, so swept, or the 2 m fringe stun near a bank (the end segments' bank cells drop them on the
        // ground). The chain reads segment failed → launched →
        // landed (swept) → drowned, back to the Rot. The landing spread is exactly the landings.
        var (simulation, battle) = CollapsedBridge(new SerialRunner());
        var world = simulation.World;
        var events = world.Causes!.Events;
        Assert.That(
            world.Counters.Fell + world.Counters.LandingKilled,
            Is.Zero,
            "water breaks the fall"
        );
        int failed = events.ToList().FindIndex(e => e.Kind == CauseKind.SegmentFailed);
        Assert.That(failed, Is.GreaterThanOrEqualTo(0), "the rotted bridge failed");
        var launches = events.Where(e => e.Kind == CauseKind.SoldierLaunched).ToList();
        var landings = events.Where(e => e.Kind == CauseKind.SoldierLanded).ToList();
        Assert.That(launches, Is.Not.Empty);
        Assert.That(
            launches.All(e => events[e.Cause].Kind == CauseKind.SegmentFailed),
            Is.True,
            "every launch names its failed segment"
        );
        Assert.That(landings, Has.Count.EqualTo(launches.Count), "everyone came down");
        var outcomes = landings.Select(e => Landing.Outcome(e.Detail)).ToList();
        Assert.That(
            outcomes.All(o =>
                o == LandingOutcome.Swept
                || o == LandingOutcome.Fringe
                || o == LandingOutcome.Ground
            ),
            Is.True,
            string.Join(",", outcomes)
        );
        Assert.That(
            outcomes,
            Does.Contain(LandingOutcome.Swept),
            "the middle of the span drops into the river"
        );
        int drowned = events.ToList().FindIndex(e => e.Kind == CauseKind.SoldierDrowned);
        Assert.That(drowned, Is.GreaterThanOrEqualTo(0), "some drowned");
        Assert.That(
            world.Causes.Trace(drowned).Select(e => e.Kind),
            Is.EqualTo(
                new[]
                {
                    CauseKind.SoldierDrowned,
                    CauseKind.SoldierLanded,
                    CauseKind.SoldierLaunched,
                    CauseKind.SegmentFailed,
                    CauseKind.CastFired,
                }
            )
        );

        var spreads = LandingSpread.FromLog(
            world.Causes,
            Columns,
            1_500,
            battle.Rules.Launches.PreviewCellMm
        );
        var collapse = battle.Rules.Launches.Source("collapse")!;
        Assert.That(
            spreads.Select(s => s.Source),
            Is.EquivalentTo(
                launches
                    .Select(e => Ballistics.SourceId(collapse, events[e.Cause].Subject))
                    .Distinct()
            ),
            "one spread per failed segment, keyed by the segment"
        );
        Assert.That(
            spreads.SelectMany(s => s.Events),
            Is.EquivalentTo(launches.Select(e => e.Cause).Distinct()),
            "each keeps its link to the failure event"
        );
        Assert.That(spreads.Sum(s => s.Stunned + s.Lethal), Is.EqualTo(landings.Count));
        Assert.That(
            spreads.Sum(s => s.Lethal),
            Is.EqualTo(outcomes.Count(o => o == LandingOutcome.Swept))
        );
        Assert.That(
            spreads.Sum(s => s.Stunned),
            Is.EqualTo(outcomes.Count(o => o != LandingOutcome.Swept))
        );
        var again = LandingSpread.FromLog(
            CollapsedBridge(new ThreadedRunner(4)).Simulation.World.Causes!,
            Columns,
            1_500,
            1_500
        );
        Assert.That(
            again.SelectMany(s => s.Cells.Select(c => (s.Source, c.X, c.Y, c.Stunned, c.Lethal))),
            Is.EqualTo(
                spreads.SelectMany(s =>
                    s.Cells.Select(c => (s.Source, c.X, c.Y, c.Stunned, c.Lethal))
                )
            ),
            "the preview's spread is the battle's"
        );
    }

    [Test]
    public void Decision_2026_10_09_ThePreviewShowsWhereSoldiersWillLand_AnUnrelatedEarlierEventDoesntMoveTheSpread()
    {
        // Brief 2026-10-09: the spread is seeded by the source (here the failed segment) and the soldier, never by the
        // event's place in the log, so a small edit moves it smoothly. A Rot cast far from the bridge fires first and logs
        // an event before the collapse; every soldier on the bridge still lands where he did, the same way.
        static List<(int Regiment, int Detail, int Tick)> Landings(string extraCast)
        {
            var world = CollapsedBridge(new SerialRunner(), 70, extraCast).Simulation.World;
            var events = world.Causes!.Events;
            return events
                .Where(e => e.Kind == CauseKind.SoldierLanded)
                .Select(e => (e.Subject, e.Detail, e.Tick))
                .ToList();
        }

        var plain = Landings("");
        var edited = Landings(
            "{\"id\":\"a-far\",\"power\":\"rot\",\"tick\":40,\"xMm\":60000,\"yMm\":20000,\"radiusMm\":10000}"
        );
        Assert.That(plain, Is.Not.Empty);
        Assert.That(edited, Is.EqualTo(plain));
    }

    // A river 30 m wide (columns 110–129) and 2 m deep, full of still water, spanned by a wooden bridge: from its
    // middle, footing is further than a swept soldier looks for it, so the swept drown.
    private const int RiverColumns = 240;
    private const int RiverRows = 100;

    private static bool InRiver(int x) => x >= 110 && x <= 129;

    /// <summary>A jam of 400 put on a rotted wooden bridge over the river at 45 s, run until 70 s.</summary>
    internal static (Simulation Simulation, Battle Battle) CollapsedBridge(
        IParallelRunner runner,
        int seconds = 70,
        string extraCast = ""
    )
    {
        var map = TestBattles.Map((x, _) => InRiver(x) ? -2_000 : 0, RiverColumns, RiverRows);
        string army =
            "{\"id\":\"d\",\"side\":\"defender\",\"unitType\":\"infantry\",\"soldiers\":200,\"xMm\":352000,\"yMm\":40000,\"facingCentiDegrees\":0},"
            + "{\"id\":\"e\",\"side\":\"defender\",\"unitType\":\"infantry\",\"soldiers\":200,\"xMm\":352000,\"yMm\":110000,\"facingCentiDegrees\":0}";
        string bridge =
            "{\"id\":\"b\",\"piece\":\"wooden-bridge\",\"points\":[[163500,75750],[196500,75750]],\"widthMm\":4500}";
        string rot =
            "{\"id\":\"rot\",\"power\":\"rot\",\"tick\":100,\"xMm\":180000,\"yMm\":75750,\"radiusMm\":20000}";
        var battle = new Battle(
            RepoData.Rules(),
            map,
            BattlePlan.Load(
                Plan(army, bridge, rot + (extraCast.Length > 0 ? "," + extraCast : "")),
                map,
                RepoData.Rules()
            ),
            1
        );
        var simulation = battle.Start(runner);
        var world = simulation.World;
        var water = world.Fields!.Get(WaterModel.Depth);
        var wetness = world.Fields!.Get(GroundModel.Wetness);
        for (int y = 0; y < RiverRows; y++)
        {
            for (int x = 0; x < RiverColumns; x++)
            {
                if (InRiver(x))
                {
                    water.Set(x, y, 2_000_000);
                    wetness.Set(x, y, GroundModel.Full);
                }
            }
        }
        simulation.Step(45 * SimConstants.TicksPerSecond); // the Rot is done
        var deck = battle
            .PlayedMap.Decks!.Segments.SelectMany(seg => seg.Cells)
            .OrderBy(c => c)
            .ToArray();
        var soldiers = TestBattles.LiveUnits(world).ToArray();
        for (int k = 0; k < soldiers.Length; k++)
        {
            int cell = deck[k % deck.Length];
            int round = k / deck.Length;
            world.Position[soldiers[k]] = new FixVec2(
                Fix.FromRatio((cell % RiverColumns) * 1_500 + 300 + (round % 3) * 450, 1000),
                Fix.FromRatio((cell / RiverColumns) * 1_500 + 300 + (round / 3) * 450, 1000)
            );
        }
        simulation.Step((seconds - 45) * SimConstants.TicksPerSecond);
        return (simulation, battle);
    }
}

/// <summary>Launches across thread counts and restores mid-flight (M5 item 5).</summary>
[TestFixture, Category("Fast")]
public class LaunchDeterminismTests
{
    /// <summary>
    /// Three regiments of 200 (600 units: two movement chunks) beside a deep pool; a stampede's throw flings every second
    /// soldier west, then the rest 40 ticks later, so bodies land among bodies still in the air: knock-downs, sweeps,
    /// fringe landings and drownings.
    /// </summary>
    private static Simulation Start(IParallelRunner runner)
    {
        var map = TestBattles.Map((x, _) => x >= 150 && x <= 227 ? -1_500 : 0, 240, 100);
        var rules = RepoData.Rules();
        string army = string.Join(
            ",",
            new[] { 30_000, 75_000, 120_000 }.Select(
                (y, k) =>
                    "{\"id\":\"d"
                    + k
                    + "\",\"side\":\"defender\",\"unitType\":\"infantry\",\"soldiers\":200,\"xMm\":352000,\"yMm\":"
                    + y
                    + ",\"facingCentiDegrees\":0}"
            )
        );
        string plan =
            "{\"version\":0,\"map\":\"test\",\"army\":[" + army + "],\"terrain\":[],\"casts\":[]}";
        var simulation = new Battle(rules, map, BattlePlan.Load(plan, map, rules), 5).Start(runner);
        var water = simulation.World.Fields!.Get(WaterModel.Depth);
        for (int y = 0; y < 100; y++)
        {
            for (int x = 150; x <= 227; x++)
            {
                water.Set(x, y, 1_500_000);
            }
        }
        return simulation;
    }

    private static readonly LaunchRules Rules = RepoData.Rules().Launches;

    /// <summary>Steps to <paramref name="until"/>, launching at ticks 20 and 60; the hash after every tick.</summary>
    private static List<ulong> Run(Simulation simulation, int until)
    {
        var world = simulation.World;
        var stampede = Rules.Source("stampede")!;
        var hashes = new List<ulong>();
        while (simulation.Tick < until)
        {
            if (simulation.Tick == 20 || simulation.Tick == 60)
            {
                int half = simulation.Tick == 20 ? 0 : 1;
                foreach (int u in TestBattles.LiveUnits(world).Where(k => k % 2 == half).ToList())
                {
                    Ballistics.Launch(
                        world,
                        Rules,
                        u,
                        stampede,
                        half, // the herd, say
                        Angle.HalfTurn,
                        CauseLog.None,
                        simulation.Tick,
                        simulation.Seed,
                        0
                    );
                }
            }
            simulation.Step();
            hashes.Add(simulation.StateHash());
        }
        return hashes;
    }

    [Test]
    public void Launches_AreTheSameOnAnyRunner_TickByTick_AndAcrossARestoreMidFlight()
    {
        const int end = 420;
        var serial = Start(new SerialRunner());
        var threaded = Start(new ThreadedRunner(4));
        Assert.That(serial.World.Units.HighWater, Is.GreaterThan(512), "two movement chunks");
        var before = Run(serial, 70);
        Assert.That(Run(threaded, 70), Is.EqualTo(before), "every tick up to mid-flight");
        Assert.That(
            TestBattles
                .LiveUnits(serial.World)
                .Count(u => (serial.World.Flags[u] & UnitFlags.Launched) != 0),
            Is.GreaterThan(0),
            "the snapshot below is taken mid-flight"
        );
        byte[] snapshot = serial.CaptureSnapshot();
        var after = Run(serial, end);
        Assert.That(Run(threaded, end), Is.EqualTo(after), "every tick through the landings");
        var log = serial.World.Causes!;
        Assert.That(log.Events.Count(e => e.Kind == CauseKind.SoldierLanded), Is.EqualTo(600));
        Assert.That(
            log.Events.Any(e => e.Kind == CauseKind.SoldierDrowned),
            Is.True,
            "sweeps that ran their course"
        );

        var restored = Start(new ThreadedRunner(3));
        restored.Step(37);
        restored.RestoreSnapshot(snapshot);
        Assert.That(Run(restored, end), Is.EqualTo(after), "restored mid-flight, every tick");
    }
}
