using GodGame.Sim.Hosting;
using GodGame.Sim.Rules;

namespace GodGame.Sim.Scenarios;

/// <summary>
/// The M2 exit criterion (plan, section 9): 10,000 attackers cross 1 km through a bridge chokepoint; congestion
/// emerges and regiments queue rather than overlap. Runs the repo's m2-bridge map and plan to the end of the crossing.
/// </summary>
[TestFixture, Category("Slow")]
public class M2ExitScenarios
{
    // A sample every 5 s of game time.
    private const int SampleTicks = 5 * SimConstants.TicksPerSecond;

    [Test]
    public void TenThousandAttackers_CrossABridgeChokepoint_AndQueueRatherThanOverlap()
    {
        string root = RepoFiles.FindRoot(TestContext.CurrentContext.TestDirectory);
        var rules = RepoFiles.LoadRepoRules(root);
        var map = RepoFiles.LoadRepoMap(root, "m2-bridge");
        var plan = BattlePlan.Load(
            File.ReadAllText(Path.Combine(root, "data", "plans", "m2-bridge-ten-thousand.json")),
            map,
            rules
        );
        var simulation = new Battle(rules, map, plan, seed: 1).Start(new ThreadedRunner(4));
        var world = simulation.World;
        var navigation = new Navigation(map, rules, new SerialRunner());
        var movement = (MovementSystem)simulation.Systems[(int)TickStep.Movement];

        const double RiverEastM = 610;
        int maxSeconds = 45 * 60;
        int crossedAt = -1;
        int peakQueue = 0; // soldiers waiting within 60 m west of the river at once
        int peakPressureMm = 0;
        var crossedOverTime = new List<int>();
        double worstOverlapShare = 0;
        double worstCrushedShare = 0; // soldiers overlapping a neighbour by more than a quarter of contact

        for (
            int tick = SampleTicks;
            tick <= maxSeconds * SimConstants.TicksPerSecond;
            tick += SampleTicks
        )
        {
            simulation.Step(SampleTicks);
            int crossed = 0;
            int queue = 0;
            foreach (int u in Live(world))
            {
                double x = world.Position[u].X.Raw / 65536.0;
                crossed += x > RiverEastM ? 1 : 0;
                queue += x > 530 && x < 590 ? 1 : 0;
                navigation.Terrain.CellOf(world.Position[u], out int cx, out int cy);
                var c = rules.UnitTypes[world.UnitType[u]].MovementClass;
                Assert.That(
                    navigation.Costs.Cost(c, cx, cy),
                    Is.Not.EqualTo(CostFields.Impassable),
                    $"unit {u} stands in a closed cell at {tick / 20} s"
                );
            }
            peakQueue = System.Math.Max(peakQueue, queue);
            peakPressureMm = System.Math.Max(peakPressureMm, movement.Pressure.ToArray().Max());
            if (tick % (60 * SimConstants.TicksPerSecond) == 0)
            {
                var (worst, crushed) = Overlaps(world, rules);
                worstOverlapShare = System.Math.Max(worstOverlapShare, worst);
                worstCrushedShare = System.Math.Max(worstCrushedShare, crushed);
            }
            crossedOverTime.Add(crossed);
            if (crossed == world.Units.AliveCount)
            {
                crossedAt = tick / SimConstants.TicksPerSecond;
                break;
            }
        }

        TestContext.Out.WriteLine(
            $"crossed by {crossedAt} s; peak queue {peakQueue}; peak pressure {peakPressureMm} mm; worst overlap {worstOverlapShare:P0} of contact; at most {worstCrushedShare:P2} of soldiers overlapping by over 25%; crossed per minute: "
                + string.Join(" ", crossedOverTime.Where((_, i) => (i + 1) % 12 == 0))
        );
        Assert.That(crossedAt, Is.GreaterThan(0), "all 10,000 crossed within 45 minutes");
        Assert.That(peakQueue, Is.GreaterThan(1_000), "congestion: a crowd built up at the bridge");
        Assert.That(peakPressureMm, Is.GreaterThan(0), "the crowd pressed");
        Assert.That(worstCrushedShare, Is.LessThan(0.01), "queued rather than overlapped");
        Assert.That(
            worstOverlapShare,
            Is.LessThan(0.65),
            "even in the deepest crush, no one stands inside another"
        );
    }

    private static IEnumerable<int> Live(World world)
    {
        for (int u = 0; u < world.Units.HighWater; u++)
        {
            if (world.Units.IsAlive(u))
            {
                yield return u;
            }
        }
    }

    // The deepest overlap between any two soldiers, as a share of their contact distance, and the share of soldiers
    // overlapping some neighbour by more than a quarter of it (a grid scan, not all pairs).
    private static (double Worst, double Crushed) Overlaps(World world, Ruleset rules)
    {
        var cells = new Dictionary<(int, int), List<int>>();
        foreach (int u in Live(world))
        {
            var key = ((int)(world.Position[u].X.Raw >> 17), (int)(world.Position[u].Y.Raw >> 17)); // 2 m cells
            if (!cells.TryGetValue(key, out var list))
            {
                cells[key] = list = new List<int>();
            }
            list.Add(u);
        }
        double worst = 0;
        var crushed = new HashSet<int>();
        foreach (var (key, list) in cells)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (!cells.TryGetValue((key.Item1 + dx, key.Item2 + dy), out var other))
                    {
                        continue;
                    }
                    foreach (int a in list)
                    {
                        foreach (int b in other)
                        {
                            if (a >= b)
                            {
                                continue;
                            }
                            double ddx =
                                (world.Position[a].X.Raw - world.Position[b].X.Raw) / 65536.0;
                            double ddy =
                                (world.Position[a].Y.Raw - world.Position[b].Y.Raw) / 65536.0;
                            double contact =
                                (
                                    rules.UnitTypes[world.UnitType[a]].RadiusMm
                                    + rules.UnitTypes[world.UnitType[b]].RadiusMm
                                ) / 1000.0;
                            double d = System.Math.Sqrt(ddx * ddx + ddy * ddy);
                            double share = (contact - d) / contact;
                            worst = System.Math.Max(worst, share);
                            if (share > 0.25)
                            {
                                crushed.Add(a);
                                crushed.Add(b);
                            }
                        }
                    }
                }
            }
        }
        return (worst, crushed.Count / (double)world.Units.AliveCount);
    }
}
