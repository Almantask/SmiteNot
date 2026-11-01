using System.Buffers.Binary;
using GodGame.Sim.Rules;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim.Tests;

internal static class RepoData
{
    public static string Root
    {
        get
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "data", "rules")))
            {
                dir = dir.Parent;
            }
            return dir?.FullName
                ?? throw new DirectoryNotFoundException("No data/rules above the test directory.");
        }
    }

    public static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { Root }.Concat(parts).ToArray()));

    public static Ruleset Rules() =>
        RulesetLoader.Load(
            new[]
            {
                KeyValuePair.Create("unit-types.json", Read("data", "rules", "unit-types.json")),
            }
        );
}

[TestFixture, Category("Fast")]
public class JsonReaderTests
{
    [Test]
    public void Parses_ObjectsArraysStringsIntegersAndLiterals()
    {
        var value = JsonReader.Parse(
            "{ \"a\": [1, -2, 9223372036854775807, -9223372036854775808], \"b\": \"x\\n\\u00e9\", \"c\": true, \"d\": null }"
        );

        Assert.That(
            value["a"].Items.Select(v => v.AsInteger),
            Is.EqualTo(new[] { 1L, -2L, long.MaxValue, long.MinValue })
        );
        Assert.That(value.Str("b"), Is.EqualTo("x\n\u00e9"));
        Assert.That(value["c"].AsBool, Is.True);
        Assert.That(value["d"].Kind, Is.EqualTo(JsonKind.Null));
        Assert.That(value["a"].Items[2].Path, Is.EqualTo("json.a[2]"));
    }

    [TestCase("{\"a\": 1.5}", "integers")]
    [TestCase("{\"a\": 1e3}", "integers")]
    [TestCase("{\"a\": 1, \"a\": 2}", "duplicate")]
    [TestCase("{\"a\": 1,}", "expected a key")]
    [TestCase("[1, 2] x", "after the value")]
    [TestCase("{\"a\": 01}", "leading zero")]
    [TestCase("{\"a\": 9223372036854775808}", "out of range")]
    [TestCase("\"abc", "unterminated")]
    public void Rejects_WhatRulesDataForbids(string json, string why)
    {
        var error = Assert.Throws<DataFormatException>(() => JsonReader.Parse(json, "test.json"));

        Assert.That(error!.Message, Does.Contain(why).And.StartWith("test.json("));
    }

    [Test]
    public void Rejects_UnpairedSurrogates_RawOrEscaped()
    {
        Assert.Throws<DataFormatException>(() => JsonReader.Parse("{\"a\": \"x\\uD800\"}"));
        Assert.Throws<DataFormatException>(() => JsonReader.Parse("{\"a\": \"x\uDBFF\"}"));
        Assert.That(
            JsonReader.Parse("{\"a\": \"\\uD83D\\uDE00\"}").Str("a"),
            Is.EqualTo("\U0001F600")
        );
    }

    [Test]
    public void Canonical_IsAscii_DropsComments_AndIgnoresCrLfAndBom()
    {
        string unix = "{\"$comment\": \"one\", \"a\": \"\u00e9\"}";
        string windows = "\uFEFF{\r\n  \"a\": \"\\u00e9\",\r\n  \"$comment\": \"two\"\r\n}";

        string canonical = JsonCanonical.ToText(JsonReader.Parse(unix));

        Assert.That(canonical, Is.EqualTo("{\"a\":\"\\u00e9\"}"));
        Assert.That(JsonCanonical.ToText(JsonReader.Parse(windows)), Is.EqualTo(canonical));
        Assert.That(JsonCanonical.ToUtf8(JsonReader.Parse(unix)).All(b => b < 0x80), Is.True);
    }

    [Test]
    public void Canonical_IgnoresWhitespaceAndKeyOrder_ButNotMeaning()
    {
        byte[] a = JsonCanonical.ToUtf8(JsonReader.Parse("{ \"b\": [1, 2], \"a\": \"x\" }"));
        byte[] b = JsonCanonical.ToUtf8(JsonReader.Parse("{\"a\":\"x\",\n  \"b\":[1,2]}"));
        byte[] c = JsonCanonical.ToUtf8(JsonReader.Parse("{\"a\":\"x\",\"b\":[2,1]}"));

        Assert.That(
            System.Text.Encoding.ASCII.GetString(a),
            Is.EqualTo("{\"a\":\"x\",\"b\":[1,2]}")
        );
        Assert.That(b, Is.EqualTo(a));
        Assert.That(c, Is.Not.EqualTo(a));
    }
}

[TestFixture, Category("Fast")]
public class RulesetHashTests
{
    private static ulong Hash(params (string Name, string Json)[] files) =>
        RulesetHash.Of(
            files.Select(f => KeyValuePair.Create(f.Name, JsonReader.Parse(f.Json))).ToList()
        );

