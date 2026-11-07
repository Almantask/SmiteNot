using GodGame.Sim.Rules;

namespace GodGame.Sim.Hosting;

/// <summary>Reads rules, maps and plans from the repository's data/ folder for the CLI, the tests and the goldens.</summary>
public static class RepoFiles
{
    /// <summary>The nearest folder at or above <paramref name="start"/> (default: the current folder) that holds data/rules.</summary>
    public static string FindRoot(string? start = null)
    {
        var dir = new DirectoryInfo(start ?? Directory.GetCurrentDirectory());
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "data", "rules")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName
            ?? throw new DirectoryNotFoundException("No data/rules folder above " + (start ?? "."));
    }

    /// <summary>Every file ending exactly in ".json" (case matters on every OS), in ordinal file-name order.</summary>
    public static Ruleset LoadRules(string dir) =>
        RulesetLoader.Load(
            Directory
                .GetFiles(dir)
                .Where(f => f.EndsWith(".json", StringComparison.Ordinal))
                .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal)
                .Select(f => KeyValuePair.Create(Path.GetFileName(f), File.ReadAllText(f)))
                .ToList()
        );

    /// <summary>map.json plus height.bin when it exists.</summary>
    public static MapData LoadMap(string dir)
    {
        string height = Path.Combine(dir, "height.bin");
        string water = Path.Combine(dir, "water.bin");
        return MapData.Load(
            File.ReadAllText(Path.Combine(dir, "map.json")),
            File.Exists(height) ? File.ReadAllBytes(height) : null,
            File.Exists(water) ? File.ReadAllBytes(water) : null
        );
    }

    public static Ruleset LoadRepoRules(string root) =>
        LoadRules(Path.Combine(root, "data", "rules"));

    public static MapData LoadRepoMap(string root, string mapId) =>
        LoadMap(Path.Combine(root, "data", "maps", mapId));
}
