using System.Buffers.Binary;
using GodGame.Sim.Hosting;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim.Tests;

[TestFixture, Category("Fast")]
public class TerrainTests
{
    // 20 × 10 cells of 1.5 m; heights given per cell in mm, row-major from the south-west.
    internal static MapData Map(Func<int, int, int> heightMm, int columns = 20, int rows = 10)
    {
        string json =
            "{\"version\":0,\"id\":\"t\",\"widthMm\":"
            + columns * 1500
            + ",\"depthMm\":"
            + rows * 1500
            + ",\"cellSizeMm\":1500,\"deployment\":[]}";
        var bytes = new byte[columns * rows * 4];
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < columns; x++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(
                    bytes.AsSpan((y * columns + x) * 4),
                    heightMm(x, y)
                );
            }
        }
        return MapData.Load(json, bytes);
    }

    [Test]
    public void CellOf_ClampsToTheMap()
    {
        var terrain = new Terrain(Map((x, y) => 0));

        terrain.CellOf(
            new FixVec2(Fix.FromRatio(3, 2), Fix.FromRatio(299, 100)),
            out int x,
            out int y
        );
        Assert.That((x, y), Is.EqualTo((1, 1)));
        terrain.CellOf(FixVec2.FromInts(-5, 9999), out x, out y);
        Assert.That((x, y), Is.EqualTo((0, 9)));
    }

    [Test]
    public void HeightAt_IsExactAtCellCentres_AndInterpolatesBetween()
    {
        // A ramp east: 150 mm more per cell.
        var terrain = new Terrain(Map((x, y) => 150 * x));

        Assert.That(
            terrain.HeightMmAt(new FixVec2(Fix.FromRatio(3 * 1500 + 750, 1000), Fix.One)),
            Is.EqualTo(450)
        );
        Assert.That(
            terrain.HeightMmAt(new FixVec2(Fix.FromRatio(3 * 1500 + 1500, 1000), Fix.One)),
            Is.EqualTo(525)
        );
        Assert.That(
            terrain.HeightMmAt(FixVec2.FromInts(-10, -10)),
            Is.EqualTo(0),
            "off the map: the edge height"
        );
    }

    [Test]
    public void Slope_IsTheSteepestNeighbourStep()
    {
        var terrain = new Terrain(Map((x, y) => x < 10 ? 150 * x : 150 * 9 + 3000));

        Assert.That(terrain.SlopePermille(4, 4), Is.EqualTo(100), "150 mm over 1.5 m");
        Assert.That(
            terrain.SlopePermille(9, 4),
            Is.EqualTo(2000),
            "the cliff at x = 10 is 3 m over 1.5 m"
        );
        Assert.That(terrain.SlopePermille(0, 0), Is.EqualTo(100), "edges ignore the outside");
        Assert.That(new Terrain(Map((x, y) => 0)).SlopePermille(5, 5), Is.Zero);
    }
}

[TestFixture, Category("Fast")]
public class CostFieldTests
{
    private static Ruleset Rules() => RepoData.Rules();

    [Test]
    public void FlatGround_CostsTheBaseForLandClasses_AndWaterIsImpassable()
    {
        var costs = new CostFields(
            new Terrain(TerrainTests.Map((x, y) => 0)),
            Rules(),
            new SerialRunner()
        );

        foreach (
            var land in new[] { MovementClass.Foot, MovementClass.Horse, MovementClass.Wheeled }
        )
        {
            Assert.That(
                costs.Of(land).ToArray(),
                Is.All.EqualTo(Rules().Movement(land).BaseCost),
                land.ToString()
            );
        }
        Assert.That(costs.Of(MovementClass.Water).ToArray(), Is.All.EqualTo(CostFields.Impassable));
    }

    [Test]
    public void SlopeRaisesCost_AndTooSteepIsImpassable_PerClass()
    {
        var rules = Rules();
        var foot = rules.Movement(MovementClass.Foot);
        var wheeled = rules.Movement(MovementClass.Wheeled);

        Assert.That(CostFields.CellCost(foot, 500), Is.EqualTo(foot.BaseCost + foot.SlopeCost / 2));
        Assert.That(
            CostFields.CellCost(wheeled, wheeled.MaxSlopePermille),
            Is.Not.EqualTo(CostFields.Impassable)
        );
        Assert.That(
            CostFields.CellCost(wheeled, wheeled.MaxSlopePermille + 1),
            Is.EqualTo(CostFields.Impassable)
        );
        Assert.That(
            CostFields.CellCost(wheeled, 200),
            Is.GreaterThan(CostFields.CellCost(foot, 200)),
            "siege cares about slope far more than infantry (tech notes, section 3)"
        );
    }

