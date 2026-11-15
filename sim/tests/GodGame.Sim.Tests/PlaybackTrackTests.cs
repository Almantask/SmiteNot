using GodGame.Sim.Hosting;

namespace GodGame.Sim.Tests;

/// <summary>The playback track (M4 item 3): unit samples 5 times a second and field frames every second, for scrubbing.</summary>
[TestFixture, Category("Fast"), Parallelizable(ParallelScope.Children)]
public class PlaybackTrackTests
{
    private static readonly MapData Valley = RainTests.ValleyBattle(rain: false).Map;

    private static Battle Plan(int stormTick) =>
        TestBattles.Battle(
            Valley,
            new[]
            {
                ("defender", "infantry", 50, 352.5, 120.0),
                ("attacker", "infantry", 50, 7.5, 120.0),
            },
            1,
            new[] { ("storm", "rain", stormTick, 180.0, 180.0, 40.0) }
        );

    private static Simulation FreshAt(Battle battle, int tick)
    {
        var simulation = battle.Start(new ThreadedRunner(4));
        simulation.Step(tick);
        return simulation;
    }

    private static void AssertSampleMatches(UnitSample sample, Simulation simulation)
    {
        var world = simulation.World;
        Assert.That(sample.Tick, Is.EqualTo(simulation.Tick));
        for (int u = 0; u < world.Units.HighWater; u++)
        {
            Assert.That(
                (sample.State[u] & UnitSample.Alive) != 0,
                Is.EqualTo(world.Units.IsAlive(u))
            );
            if (world.Units.IsAlive(u))
            {
                Assert.That(sample.XCm[u], Is.EqualTo((int)(world.Position[u].X.Raw * 100 >> 16)));
                Assert.That(sample.YCm[u], Is.EqualTo((int)(world.Position[u].Y.Raw * 100 >> 16)));
            }
        }
    }

    private static void AssertFieldsMatch(PlaybackTrack track, Simulation simulation)
    {
        var fields = simulation.World.Fields!;
        var expected = new uint[fields.Columns * fields.Rows * PlaybackTrack.Stride];
        PlaybackTrack.Fill(fields, Valley.CellSizeMm, expected);
        var replayed = new uint[expected.Length];
        Assert.That(track.FieldsAt(simulation.Tick, replayed), Is.EqualTo(simulation.Tick));
        Assert.That(replayed, Is.EqualTo(expected));
    }

    [Test]
    public void TheTrack_HoldsASampleEvery4Ticks_AndFieldFramesThatRebuildTheFieldsExactly()
    {
        var timeline = new Timeline(
            Plan(200),
            new ThreadedRunner(4),
            new KeyframeStore(long.MaxValue)
        );
        timeline.AdvanceTo(1_000);
        var track = timeline.Track;

        Assert.That(track.SampleCount, Is.EqualTo(1_000 / 4 + 1));
        Assert.That(track.FrameCount, Is.EqualTo(1_000 / 20 + 1));
        Assert.That(track.Frame(0).Full && track.Frame(10).Full && !track.Frame(11).Full, Is.True);
        Assert.That(
            track.Frame(30).Tiles.Length,
            Is.LessThan(track.Frame(30).Full ? int.MaxValue : track.TileColumns * track.TileRows)
        );
        AssertSampleMatches(track.SampleAtOrBefore(803)!, FreshAt(Plan(200), 800));
        AssertFieldsMatch(track, FreshAt(Plan(200), 760)); // a delta frame, 16 after a full one
    }

    [Test]
    public void AnEdit_ForksTheTrack_KeepingThePast_AndRecordsTheEditedFuture()
    {
        var timeline = new Timeline(
            Plan(500),
            new ThreadedRunner(4),
            new KeyframeStore(long.MaxValue)
        );
        timeline.AdvanceTo(1_000);
        var before = timeline.Track;
        var edited = Plan(700);

        int resumed = timeline.Edit(edited.Plan);
        timeline.AdvanceTo(1_000);
        var after = timeline.Track;

        Assert.That(resumed, Is.EqualTo(500));
        Assert.That(
            after,
            Is.Not.SameAs(before),
            "readers of the old track keep a consistent picture"
        );
        Assert.That(before.SampleCount, Is.EqualTo(251), "the old track is never cut");
        Assert.That(
            after.SampleAtOrBefore(400),
            Is.SameAs(before.SampleAtOrBefore(400)),
            "the past is shared"
        );
        AssertSampleMatches(after.SampleAtOrBefore(900)!, FreshAt(edited, 900));
        AssertFieldsMatch(after, FreshAt(edited, 940));
    }

