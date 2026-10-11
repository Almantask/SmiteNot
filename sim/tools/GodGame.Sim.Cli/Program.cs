using System.Globalization;
using GodGame.Sim;

namespace GodGame.Sim.Cli;

/// <summary>
/// godsim: the headless runner. M0 ships the placeholder commands; M1 adds maps, plans, replays and compare.
/// </summary>
public static class Program
{
    private const string Usage = """
        godsim <command> [options]

        Commands:
          version                 Print SimVersion.
          run                     Run the placeholder battle and print a summary.
          hash                    Run the placeholder battle and write one state hash per simulated second.

        Options (run, hash):
          --seed <n>              Battle seed (default 1).
          --units <n>             Unit count (default 1000).
          --seconds <n>           Simulated seconds (default 180).
          --out <file>            hash only: write to this file instead of standard output.
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
            var options = Options.Parse(args.Skip(1).ToArray());
            switch (args[0])
            {
                case "version":
                    Console.WriteLine(SimVersion.Current.ToString(CultureInfo.InvariantCulture));
                    return 0;
                case "run":
                    return Run(options);
                case "hash":
                    return Hash(options);
                default:
                    Console.Error.WriteLine($"Unknown command '{args[0]}'.\n\n{Usage}");
                    return 1;
            }
        }
        catch (ArgumentException e)
        {
            Console.Error.WriteLine(e.Message);
            return 1;
        }
    }

    private static int Run(Options options)
    {
        var battle = new PlaceholderBattle(options.Setup());
        battle.Step(options.Seconds * SimConstants.TicksPerSecond);
        Console.WriteLine(
            $"SimVersion {SimVersion.Current}; seed {options.Seed}; {options.Units} units; "
                + $"{options.Seconds} s ({battle.Tick} ticks); final hash {battle.StateHash():x16}"
        );
        return 0;
    }

    private static int Hash(Options options)
    {
        var battle = new PlaceholderBattle(options.Setup());
        using TextWriter writer = options.Out is null ? Console.Out : new StreamWriter(options.Out);
        writer.NewLine = "\n";
        writer.WriteLine(
            $"# godsim hash; SimVersion {SimVersion.Current}; seed {options.Seed}; units {options.Units}"
        );
        writer.WriteLine("second,tick,hash");
        for (int second = 1; second <= options.Seconds; second++)
        {
            battle.Step(SimConstants.TicksPerSecond);
            writer.WriteLine($"{second},{battle.Tick},{battle.StateHash():x16}");
        }
        return 0;
    }

    private sealed class Options
    {
        public ulong Seed { get; private set; } = 1;
        public int Units { get; private set; } = 1000;
        public int Seconds { get; private set; } = 180;
        public string? Out { get; private set; }

        public PlaceholderSetup Setup() => new(seed: Seed, unitCount: Units);

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
                    case "--seed":
                        options.Seed = ulong.Parse(Value(), CultureInfo.InvariantCulture);
                        break;
                    case "--units":
                        options.Units = int.Parse(Value(), CultureInfo.InvariantCulture);
                        break;
                    case "--seconds":
                        options.Seconds = int.Parse(Value(), CultureInfo.InvariantCulture);
                        break;
                    case "--out":
                        options.Out = Value();
                        break;
                    default:
                        throw new ArgumentException($"Unknown option '{args[i]}'.");
                }
            }
            return options;
        }
    }
}
