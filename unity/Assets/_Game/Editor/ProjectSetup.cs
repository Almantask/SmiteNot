using GodGame.Bridge;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
#if GODGAME_URP
using UnityEngine.Rendering.Universal;
#endif

namespace GodGame.Editor
{
    /// <summary>
    /// Applies the project settings from the implementation plan (section 3.7) and creates the M0 bootstrap
    /// scene, so nobody hand-edits ProjectSettings YAML. Safe to run again.
    /// Menu: GodGame → Setup → Apply Project Settings.
    /// Batch: Unity -batchmode -quit -projectPath unity -executeMethod GodGame.Editor.ProjectSetup.ApplyFromCommandLine
    /// </summary>
    public static class ProjectSetup
    {
        private const string SettingsFolder = "Assets/_Game/Settings";
        private const string PipelinePath = SettingsFolder + "/URP_Pipeline.asset";
        private const string RendererPath = SettingsFolder + "/URP_Renderer.asset";
        private const string BootstrapScenePath = "Assets/_Game/Scenes/Bootstrap.unity";

        [MenuItem("GodGame/Setup/Apply Project Settings")]
        public static void Apply()
        {
            ApplyEditorSettings();
            ApplyPlayerSettings();
            ApplyRenderPipeline();
            CreateBootstrapScene();
            AssetDatabase.SaveAssets();
            Debug.Log("GodGame: project settings applied. Commit ProjectSettings/ and the new .meta files.");
        }

        public static void ApplyFromCommandLine()
        {
            Apply();
            EditorApplication.Exit(0);
        }

        private static void ApplyEditorSettings()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";
            // Unity is removing domain reloads on its way to CoreCLR; Unity-side code must not rely on statics resetting.
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
        }

        private static void ApplyPlayerSettings()
        {
            PlayerSettings.colorSpace = ColorSpace.Linear;
            var standalone = NamedBuildTarget.Standalone;
            PlayerSettings.SetScriptingBackend(standalone, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetApiCompatibilityLevel(standalone, ApiCompatibilityLevel.NET_Standard);

            // Windows is the only player platform: DirectX 12 first, DirectX 11 as the fallback for weak iGPU drivers.
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(
                BuildTarget.StandaloneWindows64,
                new[] { GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Direct3D11 }
            );
        }

        private static void ApplyRenderPipeline()
        {
#if GODGAME_URP
            EnsureFolder(SettingsFolder);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            // Quality levels leave their own pipeline empty, so every level uses this one.
            GraphicsSettings.defaultRenderPipeline = pipeline;
#else
            Debug.LogWarning("GodGame: URP (com.unity.render-pipelines.universal) is not installed; skipped the pipeline asset.");
#endif
        }

        private static void CreateBootstrapScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath) == null)
            {
                EnsureFolder("Assets/_Game/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

                var camera = Camera.main;
                if (camera != null)
                {
                    camera.transform.SetPositionAndRotation(new Vector3(10f, 18f, -6f), Quaternion.Euler(60f, 0f, 0f));
                }

                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Arena (20 x 10 m)";
                ground.transform.position = new Vector3(10f, 0f, 5f);
                ground.transform.localScale = new Vector3(2f, 1f, 1f);

                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Sim Cube";
                cube.transform.position = new Vector3(0f, 0.5f, 0f);
                cube.AddComponent<SimCubeDriver>();

                EditorSceneManager.SaveScene(scene, BootstrapScenePath);
            }

            var scenes = EditorBuildSettings.scenes;
            foreach (var entry in scenes)
            {
                if (entry.path == BootstrapScenePath)
                {
                    return;
                }
            }
            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            updated[0] = new EditorBuildSettingsScene(BootstrapScenePath, true);
            scenes.CopyTo(updated, 1);
            EditorBuildSettings.scenes = updated;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
