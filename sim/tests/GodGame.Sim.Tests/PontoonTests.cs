using System.Buffers.Binary;
using System.Globalization;
using GodGame.Sim.Hosting;

namespace GodGame.Sim.Tests;

/// <summary>The Pontoon Train (M5 item 4 part two, spec S2 section 14.5) on a still lake band.</summary>
[TestFixture, Category("Fast")]
public class PontoonTests
{
    // 360 × 150 m with 15 m zones; a still lake band 0.7 m deep across the whole map at x = 100.5–115.5 m (cells 67–76):
    // foot wade it (up to 1 m), wheeled can't enter it (over 0.5 m).
    private const int Columns = 240;
    private const int Rows = 100;

    private static bool InLake(int x) => x >= 67 && x <= 76;

    internal static MapData LakeMap(int attackerZoneMm = 15_000)
    {
        int width = Columns * 1_500;
        int depth = Rows * 1_500;
        string json = string.Format(
            CultureInfo.InvariantCulture,
            "{{\"version\":0,\"id\":\"lake\",\"widthMm\":{0},\"depthMm\":{1},\"cellSizeMm\":1500,\"deployment\":["
                + "{{\"side\":\"attacker\",\"minXMm\":0,\"minYMm\":0,\"maxXMm\":{3},\"maxYMm\":{1}}},"
                + "{{\"side\":\"defender\",\"minXMm\":{2},\"minYMm\":0,\"maxXMm\":{0},\"maxYMm\":{1}}}]}}",
            width,
            depth,
            width - 15_000,
            attackerZoneMm
        );
        var heights = new byte[Columns * Rows * 4];
        var water = new int[Columns * Rows];
        for (int y = 0; y < Rows; y++)
        {
            for (int x = 0; x < Columns; x++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(
                    heights.AsSpan((y * Columns + x) * 4),
                    InLake(x) ? -700 : 0
                );
                water[y * Columns + x] = InLake(x) ? 700_000 : 0;
            }
        }
        var layer = MapData.WriteFieldLayer(
            new[] { new KeyValuePair<string, int[]>(WaterModel.Depth, water) }
        );
        return MapData.Load(json, heights, layer);
    }

    internal static Battle LakeBattle() =>
        new Battle(
            RepoData.Rules(),
            LakeMap(),
            BattlePlan.Load(
                "{\"version\":0,\"map\":\"lake\",\"army\":["
                    + "{\"id\":\"p\",\"side\":\"attacker\",\"unitType\":\"pontoon-train\",\"soldiers\":4,\"xMm\":8000,\"yMm\":75000,\"facingCentiDegrees\":0},"
                    + "{\"id\":\"q\",\"side\":\"attacker\",\"unitType\":\"infantry\",\"soldiers\":100,\"xMm\":8000,\"yMm\":50000,\"facingCentiDegrees\":0}],"
                    + "\"terrain\":[],\"casts\":[]}",
                LakeMap(),
                RepoData.Rules()
            ),
            1
        );

