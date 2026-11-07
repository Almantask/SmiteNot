using System.Globalization;
using GodGame.Sim.Hosting;
using GodGame.Sim.Rules;

namespace GodGame.Sim.Scenarios;

/// <summary>
/// The M3 exit invariant (plan, section 9; brief, 2026-10-09): no soldier enters a lethal cell under its own steering.
/// Random storms (place, time, size; seeded) over the m3-exit valley while 600 attackers march for its ford; after every
/// tick, no soldier that wasn't swept may stand in a cell whose water sweeps its class when the cell it came from didn't,
/// unless the crowd pushed him over after teetering. Lethal is read from the speed codes movement steers by.
/// </summary>
[TestFixture, Category("Fuzz")]
public class HazardFuzz
{
    [TestCase(1)]
    [TestCase(2)]
    [TestCase(3)]
    [TestCase(4)]
    public void NoSoldierWalksIntoLethalWater(int seed)
    {
        string root = RepoFiles.FindRoot(TestContext.CurrentContext.TestDirectory);
        var rules = RepoFiles.LoadRepoRules(root);
        var map = RepoFiles.LoadRepoMap(root, "m3-exit");
        string plan = File.ReadAllText(Path.Combine(root, "data", "plans", "m3-exit-dry.json"));
        var random = new System.Random(seed); // test-side only: picks the plan, never runs inside the simulation
        var casts = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            int radius = random.Next(40, 101) * 1000;
            casts.Add(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{{\"id\":\"s{0}\",\"power\":\"rain\",\"tick\":{1},\"xMm\":{2},\"yMm\":{3},\"radiusMm\":{4}}}",
                    i,
                    random.Next(0, 2_400) / 4 * 4,
                    random.Next(radius, 480_000 - radius),
                    random.Next(radius, 600_000 - radius),
                    radius
                )
            );
        }
        plan = plan.Replace("\"casts\": []", "\"casts\": [" + string.Join(",", casts) + "]");
        var simulation = new Battle(rules, map, BattlePlan.Load(plan, map, rules), 1).Start(
            new ThreadedRunner(4)
        );
        var world = simulation.World;
        var terrain = new Terrain(map);
        var codes = world.Fields!.Get(FieldSpeed.FieldId);
        var before = new GodGame.Sim.Math.FixVec2[world.Units.Capacity];
        var wasDown = new bool[world.Units.Capacity];
        var wasPushed = new bool[world.Units.Capacity];
        int violations = 0;
        string first = "";

        for (int tick = 0; tick < 240 * SimConstants.TicksPerSecond; tick++)
        {
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                before[u] = world.Position[u];
                wasDown[u] = (world.Flags[u] & UnitFlags.Swept) != 0;
                wasPushed[u] =
                    (world.Flags[u] & UnitFlags.Teetering) != 0 && world.HazardTimer[u] <= 1;
            }
            simulation.Step();
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (!world.Units.IsAlive(u) || wasDown[u] || wasPushed[u])
                {
                    continue;
                }
                var movementClass = rules.UnitTypes[world.UnitType[u]].MovementClass;
                terrain.CellOf(before[u], out int ox, out int oy);
                terrain.CellOf(world.Position[u], out int nx, out int ny);
                bool lethalNow =
                    FieldSpeed.Code(codes.Get(nx, ny), movementClass) == FieldSpeed.Swept;
                bool lethalBefore =
                    FieldSpeed.Code(codes.Get(ox, oy), movementClass) == FieldSpeed.Swept;
                if (lethalNow && !lethalBefore)
                {
                    if (violations++ == 0)
                    {
                        first = $"unit {u} walked from ({ox},{oy}) into ({nx},{ny}) at tick {tick}";
                    }
                }
            }
        }

        TestContext.Out.WriteLine(
            $"seed {seed}: swept {world.Counters.Swept}, drowned {world.Counters.Drowned}"
        );
        Assert.That(violations, Is.Zero, first);
    }
}
