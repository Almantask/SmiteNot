using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GodGame.Sim;
using GodGame.Sim.Rules;
using UnityEngine;

namespace GodGame.Bridge
{
    /// <summary>
    /// Reads the repository's rules, maps and plans from disk for Unity (the editor, tests and development players).
    /// The same order and rules as the .NET hosts' RepoFiles: every rules file ending exactly in ".json", in ordinal
    /// file-name order. Shipping builds will read packaged data instead (M8).
    /// </summary>
    public static class RepoLoader
    {
        /// <summary>The repository root seen from the Unity project (unity/Assets → two levels up).</summary>
        public static string EditorRepoRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        public static Ruleset LoadRules(string repoRoot) =>
            RulesetLoader.Load(
                Directory
                    .GetFiles(Path.Combine(repoRoot, "data", "rules"))
                    .Where(f => f.EndsWith(".json", StringComparison.Ordinal))
                    .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal)
                    .Select(f => new KeyValuePair<string, string>(
                        Path.GetFileName(f),
                        File.ReadAllText(f)
                    ))
                    .ToList()
            );

        public static MapData LoadMap(string repoRoot, string mapId)
        {
            string dir = Path.Combine(repoRoot, "data", "maps", mapId);
            string height = Path.Combine(dir, "height.bin");
            string water = Path.Combine(dir, "water.bin");
            return MapData.Load(
                File.ReadAllText(Path.Combine(dir, "map.json")),
                File.Exists(height) ? File.ReadAllBytes(height) : null,
                File.Exists(water) ? File.ReadAllBytes(water) : null
            );
        }

        /// <summary>A plan file under data/plans, e.g. "m2-bridge-ten-thousand.json".</summary>
        public static BattlePlan LoadPlan(
            string repoRoot,
            string planFile,
            MapData map,
            Ruleset rules
        ) =>
            BattlePlan.Load(
                File.ReadAllText(Path.Combine(repoRoot, "data", "plans", planFile)),
                map,
                rules
            );
    }
}
