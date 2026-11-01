using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GodGame.Editor
{
    /// <summary>
    /// Builds the Windows IL2CPP player the determinism harness runs (plan, M1): started with
    /// <c>-batchmode -run-goldens -repo &lt;repo&gt; -golden-results &lt;file&gt;</c>, it replays every golden and
    /// quits (see GodGame.Bridge.GoldenRunner). By hand now, nightly from M2.
    /// Menu: GodGame → Build → Determinism Player (IL2CPP).
    /// Batch: Unity -batchmode -quit -projectPath unity -executeMethod GodGame.Editor.DeterminismPlayerBuild.BuildFromCommandLine
    /// </summary>
    public static class DeterminismPlayerBuild
    {
        public const string OutputPath = "Builds/DeterminismPlayer/GodGameDeterminism.exe";

        [MenuItem("GodGame/Build/Determinism Player (IL2CPP)")]
        public static void Build()
        {
            var standalone = NamedBuildTarget.Standalone;
            if (PlayerSettings.GetScriptingBackend(standalone) != ScriptingImplementation.IL2CPP)
            {
                throw new InvalidOperationException(
                    "Standalone scripting backend is not IL2CPP; run GodGame → Setup → Apply Project Settings first."
                );
            }
            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Game/Scenes/Bootstrap.unity" },
                locationPathName = OutputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException("Determinism player build " + report.summary.result + ".");
            }
            Debug.Log("GodGame: determinism player built at " + Path.GetFullPath(OutputPath));
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
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