    [Test]
    public void Decision_2026_10_11_TheFirstSetIncludesSpecialists_APontoonSpansWaterClosedToWheeled_AndTheMarchRouteCrossesOnIt()
    {
        // S2 14.5: water on the army's foot path that wheeled can't enter gets a floating bridge from the near bank, a
        // segment every 2 s, the water's width plus a cell at each end, 3 segments per boat.
        var battle = LakeBattle();
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        simulation.Step(240 * SimConstants.TicksPerSecond);
        var states = world.Structures!;
        Assert.That(
            states.Count,
            Is.InRange(4, 12),
            "the lake's width plus a cell each end, 2 cells a segment"
        );
        Assert.That(battle.Rules.Structures.Kinds[states.BattleKind[0]].Id, Is.EqualTo("pontoon"));
        var started = world.Causes!.Events.First(e =>
            e.Kind == CauseKind.SpecialistStarted && e.Detail == SpecialistExecutor.PontoonAction
        );
        var ended = world.Causes.Events.First(e =>
            e.Kind == CauseKind.SpecialistEnded && e.Detail == SpecialistExecutor.PontoonAction
        );
        Assert.That(
            ended.Tick - started.Tick,
            Is.LessThan(150 * SimConstants.TicksPerSecond),
            "the train drove to the bank and laid it"
        );
        Assert.That(world.Army.Spent[0], Is.EqualTo(states.Count), "a segment per charge");
        // The infantry wade across before the slow train arrives (fair play); what the bridge changes is the route: once
        // it's down, the foot march path along its row runs over the deck, not through the water.
        var navigation = new Navigation(battle.PlayedMap, battle.Rules, new SerialRunner());
        navigation.RebuildFromWorld(world, new SerialRunner());
        var field = navigation.MarchField(
            GodGame.Sim.Rules.MovementClass.Foot,
            new SerialRunner()
        )!;
        int row = states.BattleCells[0]![0] / Columns;
        int px = 60;
        int py = row;
        bool overDeck = true;
        var trace = new System.Text.StringBuilder(
            $"deck cells {string.Join(" ", states.BattleCells.Take(states.Count).SelectMany(c => c!).Select(c => $"{c % Columns},{c / Columns}"))} path"
        );
        var directions = field.Directions.ToArray();
        for (int k = 0; k < 40 && px <= 80; k++)
        {
            byte d = directions[py * Columns + px];
            if (d >= FlowField.AtGoal)
            {
                break;
            }
            var (dx, dy) = FlowField.Offset(d);
            px += dx;
            py += dy;
            trace.Append(
                $" {px},{py}{(navigation.Terrain.StandingSegment(py * Columns + px) >= 0 ? "D" : "")}"
            );
            if (InLake(px) && navigation.Terrain.StandingSegment(py * Columns + px) < 0)
            {
                overDeck = false;
            }
        }
        Assert.That(overDeck, Is.True, trace.ToString());
        Assert.That(world.Counters.SegmentsFailed, Is.Zero, "still water never breaks it");
    }

    [Test]
    public void PontoonsAreDeterministic_AcrossThreadCountsAndRestores()
    {
        var battle = LakeBattle();
        ulong[] serial = battle.PerSecondHashes(180, new SerialRunner());
        Assert.That(battle.PerSecondHashes(180, new ThreadedRunner(4)), Is.EqualTo(serial));
        var a = battle.Start(new SerialRunner());
        var snapshots = new List<(int Tick, byte[] Bytes)>();
        while (a.Tick < 180 * SimConstants.TicksPerSecond)
        {
            a.Step(1);
            if (a.Tick % 571 == 0)
            {
                snapshots.Add((a.Tick, a.CaptureSnapshot()));
            }
        }
        Assert.That(
            a.World.Structures!.Count,
            Is.GreaterThan(0),
            "the pontoon was laid in the run"
        );
        ulong end = a.StateHash();
        foreach (var (tick, bytes) in snapshots)
        {
            var b = battle.Start(new ThreadedRunner(3));
            b.Step(1_777);
            b.RestoreSnapshot(bytes);
            b.Step(180 * SimConstants.TicksPerSecond - tick);
            Assert.That(b.StateHash(), Is.EqualTo(end), $"restored at tick {tick}");
        }
    }

    /// <summary>A train of 4 boats on the lake's near bank (and, optionally, an infantry regiment behind it), in a 90 m attacker zone.</summary>
    private static Battle NearBankBattle(bool infantry)
    {
        var map = LakeMap(90_000);
        return new Battle(
            RepoData.Rules(),
            map,
            BattlePlan.Load(
                "{\"version\":0,\"map\":\"lake\",\"army\":["
                    + "{\"id\":\"p\",\"side\":\"attacker\",\"unitType\":\"pontoon-train\",\"soldiers\":4,\"xMm\":85000,\"yMm\":75000,\"facingCentiDegrees\":0}"
                    + (
                        infantry
                            ? ",{\"id\":\"q\",\"side\":\"attacker\",\"unitType\":\"infantry\",\"soldiers\":100,\"xMm\":20000,\"yMm\":75000,\"facingCentiDegrees\":0}"
                            : ""
                    )
                    + "],\"terrain\":[],\"casts\":[]}",
                map,
                RepoData.Rules()
            ),
            1
        );
    }

    /// <summary>Fills the lake north of the deck (rows up to 6 short of it, columns <paramref name="west"/> to the far shore) to 2 m above its bed.</summary>
    private static void Flood(World world, int deckRow, int west)
    {
        var water = world.Fields!.Get(WaterModel.Depth);
        for (int y = 0; y < deckRow - 6; y++)
        {
            for (int x = west; x <= 76; x++)
            {
                water.Set(x, y, 2_000_000);
            }
        }
    }

