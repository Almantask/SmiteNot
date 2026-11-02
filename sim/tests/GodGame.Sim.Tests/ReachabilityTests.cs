using GodGame.Sim.Hosting;
using GodGame.Sim.Rules;

namespace GodGame.Sim.Tests;

[TestFixture, Category("Fast")]
public class ReachabilityTests
{
    private const int Wall = 5_000;

    private static ReachabilityResult Check(
        Func<int, int, int> heightMm,
        int columns,
        int rows,
        IParallelRunner? runner = null
    )
    {
        var rules = RepoData.Rules();
        var map = TestBattles.Map(heightMm, columns, rows);
        var costs = new CostFields(new Terrain(map), rules, new SerialRunner());
        return new Reachability(costs, map, rules.Routes).Check(runner ?? new SerialRunner());
    }

    private static RouteCheck Find(
        ReachabilityResult result,
        Side side,
        MovementClass c,
        RouteTarget target
    ) => result.Checks.Single(k => k.Side == side && k.MovementClass == c && k.Target == target);

    [Test]
    public void OpenGround_PassesEveryRoute_AtAboutTheStraightLine()
    {
        var result = Check((x, y) => 0, 80, 40);

        Assert.That(result.Ok, Is.True);
        Assert.That(result.Checks, Has.Count.EqualTo(12), "2 sides × 3 classes × 2 targets");
        var attack = Find(result, Side.Attacker, MovementClass.Foot, RouteTarget.Enemy);
        Assert.That(
            attack.LengthMm,
            Is.InRange(80 * 1_500 - 30_000 - 3_000, 80 * 1_500 - 30_000 + 3_000)
        );
        Assert.That(attack.PercentOfCap, Is.LessThan(55), "about 1x of a 2x cap");
        Assert.That(
            Find(result, Side.Defender, MovementClass.Wheeled, RouteTarget.Retreat).LengthMm,
            Is.Zero,
            "the zone touches its edge"
        );
    }

    [Test]
    public void Decision_2026_10_09_NoPlacementMayFullyTrapEitherArmy_AWallAcrossTheMapIsRefused()
    {
        var closed = Check((x, y) => x == 40 ? Wall : 0, 80, 40);
        var gap = Check((x, y) => x == 40 && (y < 15 || y > 25) ? Wall : 0, 80, 40);

        Assert.That(closed.Ok, Is.False);
        var failure = closed.FirstFailure!.Value;
        Assert.That(
            (failure.Side, failure.MovementClass, failure.Target, failure.Reached),
            Is.EqualTo((Side.Attacker, MovementClass.Foot, RouteTarget.Enemy, false))
        );
        Assert.That(gap.Ok, Is.True, "a gate in the wall keeps the route open");
    }

    [Test]
    public void Decision_2026_10_09_NoPlacementMayFullyTrapEitherArmy_SiegeNeedsAWideGap()
    {
        // The open part of a 6-cell gap is about 6 m once the wall's steep sides are taken off: wide enough for foot
        // and horse, too narrow for the siege engines' 9 m corridor.
        var narrow = Check((x, y) => x == 40 && (y < 17 || y > 22) ? Wall : 0, 80, 40);

        Assert.That(Find(narrow, Side.Attacker, MovementClass.Foot, RouteTarget.Enemy).Ok, Is.True);
        Assert.That(
            Find(narrow, Side.Attacker, MovementClass.Horse, RouteTarget.Enemy).Ok,
            Is.True
        );
        Assert.That(
            Find(narrow, Side.Attacker, MovementClass.Wheeled, RouteTarget.Enemy).Reached,
            Is.False
        );
    }