    [Test]
    public void SameMeaning_SameHash_WhateverTheFormattingOrFileOrder()
    {
        ulong first = Hash(("a.json", "{\"x\":1,\"y\":2}"), ("b.json", "{}"));
        ulong second = Hash(("b.json", " { } "), ("a.json", "{ \"y\": 2, \"x\": 1 }"));

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void ChangedValueOrFileName_ChangesHash()
    {
        ulong baseline = Hash(("a.json", "{\"x\":1}"));

        Assert.That(Hash(("a.json", "{\"x\":2}")), Is.Not.EqualTo(baseline));
        Assert.That(Hash(("b.json", "{\"x\":1}")), Is.Not.EqualTo(baseline));
    }

    [Test]
    public void DuplicateFileNames_AreRejected()
    {
        Assert.Throws<DataFormatException>(() => Hash(("a.json", "{}"), ("a.json", "{}")));
    }
}

[TestFixture, Category("Fast")]
public class RulesetLoaderTests
{
    [Test]
    public void RepoRules_LoadTheFourBaseTypes()
    {
        var rules = RepoData.Rules();

        Assert.That(
            rules.UnitTypes.Select(t => t.Id),
            Is.EqualTo(new[] { "infantry", "cavalry", "ranged", "siege" })
        );
        Assert.That(rules.UnitType("cavalry").MovementClass, Is.EqualTo(MovementClass.Horse));
        Assert.That(rules.UnitType("siege").Index, Is.EqualTo(3));
        Assert.That(rules.HasPlaceholders, Is.True, "health and morale wait for spec pass S2");
    }

    [Test]
    public void RepoRules_HashIsPinned()
    {
        // Changing data/rules changes this on purpose: bump it together with the goldens (/golden-replay).
        Assert.That(RepoData.Rules().Hash, Is.EqualTo(PinnedHashes.RulesV0));
    }

    [TestCase(
        "{\"version\":0,\"unitTypes\":[{\"id\":\"a\",\"movementClass\":\"boat\",\"health\":1,\"morale\":1}]}",
        "movement class"
    )]
    [TestCase(
        "{\"version\":0,\"unitTypes\":[{\"id\":\"a\",\"movementClass\":\"foot\",\"health\":1,\"morale\":1,\"speed\":3}]}",
        "unknown \"speed\""
    )]
    [TestCase(
        "{\"version\":0,\"unitTypes\":[{\"id\":\"a\",\"movementClass\":\"foot\",\"health\":0,\"morale\":1}]}",
        "outside"
    )]
    [TestCase(
        "{\"version\":0,\"unitTypes\":[{\"id\":\"A\",\"movementClass\":\"foot\",\"health\":1,\"morale\":1}]}",
        "ids use only"
    )]
    [TestCase("{\"version\":1,\"unitTypes\":[]}", "version")]
    [TestCase("{\"version\":0,\"unitTypes\":[]}", "empty")]
    public void BadRules_AreRejectedWithTheirPath(string json, string why)
    {
        var error = Assert.Throws<DataFormatException>(() =>
            RulesetLoader.Load(new[] { KeyValuePair.Create("unit-types.json", json) })
        );

        Assert.That(error!.Message, Does.Contain(why));
    }

    [Test]
    public void UnknownRulesFiles_AreRejected()
    {
        Assert.Throws<DataFormatException>(() =>
            RulesetLoader.Load(new[] { KeyValuePair.Create("powers.json", "{}") })
        );
    }
}

[TestFixture, Category("Fast")]
public class MapAndPlanTests
{
    private const string SmallMap =
        "{\"version\":0,\"id\":\"small\",\"widthMm\":30000,\"depthMm\":15000,\"cellSizeMm\":1500,"
        + "\"deployment\":[{\"side\":\"defender\",\"minXMm\":20000,\"minYMm\":0,\"maxXMm\":30000,\"maxYMm\":15000},"
        + "{\"side\":\"attacker\",\"minXMm\":0,\"minYMm\":0,\"maxXMm\":10000,\"maxYMm\":15000}]}";

    private static string Plan(string army) =>
        "{\"version\":0,\"map\":\"small\",\"army\":[" + army + "],\"terrain\":[],\"casts\":[]}";

    private const string Regiment =
        "{\"id\":\"r1\",\"side\":\"attacker\",\"unitType\":\"infantry\",\"soldiers\":9,\"xMm\":5000,\"yMm\":7500,\"facingCentiDegrees\":0}";

