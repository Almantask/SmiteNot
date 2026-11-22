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
    /// Builds the debug-view scene (a Cinemachine strategic camera, a light, the BattleDebugView with its shaders
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
            camera.clearFlags = CameraClearFlags.Skybox;
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
            serialized.FindProperty("_waterShader").objectReferenceValue = Shader.Find(
                "GodGame/DebugWater"
            );
            serialized.FindProperty("_lineShader").objectReferenceValue = Shader.Find(
                "Universal Render Pipeline/Unlit"
            );
            serialized.FindProperty("_deckShader").objectReferenceValue = Shader.Find(
                "GodGame/DebugDeck"
            );
            serialized.FindProperty("_markerShader").objectReferenceValue = Shader.Find(
                "GodGame/DebugMarkers"
            );
            serialized.FindProperty("_rainShader").objectReferenceValue = Shader.Find(
                "GodGame/RainCurtain"
            );
            serialized.FindProperty("_skyShader").objectReferenceValue = Shader.Find(
                "GodGame/StylizedSky"
            );
            serialized.FindProperty("_skirtShader").objectReferenceValue = Shader.Find(
                "GodGame/MapSkirt"
            );
            serialized.FindProperty("_shadowShader").objectReferenceValue = Shader.Find(
                "GodGame/ContactShadow"
            );
            serialized.FindProperty("_bannerShader").objectReferenceValue = Shader.Find(
                "GodGame/RegimentBanner"
            );
            serialized.FindProperty("_panelSettings").objectReferenceValue = PanelSettingsAsset();
            serialized.FindProperty("_camera").objectReferenceValue = strategic;
            AssignUnitMeshes(serialized, "Battle debug scene");
            AssignWorldProps(serialized, "Battle debug scene");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach (
                string name in new[]
                {
                    "_unitShader",
                    "_groundShader",
                    "_waterShader",
                    "_lineShader",
                    "_deckShader",
                    "_markerShader",
                    "_rainShader",
                    "_skyShader",
                    "_skirtShader",
                    "_shadowShader",
                    "_bannerShader",
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

        public static void AssignUnitMeshes(SerializedObject serialized, string context)
        {
            var rules = GodGame.Bridge.RepoLoader.LoadRules(
                GodGame.Bridge.RepoLoader.EditorRepoRoot
            );
            SerializedProperty meshesProp = serialized.FindProperty("_unitMeshes");
            SerializedProperty lod2Prop = serialized.FindProperty("_unitMeshesLod2");
            SerializedProperty vatsProp = serialized.FindProperty("_unitVats");
            meshesProp.arraySize = rules.UnitTypes.Count;
            if (lod2Prop != null)
            {
                lod2Prop.arraySize = rules.UnitTypes.Count;
            }
            if (vatsProp != null)
            {
                vatsProp.arraySize = rules.UnitTypes.Count;
            }
            for (int i = 0; i < rules.UnitTypes.Count; i++)
            {
                if (UnitArt.AwaitsModel(rules.UnitTypes[i].Id))
                {
                    // No model yet: drawn as a capsule.
                    meshesProp.GetArrayElementAtIndex(i).objectReferenceValue = null;
                    if (lod2Prop != null)
                    {
                        lod2Prop.GetArrayElementAtIndex(i).objectReferenceValue = null;
                    }
                    if (vatsProp != null)
                    {
                        vatsProp.GetArrayElementAtIndex(i).objectReferenceValue = null;
                    }
                    continue;
                }
                string cleanId = UnitArt.CleanId(rules.UnitTypes[i].Id);
                string path = $"Assets/_Game/Art/Generated/U_{cleanId}_a/U_{cleanId}_a_LOD0.fbx";
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                {
                    throw new InvalidOperationException(
                        $"{context}: unit mesh not found at {path}."
                    );
                }
                meshesProp.GetArrayElementAtIndex(i).objectReferenceValue = mesh;
                if (lod2Prop != null)
                {
                    string lod2Path =
                        $"Assets/_Game/Art/Generated/U_{cleanId}_a/U_{cleanId}_a_LOD2.fbx";
                    lod2Prop.GetArrayElementAtIndex(i).objectReferenceValue =
                        AssetDatabase.LoadAssetAtPath<Mesh>(lod2Path);
                }
                if (vatsProp != null)
                {
                    vatsProp.GetArrayElementAtIndex(i).objectReferenceValue =
                        VatBaker.BakeOrLoadUnit(cleanId, forceBake: true);
                }
            }
        }

        public static void AssignWorldProps(SerializedObject serialized, string context)
        {
            var mappings = new (string Property, string Path)[]
            {
                ("_wreckMesh", "Assets/_Game/Art/Generated/P_wreck_siege/P_wreck_siege_LOD0.fbx"),
                ("_treeMesh", "Assets/_Game/Art/Generated/P_tree_grove/P_tree_grove_LOD0.fbx"),
                (
                    "_felledTrunkMesh",
                    "Assets/_Game/Art/Generated/P_trunk_felled/P_trunk_felled_LOD0.fbx"
                ),
                ("_fireMesh", "Assets/_Game/Art/Generated/P_fire_small/P_fire_small_LOD0.fbx"),
                ("_boulderMesh", "Assets/_Game/Art/Generated/P_boulder_a/P_boulder_a_LOD0.fbx"),
                ("_hedgerowMesh", "Assets/_Game/Art/Generated/P_hedgerow_a/P_hedgerow_a_LOD0.fbx"),
                (
                    "_wallDrystoneMesh",
                    "Assets/_Game/Art/Generated/P_wall_drystone/P_wall_drystone_LOD0.fbx"
                ),
                ("_herdPenMesh", "Assets/_Game/Art/Generated/P_pen_herd/P_pen_herd_LOD0.fbx"),
                (
                    "_woodenBridgeMesh",
                    "Assets/_Game/Art/Generated/P_bridge_wood/P_bridge_wood_LOD0.fbx"
                ),
                (
                    "_footbridgeMesh",
                    "Assets/_Game/Art/Generated/P_bridge_foot/P_bridge_foot_LOD0.fbx"
                ),
            };
            foreach (var (property, path) in mappings)
            {
                SerializedProperty prop = serialized.FindProperty(property);
                if (prop == null)
                {
                    continue;
                }
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (mesh == null)
                {
                    throw new InvalidOperationException(
                        $"{context}: prop mesh not found at {path}."
                    );
                }
                prop.objectReferenceValue = mesh;
            }
        }

        [MenuItem("GodGame/Build/Debug View Player")]
        public static void BuildPlayer()
        {
            Create();
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

        internal static void RunAndExit(Action action)
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

        internal static PanelSettings PanelSettingsAsset()
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

        internal static void AddToBuildSettings(string path)
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

        internal static void EnsureFolder(string path)
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