    [Test]
    public void Decision_2026_10_09_TheGuaranteedRouteHasAMaximumLength_AMazeIsRefused()
    {
        // Two long walls with gates at opposite ends: the only route snakes 3 times the length of the map.
        int Maze(int x, int y) => (x == 30 && y < 120) || (x == 50 && y > 10) ? Wall : 0;
        var maze = Check(Maze, 80, 130);
        var oneWall = Check((x, y) => x == 30 && y < 120 ? Wall : 0, 80, 130);

        var route = Find(maze, Side.Attacker, MovementClass.Foot, RouteTarget.Enemy);
        Assert.That(route.Reached, Is.True, "there is a way through");
        Assert.That(route.LengthMm, Is.GreaterThan(route.CapMm), "but far longer than 2x");
        Assert.That(maze.Ok, Is.False);
        Assert.That(
            Find(oneWall, Side.Attacker, MovementClass.Foot, RouteTarget.Enemy).Ok,
            Is.True,
            "one gate at the far end is fine: the zones reach along the whole edge"
        );
        Assert.That(
            Check((x, y) => x == 30 && y < 40 ? Wall : 0, 80, 130).Ok,
            Is.True,
            "a short wall is a detour, not a maze"
        );
    }

    [Test]
    public void SlowGroundStaysLegal_TheCapCountsDistanceNotCost()
    {
        // A steep, costly band across the whole map (500 ‰, open to foot and horse): slow, not closed.
        var steep = Check(
            (x, y) => x >= 30 && x < 50 ? (x - 30) * 750 : (x >= 50 ? 15_000 : 0),
            80,
            40
        );

        Assert.That(Find(steep, Side.Attacker, MovementClass.Foot, RouteTarget.Enemy).Ok, Is.True);
        Assert.That(
            Find(steep, Side.Attacker, MovementClass.Wheeled, RouteTarget.Enemy).Reached,
            Is.False,
            "too steep for siege: a hard blocker for that class"
        );
    }

    [Test]
    public void TheRouteOverlay_RunsFromTheZoneToTheTarget_CellByCell()
    {
        var result = Check((x, y) => x == 40 && (y < 15 || y > 25) ? Wall : 0, 80, 40);
        int index = result
            .Checks.ToList()
            .FindIndex(k =>
                k.Side == Side.Attacker
                && k.MovementClass == MovementClass.Foot
                && k.Target == RouteTarget.Enemy
            );

        var route = result.Route(index);
        Assert.That(route[0].X(), Is.LessThan(15 + 4.5), "starts in a cell of the attacker zone");
        Assert.That(
            route[^1].X(),
            Is.GreaterThan(80 * 1.5 - 15 - 4.5),
            "ends in a cell of the defender zone"
        );
        for (int i = 1; i < route.Count; i++)
        {
            Assert.That(
                System.Math.Abs(route[i].X() - route[i - 1].X()),
                Is.LessThanOrEqualTo(4.5001)
            );
            Assert.That(
                System.Math.Abs(route[i].Y() - route[i - 1].Y()),
                Is.LessThanOrEqualTo(4.5001)
            );
        }
        Assert.That(
            route.Any(p => p.X() > 58 && p.X() < 63 && p.Y() > 22 && p.Y() < 38),
            Is.True,
            "through the gate"
        );
    }

    [Test]
    public void TheCheck_IsTheSameOnAnyRunner_AndAfterIncrementalUpdates()
    {
        var rules = RepoData.Rules();
        var map = TestBattles.Map((x, y) => 0, 90, 60);
        var terrain = new Terrain(map);
        var costs = new CostFields(terrain, rules, new SerialRunner());
        var incremental = new Reachability(costs, map, rules.Routes);
        for (int y = 0; y < 60; y++)
        {
            if (y < 25 || y > 35)
            {
                terrain.SetHeight(45, y, Wall);
            }
        }
        var dirty = terrain.DirtyAround(45, 0, 45, 59);
        costs.Rebuild(dirty, new SerialRunner());
        incremental.Update(dirty);

        var fresh = new Reachability(costs, map, rules.Routes);
        var a = incremental.Check(new SerialRunner());
        var b = fresh.Check(new ThreadedRunner(4));
        Assert.That(
            a.Checks.Select(k => (k.Reached, k.LengthMm, k.CapMm)),
            Is.EqualTo(b.Checks.Select(k => (k.Reached, k.LengthMm, k.CapMm)))
        );
        for (int i = 0; i < a.Checks.Count; i++)
        {
            Assert.That(a.Route(i), Is.EqualTo(b.Route(i)));
        }
    }
}
