using System.Diagnostics;
using System.Globalization;
using GodGame.Sim;
using GodGame.Sim.Hosting;
using GodGame.Sim.Rules;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim.Cli;

/// <summary>
/// godsim: the headless runner (plan, section 3.2). It builds battles from rules, a map, a plan and a seed (or from
/// a replay), runs them on the serial or threaded runner, and writes and compares per-second state hashes.
/// </summary>
public static class Program
{
    private const string Usage = """
        godsim <command> [options]

        Commands:
          version                 Print SimVersion.
          run                     Run a battle and print a summary with the speed in multiples of real time.
          hash                    Run a battle and write one state hash per simulated second (CSV).
          compare <a> <b>         Compare two hash files; report the first second that differs.
          compare --replay <file> Re-run a replay and compare with the hashes it recorded.
          compare --golden <name> Re-run one golden replay (sim/tests/golden) and compare.
          settle --map <dir>      Run the map's water alone (--seconds, default 600) and write <dir>/water.bin, the
                                  settled rivers every battle on it starts with (spec S1, section 7).
          golden list|verify      List the goldens, or verify them all (exit 2 on any divergence).
          golden add <name>       Record a new golden from the battle options below.
          golden regenerate --all|<name>
                                  Rewrite goldens with this build. Only /golden-replay does this, after it bumps
                                  SimVersion and writes sim/CHANGELOG-sim.md.

        Battle options (run, hash):
          --rules <dir>           Rules folder (default data/rules).
          --map <dir>             Map folder holding map.json and an optional height.bin (default data/maps/m1-flat).
          --plan <file>           Plan (default data/plans/m1-ten-thousand.json).
          --replay <file>         Take rules hash, map, plan and seed from a replay instead.
          --seed <n>              Battle seed (default 1).
          --seconds <n>           Simulated seconds (default 180; a replay's own length when --replay is given).
          --threads <n>           1 runs serially (default); more uses the threaded runner. Results never change.
          --out <file>            hash: write here instead of standard output.
          --save-replay <file>    run: save a replay with its per-second hashes.

        Paths are relative to the current folder; the defaults are found by walking up to the repository root.
        """;

    public static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
        {
            Console.WriteLine(Usage);
            return args.Length == 0 ? 1 : 0;
        }