    [Test]
    public void Track_RecordsStructuresAndLaunches_OnBridgeFailure_AndForkTrimsFutureLaunches()
    {
        var rules = RepoData.Rules();
        // Steep 6 m deep gorge in columns 94..102 (141..153 m) spanned by a wooden bridge (>100 m from both zones).
        var map = TestBattles.Map(
            (x, _) => x >= 94 && x <= 102 ? -6_000 : 0,
            200,
            24,
            zoneMetres: 20,
            id: "gorge-track"
        );
        string bridge =
            "{\"id\":\"b0\",\"piece\":\"wooden-bridge\",\"points\":[[138000,18000],[156000,18000]],\"widthMm\":4500}";
        string army =
            "{\"id\":\"a0\",\"side\":\"attacker\",\"unitType\":\"cavalry\",\"soldiers\":80,\"xMm\":16000,\"yMm\":18000,\"facingCentiDegrees\":0},"
            + "{\"id\":\"d0\",\"side\":\"defender\",\"unitType\":\"infantry\",\"soldiers\":50,\"xMm\":288000,\"yMm\":18000,\"facingCentiDegrees\":18000}";
        string planJson =
            "{\"version\":0,\"map\":\""
            + map.Id
            + "\",\"army\":["
            + army
            + "],\"terrain\":["
            + bridge
            + "],\"casts\":[{\"id\":\"c0\",\"power\":\"rot\",\"tick\":1,\"xMm\":147000,\"yMm\":18000,\"radiusMm\":15000}]}";
        var battle = new Battle(rules, map, BattlePlan.Load(planJson, map, rules), 1);
        var timeline = new Timeline(
            battle,
            new ThreadedRunner(4),
            new KeyframeStore(long.MaxValue)
        );
        timeline.AdvanceTo(1_200);
        var track = timeline.Track;

        Assert.That(track.StructureSampleCount, Is.GreaterThan(0));
        bool sawStrain = false;
        bool sawBroken = false;
        for (int i = 0; i < track.StructureSampleCount; i++)
        {
            var segs = track.Structure(i).Segments;
            for (int s = 0; s < segs.Length; s++)
            {
                if (segs[s].LoadPermille > 0 || segs[s].Stage != StrainStage.Quiet)
                {
                    sawStrain = true;
                }
                if (segs[s].Broken)
                {
                    sawBroken = true;
                }
            }
        }
        Assert.That(sawStrain, Is.True, "marching over the bridge produces nonzero load/strain");
        Assert.That(sawBroken, Is.True, "rotted overloaded bridge breaks");
        Assert.That(track.LaunchCount, Is.GreaterThan(0), "bridge failure records launch events");

        var firstLaunch = track.Launch(0);
        Assert.That(firstLaunch.GravityMmPerS2, Is.GreaterThan(0));
        Assert.That(firstLaunch.SpinCdPerS, Is.Not.Zero);
        Assert.That(
            track.LaunchLandingCount,
            Is.GreaterThan(0),
            "airborne soldiers finish landing"
        );

        // Editing just before the first launch trims the future launches on the forked track.
        int cutTick = System.Math.Max(0, firstLaunch.LaunchTick - 1);
        string noRotJson =
            "{\"version\":0,\"map\":\""
            + map.Id
            + "\",\"army\":["
            + army
            + "],\"terrain\":["
            + bridge
            + "],\"casts\":[]}";
        int resumed = timeline.Edit(BattlePlan.Load(noRotJson, map, rules));
        Assert.That(resumed, Is.LessThanOrEqualTo(firstLaunch.LaunchTick));
        var trimmed = track.Fork(cutTick);
        Assert.That(trimmed.LaunchCount, Is.EqualTo(0));
    }

    [Test]
    public void Track_RecordsRegimentMoraleRoutingAndUnitRoutingFlag()
    {
        var timeline = new Timeline(
            Plan(100),
            new ThreadedRunner(4),
            new KeyframeStore(long.MaxValue)
        );
        timeline.AdvanceTo(1_200);
        var track = timeline.Track;

        Assert.That(track.RegimentFrameCount, Is.GreaterThan(10));
        Assert.That(track.RegimentSides.Length, Is.EqualTo(2));
        Assert.That(track.RegimentStartingSizes[0], Is.EqualTo(50));
        Assert.That(track.RegimentStartingSizes[1], Is.EqualTo(50));

        var start = track.RegimentsAtOrBefore(0)!;
        Assert.That(start.Regiments[0].Morale, Is.EqualTo(1000));
        Assert.That(start.Regiments[1].Morale, Is.EqualTo(1000));

        // Direct unit/regiment recording check ensures all 7 UnitFlags bits survive the UnitSample mask.
        var sim = FreshAt(Plan(100), 20);
        sim.World.Flags[0] |= UnitFlags.Routing | UnitFlags.Burning | UnitFlags.Garrisoned;
        var direct = new PlaybackTrack(track.Columns, track.Rows, 16);
        direct.RecordUnits(20, sim.World);
        direct.RecordRegiments(20, sim.World);
        byte state = direct.Sample(0).State[0];
        Assert.That((state & UnitSample.Alive) != 0, Is.True);
        Assert.That((state & (byte)UnitFlags.Routing) != 0, Is.True);
        Assert.That((state & (byte)UnitFlags.Burning) != 0, Is.True);
        Assert.That((state & (byte)UnitFlags.Garrisoned) != 0, Is.True);
        Assert.That(direct.RegimentsAtOrBefore(20)!.Regiments[0].AliveCount, Is.EqualTo(50));
    }
}
