using GodGame.Sim.Rules;

namespace GodGame.Sim.Tests;

[TestFixture, Category("Fast")]
public class RoutePreviewTests
{
    private const int Wall = 5_000;

    [Test]
    public void TheRoute_FollowsTheFlowThroughTheGap_ToTheGoal_KeepingOnlyCorners()
    {
        var map = TestBattles.Map((x, y) => x == 40 && (y < 17 || y > 23) ? Wall : 0, 80, 40);
        var navigation = new Navigation(map, RepoData.Rules(), new SerialRunner());
        var battle = TestBattles.Battle(map, new[] { ("attacker", "infantry", 50, 7.5, 7.5) });
        var world = battle.Start(new SerialRunner()).World;

        var route = RoutePreview.AttackerRoutes(navigation, world)[0];

        Assert.That(route.Count, Is.InRange(3, 12), "start, a few corners, goal");
        Assert.That(
            route[^1].X(),
            Is.GreaterThanOrEqualTo(80 * 1.5 - 15),
            "ends in the defender zone"
        );
        Assert.That(
            route.Any(p => p.X() > 58 && p.X() < 63 && p.Y() > 25.5 && p.Y() < 36),
            Is.True,
            "turns at the gap"
        );
        // Between corners the line is straight along one of the eight directions: every leg is axial or 45°.
        for (int i = 1; i < route.Count; i++)
        {
            double dx = System.Math.Abs(route[i].X() - route[i - 1].X());
            double dy = System.Math.Abs(route[i].Y() - route[i - 1].Y());
            Assert.That(
                dx < 1e-6 || dy < 1e-6 || System.Math.Abs(dx - dy) < 1e-6,
                Is.True,
                $"leg {i}"
            );
        }
    }

    [Test]
    public void AProposedEdit_MovesThePreview_BeforeAnythingIsCommitted()
    {
        // Close the gap at y = 17 to 23 and open one at the north end: the preview swings north.
        var map = TestBattles.Map((x, y) => x == 40 && (y < 17 || y > 23) ? Wall : 0, 80, 40);
        var navigation = new Navigation(map, RepoData.Rules(), new SerialRunner());
        var world = TestBattles
            .Battle(map, new[] { ("attacker", "infantry", 50, 7.5, 7.5) })
            .Start(new SerialRunner())
            .World;
        var before = RoutePreview.AttackerRoutes(navigation, world)[0];

        var terrain = navigation.Terrain;
        for (int y = 17; y <= 23; y++)
        {
            terrain.SetHeight(40, y, Wall);
        }
        for (int y = 32; y <= 39; y++)
        {
            terrain.SetHeight(40, y, 0);
        }
        var dirty = terrain.DirtyAround(40, 17, 40, 39);
        navigation.Costs.Rebuild(dirty, new SerialRunner());
        navigation.FlowFields.Repair(dirty, new SerialRunner());
        var after = RoutePreview.AttackerRoutes(navigation, world)[0];

        Assert.That(before.Max(p => p.Y()), Is.LessThan(40));
        Assert.That(
            after.Any(p => p.X() > 58 && p.X() < 63 && p.Y() > 48),
            Is.True,
            "through the new gap"
        );
    }

    [Test]
    public void NoRoute_GivesAnEmptyPreview_AndDefendersHaveNone()
    {
        var map = TestBattles.Map((x, y) => x == 40 ? Wall : 0, 80, 40);
        var navigation = new Navigation(map, RepoData.Rules(), new SerialRunner());
        var world = TestBattles
            .Battle(
                map,
                new[]
                {
                    ("attacker", "infantry", 50, 7.5, 7.5),
                    ("defender", "infantry", 50, 112.5, 7.5),
                }
            )
            .Start(new SerialRunner())
            .World;

        var routes = RoutePreview.AttackerRoutes(navigation, world);

        Assert.That(routes, Has.Length.EqualTo(2));
        Assert.That(routes.All(r => r.Count == 0), Is.True);
    }
}