    [Test]
    public void PartialRebuildAfterAnEdit_MatchesAFullRebuild()
    {
        var rules = Rules();
        var terrain = new Terrain(TerrainTests.Map((x, y) => 100 * ((x * 7 + y * 3) % 5)));
        var partial = new CostFields(terrain, rules, new SerialRunner());

        terrain.SetHeight(6, 4, 2_000); // a mound one cell wide
        partial.Rebuild(terrain.DirtyAround(6, 4, 6, 4), new SerialRunner());
        var full = new CostFields(terrain, rules, new SerialRunner());

        foreach (MovementClass c in Enum.GetValues<MovementClass>())
        {
            Assert.That(partial.Of(c).ToArray(), Is.EqualTo(full.Of(c).ToArray()), c.ToString());
        }
    }

    [Test]
    public void Rebuild_IsTheSameOnAnyRunner()
    {
        var rules = Rules();
        var rng = new Pcg32(4, 4);
        var heights = Enumerable.Range(0, 200 * 150).Select(_ => rng.NextInt(0, 3_000)).ToArray();
        var terrain = new Terrain(TerrainTests.Map((x, y) => heights[y * 200 + x], 200, 150));
        var serial = new CostFields(terrain, rules, new SerialRunner());
        var threaded = new CostFields(terrain, rules, new SerialRunner());

        threaded.RebuildAll(new ThreadedRunner(8));

        foreach (MovementClass c in Enum.GetValues<MovementClass>())
        {
            Assert.That(threaded.Of(c).ToArray(), Is.EqualTo(serial.Of(c).ToArray()), c.ToString());
        }
    }

    [Test]
    public void ThreadedPartialRebuilds_AtEdgesAndOddOffsets_MatchAFullRebuild()
    {
        var rules = Rules();
        var rng = new Pcg32(5, 5);
        var heights = Enumerable.Range(0, 120 * 90).Select(_ => rng.NextInt(0, 2_500)).ToArray();
        var terrain = new Terrain(TerrainTests.Map((x, y) => heights[y * 120 + x], 120, 90));
        var partial = new CostFields(terrain, rules, new ThreadedRunner(4));

        // Corner, edge and an interior block whose height (37 rows) is not a multiple of the 16-row chunk.
        terrain.SetHeight(0, 0, 9_000);
        partial.Rebuild(terrain.DirtyAround(0, 0, 0, 0), new ThreadedRunner(4));
        terrain.SetHeight(119, 45, -9_000);
        partial.Rebuild(terrain.DirtyAround(119, 45, 119, 45), new ThreadedRunner(4));
        for (int y = 21; y <= 57; y++)
        {
            terrain.SetHeight(60, y, 4_000 + y);
        }
        partial.Rebuild(terrain.DirtyAround(60, 21, 60, 57), new ThreadedRunner(4));
        var full = new CostFields(terrain, rules, new SerialRunner());

        foreach (MovementClass c in Enum.GetValues<MovementClass>())
        {
            Assert.That(partial.Of(c).ToArray(), Is.EqualTo(full.Of(c).ToArray()), c.ToString());
        }
    }

    [Test]
    public void DataLimits_TenKilometreHeightsOnTenCentimetreCells()
    {
        string json =
            "{\"version\":0,\"id\":\"tall\",\"widthMm\":1000,\"depthMm\":1000,\"cellSizeMm\":100,\"deployment\":[]}";
        var bytes = new byte[10 * 10 * 4];
        for (int i = 0; i < 100; i++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(
                bytes.AsSpan(i * 4),
                i % 2 == 0 ? 10_000_000 : -10_000_000
            );
        }
        var terrain = new Terrain(MapData.Load(json, bytes));
        var costs = new CostFields(terrain, Rules(), new SerialRunner());

        Assert.That(terrain.SlopePermille(3, 0), Is.EqualTo(200_000_000));
        Assert.That(costs.Cost(MovementClass.Foot, 3, 0), Is.EqualTo(CostFields.Impassable));
        Assert.That(terrain.HeightMmAt(FixVec2.FromInts(-1_000, -1_000)), Is.EqualTo(10_000_000));
        Assert.That(terrain.HeightMmAt(FixVec2.FromInts(30_000, 0)), Is.EqualTo(-10_000_000));
    }

