using System.Buffers.Binary;
using System.Globalization;
using GodGame.Sim.Math;

namespace GodGame.Sim.Tests;

/// <summary>
/// Small battles for movement tests: a map from a height function with an attacker zone in the west and a defender
/// zone in the east, and a plan from (side, unit type, soldiers, x, y) tuples and optional (id, power, tick, x, y,
/// radius) casts. Lengths in metres unless named mm.
/// </summary>
internal static class TestBattles
{
    public const int CellMm = 1_500;

    /// <param name="zoneMetres">How deep each deployment zone reaches in from its edge.</param>
    /// <param name="extraJson">More map.json members, starting with a comma (water sources and sinks, soils).</param>
    public static MapData Map(
        Func<int, int, int> heightMm,
        int columns,
        int rows,
        int zoneMetres = 15,
        string id = "test",
        string extraJson = ""
    )
    {
        int width = columns * CellMm;
        int depth = rows * CellMm;
        string json = string.Format(
            CultureInfo.InvariantCulture,
            "{{\"version\":0,\"id\":\"{0}\",\"widthMm\":{1},\"depthMm\":{2},\"cellSizeMm\":{3},\"deployment\":["
                + "{{\"side\":\"attacker\",\"minXMm\":0,\"minYMm\":0,\"maxXMm\":{4},\"maxYMm\":{2}}},"
                + "{{\"side\":\"defender\",\"minXMm\":{5},\"minYMm\":0,\"maxXMm\":{1},\"maxYMm\":{2}}}]{6}}}",
            id,
            width,
            depth,
            CellMm,
            zoneMetres * 1000,
            width - zoneMetres * 1000,
            extraJson
        );
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

    public static Battle Battle(
        MapData map,
        IEnumerable<(string Side, string Type, int Soldiers, double X, double Y)> regiments,
        ulong seed = 1,
        IEnumerable<(string Id, string Power, int Tick, double X, double Y, double Radius)>? casts = null
    )
    {
        var rules = RepoData.Rules();
        var items = regiments.Select(
            (r, i) =>
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{{\"id\":\"r{0:D3}\",\"side\":\"{1}\",\"unitType\":\"{2}\",\"soldiers\":{3},\"xMm\":{4},\"yMm\":{5},\"facingCentiDegrees\":0}}",
                    i,
                    r.Side,
                    r.Type,
                    r.Soldiers,
                    (int)(r.X * 1000),
                    (int)(r.Y * 1000)
                )
        );
        var castItems = (casts ?? Enumerable.Empty<(string, string, int, double, double, double)>()).Select(
            c =>
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{{\"id\":\"{0}\",\"power\":\"{1}\",\"tick\":{2},\"xMm\":{3},\"yMm\":{4},\"radiusMm\":{5}}}",
                    c.Item1,
                    c.Item2,
                    c.Item3,
                    (int)(c.Item4 * 1000),
                    (int)(c.Item5 * 1000),
                    (int)(c.Item6 * 1000)
                )
        );
        string plan =
            "{\"version\":0,\"map\":\""
            + map.Id
            + "\",\"army\":["
            + string.Join(",", items)
            + "],\"terrain\":[],\"casts\":["
            + string.Join(",", castItems)
            + "]}";
        return new Battle(rules, map, BattlePlan.Load(plan, map, rules), seed);
    }

    public static double X(this FixVec2 v) => v.X.Raw / 65536.0;

    public static double Y(this FixVec2 v) => v.Y.Raw / 65536.0;

    public static IEnumerable<int> LiveUnits(World world) =>
        Enumerable.Range(0, world.Units.HighWater).Where(world.Units.IsAlive);
}
