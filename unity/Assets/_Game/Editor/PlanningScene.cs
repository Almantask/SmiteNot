using System;
using System.IO;
using GodGame.Rendering;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GodGame.Editor
{
    /// <summary>
    /// Builds the M4 planning scene (a Cinemachine strategic camera, a light, the PlanningView with its shaders and panel
    /// settings) and a Windows player for it. The scene is generated, never edited by hand: change this script and
    /// rebuild it. Menu: GodGame → Setup → Create Planning Scene; GodGame → Build → Planning Player.
    /// Batch: -executeMethod GodGame.Editor.PlanningScene.BuildPlayerFromCommandLine.
    /// Run the player with <c>-repo &lt;repo&gt; [-map id -plan file] [-measure-preview file]</c>.
    /// </summary>
    public static class PlanningScene
    {
        public const string ScenePath = "Assets/_Game/Scenes/Planning.unity";
        public const string PlayerPath = "Builds/PlanningPlayer/GodGamePlanning.exe";

        [MenuItem("GodGame/Setup/Create Planning Scene")]
        public static void Create()
        {
            BattleDebugScene.EnsureFolder("Assets/_Game/Scenes");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.farClipPlane = 5000f;
            camera.nearClipPlane = 0.5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.65f, 0.78f);
            cameraObject.AddComponent<CinemachineBrain>();

            var virtualCamera = new GameObject("Strategic Camera");
            var cinemachine = virtualCamera.AddComponent<CinemachineCamera>();
            cinemachine.Lens = new LensSettings
            {
                FieldOfView = 45f,
                NearClipPlane = 0.5f,
                FarClipPlane = 5000f,
            };
            var strategic = virtualCamera.AddComponent<StrategicCamera>();

            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            light.shadows = LightShadows.None;

            var view = new GameObject("Planning View").AddComponent<PlanningView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_unitShader").objectReferenceValue = Shader.Find(
                "GodGame/DebugUnits"
            );
            serialized.FindProperty("_groundShader").objectReferenceValue = Shader.Find(
                "GodGame/DebugGround"
            );
            serialized.FindProperty("_waterShader").objectReferenceValue = Shader.Find(
                "GodGame/DebugWater"
            );
            serialized.FindProperty("_lineShader").objectReferenceValue = Shader.Find(
                "Universal Render Pipeline/Unlit"
            );
            serialized.FindProperty("_panelSettings").objectReferenceValue =
                BattleDebugScene.PanelSettingsAsset();
            serialized.FindProperty("_camera").objectReferenceValue = strategic;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (
                string name in new[]
                {
                    "_unitShader",
                    "_groundShader",
                    "_waterShader",
                    "_lineShader",
                    "_panelSettings",
                }
            )
            {
                if (serialized.FindProperty(name).objectReferenceValue == null)
                {
                    throw new InvalidOperationException("Planning scene: " + name + " not found.");
                }
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            BattleDebugScene.AddToBuildSettings(ScenePath);
            Debug.Log("GodGame: planning scene written to " + ScenePath);
        }

        [MenuItem("GodGame/Build/Planning Player")]
        public static void BuildPlayer()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Create();
            }
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = PlayerPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException(
                    "Planning player build " + report.summary.result + "."
                );
            }
            Debug.Log("GodGame: planning player built at " + Path.GetFullPath(PlayerPath));
        }

        public static void BuildPlayerFromCommandLine() =>
            BattleDebugScene.RunAndExit(() =>
            {
                Create();
                BuildPlayer();
            });
    }
}
