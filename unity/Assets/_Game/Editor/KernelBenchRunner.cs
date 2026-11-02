using System;
using System.IO;
using GodGame.Benchmarks;
using GodGame.Bridge;
using UnityEditor;
using UnityEngine;

namespace GodGame.Editor
{
    /// <summary>
    /// Runs the Burst decision's benchmarks in the editor (Mono with Burst): GodGame → Benchmarks → Burst Decision, or
    /// batch: -executeMethod GodGame.Editor.KernelBenchRunner.RunFromCommandLine -bench-results &lt;file&gt;.
    /// The IL2CPP numbers come from the determinism player run with -run-bench &lt;file&gt;.
    /// </summary>
    public static class KernelBenchRunner
    {
        [MenuItem("GodGame/Benchmarks/Burst Decision")]
        public static void Run() => Debug.Log(KernelBench.Run(RepoLoader.EditorRepoRoot));

        public static void RunFromCommandLine()
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-bench-results");
            try
            {
                string report = KernelBench.Run(RepoLoader.EditorRepoRoot);
                if (at >= 0 && at + 1 < args.Length)
                {
                    File.WriteAllText(args[at + 1], report);
                }
                Debug.Log(report);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                EditorApplication.Exit(1);
            }
        }
    }
}
