using GodGame.Sim.Hosting;
using GodGame.Sim.Rules;

namespace GodGame.Sim.Scenarios;

/// <summary>
/// The M3 exit criteria (plan, section 9; spec S1, section 13) on data/maps/m3-exit: a valley river at base flow (its
/// settled water in water.bin), a 30 m ford at y = 140 to 160 m, and 600 attackers marching west to east for it. The
/// rain plan adds an 80 m storm on the river 300 m upstream at 0:50; the moat plan adds a 1.5 m channel across the
/// valley between them. Written by tools/maps/make_m3_exit.py.
/// </summary>
[TestFixture, Category("Slow")]
public class M3ExitScenarios
{
    private const int FordX = 155;
    private const int FordY = 99;
    private const double FarBankM = 255;

    private sealed class Run
    {
        public int FordLethalTick = -1;
        public int HalfAcrossTick = -1;
        public int MaxMoatDepthUm;
        public Simulation Simulation = null!;
    }

    private static Run Play(string planFile, int seconds)
    {
        string root = RepoFiles.FindRoot(TestContext.CurrentContext.TestDirectory);
        var rules = RepoFiles.LoadRepoRules(root);
        var map = RepoFiles.LoadRepoMap(root, "m3-exit");
        var plan = BattlePlan.Load(
            File.ReadAllText(Path.Combine(root, "data", "plans", planFile)),
            map,
            rules
        );
        var simulation = new Battle(rules, map, plan, seed: 1).Start(new ThreadedRunner(4));
        var world = simulation.World;
        var codes = world.Fields!.Get(FieldSpeed.FieldId);
        var depth = world.Fields!.Get(WaterModel.Depth);
        var run = new Run { Simulation = simulation };
        for (
            int tick = 0;
            tick < seconds * SimConstants.TicksPerSecond;
            tick += SimConstants.TicksPerSecond
        )
        {
            simulation.Step(SimConstants.TicksPerSecond);
            if (
                run.FordLethalTick < 0
                && FieldSpeed.Code(codes.Get(FordX, FordY), MovementClass.Foot) == FieldSpeed.Swept
            )
            {
                run.FordLethalTick = simulation.Tick;
            }
            int across = 0;
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (
                    world.Units.IsAlive(u)
                    && world.RegimentSide[world.UnitRegiment[u]] == Side.Attacker
                    && world.Position[u].X.Raw / 65536.0 > FarBankM
                )
                {
                    across++;
                }
            }
            if (run.HalfAcrossTick < 0 && across >= 300)
            {
                run.HalfAcrossTick = simulation.Tick;
            }
            if (simulation.Tick <= 150 * SimConstants.TicksPerSecond)
            {
                // The channel near the river (x = 225 m), up to when the column reaches the river.
                run.MaxMoatDepthUm = System.Math.Max(run.MaxMoatDepthUm, depth.Get(150, 220));
            }
        }
        return run;
    }

    [Test]
    public void RainUpstream_FloodsTheFord_DelaysTheAttack_AndEveryDrownedSoldierTracesBackToTheCast()
    {
        var dry = Play("m3-exit-dry.json", 330);
        var rain = Play("m3-exit-rain.json", 330);
        var log = rain.Simulation.World.Causes!;
        var casts = rain.Simulation.World.Casts!;

        // The flood: S1 targets about 2:40 (tick 3,200 ± 400); this map measures about 3:05.
        Assert.That(
            dry.FordLethalTick,
            Is.EqualTo(-1),
            "at base flow the ford never sweeps a foot soldier"
        );
        Assert.That(
            casts.Status[0],
            Is.EqualTo(CastStatus.Fired),
            "the storm is far from both armies at 0:50"
        );
        Assert.That(
            rain.FordLethalTick,
            Is.InRange(2 * 60 * 20, 3 * 60 * 20 + 30 * 20),
            "the ford turns lethal for foot between 2:00 and 3:30"
        );

        // The attack: half the army reaches the far bank at least 60 s later than without rain (or never, in 5:30).
        Assert.That(dry.HalfAcrossTick, Is.GreaterThan(0));
        int rainHalf = rain.HalfAcrossTick < 0 ? int.MaxValue : rain.HalfAcrossTick;
        Assert.That(
            rainHalf - dry.HalfAcrossTick,
            Is.GreaterThanOrEqualTo(60 * SimConstants.TicksPerSecond),
            $"half across at {dry.HalfAcrossTick / 20} s without rain, {rain.HalfAcrossTick / 20} s with"
        );

        // The trace: every drowned soldier's chain reaches the cast (S1 section 12, the M3 exit criterion).
        Assert.That(dry.Simulation.World.Counters.Drowned, Is.Zero, "no deaths without the god");
        int castEvent = casts.Event[0];
        int drowned = 0;
        for (int e = 0; e < log.Events.Count; e++)
        {
            if (log.Events[e].Kind != CauseKind.SoldierDrowned)
            {
                continue;
            }
            drowned++;
            var trace = log.Trace(e);
            Assert.That(
                trace.Any(t => t.Kind == CauseKind.CastFired && t.Subject == 0),
                Is.True,
                $"drowned event {e} traces back to the storm: "
                    + string.Join(" <- ", trace.Select(t => t.Kind + "@" + t.Tick))
            );
            Assert.That(
                trace[^1].Kind,
                Is.EqualTo(CauseKind.CastFired).Or.EqualTo(CauseKind.SoldierPushedOver)
            );
        }
        Assert.That(castEvent, Is.Not.EqualTo(CauseLog.None));
        Assert.That(drowned, Is.EqualTo(rain.Simulation.World.Counters.Drowned));
        Assert.That(drowned, Is.GreaterThan(10), "the flood caught soldiers in the ford");
    }

    [Test]
    public void ADugChannelPlusRain_BecomesAMoat()
    {
        // S1 section 13: the 1.5 m channel below the storm holds at least 1 m of water before the column reaches the river.
        var moat = Play("m3-exit-moat.json", 150);

        Assert.That(moat.MaxMoatDepthUm, Is.GreaterThanOrEqualTo(1_000_000));
        Assert.That(moat.Simulation.World.Casts!.Status[0], Is.EqualTo(CastStatus.Fired));
    }
}
