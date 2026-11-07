using GodGame.Sim.Hosting;

namespace GodGame.Sim.Tests;

/// <summary>The timeline (M4 item 1): keyframes every 5 s, and edits that re-simulate from the earliest change.</summary>
[TestFixture, Category("Fast"), Parallelizable(ParallelScope.Children)] // independent battles, each seconds long
public class TimelineTests
{
    private static readonly MapData Valley = RainTests.ValleyBattle(rain: false).Map;

    /// <summary>The valley river with the defenders far away, and the given storms (id, tick, y).</summary>
    private static Battle Plan(params (string Id, int Tick, double Y)[] storms) =>
        TestBattles.Battle(
            Valley,
            new[] { ("defender", "infantry", 50, 352.5, 120.0) },
            1,
            storms.Select(s => (s.Id, "rain", s.Tick, 180.0, s.Y, 40.0))
        );

    private static ulong FreshHashAt(Battle battle, int tick)
    {
        var simulation = battle.Start(new ThreadedRunner(4));
        simulation.Step(tick);
        return simulation.StateHash();
    }

    private static Timeline Run(Battle battle, int to, KeyframeStore? store = null)
    {
        var timeline = new Timeline(
            battle,
            new ThreadedRunner(4),
            store ?? new KeyframeStore(long.MaxValue)
        );
        timeline.AdvanceTo(to);
        return timeline;
    }

    [Test]
    public void KeyframesAreTakenEvery5Seconds()
    {
        var timeline = Run(Plan(("storm", 400, 180.0)), 1_050);

        Assert.That(
            timeline.Keyframes.Ticks,
            Is.EqualTo(new[] { 0, 100, 200, 300, 400, 500, 600, 700, 800, 900, 1_000 })
        );
        Assert.That(timeline.Tick, Is.EqualTo(1_050));
        Assert.That(
            timeline.Simulation.StateHash(),
            Is.EqualTo(FreshHashAt(Plan(("storm", 400, 180.0)), 1_050))
        );
    }

    [Test]
    public void MovingACastLater_ResumesFromItsOldTick_AndMatchesTheEditedPlanRunFromTheStart()
    {
        var timeline = Run(Plan(("storm", 430, 180.0)), 1_500);
        var edited = Plan(("storm", 730, 180.0));

        int resumed = timeline.Edit(edited.Plan);
        timeline.AdvanceTo(1_500);

        Assert.That(resumed, Is.EqualTo(400), "the last keyframe before the old tick");
        Assert.That(timeline.Simulation.StateHash(), Is.EqualTo(FreshHashAt(edited, 1_500)));
    }

    [Test]
    public void AddingACast_AfterOneThatFired_KeepsItsState_EvenWhenTheIdsShiftItsIndex()
    {
        // "a-second" sorts before "storm", so the fired storm moves from index 0 to 1 in the edited plan.
        var timeline = Run(Plan(("storm", 220, 180.0)), 1_400);
        var edited = Plan(("storm", 220, 180.0), ("a-second", 960, 140.0));

        int resumed = timeline.Edit(edited.Plan);
        timeline.AdvanceTo(1_400);

        Assert.That(resumed, Is.EqualTo(900));
        Assert.That(timeline.Simulation.World.Casts!.Status[1], Is.EqualTo(CastStatus.Fired));
        Assert.That(
            timeline
                .Simulation
                .World
                .Causes!
                .Events[timeline.Simulation.World.Casts.Event[1]]
                .Subject,
            Is.EqualTo(1)
        );
        Assert.That(timeline.Simulation.StateHash(), Is.EqualTo(FreshHashAt(edited, 1_400)));
    }

    [Test]
    public void RemovingACast_ResumesFromItsTick()
    {
        var timeline = Run(Plan(("storm", 220, 180.0), ("late", 640, 140.0)), 1_000);
        var edited = Plan(("storm", 220, 180.0));

        Assert.That(timeline.Edit(edited.Plan), Is.EqualTo(600));
        timeline.AdvanceTo(1_000);
        Assert.That(timeline.Simulation.StateHash(), Is.EqualTo(FreshHashAt(edited, 1_000)));
    }