    [Test]
    public void Map_LoadsSizeZonesAndHeight()
    {
        var height = new byte[20 * 10 * 4];
        BinaryPrimitives.WriteInt32LittleEndian(height.AsSpan(4 * 21), 1234); // column 1, row 1

        var map = MapData.Load(SmallMap, height);

        Assert.That((map.Columns, map.Rows), Is.EqualTo((20, 10)));
        Assert.That(map.HeightMm[21], Is.EqualTo(1234));
        Assert.That(
            map.DeploymentZones.Select(z => z.Side),
            Is.EqualTo(new[] { Side.Attacker, Side.Defender }),
            "sorted by side"
        );
        Assert.That(map.ZoneOf(Side.Defender).Area.MinX, Is.EqualTo(20000));
        Assert.That(MapData.Load(SmallMap, height).Hash, Is.EqualTo(map.Hash));
        Assert.That(
            MapData.Load(SmallMap).Hash,
            Is.Not.EqualTo(map.Hash),
            "the height layer is hashed"
        );
        Assert.Throws<DataFormatException>(() => MapData.Load(SmallMap, new byte[12]));
    }

    [Test]
    public void Plan_SortsRegimentsById_AndChecksZones()
    {
        var map = MapData.Load(SmallMap);
        var rules = RepoData.Rules();
        string r2 = Regiment.Replace("\"r1\"", "\"r0\"");

        var plan = BattlePlan.Load(Plan(Regiment + "," + r2), map, rules);
        var swapped = BattlePlan.Load(Plan(r2 + "," + Regiment), map, rules);

        Assert.That(plan.Army.Select(r => r.Id), Is.EqualTo(new[] { "r0", "r1" }));
        Assert.That(swapped.Army.Select(r => r.Id), Is.EqualTo(new[] { "r0", "r1" }));
        Assert.Throws<DataFormatException>(() =>
            BattlePlan.Load(Plan(Regiment.Replace("5000", "25000")), map, rules)
        );
        Assert.Throws<DataFormatException>(() =>
            BattlePlan.Load(Plan(Regiment + "," + Regiment), map, rules)
        );
        Assert.Throws<DataFormatException>(() =>
            BattlePlan.Load(Plan(Regiment.Replace("infantry", "dragons")), map, rules)
        );
        Assert.Throws<DataFormatException>(() =>
            BattlePlan.Load(Plan(Regiment).Replace("\"casts\":[]", "\"casts\":[{}]"), map, rules)
        );
    }

    [Test]
    public void Ids_AreLimitedToLowercaseAsciiDigitsAndDashes()
    {
        var map = MapData.Load(SmallMap);
        var rules = RepoData.Rules();

        Assert.Throws<DataFormatException>(() =>
            BattlePlan.Load(Plan(Regiment.Replace("\"r1\"", "\"R1\"")), map, rules)
        );
        Assert.Throws<DataFormatException>(() =>
            BattlePlan.Load(Plan(Regiment.Replace("\"r1\"", "\"r \u00e9\"")), map, rules)
        );
        Assert.Throws<DataFormatException>(() =>
            MapData.Load(SmallMap.Replace("\"small\"", "\"\""))
        );
    }

    [Test]
    public void Limits_MapSizeZonesHeightsAndSoldiers()
    {
        var map = MapData.Load(SmallMap);
        var rules = RepoData.Rules();
        string tooMany = string.Join(
            ",",
            Enumerable
                .Range(0, 11)
                .Select(i =>
                    Regiment
                        .Replace("\"r1\"", $"\"r{i:00}\"")
                        .Replace("\"soldiers\":9", "\"soldiers\":1000")
                )
        );
        var badHeight = new byte[20 * 10 * 4];
        BinaryPrimitives.WriteInt32LittleEndian(badHeight, 20_000_000);

        Assert.Throws<DataFormatException>(() =>
            MapData.Load(SmallMap.Replace("\"widthMm\":30000", "\"widthMm\":40000000"))
        );
        Assert.Throws<DataFormatException>(() =>
            MapData.Load(SmallMap.Replace("\"maxXMm\":10000", "\"maxXMm\":0"))
        );
        Assert.Throws<DataFormatException>(() =>
            MapData.Load(
                SmallMap
                    .Replace("\"cellSizeMm\":1500", "\"cellSizeMm\":100")
                    .Replace("\"widthMm\":30000", "\"widthMm\":29000000")
                    .Replace("\"depthMm\":15000", "\"depthMm\":15000000")
            )
        );
        Assert.Throws<DataFormatException>(() => MapData.Load(SmallMap, badHeight));
        Assert.Throws<DataFormatException>(() => BattlePlan.Load(Plan(tooMany), map, rules));
    }