        try
        {
            switch (args[0])
            {
                case "version":
                    Console.WriteLine(SimVersion.Current.ToString(CultureInfo.InvariantCulture));
                    return 0;
                case "run":
                    return Run(Options.Parse(args.Skip(1).ToArray()));
                case "hash":
                    return Hash(Options.Parse(args.Skip(1).ToArray()));
                case "compare":
                    return Compare(args.Skip(1).ToArray());
                case "golden":
                    return Golden(args.Skip(1).ToArray());
                case "settle":
                    return Settle(Options.Parse(args.Skip(1).ToArray()));
                default:
                    Console.Error.WriteLine($"Unknown command '{args[0]}'.\n\n{Usage}");
                    return 1;
            }
        }
        catch (Exception e)
            when (e is ArgumentException or DataFormatException or FormatException or IOException)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }

    /// <summary>
    /// Settles a map's water offline: water and ground alone, from dry, for the given seconds; then writes every water
    /// and ground field as the map's starting layer. Overwrites any water.bin already there.
    /// </summary>
    private static int Settle(Options options)
    {
        string root = Options.RepoRoot();
        string dir = options.MapDir ?? throw new ArgumentException("settle needs --map <dir>.");
        var rules = Options.LoadRules(options.RulesDir ?? Path.Combine(root, "data", "rules"));
        string height = Path.Combine(dir, "height.bin");
        var map = MapData.Load(
            File.ReadAllText(Path.Combine(dir, "map.json")),
            File.Exists(height) ? File.ReadAllBytes(height) : null
        );
        int seconds = options.Seconds ?? 600;
        var terrain = new Terrain(map);
        var world = new World(1, 1);
        world.AttachFields(new FieldStore(rules.Fields, map.Columns, map.Rows));
        var water = new WaterModel(terrain, map, rules.Fields.Water!, TickOrder.FieldsRate);
        water.WakeSources(world.Fields!);
        var systems = TickOrder.CreateDefault();
        systems[(int)TickStep.Fields] = new FieldSystem(
            new IFieldModel[] { water, new GroundModel(terrain, map, rules, TickOrder.FieldsRate) }
        );
        var simulation = new Simulation(world, 1, options.Runner(), systems);
        simulation.Step(seconds * SimConstants.TicksPerSecond);
        var layer = new List<KeyValuePair<string, int[]>>();
        foreach (
            string id in new[]
            {
                WaterModel.Depth,
                WaterModel.OutEast,
                WaterModel.OutNorth,
                WaterModel.OutWest,
                WaterModel.OutSouth,
                GroundModel.Wetness,
                GroundModel.Mud,
            }
        )
        {
            layer.Add(new KeyValuePair<string, int[]>(id, (int[])world.Fields!.Get(id).Values.Clone()));
        }
        File.WriteAllBytes(Path.Combine(dir, "water.bin"), MapData.WriteFieldLayer(layer));
        long total = world.Fields!.Get(WaterModel.Depth).Values.Sum(v => (long)v);
        Console.WriteLine(
            $"settled {map.Id} for {seconds} s: {total / 1_000_000.0 * map.CellSizeMm * map.CellSizeMm / 1_000_000.0:0} m³ of water on the map"
        );
        return 0;
    }

    private static int Run(Options options)
    {
        var battle = options.LoadBattle(out int seconds);
        var simulation = battle.Start(options.Runner());
        int ticks = seconds * SimConstants.TicksPerSecond;
        var clock = Stopwatch.StartNew();
        ulong[] hashes = simulation.RunAndHash(ticks, SimConstants.TicksPerSecond);
        clock.Stop();
        double realTime =
            clock.Elapsed.TotalSeconds > 0
                ? seconds / clock.Elapsed.TotalSeconds
                : double.PositiveInfinity;
        double msPerTick = ticks > 0 ? clock.Elapsed.TotalMilliseconds / ticks : 0;
        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"SimVersion {SimVersion.Current}; map {battle.Map.Id}; seed {battle.Seed}; "
                    + $"{simulation.World.Units.AliveCount} units; {seconds} s ({simulation.Tick} ticks) on "
                    + $"{options.Threads} thread(s) in {clock.Elapsed.TotalSeconds:0.00} s: {realTime:0.0}x real time, "
                    + $"{msPerTick:0.000} ms/tick; final hash {Replay.Hex(simulation.StateHash())}"
            )
        );
        if (battle.Rules.HasPlaceholders)
        {
            Console.WriteLine(
                "note: the rules contain placeholder values (status \"placeholder\")."
            );
        }
        if (options.SaveReplay is not null)
        {
            File.WriteAllText(options.SaveReplay, battle.ToReplay(seconds, hashes).ToJson());
            Console.WriteLine($"replay saved to {options.SaveReplay}");
        }
        return 0;
    }

    private static int Hash(Options options)
    {
        var battle = options.LoadBattle(out int seconds);
        ulong[] hashes = battle.PerSecondHashes(seconds, options.Runner());
        using TextWriter writer = options.Out is null ? Console.Out : new StreamWriter(options.Out);
        writer.NewLine = "\n";
        writer.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"# godsim hash; SimVersion {SimVersion.Current}; rules {Replay.Hex(battle.Rules.Hash)}; "
                    + $"map {battle.Map.Id} {Replay.Hex(battle.Map.Hash)}; plan {Replay.Hex(battle.Plan.Hash)}; seed {battle.Seed}"
            )
        );
        writer.WriteLine("second,tick,hash");
        for (int i = 0; i < hashes.Length; i++)
        {
            writer.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{i + 1},{(i + 1) * SimConstants.TicksPerSecond},{hashes[i]:x16}"
                )
            );
        }
        return 0;
    }

    private static int Compare(string[] args)
    {
        if (args.Length >= 2 && args[0] == "--replay")
        {
            var options = Options.Parse(args);
            var battle = options.LoadBattle(out int seconds);
            var recorded = Replay.Parse(File.ReadAllText(args[1])).PerSecondHashes;
            if (recorded.Count == 0)
            {
                Console.Error.WriteLine("The replay holds no hashes to compare with.");
                return 1;
            }
            return Report(
                recorded.ToArray(),
                battle.PerSecondHashes(seconds, options.Runner()),
                "replay",
                "this build"
            );
        }
        if (args.Length >= 2 && args[0] == "--golden")
        {
            var goldens = new Goldens(RepoFiles.FindRoot());
            var options = Options.Parse(args.Skip(2).ToArray());
            var result = goldens.Verify(args[1], options.Runner());
            Console.WriteLine(result);
            return result.Passed ? 0 : 2;
        }
        if (args.Length != 2)
        {
            throw new ArgumentException(
                "compare needs two hash files, --replay <file> or --golden <name>."
            );
        }
        var (headerA, hashesA) = ReadHashFile(args[0]);
        var (headerB, hashesB) = ReadHashFile(args[1]);
        if (!string.Equals(headerA, headerB, StringComparison.Ordinal))
        {
            Console.WriteLine(
                $"DIFFER in setup: the files come from different builds or battles\n  {args[0]}: {headerA}\n  {args[1]}: {headerB}"
            );
            return 2;
        }
        return Report(hashesA, hashesB, args[0], args[1]);
    }

    private static int Golden(string[] args)
    {
        var goldens = new Goldens(RepoFiles.FindRoot());
        string verb = args.Length > 0 ? args[0] : "verify";
        switch (verb)
        {
            case "list":
                foreach (string name in goldens.Names())
                {
                    Console.WriteLine(name);
                }
                return 0;
            case "verify":
            {
                var options = Options.Parse(args.Skip(1).ToArray());
                int failures = 0;
                foreach (string name in goldens.Names())
                {
                    var result = goldens.Verify(name, options.Runner());
                    Console.WriteLine(result);
                    failures += result.Passed ? 0 : 1;
                }
                return failures == 0 ? 0 : 2;
            }
            case "add":
            {
                if (args.Length < 2)
                {
                    throw new ArgumentException("golden add <name> [battle options]");
                }
                var options = Options.Parse(args.Skip(2).ToArray());
                var battle = options.LoadBattle(out int seconds);
                goldens.Add(args[1], battle, seconds, options.Runner());
                Console.WriteLine($"added {goldens.PathOf(args[1])}");
                return 0;
            }
            case "regenerate":
            {
                if (args.Length < 2)
                {
                    throw new ArgumentException("golden regenerate --all | <name>");
                }
                var options = Options.Parse(args.Skip(2).ToArray());
                var names = args[1] == "--all" ? goldens.Names() : new[] { args[1] };
                foreach (string name in names)
                {
                    goldens.Regenerate(name, options.Runner());
                    Console.WriteLine($"regenerated {name} (SimVersion {SimVersion.Current})");
                }
                return 0;
            }
            default:
                throw new ArgumentException($"Unknown golden command '{verb}'.");
        }
    }

    /// <summary>0 when identical; otherwise prints the first differing second and returns 2.</summary>
    private static int Report(ulong[] a, ulong[] b, string nameA, string nameB)
    {
        int shared = System.Math.Min(a.Length, b.Length);
        for (int i = 0; i < shared; i++)
        {
            if (a[i] != b[i])
            {
                Console.WriteLine(
                    $"DIFFER at second {i + 1} (tick {(i + 1) * SimConstants.TicksPerSecond}): "
                        + $"{nameA} {a[i]:x16}, {nameB} {b[i]:x16}"
                );
                return 2;
            }
        }
        if (a.Length != b.Length)
        {
            Console.WriteLine(
                $"DIFFER in length: {nameA} has {a.Length} seconds, {nameB} {b.Length}; the first {shared} match."
            );
            return 2;
        }
        Console.WriteLine($"identical: {shared} seconds");
        return 0;
    }

    /// <summary>The '#' header (SimVersion, rules, map, plan, seed) and the hashes, checking seconds count 1, 2, 3...</summary>
    private static (string Header, ulong[] Hashes) ReadHashFile(string path)
    {
        string header = "";
        var hashes = new List<ulong>();
        foreach (string line in File.ReadAllLines(path))
        {
            if (line.Length == 0 || line.StartsWith("second,", StringComparison.Ordinal))
            {
                continue;
            }
            if (line[0] == '#')
            {
                header += line;
                continue;
            }
            string[] parts = line.Split(',');
            if (
                parts.Length != 3
                || int.Parse(parts[0], CultureInfo.InvariantCulture) != hashes.Count + 1
            )
            {
                throw new FormatException(
                    $"{path}: expected second,tick,hash counting from 1 but read '{line}'."
                );
            }
            hashes.Add(ulong.Parse(parts[2], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }
        return (header, hashes.ToArray());
    }

    private sealed class Options
    {
        public string? RulesDir { get; private set; }
        public string? MapDir { get; private set; }
        public string? PlanFile { get; private set; }
        public string? ReplayFile { get; private set; }
        public const int MaxSeconds = 24 * 60 * 60;

        public ulong Seed { get; private set; } = 1;
        public bool SeedGiven { get; private set; }
        public int? Seconds { get; private set; }
        public int Threads { get; private set; } = 1;
        public string? Out { get; private set; }
        public string? SaveReplay { get; private set; }

        public IParallelRunner Runner() =>
            Threads == 1 ? new SerialRunner() : new ThreadedRunner(Threads);

        public Battle LoadBattle(out int seconds)
        {
            string root = RepoRoot();
            var rules = LoadRules(RulesDir ?? Path.Combine(root, "data", "rules"));
            if (ReplayFile is not null)
            {
                var replay = Replay.Parse(File.ReadAllText(ReplayFile));
                var replayMap = LoadMap(MapDir ?? Path.Combine(root, "data", "maps", replay.MapId));
                seconds = Seconds ?? replay.Seconds;
                return replay.ToBattle(rules, replayMap);
            }
            var map = LoadMap(MapDir ?? Path.Combine(root, "data", "maps", "m1-flat"));
            var plan = BattlePlan.Load(
                File.ReadAllText(
                    PlanFile ?? Path.Combine(root, "data", "plans", "m1-ten-thousand.json")
                ),
                map,
                rules
            );
            seconds = Seconds ?? 180;
            return new Battle(rules, map, plan, Seed);
        }

        public static Ruleset LoadRules(string dir) => RepoFiles.LoadRules(dir);

        public static MapData LoadMap(string dir) => RepoFiles.LoadMap(dir);

        public static string RepoRoot() => RepoFiles.FindRoot();

        public static Options Parse(string[] args)
        {
            var options = new Options();
            for (int i = 0; i < args.Length; i++)
            {
                string Value() =>
                    i + 1 < args.Length
                        ? args[++i]
                        : throw new ArgumentException($"{args[i]} needs a value.");

                switch (args[i])
                {
                    case "--rules":
                        options.RulesDir = Value();
                        break;
                    case "--map":
                        options.MapDir = Value();
                        break;
                    case "--plan":
                        options.PlanFile = Value();
                        break;
                    case "--replay":
                        options.ReplayFile = Value();
                        break;
                    case "--seed":
                        options.Seed = ulong.Parse(Value(), CultureInfo.InvariantCulture);
                        options.SeedGiven = true;
                        break;
                    case "--seconds":
                        options.Seconds = int.Parse(Value(), CultureInfo.InvariantCulture);
                        if (options.Seconds < 1 || options.Seconds > MaxSeconds)
                        {
                            throw new ArgumentException("--seconds must be 1 to 86,400.");
                        }
                        break;
                    case "--threads":
                        options.Threads = int.Parse(Value(), CultureInfo.InvariantCulture);
                        if (options.Threads < 1)
                        {
                            throw new ArgumentException("--threads must be at least 1.");
                        }
                        break;
                    case "--out":
                        options.Out = Value();
                        break;
                    case "--save-replay":
                        options.SaveReplay = Value();
                        break;
                    default:
                        throw new ArgumentException($"Unknown option '{args[i]}'.");
                }
            }
            if (
                options.ReplayFile is not null
                && (options.SeedGiven || options.PlanFile is not null)
            )
            {
                throw new ArgumentException(
                    "--replay brings its own seed and plan; drop --seed and --plan."
                );
            }
            return options;
        }
    }
}
