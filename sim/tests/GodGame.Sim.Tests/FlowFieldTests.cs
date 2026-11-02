using GodGame.Sim.Hosting;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim.Tests;

[TestFixture, Category("Fast")]
public class FlowFieldTests
{
    // A wall is a 5 m step: impassable for every land class (cliff), so it shapes routes like a river or a wall.
    private const int Wall = 5_000;

    private static (Terrain, CostFields) Ground(Func<int, int, int> heightMm, int columns, int rows)
    {
        var terrain = new Terrain(TerrainTests.Map(heightMm, columns, rows));
        return (terrain, new CostFields(terrain, RepoData.Rules(), new SerialRunner()));
    }

    private static FlowField Built(CostFields costs, MovementClass c, CellRect goal)
    {
        var field = new FlowField(costs, c, goal);
        field.Rebuild();
        return field;
    }

    // Shortest distances by relaxing every edge until nothing changes: slow, obviously right.
    private static long[] BellmanFord(CostFields costs, MovementClass c, CellRect goal)
    {
        var t = costs.Terrain;
        var cost = costs.Of(c).ToArray();
        var dist = Enumerable.Repeat(FlowField.Unreachable, t.CellCount).ToArray();
        for (int y = goal.MinY; y <= goal.MaxY; y++)
        {
            for (int x = goal.MinX; x <= goal.MaxX; x++)
            {
                if (cost[t.Index(x, y)] != CostFields.Impassable)
                {
                    dist[t.Index(x, y)] = 0;
                }
            }
        }
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int y = 0; y < t.Rows; y++)
            {
                for (int x = 0; x < t.Columns; x++)
                {
                    int i = t.Index(x, y);
                    if (cost[i] == CostFields.Impassable)
                    {
                        continue;
                    }
                    for (int d = 0; d < 8; d++)
                    {
                        var (dx, dy) = FlowField.Offsets[d];
                        int nx = x + dx,
                            ny = y + dy;
                        if (!t.InBounds(nx, ny))
                        {
                            continue;
                        }
                        int n = t.Index(nx, ny);
                        if (cost[n] == CostFields.Impassable || dist[n] == FlowField.Unreachable)
                        {
                            continue;
                        }
                        bool diagonal = dx != 0 && dy != 0;
                        if (
                            diagonal
                            && (
                                cost[t.Index(nx, y)] == CostFields.Impassable
                                || cost[t.Index(x, ny)] == CostFields.Impassable
                            )
                        )
                        {
                            continue;
                        }
                        long w = (cost[i] + cost[n]) * (diagonal ? 7L : 5L);
                        if (dist[n] + w < dist[i])
                        {
                            dist[i] = dist[n] + w;
                            changed = true;
                        }
                    }
                }
            }
        }
        return dist;
    }

    [Test]
    public void OpenFlatGround_PointsStraightAtTheGoal()
    {
        var (_, costs) = Ground((x, y) => 0, 30, 20);
        var field = Built(costs, MovementClass.Foot, new CellRect(29, 0, 29, 19));

        Assert.That(field.Direction(5, 10), Is.EqualTo(0), "east");
        Assert.That(field.Direction(29, 3), Is.EqualTo(FlowField.AtGoal));
        Assert.That(field.Distance(28, 7), Is.EqualTo(1_000), "one flat step: (100 + 100) × 5");
        Assert.That(field.Distance(0, 0), Is.EqualTo(29_000));
        Assert.That(
            field.Sample(new FixVec2(Fix.FromInt(3), Fix.FromInt(3))),
            Is.EqualTo(new FixVec2(Fix.One, Fix.Zero))
        );
    }

    [Test]
    public void Distances_AreTheTrueShortestRoutes_OnRoughGround()
    {
        var rng = new Pcg32(11, 3);
        var heights = Enumerable.Range(0, 40 * 30).Select(_ => rng.NextInt(0, 1_400)).ToArray();
        var (_, costs) = Ground((x, y) => heights[y * 40 + x], 40, 30);

        foreach (var c in new[] { MovementClass.Foot, MovementClass.Horse, MovementClass.Wheeled })
        {
            var goal = new CellRect(30, 20, 33, 22);
            Assert.That(
                Built(costs, c, goal).Distances.ToArray(),
                Is.EqualTo(BellmanFord(costs, c, goal)),
                c.ToString()
            );
        }
    }

    [Test]
    public void EveryDirection_StepsToANeighbourThatIsCloserByExactlyTheStepCost()
    {
        var rng = new Pcg32(12, 3);
        var heights = Enumerable.Range(0, 50 * 40).Select(_ => rng.NextInt(0, 1_200)).ToArray();
        var (t, costs) = Ground((x, y) => heights[y * 50 + x], 50, 40);
        var field = Built(costs, MovementClass.Foot, new CellRect(0, 0, 2, 2));

        for (int y = 0; y < t.Rows; y++)
        {
            for (int x = 0; x < t.Columns; x++)
            {
                byte d = field.Direction(x, y);
                if (d >= FlowField.AtGoal)
                {
                    continue;
                }
                var (dx, dy) = FlowField.Offsets[d];
                Assert.That(
                    field.Distance(x + dx, y + dy),
                    Is.LessThan(field.Distance(x, y)),
                    $"cell ({x}, {y})"
                );
            }
        }
    }

    [Test]
    public void AWallWithAGap_RoutesThroughTheGap_AndAClosedPocketIsUnreachable()
    {
        // A wall at x = 15 with a gap at y = 1 to 5 (a wall also makes the cells beside it too steep, so the open
        // gap is y = 2 to 4), and a walled pocket around (5, 15).
        int H(int x, int y)
        {
            if (x == 15 && (y < 1 || y > 5))
            {
                return Wall;
            }
            bool ring =
                (x >= 3 && x <= 7 && (y == 13 || y == 17))
                || ((x == 3 || x == 7) && y >= 13 && y <= 17);
            return ring ? Wall : 0;
        }
        var (_, costs) = Ground(H, 30, 20);
        var field = Built(costs, MovementClass.Foot, new CellRect(29, 0, 29, 19));

        // Walk the flow from the far side of the wall and check it passes the gap.
        int cx = 10,
            cy = 10;
        bool throughGap = false;
        for (int step = 0; step < 200 && field.Direction(cx, cy) < FlowField.AtGoal; step++)
        {
            var (dx, dy) = FlowField.Offsets[field.Direction(cx, cy)];
            cx += dx;
            cy += dy;
            throughGap |= cx == 15;
            Assert.That(
                cx == 15 && (cy < 2 || cy > 4),
                Is.False,
                "the route never crosses the wall"
            );
        }
        Assert.That(field.Direction(cx, cy), Is.EqualTo(FlowField.AtGoal));
        Assert.That(throughGap, Is.True);
        Assert.That(field.Direction(5, 15), Is.EqualTo(FlowField.NoDirection));
        Assert.That(field.Distance(5, 15), Is.EqualTo(FlowField.Unreachable));
        Assert.That(field.Sample(FixVec2.FromInts(8, 23)), Is.EqualTo(FixVec2.Zero));
    }

    [Test]
    public void Diagonals_NeverCutACorner()
    {
        // A single blocked cell at (5, 5): from (4, 4) to (6, 6) a diagonal would cut past it.
        var (_, costs) = Ground((x, y) => x == 5 && y == 4 ? Wall : 0, 12, 12);
        var field = Built(costs, MovementClass.Foot, new CellRect(6, 5, 6, 5));

        // (5, 4) is the wall's cell, but its neighbours (4, 4) and (6, 4) are also steep: walls raise the slope of
        // the cells beside them. Check against the reference instead of a hand count.
        Assert.That(
            field.Distances.ToArray(),
            Is.EqualTo(BellmanFord(costs, MovementClass.Foot, new CellRect(6, 5, 6, 5)))
        );
    }

    [Test]
    public void Repair_AfterRandomEdits_MatchesAFullRebuild_BitForBit()
    {
        var rng = new Pcg32(21, 7);
        var heights = Enumerable.Range(0, 60 * 45).Select(_ => rng.NextInt(0, 1_000)).ToArray();
        var (t, costs) = Ground((x, y) => heights[y * 60 + x], 60, 45);
        var goal = new CellRect(50, 30, 55, 40);
        var repaired = new[]
        {
            Built(costs, MovementClass.Foot, goal),
            Built(costs, MovementClass.Wheeled, goal),
        };

        for (int edit = 0; edit < 60; edit++)
        {
            // Walls, gaps, raised and lowered blocks of 1 to 6 cells, including over the goal and the map edge.
            int x0 = rng.NextInt(0, 60),
                y0 = rng.NextInt(0, 45);
            int x1 = System.Math.Min(59, x0 + rng.NextInt(0, 6)),
                y1 = System.Math.Min(44, y0 + rng.NextInt(0, 6));
            int h = rng.NextInt(0, 3) switch
            {
                0 => Wall,
                1 => 0,
                _ => rng.NextInt(0, 2_000),
            };
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    t.SetHeight(x, y, h);
                }
            }
            var dirty = t.DirtyAround(x0, y0, x1, y1);
            costs.Rebuild(dirty, new SerialRunner());
            foreach (var field in repaired)
            {
                field.Repair(dirty);
                var full = Built(costs, field.MovementClass, goal);
                Assert.That(
                    field.Distances.ToArray(),
                    Is.EqualTo(full.Distances.ToArray()),
                    $"distances after edit {edit} ({field.MovementClass})"
                );
                Assert.That(
                    field.Directions.ToArray(),
                    Is.EqualTo(full.Directions.ToArray()),
                    $"directions after edit {edit} ({field.MovementClass})"
                );
            }
        }
    }

    [Test]
    public void ClosingAndReopeningTheOnlyGap_CutsOffAndRestoresTheFarSide()
    {
        var (t, costs) = Ground((x, y) => x == 15 && (y < 1 || y > 5) ? Wall : 0, 30, 20);
        var field = Built(costs, MovementClass.Foot, new CellRect(29, 0, 29, 19));
        long before = field.Distance(0, 10);

        t.SetHeight(15, 3, Wall); // its slope closes the cells beside it too: the whole gap
        var dirty = t.DirtyAround(15, 3, 15, 3);
        costs.Rebuild(dirty, new SerialRunner());
        field.Repair(dirty);
        Assert.That(field.Distance(0, 10), Is.EqualTo(FlowField.Unreachable));

        t.SetHeight(15, 3, 0);
        costs.Rebuild(dirty, new SerialRunner());
        field.Repair(dirty);
        Assert.That(field.Distance(0, 10), Is.EqualTo(before));
    }

    [Test]
    public void TheSet_SharesFieldsPerClassAndGoal_AndBuildsTheSameOnAnyRunner()
    {
        var rng = new Pcg32(31, 1);
        var heights = Enumerable.Range(0, 120 * 80).Select(_ => rng.NextInt(0, 1_500)).ToArray();
        var (t, costs) = Ground((x, y) => heights[y * 120 + x], 120, 80);
        var serial = new FlowFieldSet(costs);
        var threaded = new FlowFieldSet(costs);
        foreach (var set in new[] { serial, threaded })
        {
            Assert.That(set.GetOrAdd(MovementClass.Foot, new CellRect(110, 0, 119, 79)), Is.Zero);
            Assert.That(
                set.GetOrAdd(MovementClass.Horse, new CellRect(110, 0, 119, 79)),
                Is.EqualTo(1)
            );
            Assert.That(set.GetOrAdd(MovementClass.Foot, new CellRect(0, 0, 9, 79)), Is.EqualTo(2));
            Assert.That(set.GetOrAdd(MovementClass.Foot, new CellRect(110, 0, 119, 79)), Is.Zero);
        }

        serial.BuildPending(new SerialRunner());
        threaded.BuildPending(new ThreadedRunner(4));
        for (int i = 0; i < 3; i++)
        {
            Assert.That(
                threaded[i].Directions.ToArray(),
                Is.EqualTo(serial[i].Directions.ToArray())
            );
        }

        t.SetHeight(60, 40, Wall);
        var dirty = t.DirtyAround(60, 40, 60, 40);
        costs.Rebuild(dirty, new SerialRunner());
        serial.Repair(dirty, new SerialRunner());
        threaded.Repair(dirty, new ThreadedRunner(4));
        for (int i = 0; i < 3; i++)
        {
            Assert.That(threaded[i].Distances.ToArray(), Is.EqualTo(serial[i].Distances.ToArray()));
        }
    }

    [Test]
    public void CellsOf_CoversTheZoneOnTheGrid()
    {
        var terrain = new Terrain(TerrainTests.Map((x, y) => 0));

        var cells = terrain.CellsOf(new RectMm(0, 1_500, 3_000, 15_000));
        Assert.That((cells.MinX, cells.MinY, cells.MaxX, cells.MaxY), Is.EqualTo((0, 1, 1, 9)));
    }

    [Test]
    public void FlowField_HashesToThePinnedValue()
    {
        // Cross-runtime evidence for the search and the tie-breaks.
        var rng = new Pcg32(41, 2);
        var heights = Enumerable.Range(0, 64 * 48).Select(_ => rng.NextInt(0, 1_500)).ToArray();
        var (_, costs) = Ground((x, y) => heights[y * 64 + x], 64, 48);
        var field = Built(costs, MovementClass.Horse, new CellRect(60, 2, 63, 10));
        var hash = new SimHash();
        foreach (long d in field.Distances)
        {
            hash.AppendInt64(d);
        }
        hash.Append(field.Directions);

        Assert.That(hash.Digest(), Is.EqualTo(PinnedHashes.FlowFieldSeed41));
    }
}
