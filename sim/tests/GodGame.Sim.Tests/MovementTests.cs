using GodGame.Sim.Hosting;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim.Tests;

[TestFixture, Category("Fast")]
public class MovementTests
{
    private const int Wall = 5_000;

    // A world built by hand, with the M2 navigation and movement systems.
    private static (Simulation, MovementSystem) Run(
        MapData map,
        World world,
        IParallelRunner? runner = null
    )
    {
        runner ??= new SerialRunner();
        var navigation = new Navigation(map, RepoData.Rules(), runner);
        var movement = new MovementSystem(navigation);
        var systems = TickOrder.CreateDefault();
        systems[(int)TickStep.Navigation] = new NavigationSystem(navigation);
        systems[(int)TickStep.Movement] = movement;
        return (new Simulation(world, 1, runner, systems), movement);
    }

    private static double MinDistance(World world)
    {
        var live = TestBattles.LiveUnits(world).ToArray();
        double best = double.MaxValue;
        foreach (int a in live)
        {
            foreach (int b in live)
            {
                if (a < b)
                {
                    double dx = world.Position[a].X() - world.Position[b].X();
                    double dy = world.Position[a].Y() - world.Position[b].Y();
                    best = System.Math.Min(best, System.Math.Sqrt(dx * dx + dy * dy));
                }
            }
        }
        return best;
    }

    [Test]
    public void SoldiersOnOneSpot_PushApart_AndFeelPressureWhileTheyOverlap()
    {
        var map = TestBattles.Map((x, y) => 0, 40, 40);
        var world = new World(20, 1);
        var regiment = world.AddRegiment(Side.Defender, 0, 100); // defenders hold: only separation moves them
        for (int i = 0; i < 20; i++)
        {
            world.SpawnUnit(regiment, FixVec2.FromInts(30, 30), 100, 100);
        }
        var (simulation, movement) = Run(map, world);

        simulation.Step();
        Assert.That(movement.Pressure.ToArray().Max(), Is.GreaterThan(0));
        simulation.Step(200);

        Assert.That(MinDistance(world), Is.GreaterThan(0.6), "infantry are 0.7 m apart at contact");
        Assert.That(
            movement.Pressure.ToArray().Max(),
            Is.LessThan(100),
            "under 0.1 m of overlap left"
        );
    }

    [Test]
    public void AttackersMarchToTheDefendersZone_AtTheirSpeed_AndStopThere()
    {
        var map = TestBattles.Map((x, y) => 0, 80, 20);
        var battle = TestBattles.Battle(map, new[] { ("attacker", "infantry", 9, 7.5, 15.0) });
        var simulation = battle.Start(new SerialRunner());
        double startX = TestBattles
            .LiveUnits(simulation.World)
            .Average(u => simulation.World.Position[u].X());

        simulation.Step(10 * SimConstants.TicksPerSecond);
        double afterTen = TestBattles
            .LiveUnits(simulation.World)
            .Average(u => simulation.World.Position[u].X());
        Assert.That(
            afterTen - startX,
            Is.InRange(12.0, 15.0),
            "1.5 m/s for 10 s, less the start-up"
        );

        simulation.Step(90 * SimConstants.TicksPerSecond);
        foreach (int u in TestBattles.LiveUnits(simulation.World))
        {
            Assert.That(
                simulation.World.Position[u].X(),
                Is.GreaterThanOrEqualTo(80 * 1.5 - 15 - 1.5)
            );
            Assert.That(simulation.World.Velocity[u].Length, Is.LessThan(Fix.FromRatio(1, 100)));
        }
    }

    [Test]
    public void AColumnAtAGap_QueuesThroughIt_NeverEntersAClosedCell_AndBarelyOverlaps()
    {
        // A wall across the map at x = 40 with an open gap of about 6 m.
        var map = TestBattles.Map((x, y) => x == 40 && (y < 17 || y > 23) ? Wall : 0, 80, 40);
        var regiments = Enumerable
            .Range(0, 4)
            .Select(i => ("attacker", "infantry", 50, 5.0, 8.0 + 14.0 * i));
        var battle = TestBattles.Battle(map, regiments);
        var simulation = battle.Start(new SerialRunner());
        var navigation = new Navigation(map, battle.Rules, new SerialRunner());
        var world = simulation.World;
        double worstOverlap = 0;
        int maxPressure = 0;
        var movement = (MovementSystem)simulation.Systems[(int)TickStep.Movement];

        for (int tick = 0; tick < 150 * SimConstants.TicksPerSecond; tick++)
        {
            simulation.Step();
            maxPressure = System.Math.Max(maxPressure, movement.Pressure.ToArray().Max());
            if (tick % 20 != 0)
            {
                continue;
            }
            foreach (int u in TestBattles.LiveUnits(world))
            {
                navigation.Terrain.CellOf(world.Position[u], out int cx, out int cy);
                Assert.That(
                    navigation.Costs.Cost(MovementClass.Foot, cx, cy),
                    Is.Not.EqualTo(CostFields.Impassable),
                    $"unit {u} at tick {tick}"
                );
            }
            worstOverlap = System.Math.Max(worstOverlap, 0.7 - MinDistance(world));
        }

        int crossed = TestBattles.LiveUnits(world).Count(u => world.Position[u].X() > 41 * 1.5);
        Assert.That(crossed, Is.EqualTo(200), "everyone got through the gap");
        Assert.That(worstOverlap, Is.LessThan(0.25), "queued rather than overlapped");
        Assert.That(maxPressure, Is.GreaterThan(0), "the crowd at the gap pressed together");
    }

    [Test]
    public void Movement_IsTheSameOnAnyThreadCount()
    {
        var map = TestBattles.Map(
            (x, y) => x == 40 && (y < 17 || y > 23) ? Wall : 100 * ((x + y) % 3),
            80,
            40
        );
        var regiments = Enumerable
            .Range(0, 6)
            .Select(i =>
                (
                    i % 2 == 0 ? "attacker" : "defender",
                    i == 2 ? "cavalry" : "infantry",
                    60,
                    i % 2 == 0 ? 5.0 : 112.0,
                    8.0 + 10.0 * i
                )
            );
        var battle = TestBattles.Battle(map, regiments);

        ulong[] serial = battle.Start(new SerialRunner()).RunAndHash(400, 20);
        Assert.That(battle.Start(new ThreadedRunner(2)).RunAndHash(400, 20), Is.EqualTo(serial));
        Assert.That(battle.Start(new ThreadedRunner(8)).RunAndHash(400, 20), Is.EqualTo(serial));
    }
}
