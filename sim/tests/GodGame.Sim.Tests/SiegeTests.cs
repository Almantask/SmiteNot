using System.Globalization;
using GodGame.Sim.Hosting;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim.Tests;

/// <summary>
/// The four base types from spec S2 (M5 item 6): siege engines with real crews, stones, abandoned and wrecked engines,
/// the cavalry's 30–120, the infantry's swept modifier, the plan's order flag and the Pontoon Train's crews.
/// </summary>
[TestFixture, Category("Fast")]
public class SiegeTests
{
    private const int Columns = 160; // 240 m: zones 0-80 m and 160-240 m
    private const int Rows = 60; // 90 m

    private static MapData Flat(int columns = Columns, int rows = Rows, int zoneMetres = 80) =>
        TestBattles.Map((_, _) => 0, columns, rows, zoneMetres);

    private static string Regiment(
        string id,
        string side,
        string type,
        int soldiers,
        int xMm,
        int yMm,
        string extra = ""
    ) =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{{\"id\":\"{0}\",\"side\":\"{1}\",\"unitType\":\"{2}\",\"soldiers\":{3},\"xMm\":{4},\"yMm\":{5},\"facingCentiDegrees\":0{6}}}",
            id,
            side,
            type,
            soldiers,
            xMm,
            yMm,
            extra
        );

    private static BattlePlan Plan(MapData map, params string[] regiments) =>
        BattlePlan.Load(
            "{\"version\":0,\"map\":\""
                + map.Id
                + "\",\"army\":["
                + string.Join(",", regiments)
                + "],\"terrain\":[],\"casts\":[]}",
            map,
            RepoData.Rules()
        );

    private static Battle Load(MapData map, ulong seed, params string[] regiments) =>
        new Battle(RepoData.Rules(), map, Plan(map, regiments), seed);

    /// <summary>Removes crew as if they had fallen long ago: morale has charged the loss, so the regiment doesn't rout.</summary>
    private static void KillCrew(World world, int regiment, int crewType, int count)
    {
        foreach (int u in UnitsOf(world, regiment, crewType).Take(count))
        {
            world.DespawnUnit(world.Units.IdOf(u));
        }
        world.Army.LossesSeen[regiment] =
            2 * (world.RegimentStartingSize[regiment] - world.RegimentAlive[regiment]);
    }

    private static List<CauseEvent> Events(World world, CauseKind kind) =>
        world.Causes!.Events.Where(e => e.Kind == kind).ToList();

    private static List<int> UnitsOf(World world, int regiment, int type) =>
        TestBattles
            .LiveUnits(world)
            .Where(u => world.UnitRegiment[u] == regiment && world.UnitType[u] == type)
            .ToList();

    [Test]
    public void Decision_2026_11_07_ArmiesLoadLaunchesSpecialists_SiegeEnginesHaveRealCrews_EightFootSoldiersBesideEachEngine()
    {
        // S2 2.1, D4: the plan's soldiers count engines; each brings 8 crew, real foot soldiers of the engine's
        // regiment, standing within 3 m of it. Total units count the crews (10,000 at most).
        var map = Flat();
        var battle = Load(map, 1, Regiment("g", "attacker", "siege", 3, 40_000, 45_000));
        var world = battle.Start(new SerialRunner()).World;
        var rules = battle.Rules;
        int siege = rules.UnitType("siege").Index;
        int crew = rules.UnitType("crew").Index;
        var engines = UnitsOf(world, 0, siege);
        var crews = UnitsOf(world, 0, crew);
        Assert.That(engines, Has.Count.EqualTo(3));
        Assert.That(crews, Has.Count.EqualTo(24));
        Assert.That(world.RegimentStartingSize[0], Is.EqualTo(27));
        Assert.That(world.RegimentUnitType[0], Is.EqualTo(siege));
        Assert.That(
            engines.Select(u => world.FormationSlot[u]),
            Is.EquivalentTo(new[] { 0, 1, 2 })
        );
        for (int k = 0; k < crews.Count; k++)
        {
            int e = (world.FormationSlot[crews[k]] - 3) / 8;
            int engine = engines.Single(u => world.FormationSlot[u] == e);
            double d = (world.Position[crews[k]] - world.Position[engine]).Length.Raw / 65536.0;
            Assert.That(d, Is.EqualTo(3.0).Within(0.01), "on the ring around his own engine");
        }
        Assert.That(world.Health[crews[0]], Is.EqualTo(rules.UnitType("crew").Health));
        Assert.That(world.Army.Ammo[0], Is.EqualTo(3 * 30), "30 stones per engine");

        // The army layer can't field crew on their own, nor more than 4 engines.
        Assert.Throws<DataFormatException>(() =>
            Plan(map, Regiment("c", "attacker", "crew", 8, 40_000, 45_000))
        );
        Assert.Throws<DataFormatException>(() =>
            Plan(map, Regiment("g", "attacker", "siege", 5, 40_000, 45_000))
        );
    }

    [Test]
    public void Decision_2026_11_07_ArmiesLoadLaunchesSpecialists_AnEngineCrawlsWithFewCrew_AndIsAbandonedAsAWreckWithNone()
    {
        // S2 2.1: full speed with at least 4 crew within 6 m, 300 permille with 1 to 3, abandoned with none (a wreck:
        // foot climb over at 300 permille, horse and wheeled closed; S2 13.2).
        var map = Flat();
        var battle = Load(map, 2, Regiment("g", "attacker", "siege", 1, 40_000, 45_000));
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        int siege = battle.Rules.UnitType("siege").Index;
        int crew = battle.Rules.UnitType("crew").Index;
        int engine = UnitsOf(world, 0, siege).Single();

        double Speed()
        {
            var from = world.Position[engine];
            simulation.Step(40);
            return (world.Position[engine] - from).Length.Raw / 65536.0 / 2.0; // m/s over 2 s
        }

        simulation.Step(100);
        double full = Speed();
        Assert.That(
            full,
            Is.EqualTo(0.72).Within(0.05),
            "an ox-drawn engine at the officer's pace"
        );

        KillCrew(world, 0, crew, 5);
        simulation.Step(60);
        double crawl = Speed();
        Assert.That(crawl, Is.EqualTo(full * 0.3).Within(0.05), "three crew: a crawl");

        var at = world.Position[engine];
        foreach (int u in UnitsOf(world, 0, crew))
        {
            world.DespawnUnit(world.Units.IdOf(u));
        }
        simulation.Step(TickOrder.RegimentAiRate.Period + 1);
        Assert.That(world.Units.IsAlive(engine), Is.False, "no crew left: abandoned");
        var abandoned = Events(world, CauseKind.EngineAbandoned);
        Assert.That(abandoned, Has.Count.EqualTo(1));
        Assert.That(world.Blockers.Count, Is.EqualTo(1));
        Assert.That(world.Blockers.Kind(0), Is.EqualTo(BlockerKind.Wreck));
        Assert.That(
            world.Blockers.Event(0),
            Is.EqualTo(world.Causes!.Events.ToList().IndexOf(abandoned[0]))
        );
        Assert.That(abandoned[0].Detail, Is.EqualTo(0), "the event names its wreck");
        var terrain = new Terrain(battle.PlayedMap);
        terrain.CellOf(at, out int x, out int y);
        Assert.That(world.Blockers.Cells(0), Does.Contain(terrain.Index(x, y)));

        // The navigation reads the wreck: horse and wheeled closed, foot slowed (S2 13.2).
        var navigation = new Navigation(battle.PlayedMap, battle.Rules, new SerialRunner());
        navigation.RebuildFromWorld(world, new SerialRunner());
        Assert.That(
            navigation.Costs.Cost(MovementClass.Horse, x, y),
            Is.EqualTo(CostFields.Impassable)
        );
        Assert.That(
            navigation.Costs.Cost(MovementClass.Wheeled, x, y),
            Is.EqualTo(CostFields.Impassable)
        );
        Assert.That(
            navigation.Costs.Cost(MovementClass.Foot, x, y),
            Is.Not.EqualTo(CostFields.Impassable)
        );
        Assert.That(
            FieldSpeed.Code(navigation.Terrain.PieceCodes(terrain.Index(x, y)), MovementClass.Foot),
            Is.EqualTo(FieldSpeed.CodeForPermille(300))
        );
    }

    [Test]
    public void Decision_2026_10_11_ManyWaysToDie_SiegeStonesCrushAndLaunch_TheCrushedTraceToTheStone()
    {
        // S2 6.4 (brief 2026-10-11: siege stones are combat; 2026-10-09: they launch): an engine with its crew throws at
        // an enemy between 60 and 250 m; soldiers within 1 m of where a stone lands die, crushed; those out to 4 m are
        // launched away from it by the siege-stone source.
        var map = Flat();
        var battle = Load(
            map,
            3,
            Regiment("g", "attacker", "siege", 4, 40_000, 45_000),
            Regiment("z", "defender", "infantry", 200, 190_000, 45_000)
        );
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        while (
            simulation.Tick < 180 * SimConstants.TicksPerSecond
            && Events(world, CauseKind.StoneLanded).Count < 12
        )
        {
            simulation.Step();
        }
        var landed = Events(world, CauseKind.StoneLanded);
        Assert.That(landed, Has.Count.GreaterThanOrEqualTo(12), "three salvos of four");
        Assert.That(landed.All(e => e.Subject == 0), Is.True);
        Assert.That(world.Army.Ammo[0], Is.EqualTo(4 * 30 - landed.Count));
        Assert.That(world.Army.Order[0], Is.EqualTo(RegimentOrder.Shoot));

        var events = world.Causes!.Events;
        var crushed = Events(world, CauseKind.SoldierCrushed);
        var stone = battle.Rules.Launches.Source("siege-stone")!;
        var launched = Events(world, CauseKind.SoldierLaunched)
            .Where(e => Ballistics.SourceIndexOf(e.Detail) == stone.Index)
            .ToList();
        Assert.That(crushed.Count + launched.Count, Is.GreaterThan(0), "the stones hit the block");
        foreach (var e in crushed)
        {
            Assert.That(events[e.Cause].Kind, Is.EqualTo(CauseKind.StoneLanded));
            Assert.That(world.RegimentSide[e.Subject], Is.EqualTo(Side.Defender));
        }
        foreach (var e in launched)
        {
            Assert.That(events[e.Cause].Kind, Is.EqualTo(CauseKind.StoneLanded));
        }
        Assert.That(
            launched.Select(e => Ballistics.SourceKeyOf(e.Detail) / CombatSystem.StoneKeyStride),
            Is.All.EqualTo(0),
            "keyed by the throwing regiment"
        );
    }

    [Test]
    public void Decision_2026_11_07_ArmiesLoadLaunchesSpecialists_AnEngineWithFewerThanFourCrew_ThrowsNoStone()
    {
        // S2 6.4: a stone needs at least 4 crew within 6 m of the engine.
        var map = Flat();
        var battle = Load(
            map,
            4,
            Regiment("g", "attacker", "siege", 1, 40_000, 45_000),
            Regiment("z", "defender", "infantry", 200, 190_000, 45_000)
        );
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        int crew = battle.Rules.UnitType("crew").Index;
        KillCrew(world, 0, crew, 5);
        simulation.Step(120 * SimConstants.TicksPerSecond);
        Assert.That(Events(world, CauseKind.StoneLanded), Is.Empty);
        Assert.That(world.Army.Ammo[0], Is.EqualTo(30));
    }

    [Test]
    public void Decision_2026_11_07_ArmiesLoadLaunchesSpecialists_ASmashedEngine_LeavesAWreck()
    {
        // S2 2.1, 13.2: melee hurts engines at 250 permille; at 0 structure the engine becomes a wreck stamped into
        // navigation, while its crew fight on.
        var map = Flat(columns: 100, zoneMetres: 70); // 150 m: zones 0-70 m and 80-150 m
        var battle = Load(
            map,
            5,
            Regiment("g", "attacker", "siege", 1, 65_000, 45_000),
            Regiment("z", "defender", "infantry", 100, 90_000, 45_000)
        );
        var simulation = battle.Start(new SerialRunner());
        var world = simulation.World;
        int engine = UnitsOf(world, 0, battle.Rules.UnitType("siege").Index).Single();
        world.Health[engine] = 1;
        while (simulation.Tick < 120 * SimConstants.TicksPerSecond && world.Units.IsAlive(engine))
        {
            simulation.Step();
        }
        var wrecked = Events(world, CauseKind.EngineWrecked);
        Assert.That(wrecked, Has.Count.EqualTo(1), "smashed in the melee");
        Assert.That(world.Blockers.Count, Is.EqualTo(1));
        Assert.That(world.Blockers.Kind(0), Is.EqualTo(BlockerKind.Wreck));
        Assert.That(
            world.Blockers.Event(0),
            Is.EqualTo(world.Causes!.Events.ToList().IndexOf(wrecked[0]))
        );
        Assert.That(
            Events(world, CauseKind.SoldierKilled).Any(e => e.Subject == 0 && e.Detail == 0),
            Is.False
        );
    }

    [Test]
    public void Decision_2026_11_07_ArmiesLoadLaunchesSpecialists_OpenOrderDoublesSpacing_CloseIsTheDefault()
    {
        // S2 2.2, D1: one order flag per regiment, set in the army layer; open order doubles file and rank spacing.
        var map = Flat();
        var plan = Plan(
            map,
            Regiment("a", "attacker", "infantry", 51, 40_000, 30_000, ",\"order\":\"open\""),
            Regiment("b", "attacker", "infantry", 51, 40_000, 60_000)
        );
        Assert.That(plan.Army[0].Open, Is.True);
        Assert.That(plan.Army[1].Open, Is.False);
        var world = BattleSetup.Deploy(map, plan, RepoData.Rules());
        Assert.That(world.Army.Open[0], Is.True);
        Assert.That(world.Army.Open[1], Is.False);

        double RankGap(int r)
        {
            var units = TestBattles
                .LiveUnits(world)
                .Where(u => world.UnitRegiment[u] == r)
                .ToList();
            var officer = units.Single(u => world.FormationSlot[u] == 0);
            var first = units.Single(u => world.FormationSlot[u] == 1);
            var second = units.Single(u => world.FormationSlot[u] == 2);
            double rank = world.Position[officer].X() - world.Position[first].X();
            double file = System.Math.Abs(world.Position[first].Y() - world.Position[second].Y());
            return rank * 1000 + file;
        }
        Assert.That(RankGap(0), Is.EqualTo(2 * RankGap(1)).Within(0.01));

        Assert.Throws<DataFormatException>(() =>
            Plan(
                map,
                Regiment("a", "attacker", "infantry", 50, 40_000, 30_000, ",\"order\":\"loose\"")
            )
        );
    }

    [Test]
    public void Decision_2026_11_07_ArmiesLoadLaunchesSpecialists_InfantrySweepSooner_AndNoFootSoldierWalksIntoIt()
    {
        // S2 2.1: infantry lose footing at 900 permille of the foot class's depth x speed (540 mm.m/s instead of 600);
        // the foot codes sweep there too, so no foot soldier steers into water that would sweep an infantryman
        // (brief 2026-10-09).
        var rules = RepoData.Rules();
        var foot = rules.Movement(MovementClass.Foot);
        long depth = 500;
        long flow = 1_140; // 570 mm.m/s
        Assert.That(
            foot.Water.Sweeps(depth, flow, rules.UnitType("infantry").SweptDepthSpeedPermille),
            Is.True
        );
        Assert.That(
            foot.Water.Sweeps(depth, flow, rules.UnitType("ranged").SweptDepthSpeedPermille),
            Is.False
        );
        Assert.That(FieldSpeed.CodeFor(foot, depth, flow, 0), Is.EqualTo(FieldSpeed.Swept));
        Assert.That(FieldSpeed.CodeFor(foot, depth, 1_000, 0), Is.Not.EqualTo(FieldSpeed.Swept));
    }

    [Test]
    public void Decision_2026_11_07_ArmiesLoadLaunchesSpecialists_CavalryRegimentsAre30To120()
    {
        var map = Flat();
        Assert.That(
            Plan(map, Regiment("c", "attacker", "cavalry", 120, 40_000, 45_000)).Army,
            Has.Count.EqualTo(1)
        );
        Assert.That(
            Plan(map, Regiment("c", "attacker", "cavalry", 30, 40_000, 45_000)).Army,
            Has.Count.EqualTo(1)
        );
        Assert.Throws<DataFormatException>(() =>
            Plan(map, Regiment("c", "attacker", "cavalry", 121, 40_000, 45_000))
        );
        Assert.Throws<DataFormatException>(() =>
            Plan(map, Regiment("c", "attacker", "cavalry", 29, 40_000, 45_000))
        );
    }

    [Test]
    public void Decision_2026_11_07_ArmiesLoadLaunchesSpecialists_ThePontoonTrainsBoatsHaveCrews()
    {
        // S2 14.2, D4: 4 to 8 boats with 5 crew each; the boats carry the segments, so dead crew cost none of them.
        var map = Flat();
        var battle = Load(map, 6, Regiment("p", "attacker", "pontoon-train", 4, 40_000, 45_000));
        var world = battle.Start(new SerialRunner()).World;
        Assert.That(
            UnitsOf(world, 0, battle.Rules.UnitType("pontoon-train").Index),
            Has.Count.EqualTo(4)
        );
        Assert.That(UnitsOf(world, 0, battle.Rules.UnitType("crew").Index), Has.Count.EqualTo(20));
        Assert.That(world.RegimentStartingSize[0], Is.EqualTo(24));
    }

    [Test]
    public void EnginesStonesAndWrecks_AreTheSameOnAnyThreadCount_AndAcrossARestore()
    {
        // Two engines throw at a block while a third, its crew killed off, is abandoned mid-run: thread counts and a
        // restore (taken after the wreck) must give the same hashes, wreck stamps included.
        var map = Flat();
        var battle = Load(
            map,
            7,
            Regiment("g", "attacker", "siege", 2, 40_000, 30_000),
            Regiment("h", "attacker", "siege", 1, 40_000, 60_000),
            Regiment("z", "defender", "infantry", 200, 190_000, 45_000)
        );
        int crew = battle.Rules.UnitType("crew").Index;

        Simulation Run(IParallelRunner runner, int ticks)
        {
            var s = battle.Start(runner);
            for (int t = 0; t < ticks; t++)
            {
                if (s.Tick == 100)
                {
                    foreach (int u in UnitsOf(s.World, 1, crew))
                    {
                        s.World.DespawnUnit(s.World.Units.IdOf(u));
                    }
                }
                s.Step();
            }
            return s;
        }

        int length = 150 * SimConstants.TicksPerSecond;
        var serial = Run(new SerialRunner(), length);
        Assert.That(serial.World.Blockers.Count, Is.EqualTo(1), "the third engine was abandoned");
        Assert.That(Events(serial.World, CauseKind.StoneLanded), Is.Not.Empty);
        Assert.That(Run(new ThreadedRunner(4), length).StateHash(), Is.EqualTo(serial.StateHash()));

        var original = Run(new ThreadedRunner(4), 60 * SimConstants.TicksPerSecond);
        byte[] snapshot = original.CaptureSnapshot();
        original.Step(length - original.Tick);
        var restored = battle.Start(new ThreadedRunner(3));
        restored.Step(17);
        restored.RestoreSnapshot(snapshot);
        restored.Step(length - restored.Tick);
        Assert.That(restored.StateHash(), Is.EqualTo(original.StateHash()));
        Assert.That(original.StateHash(), Is.EqualTo(serial.StateHash()));
    }
}