    [Test]
    public void ReadingStaleFields_FailsInDebug_AndDirtyAroundRejectsBadInput()
    {
        var terrain = new Terrain(TerrainTests.Map((x, y) => 0));
        var costs = new CostFields(terrain, Rules(), new SerialRunner());

        Assert.Throws<ArgumentOutOfRangeException>(() => terrain.DirtyAround(5, 5, 4, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => terrain.DirtyAround(0, 0, 20, 0));
        terrain.SetHeight(3, 3, 500);
        Assert.That(costs.BuiltFromVersion, Is.Not.EqualTo(terrain.Version));
        costs.Rebuild(terrain.DirtyAround(3, 3, 3, 3), new SerialRunner());
        Assert.That(costs.BuiltFromVersion, Is.EqualTo(terrain.Version));
    }

    [Test]
    public void CostArrays_HashToThePinnedValue()
    {
        // Cross-runtime evidence for the slope and cost arithmetic, like the other pins.
        var rng = new Pcg32(6, 6);
        var heights = Enumerable
            .Range(0, 64 * 48)
            .Select(_ => rng.NextInt(-3_000, 3_000))
            .ToArray();
        var costs = new CostFields(
            new Terrain(TerrainTests.Map((x, y) => heights[y * 64 + x], 64, 48)),
            Rules(),
            new SerialRunner()
        );
        var hash = new SimHash();
        foreach (MovementClass c in Enum.GetValues<MovementClass>())
        {
            foreach (ushort cost in costs.Of(c))
            {
                hash.AppendInt32(cost);
            }
        }

        Assert.That(hash.Digest(), Is.EqualTo(PinnedHashes.CostFieldsSeed6));
    }

    [Test]
    public void RepoRules_NeedBothFiles_AndEveryClass()
    {
        Assert.Throws<GodGame.Sim.Rules.Json.DataFormatException>(() =>
            RulesetLoader.Load(
                new[]
                {
                    KeyValuePair.Create(
                        "unit-types.json",
                        RepoData.Read("data", "rules", "unit-types.json")
                    ),
                }
            )
        );
        static string Class(string id) =>
            "{\"id\":\""
            + id
            + "\",\"baseCost\":100,\"slopeCost\":1,\"maxSlopePermille\":1,"
            + "\"water\":{\"speed\":[[0,1000]],\"maxEnterDepthMm\":1,\"sweptDepthMm\":1,"
            + "\"sweptDepthSpeed\":1,\"sweptMinDepthMm\":1,\"carriedPermille\":1000,\"paddleMmPerS\":0,"
            + "\"drowningTicks\":1,\"regainPermille\":1000},\"mudSpeed\":[[0,1000]]}";
        string ThreeClasses =
            "{\"version\":0,\"fieldCostRefreshTicks\":20,\"classes\":["
            + Class("foot")
            + ","
            + Class("horse")
            + ","
            + Class("wheeled")
            + "]}";
        var error = Assert.Throws<GodGame.Sim.Rules.Json.DataFormatException>(() =>
            RulesetLoader.Load(
                new[]
                {
                    KeyValuePair.Create("movement-classes.json", ThreeClasses),
                    KeyValuePair.Create(
                        "hazards.json",
                        RepoData.Read("data", "rules", "hazards.json")
                    ),
                    KeyValuePair.Create(
                        "routes.json",
                        RepoData.Read("data", "rules", "routes.json")
                    ),
                    KeyValuePair.Create(
                        "fields.json",
                        RepoData.Read("data", "rules", "fields.json")
                    ),
                    KeyValuePair.Create(
                        "unit-types.json",
                        RepoData.Read("data", "rules", "unit-types.json")
                    ),
                }
            )
        );
        Assert.That(error!.Message, Does.Contain("every movement class"));
    }
}
