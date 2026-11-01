using System.Globalization;

namespace GodGame.Sim.Hosting;

/// <summary>What checking one golden found.</summary>
public sealed record GoldenResult(string Name, bool Passed, string Detail)
{
    public override string ToString() => (Passed ? "ok   " : "FAIL ") + Name + ": " + Detail;
}

/// <summary>
/// Golden replays (plan, section 3.8): each scenario is a <c>.godreplay</c> in sim/tests/golden/ holding its plan,
/// seed, length and per-second state hashes. Verifying rebuilds the battle from the replay with the current rules and
/// map and compares every second. Regenerating rewrites the files and is the golden-replay skill's job alone: it
/// bumps SimVersion and logs the reason in sim/CHANGELOG-sim.md first.
/// </summary>
public sealed class Goldens
{
    public const string Extension = ".godreplay";

    public Goldens(string repoRoot)
    {
        Root = repoRoot;
        Directory = Path.Combine(repoRoot, "sim", "tests", "golden");
    }

    public string Root { get; }

    public string Directory { get; }

    /// <summary>Scenario names in ordinal order.</summary>
    public IReadOnlyList<string> Names() =>
        System
            .IO.Directory.GetFiles(Directory, "*" + Extension)
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

    public string PathOf(string name) => Path.Combine(Directory, name + Extension);

    public Replay Load(string name) => Replay.Parse(File.ReadAllText(PathOf(name)));

    public GoldenResult Verify(string name, IParallelRunner runner)
    {
        var replay = Load(name);
        var rules = RepoFiles.LoadRepoRules(Root);
        var map = RepoFiles.LoadRepoMap(Root, replay.MapId);
        Battle battle;
        try
        {
            battle = replay.ToBattle(rules, map);
        }
        catch (Rules.Json.DataFormatException e)
        {
            return new GoldenResult(name, false, e.Message);
        }
        if (replay.PerSecondHashes.Count != replay.Seconds || replay.Seconds == 0)
        {
            return new GoldenResult(name, false, "the golden holds no per-second hashes");
        }
        ulong[] actual = battle.PerSecondHashes(replay.Seconds, runner);
        int diverged = FirstDifference(replay.PerSecondHashes, actual);
        return diverged < 0
            ? new GoldenResult(name, true, Seconds(replay.Seconds) + " match")
            : new GoldenResult(
                name,
                false,
                "first divergence at second "
                    + (diverged + 1).ToString(CultureInfo.InvariantCulture)
                    + " (tick "
                    + ((diverged + 1) * SimConstants.TicksPerSecond).ToString(
                        CultureInfo.InvariantCulture
                    )
                    + "): golden "
                    + Replay.Hex(replay.PerSecondHashes[diverged])
                    + ", now "
                    + Replay.Hex(actual[diverged])
            );
    }

    /// <summary>
    /// Re-runs a golden's plan and seed with the current rules, map and SimVersion and rewrites the file. Only the
    /// golden-replay skill calls this, after it has bumped SimVersion and written the changelog line.
    /// </summary>
    public void Regenerate(string name, IParallelRunner runner)
    {
        var replay = Load(name);
        var rules = RepoFiles.LoadRepoRules(Root);
        var map = RepoFiles.LoadRepoMap(Root, replay.MapId);
        var battle = new Battle(
            rules,
            map,
            BattlePlan.Load(replay.PlanJson, map, rules),
            replay.Seed
        );
        Write(name, battle, replay.Seconds, runner);
    }

    /// <summary>Records a new golden scenario (the golden-replay skill's "add").</summary>
    public void Add(string name, Battle battle, int seconds, IParallelRunner runner)
    {
        Rules.Json.DataIds.Require(Rules.Json.JsonReader.Parse("\"" + name + "\"", "golden name"));
        if (File.Exists(PathOf(name)))
        {
            throw new ArgumentException("Golden " + name + " exists; regenerate it instead.");
        }
        Write(name, battle, seconds, runner);
    }

    private void Write(string name, Battle battle, int seconds, IParallelRunner runner)
    {
        ulong[] hashes = battle.PerSecondHashes(seconds, runner);
        File.WriteAllText(PathOf(name), battle.ToReplay(seconds, hashes).ToJson());
    }

    private static int FirstDifference(IReadOnlyList<ulong> expected, IReadOnlyList<ulong> actual)
    {
        int n = System.Math.Min(expected.Count, actual.Count);
        for (int i = 0; i < n; i++)
        {
            if (expected[i] != actual[i])
            {
                return i;
            }
        }
        return expected.Count == actual.Count ? -1 : n;
    }

    private static string Seconds(int n) => n.ToString(CultureInfo.InvariantCulture) + " seconds";
}
