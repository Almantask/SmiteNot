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
}