    [Test]
    public void AnArmyEdit_StartsOver()
    {
        var timeline = Run(Plan(("storm", 220, 180.0)), 600);
        var moved = TestBattles.Battle(
            Valley,
            new[] { ("defender", "infantry", 50, 352.5, 100.0) },
            1,
            new[] { ("storm", "rain", 220, 180.0, 180.0, 40.0) }
        );

        Assert.That(timeline.Edit(moved.Plan), Is.Zero);
        Assert.That(timeline.Keyframes.Ticks, Is.EqualTo(new[] { 0 }));
        timeline.AdvanceTo(600);
        Assert.That(timeline.Simulation.StateHash(), Is.EqualTo(FreshHashAt(moved, 600)));
    }

    [Test]
    public void OverItsMemoryBudget_OlderKeyframesSpillToDisk_AndEditsStillResumeFromThem()
    {
        string dir = Path.Combine(
            Path.GetTempPath(),
            "godgame-keyframes-" + Guid.NewGuid().ToString("N")
        );
        try
        {
            var store = new KeyframeStore(1, new DirectoryKeyframeSpill(dir)); // everything but the newest spills
            var timeline = Run(Plan(("storm", 430, 180.0)), 1_000, store);
            Assert.That(Directory.GetFiles(dir), Has.Length.EqualTo(10));
            var edited = Plan(("storm", 530, 180.0));

            Assert.That(timeline.Edit(edited.Plan), Is.EqualTo(400));
            Assert.That(
                Directory.GetFiles(dir),
                Has.Length.LessThanOrEqualTo(4),
                "stale spilled keyframes are deleted"
            );
            timeline.AdvanceTo(1_000);
            Assert.That(timeline.Simulation.StateHash(), Is.EqualTo(FreshHashAt(edited, 1_000)));
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    [Test]
    public void EarliestChange_IsTheEarlierOfAnEditedCastsTwoTicks_OrZeroForTheArmy()
    {
        var plan = Plan(("storm", 430, 180.0), ("late", 900, 140.0)).Plan;

        Assert.That(
            Timeline.EarliestChange(plan, Plan(("storm", 430, 180.0), ("late", 700, 140.0)).Plan),
            Is.EqualTo(700)
        );
        Assert.That(
            Timeline.EarliestChange(plan, Plan(("storm", 430, 170.0), ("late", 900, 140.0)).Plan),
            Is.EqualTo(430)
        );
        Assert.That(
            Timeline.EarliestChange(plan, Plan(("storm", 430, 180.0), ("late", 900, 140.0)).Plan),
            Is.EqualTo(int.MaxValue)
        );
        Assert.That(
            Timeline.EarliestChange(
                plan,
                TestBattles
                    .Battle(Valley, new[] { ("defender", "infantry", 60, 352.5, 120.0) })
                    .Plan
            ),
            Is.Zero
        );
    }

    [Test]
    public void ChainedEdits_ResumeFromKeyframesOfEarlierPlans_AndStillMatchAFreshRun()
    {
        // The second edit resumes before the first one's resume point, from a keyframe the first plan wrote: its casts
        // ("c", "d") are laid out differently from the current plan's ("a", "c", "e").
        var timeline = Run(Plan(("c", 200, 180.0), ("d", 1_500, 140.0)), 2_000);
        Assert.That(timeline.Edit(Plan(("a", 1_500, 140.0), ("c", 200, 180.0)).Plan), Is.EqualTo(1_500));
        timeline.AdvanceTo(1_800);
        var last = Plan(("a", 1_500, 140.0), ("c", 200, 180.0), ("e", 1_400, 150.0));

        Assert.That(timeline.Edit(last.Plan), Is.EqualTo(1_400));
        Assert.That(timeline.Simulation.StateHash(), Is.EqualTo(FreshHashAt(last, 1_400)));
        timeline.AdvanceTo(2_000);
        Assert.That(timeline.Simulation.StateHash(), Is.EqualTo(FreshHashAt(last, 2_000)));
    }

    [Test]
    public void AnEditBeyondTheHead_KeepsWhatIsAlreadySimulated()
    {
        var timeline = Run(Plan(("storm", 220, 180.0)), 650);
        var edited = Plan(("storm", 220, 180.0), ("late", 900, 140.0));

        Assert.That(timeline.Edit(edited.Plan), Is.EqualTo(650), "nothing before tick 900 changes");
        timeline.AdvanceTo(1_100);
        Assert.That(timeline.Simulation.StateHash(), Is.EqualTo(FreshHashAt(edited, 1_100)));
    }
}