    [Test]
    public void Decision_2026_10_11_TheFirstSetIncludesSpecialists_ASurgeBreaksAPontoonLoose_FromTheSurgingSegmentOn()
    {
        // S2 14.5: a segment where the current runs at 1,800 mm/s for 40 ticks breaks loose, and every segment beyond it
        // (away from the near bank) goes with it; the ones nearer the bank stand. A steady 2 m head on the far half of the
        // lake drives the current through the far segments first.
        var battle = NearBankBattle(infantry: false);
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        int deckRow = -1;
        while (
            simulation.Tick < 60 * SimConstants.TicksPerSecond && world.Counters.SegmentsFailed == 0
        )
        {
            simulation.Step();
            if (
                deckRow < 0
                && world.Causes!.Events.Any(e =>
                    e.Kind == CauseKind.SpecialistEnded
                    && e.Detail == SpecialistExecutor.PontoonAction
                )
            )
            {
                deckRow = world.Structures!.BattleCells[0]![0] / Columns;
            }
            if (deckRow >= 0)
            {
                Flood(world, deckRow, 74);
            }
        }
        var states = world.Structures!;
        Assert.That(
            deckRow,
            Is.GreaterThanOrEqualTo(0),
            "the bridge was finished before the flood"
        );
        Assert.That(
            states.Count,
            Is.EqualTo(6),
            "10 cells of lake and a bank cell each end, 2 cells a segment"
        );
        var failed = world
            .Causes!.Events.Where(e => e.Kind == CauseKind.SegmentFailed)
            .Select(e => e.Subject)
            .OrderBy(s => s)
            .ToList();
        Assert.That(failed, Is.Not.Empty, "the surge broke the bridge");
        int first = failed[0];
        Assert.That(first, Is.GreaterThan(0), "the far half surged first");
        Assert.That(
            failed,
            Is.EqualTo(Enumerable.Range(first, states.Count - first)),
            "it and every segment beyond it, at once"
        );
        for (int s = 0; s < first; s++)
        {
            Assert.That(states.Broken[s], Is.False, $"segment {s}, nearer the bank, stands");
        }
    }

    [Test]
    public void Decision_2026_10_11_TheFirstSetIncludesSpecialists_APontoonBrokenLoose_SweepsTheSoldiersOnIt()
    {
        // S2 14.5: units on segments that break loose are swept, though the deck held them above the water. (On the bank
        // cell at its end, where the flood has spilled only a little water, they climb straight out, stunned.) The infantry
        // queue onto the near end of the deck; then the whole lake floods.
        var battle = NearBankBattle(infantry: true);
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        var terrain = new Terrain(battle.PlayedMap);
        int infantry = battle.Rules.UnitType("infantry").Index;
        var onDeck = new List<int>();
        bool flooded = false;
        while (
            simulation.Tick < 150 * SimConstants.TicksPerSecond
            && world.Counters.SegmentsFailed == 0
        )
        {
            var states = world.Structures!;
            onDeck.Clear();
            foreach (int u in TestBattles.LiveUnits(world))
            {
                terrain.CellOf(world.Position[u], out int x, out int y);
                int cell = terrain.Index(x, y);
                bool deck = Enumerable
                    .Range(0, states.Count)
                    .Any(s => !states.Broken[s] && states.BattleCells[s]!.Contains(cell));
                if (world.UnitType[u] == infantry && deck && InLake(x))
                {
                    onDeck.Add(u);
                }
            }
            if (!flooded && onDeck.Count >= 10)
            {
                Flood(world, states.BattleCells[0]![0] / Columns, 67);
                flooded = true;
            }
            simulation.Step();
        }
        Assert.That(flooded, Is.True, "the infantry reached the deck");
        Assert.That(world.Counters.SegmentsFailed, Is.GreaterThan(0), "the flood broke the bridge");
        Assert.That(
            world.Structures!.Broken[0],
            Is.True,
            "the near segment broke, and with it the rest"
        );
        Assert.That(onDeck, Is.Not.Empty);
        foreach (int u in onDeck)
        {
            Assert.That(
                world.Flags[u] & UnitFlags.Swept,
                Is.EqualTo(UnitFlags.Swept),
                $"unit {u} was on the deck over the lake"
            );
        }
    }
}
