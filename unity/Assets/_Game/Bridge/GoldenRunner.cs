using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GodGame.Sim;
using GodGame.Sim.Rules;
using UnityEngine;

namespace GodGame.Bridge
{
    /// <summary>
    /// Replays the golden battles (sim/tests/golden/*.godreplay) inside Unity, so the same hashes are checked under the
    /// editor's Mono and the IL2CPP player (plan, M1: the determinism harness). It reads the repository's data and
    /// goldens straight from disk; no gameplay happens here, only loading and comparing.
    /// </summary>
    public static class GoldenRunner
    {
        public const string RunArgument = "-run-goldens";
        public const string RepoArgument = "-repo";
        public const string ResultsArgument = "-golden-results";

        /// <summary>The repository root seen from the Unity project (unity/Assets → two levels up).</summary>
        public static string EditorRepoRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));

        public static IReadOnlyList<string> GoldenNames(string repoRoot) =>
            Directory
                .GetFiles(Path.Combine(repoRoot, "sim", "tests", "golden"), "*.godreplay")
                .Select(f => Path.GetFileNameWithoutExtension(f))
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

        /// <summary>Replays one golden on one thread; returns null when it matches, otherwise what differed.</summary>
        public static string Verify(string repoRoot, string name)
        {
            var replay = Replay.Parse(
                File.ReadAllText(
                    Path.Combine(repoRoot, "sim", "tests", "golden", name + ".godreplay")
                )
            );
            var rules = LoadRules(Path.Combine(repoRoot, "data", "rules"));
            var map = LoadMap(Path.Combine(repoRoot, "data", "maps", replay.MapId));
            var battle = replay.ToBattle(rules, map);
            ulong[] actual = battle.PerSecondHashes(replay.Seconds, new SerialRunner());
            for (int i = 0; i < actual.Length; i++)
            {
                if (i >= replay.PerSecondHashes.Count || actual[i] != replay.PerSecondHashes[i])
                {
                    return string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}: second {1} differs (golden {2}, {3} {4})",
                        name,
                        i + 1,
                        i < replay.PerSecondHashes.Count
                            ? Replay.Hex(replay.PerSecondHashes[i])
                            : "missing",
                        Application.isEditor ? "editor" : "player",
                        Replay.Hex(actual[i])
                    );
                }
            }
            return null;
        }

        private static Ruleset LoadRules(string dir) =>
            RulesetLoader.Load(
                Directory
                    .GetFiles(dir, "*.json")
                    .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal)
                    .Select(f => new KeyValuePair<string, string>(
                        Path.GetFileName(f),
                        File.ReadAllText(f)
                    ))
                    .ToList()
            );

        private static MapData LoadMap(string dir)
        {
            string height = Path.Combine(dir, "height.bin");
            return MapData.Load(
                File.ReadAllText(Path.Combine(dir, "map.json")),
                File.Exists(height) ? File.ReadAllBytes(height) : null
            );
        }

        /// <summary>
        /// Player entry point: <c>Game.exe -batchmode -run-goldens -repo &lt;repo root&gt; -golden-results &lt;file&gt;</c>
        /// replays every golden, writes one line per golden, and quits with 0 when all match.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RunFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (Array.IndexOf(args, RunArgument) < 0)
            {
                return;
            }
            string repo = ValueAfter(args, RepoArgument) ?? EditorRepoRoot;
            string resultsPath = ValueAfter(args, ResultsArgument);
            var report = new StringBuilder();
            report
                .Append("runtime ")
                .Append(Application.isEditor ? "editor" : "player")
                .Append(" scripting ")
                .Append(ScriptingBackendName())
                .Append('\n');
            int failures = 0;
            try
            {
                foreach (string name in GoldenNames(repo))
                {
                    string problem = Verify(repo, name);
                    report
                        .Append(problem == null ? "ok   " + name : "FAIL " + problem)
                        .Append('\n');
                    if (problem != null)
                    {
                        failures++;
                    }
                }
            }
            catch (Exception e)
            {
                report
                    .Append("ERROR ")
                    .Append(e.GetType().Name)
                    .Append(": ")
                    .Append(e.Message)
                    .Append('\n');
                failures++;
            }
            if (resultsPath != null)
            {
                File.WriteAllText(resultsPath, report.ToString());
            }
            Debug.Log(report.ToString());
            Application.Quit(failures == 0 ? 0 : 1);
        }

        private static string ScriptingBackendName()
        {
#if ENABLE_IL2CPP
            return "IL2CPP";
#else
            return "Mono";
#endif
        }

        private static string ValueAfter(string[] args, string flag)
        {
            int i = Array.IndexOf(args, flag);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }
    }
}