    [Test]
    public void PlanHash_IgnoresArmyOrderAndComments()
    {
        var map = MapData.Load(SmallMap);
        var rules = RepoData.Rules();
        string r0 = Regiment.Replace("\"r1\"", "\"r0\"");

        var a = BattlePlan.Load(Plan(Regiment + "," + r0), map, rules);
        var b = BattlePlan.Load(
            Plan(r0 + "," + Regiment).Replace("{\"version\"", "{\"$comment\":\"x\",\"version\""),
            map,
            rules
        );

        Assert.That(b.Hash, Is.EqualTo(a.Hash));
        Assert.That(b.CanonicalJson, Is.EqualTo(a.CanonicalJson));
        Assert.That(
            a.CanonicalJson.IndexOf("\"r0\"", StringComparison.Ordinal),
            Is.LessThan(a.CanonicalJson.IndexOf("\"r1\"", StringComparison.Ordinal))
        );
    }

    [Test]
    public void Deploy_CentresASquareBlockOnThePlanPoint()
    {
        var map = MapData.Load(SmallMap);
        var rules = RepoData.Rules();
        var plan = BattlePlan.Load(Plan(Regiment), map, rules);

        var world = BattleSetup.Deploy(map, plan, rules);

        Assert.That(world.Units.AliveCount, Is.EqualTo(9));
        var xs = Enumerable.Range(0, 9).Select(u => world.Position[u].X.Raw).ToArray();
        var ys = Enumerable.Range(0, 9).Select(u => world.Position[u].Y.Raw).ToArray();
        Assert.That(xs.Average(), Is.EqualTo(5L << 16).Within(2));
        Assert.That(ys.Average(), Is.EqualTo(7.5 * 65536).Within(2));
        Assert.That(xs.Max() - xs.Min(), Is.EqualTo(3L << 16).Within(2), "three ranks 1.5 m apart");
        Assert.That(world.Health[0], Is.EqualTo(rules.UnitType("infantry").Health));
    }

    [Test]
    public void Deploy_TurnsTheBlockWithTheFacing()
    {
        var map = MapData.Load(SmallMap);
        var rules = RepoData.Rules();
        string pair = Regiment.Replace("\"soldiers\":9", "\"soldiers\":2");
        var east = BattleSetup.Deploy(map, BattlePlan.Load(Plan(pair), map, rules), rules);
        var north = BattleSetup.Deploy(
            map,
            BattlePlan.Load(
                Plan(pair.Replace("\"facingCentiDegrees\":0", "\"facingCentiDegrees\":9000")),
                map,
                rules
            ),
            rules
        );

        // Two soldiers stand side by side across the facing: along Y when facing east, along X when facing north.
        Assert.That(east.Position[0].X, Is.EqualTo(east.Position[1].X));
        Assert.That(north.Position[0].Y, Is.EqualTo(north.Position[1].Y));
    }

    [Test]
    public void RepoData_HashesArePinned()
    {
        // Pins the formats and deployment across runtimes; changing data/ or the formats updates these on purpose.
        var rules = RepoData.Rules();
        var map = MapData.Load(RepoData.Read("data", "maps", "m1-flat", "map.json"));
        var plan = BattlePlan.Load(
            RepoData.Read("data", "plans", "m1-ten-thousand.json"),
            map,
            rules
        );
        var hash = new GodGame.Sim.Math.SimHash();
        BattleSetup.Deploy(map, plan, rules).AppendTo(hash);

        Assert.Multiple(() =>
        {
            Assert.That(map.Hash, Is.EqualTo(PinnedHashes.MapM1Flat), "map");
            Assert.That(plan.Hash, Is.EqualTo(PinnedHashes.PlanM1TenThousand), "plan");
            Assert.That(
                hash.Digest(),
                Is.EqualTo(PinnedHashes.WorldM1TenThousandDeployed),
                "deployed world"
            );
        });
    }

    [Test]
    public void RepoMapAndTenThousandPlan_DeployDeterministically()
    {
        var rules = RepoData.Rules();
        var map = MapData.Load(RepoData.Read("data", "maps", "m1-flat", "map.json"));
        var plan = BattlePlan.Load(
            RepoData.Read("data", "plans", "m1-ten-thousand.json"),
            map,
            rules
        );

        var a = BattleSetup.Deploy(map, plan, rules);
        var b = BattleSetup.Deploy(map, plan, rules);
        var hashA = new GodGame.Sim.Math.SimHash();
        var hashB = new GodGame.Sim.Math.SimHash();
        a.AppendTo(hashA);
        b.AppendTo(hashB);

        Assert.That(a.Units.AliveCount, Is.EqualTo(10_000));
        Assert.That(a.Regiments.AliveCount, Is.EqualTo(100));
        Assert.That(hashB.Digest(), Is.EqualTo(hashA.Digest()));
    }
}
