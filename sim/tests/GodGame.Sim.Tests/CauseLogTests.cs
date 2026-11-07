using GodGame.Sim.Hosting;

namespace GodGame.Sim.Tests;

/// <summary>The cause log v1 (M3 item 9, spec S1 section 12).</summary>
[TestFixture, Category("Fast")]
public class CauseLogTests
{
    [Test]
    public void Trace_WalksEveryLinkBack_EachEventOnce_CauseBeforeSecondCause()
    {
        var log = new CauseLog(1, 1, 1);
        int cast = log.Add(CauseKind.CastFired, 1_000, 0, 0, CauseLog.None);
        int overflow = log.Add(CauseKind.RiverOverflowed, 2_000, 7, 0, cast);
        int push = log.Add(CauseKind.SoldierPushedOver, 2_100, 0, 12, CauseLog.None);
        int swept = log.Add(CauseKind.RegimentSwept, 2_100, 0, 0, cast);
        int drowned = log.Add(CauseKind.SoldierDrowned, 2_400, 0, 12, swept, push);

        var trace = log.Trace(drowned);

        Assert.That(
            trace.Select(e => e.Kind),
            Is.EqualTo(
                new[]
                {
                    CauseKind.SoldierDrowned,
                    CauseKind.RegimentSwept,
                    CauseKind.CastFired,
                    CauseKind.SoldierPushedOver,
                }
            )
        );
        Assert.That(
            log.Trace(overflow).Select(e => e.Kind),
            Is.EqualTo(new[] { CauseKind.RiverOverflowed, CauseKind.CastFired })
        );
        Assert.Throws<ArgumentException>(() => log.Add(CauseKind.SoldierDrowned, 0, 0, 0, 99));
    }

    [Test]
    public void Casts_LogFiredOrFizzled_AndRainTagsTheWaterItFeeds_DownTheRiver()
    {
        // The valley river (RainTests): a storm at tick 20 on the river 120 m upstream of the gauge.
        var simulation = RainTests.ValleyBattle(rain: true, castTick: 20).Start(new SerialRunner());
        var log = simulation.World.Causes!;
        var casts = simulation.World.Casts!;
        var depth = simulation.World.Fields!.Get(WaterModel.Depth);
        int gauge = depth.TileOf(120, 40); // 120 m downstream of the storm's edge
        int storm = depth.TileOf(120, 120);

        simulation.Step(21);
        Assert.That(casts.Event[0], Is.Not.EqualTo(CauseLog.None));
        Assert.That(log.Events[casts.Event[0]].Kind, Is.EqualTo(CauseKind.CastFired));
        Assert.That(
            log.TileTag[gauge],
            Is.EqualTo(CauseLog.None),
            "the map's own river has no cause"
        );

        simulation.Step(220 + 30 * SimConstants.TicksPerSecond);
        Assert.That(
            log.TileTag[storm],
            Is.EqualTo(casts.Event[0]),
            "rain stamps its cast on the water it adds"
        );

        simulation.Step(90 * SimConstants.TicksPerSecond);
        Assert.That(
            log.TileTag[gauge],
            Is.EqualTo(casts.Event[0]),
            "the tag rode the river down to the gauge"
        );
    }

    [Test]
    public void AFizzledCast_IsLoggedWithItsReasons()
    {
        var battle = TestBattles.Battle(
            TestBattles.Map((x, y) => 0, 300, 100),
            new[] { ("attacker", "infantry", 50, 7.5, 75.0) },
            1,
            new[] { ("near", "rain", 5, 100.0, 75.0, 40.0) }
        );
        var simulation = battle.Start(new SerialRunner());

        simulation.Step(6);

        var e = simulation.World.Causes!.Events[simulation.World.Casts!.Event[0]];
        Assert.That(e.Kind, Is.EqualTo(CauseKind.CastFizzled));
        Assert.That(e.Tick, Is.EqualTo(5));
        Assert.That((FizzleReasons)e.Detail, Is.EqualTo(FizzleReasons.TooCloseToAttacker));
    }
}
