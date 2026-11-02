using System;
using System.IO;
using GodGame.Rendering;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace GodGame.Editor
{
    /// <summary>
    /// Builds the M2 debug-view scene (a Cinemachine strategic camera, a light, the BattleDebugView with its shaders
    /// and HUD panel settings) and the Windows player that measures its frame rate. The scene is generated, never
    /// edited by hand: change this script and rebuild it.
    /// Menu: GodGame → Setup → Create Battle Debug Scene; GodGame → Build → Debug View Player.
    /// Batch: -executeMethod GodGame.Editor.BattleDebugScene.CreateFromCommandLine (or BuildPlayerFromCommandLine).
    /// Run the player with <c>-repo &lt;repo&gt; -measure-fps &lt;file&gt;</c> to write frame statistics and quit.
    /// </summary>
    public static class BattleDebugScene
    {
        public const string ScenePath = "Assets/_Game/Scenes/BattleDebug.unity";
        public const string PanelSettingsPath = "Assets/_Game/UI/DebugHudPanelSettings.asset";
        public const string PlayerPath = "Builds/DebugViewPlayer/GodGameDebugView.exe";

        [MenuItem("GodGame/Setup/Create Battle Debug Scene")]
        public static void Create()
        {
            EnsureFolder("Assets/_Game/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

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

            var view = new GameObject("Battle Debug View").AddComponent<BattleDebugView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_unitShader").objectReferenceValue = Shader.Find(
                "GodGame/DebugUnits"
            );
            serialized.FindProperty("_groundShader").objectReferenceValue = Shader.Find(
                "GodGame/DebugGround"
            );
            serialized.FindProperty("_lineShader").objectReferenceValue = Shader.Find(
                "Universal Render Pipeline/Unlit"
            );
            serialized.FindProperty("_panelSettings").objectReferenceValue = PanelSettingsAsset();
            serialized.FindProperty("_camera").objectReferenceValue = strategic;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (
                string name in new[]
                {
                    "_unitShader",
                    "_groundShader",
                    "_lineShader",
                    "_panelSettings",
                }
            )
            {
                if (serialized.FindProperty(name).objectReferenceValue == null)
                {
                    throw new InvalidOperationException(
                        "Battle debug scene: " + name + " not found."
                    );
                }
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings(ScenePath);
            Debug.Log("GodGame: battle debug scene written to " + ScenePath);
        }

        [MenuItem("GodGame/Build/Debug View Player")]
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
                    "Debug view player build " + report.summary.result + "."
                );
            }
            Debug.Log("GodGame: debug view player built at " + Path.GetFullPath(PlayerPath));
        }

        public static void CreateFromCommandLine() => RunAndExit(Create);

        public static void BuildPlayerFromCommandLine() =>
            RunAndExit(() =>
            {
                Create();
                BuildPlayer();
            });

        private static void RunAndExit(Action action)
        {
            try
            {
                action();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                EditorApplication.Exit(1);
            }
        }

        private static PanelSettings PanelSettingsAsset()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings != null)
            {
                return settings;
            }
            EnsureFolder("Assets/_Game/UI");
            settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.scaleMode = PanelScaleMode.ConstantPhysicalSize;
            AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (var entry in scenes)
            {
                if (entry.path == path)
                {
                    return;
                }
            }
            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            scenes.CopyTo(updated, 0);
            updated[scenes.Length] = new EditorBuildSettingsScene(path, true);
            EditorBuildSettings.scenes = updated;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
