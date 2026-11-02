using GodGame.Sim.Math;

namespace GodGame.Sim.Tests;

[TestFixture, Category("Fast")]
public class LethalDistanceTests
{
    private const int Drop = 4_000;

    // Dijkstra over the same 8-neighbour chamfer weights, capped: slow, obviously right.
    private static int[] Reference(Terrain t, LethalDistance field)
    {
        int orth = t.CellSizeMm,
            diag = (int)((long)t.CellSizeMm * 1414 / 1000);
        var dist = new int[t.CellCount];
        var queue = new PriorityQueue<int, int>();
        for (int y = 0; y < t.Rows; y++)
        {
            for (int x = 0; x < t.Columns; x++)
            {
                int i = t.Index(x, y);
                dist[i] = field.IsLethal(x, y) ? 0 : int.MaxValue;
                if (dist[i] == 0)
                {
                    queue.Enqueue(i, 0);
                }
            }
        }
        while (queue.TryDequeue(out int cell, out int d))
        {
            if (d != dist[cell])
            {
                continue;
            }
            int cx = cell % t.Columns,
                cy = cell / t.Columns;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if ((dx | dy) == 0 || !t.InBounds(cx + dx, cy + dy))
                    {
                        continue;
                    }
                    int n = t.Index(cx + dx, cy + dy);
                    int nd = d + (dx != 0 && dy != 0 ? diag : orth);
                    if (nd < dist[n])
                    {
                        dist[n] = nd;
                        queue.Enqueue(n, nd);
                    }
                }
            }
        }
        return dist.Select(d => System.Math.Min(d, LethalDistance.MaxDistanceMm)).ToArray();
    }

    [Test]
    public void TheMapEdgeAndCliffTopsAreLethal_TheFootOfACliffIsNot()
    {
        // A 6 m step down going east at x = 30.
        var t = new Terrain(TerrainTests.Map((x, y) => x < 30 ? 6_000 : 0, 60, 40));
        var field = new LethalDistance(t, Drop);

        Assert.That(
            field.IsLethal(0, 20) && field.IsLethal(59, 20) && field.IsLethal(30, 0),
            Is.True,
            "edges"
        );
        Assert.That(field.IsLethal(29, 20), Is.True, "the cliff top");
        Assert.That(field.IsLethal(30, 20), Is.False, "the foot of the cliff");
        Assert.That(field.DistanceMm(25, 20), Is.EqualTo(4 * 1_500));
        Assert.That(
            field.DistanceMm(31, 20),
            Is.EqualTo(1_500 * 2),
            "two cells to the cliff top above"
        );
        Assert.That(
            new LethalDistance(t, 7_000).IsLethal(29, 20),
            Is.False,
            "a 6 m drop under a 7 m threshold"
        );
    }

    [Test]
    public void Distances_MatchShortestChamferPaths_CappedAt50m()
    {
        var rng = new Pcg32(51, 1);
        var heights = Enumerable
            .Range(0, 90 * 70)
            .Select(_ => rng.NextInt(0, 100) < 3 ? 9_000 : 0)
            .ToArray();
        var t = new Terrain(TerrainTests.Map((x, y) => heights[y * 90 + x], 90, 70));
        var field = new LethalDistance(t, Drop);

        Assert.That(field.Distances.ToArray(), Is.EqualTo(Reference(t, field)));
        var open = new Terrain(TerrainTests.Map((x, y) => 0, 120, 120));
        Assert.That(
            new LethalDistance(open, Drop).DistanceMm(60, 60),
            Is.EqualTo(LethalDistance.MaxDistanceMm)
        );
    }

    [Test]
    public void PartialRebuildsAfterEdits_MatchAFullBuild()
    {
        var rng = new Pcg32(52, 2);
        var t = new Terrain(TerrainTests.Map((x, y) => 0, 140, 100));
        var partial = new LethalDistance(t, Drop);

        for (int edit = 0; edit < 40; edit++)
        {
            int x0 = rng.NextInt(0, 140),
                y0 = rng.NextInt(0, 100);
            int x1 = System.Math.Min(139, x0 + rng.NextInt(0, 4)),
                y1 = System.Math.Min(99, y0 + rng.NextInt(0, 4));
            int h = rng.NextInt(0, 2) == 0 ? 0 : rng.NextInt(1_000, 12_000);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    t.SetHeight(x, y, h);
                }
            }
            partial.Rebuild(t.DirtyAround(x0, y0, x1, y1));
            Assert.That(
                partial.Distances.ToArray(),
                Is.EqualTo(new LethalDistance(t, Drop).Distances.ToArray()),
                $"after edit {edit}"
            );
        }
    }

    [Test]
    public void TheBattleNavigationBuildsIt_FromTheRulesDrop()
    {
        var map = TestBattles.Map((x, y) => 0, 40, 40);
        var navigation = new Navigation(map, RepoData.Rules(), new SerialRunner());

        Assert.That(
            navigation.Lethal.LethalDropMm,
            Is.EqualTo(RepoData.Rules().Hazards.LethalDropMm)
        );
        Assert.That(navigation.Lethal.DistanceMm(20, 20), Is.EqualTo(19 * 1_500));
    }
}
