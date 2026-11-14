using System;
using System.Collections.Generic;
using GodGame.Bridge;
using GodGame.Rendering;
using UnityEditor;
using UnityEngine;

namespace GodGame.Editor
{
    /// <summary>
    /// Samples each unit's exported _Anims.fbx clips (locomotion + combat variants) on its SkinnedMeshRenderer and saves
    /// a <see cref="UnitVatData"/> asset alongside the imported FBX files for runtime indirect instanced playback.
    /// Menu: GodGame → Art → Bake Unit VATs.
    /// </summary>
    public static class VatBaker
    {
        public const int DefaultFramesPerClip = 30;
        public const float DefaultFps = 30f;

        [MenuItem("GodGame/Art/Bake Unit VATs")]
        public static void BakeAll()
        {
            var rules = RepoLoader.LoadRules(RepoLoader.EditorRepoRoot);
            for (int i = 0; i < rules.UnitTypes.Count; i++)
            {
                string cleanId = rules.UnitTypes[i].Id.Replace("-", string.Empty);
                BakeOrLoadUnit(cleanId, forceBake: true);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"GodGame: baked {rules.UnitTypes.Count} unit VAT assets.");
        }

        public static void BakeAllFromCommandLine() => BattleDebugScene.RunAndExit(BakeAll);

        public static string VatAssetPath(string cleanId) =>
            $"Assets/_Game/Art/Generated/U_{cleanId}_a/U_{cleanId}_a_VAT.asset";

        public static UnitVatData BakeOrLoadUnit(string cleanId, bool forceBake = false)
        {
            string vatPath = VatAssetPath(cleanId);
            if (!forceBake)
            {
                var existing = AssetDatabase.LoadAssetAtPath<UnitVatData>(vatPath);
                if (
                    existing != null
                    && existing.ClipCount > 0
                    && existing.FramesPerClip > 0
                    && existing.Positions != null
                    && existing.Positions.Length >= existing.FrameCount * existing.VertexCount
                )
                {
                    return existing;
                }
            }

            string folder = $"Assets/_Game/Art/Generated/U_{cleanId}_a";
            string lod0Path = $"{folder}/U_{cleanId}_a_LOD0.fbx";
            string animsPath = $"{folder}/U_{cleanId}_a_Anims.fbx";

            Mesh lod0Mesh = AssetDatabase.LoadAssetAtPath<Mesh>(lod0Path);
            if (lod0Mesh == null)
            {
                throw new InvalidOperationException($"VatBaker: missing LOD0 mesh at {lod0Path}");
            }

            GameObject animsPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(animsPath);
            if (animsPrefab == null)
            {
                throw new InvalidOperationException(
                    $"VatBaker: missing anims prefab at {animsPath}"
                );
            }

            List<AnimationClip> clips = FindClips(animsPath);
            if (clips.Count == 0)
            {
                throw new InvalidOperationException(
                    $"VatBaker: no animation clip found in {animsPath}"
                );
            }

            int vertexCount = lod0Mesh.vertexCount;
            int framesPerClip = DefaultFramesPerClip;
            int clipCount = clips.Count;
            int totalFrames = clipCount * framesPerClip;
            var positions = new Vector4[totalFrames * vertexCount];
            var normals = new Vector4[totalFrames * vertexCount];

            GameObject instance = UnityEngine.Object.Instantiate(animsPrefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;

            var bakedMesh = new Mesh();
            var frameVerts = new List<Vector3>(vertexCount);
            var frameNorms = new List<Vector3>(vertexCount);
            Transform[] hierarchy = instance.GetComponentsInChildren<Transform>(true);
            var bindPos = new Vector3[hierarchy.Length];
            var bindRot = new Quaternion[hierarchy.Length];
            var bindScale = new Vector3[hierarchy.Length];
            for (int t = 0; t < hierarchy.Length; t++)
            {
                bindPos[t] = hierarchy[t].localPosition;
                bindRot[t] = hierarchy[t].localRotation;
                bindScale[t] = hierarchy[t].localScale;
            }

            try
            {
                SkinnedMeshRenderer smr = instance.GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr == null)
                {
                    throw new InvalidOperationException(
                        $"VatBaker: no SkinnedMeshRenderer in {animsPath}"
                    );
                }

                smr.updateWhenOffscreen = true;
                Matrix4x4 relMat =
                    instance.transform.worldToLocalMatrix * smr.transform.localToWorldMatrix;

                float maxDisp = 0f;
                for (int c = 0; c < clipCount; c++)
                {
                    for (int t = 0; t < hierarchy.Length; t++)
                    {
                        hierarchy[t].localPosition = bindPos[t];
                        hierarchy[t].localRotation = bindRot[t];
                        hierarchy[t].localScale = bindScale[t];
                    }

                    AnimationClip clip = clips[c];
                    float clipLength = Mathf.Max(0.001f, clip.length);
                    int clipBaseFrame = c * framesPerClip;

                    for (int f = 0; f < framesPerClip; f++)
                    {
                        float sampleTime =
                            (
                                c >= 4
                                    ? f / (float)Math.Max(1, framesPerClip - 1)
                                    : f / (float)framesPerClip
                            ) * clipLength;
                        clip.SampleAnimation(instance, sampleTime);
                        smr.BakeMesh(bakedMesh, true);
                        if (bakedMesh.vertexCount != vertexCount)
                        {
                            throw new InvalidOperationException(
                                $"VatBaker: {animsPath} vertex count {bakedMesh.vertexCount} != LOD0 vertex count {vertexCount}."
                            );
                        }

                        bakedMesh.GetVertices(frameVerts);
                        bakedMesh.GetNormals(frameNorms);
                        int row = (clipBaseFrame + f) * vertexCount;
                        int baseRow = clipBaseFrame * vertexCount;
                        for (int v = 0; v < vertexCount; v++)
                        {
                            Vector3 p = relMat.MultiplyPoint3x4(frameVerts[v]);
                            Vector3 n =
                                v < frameNorms.Count
                                    ? relMat.MultiplyVector(frameNorms[v]).normalized
                                    : Vector3.up;
                            positions[row + v] = new Vector4(p.x, p.y, p.z, 1f);
                            normals[row + v] = new Vector4(n.x, n.y, n.z, 0f);
                            if (f > 0)
                            {
                                float disp = Vector3.Distance((Vector3)positions[baseRow + v], p);
                                if (disp > maxDisp)
                                {
                                    maxDisp = disp;
                                }
                            }
                        }
                    }
                }

                Debug.Log(
                    $"GodGame: baked VAT for U_{cleanId}_a ({clipCount} clips x {framesPerClip} frames, {vertexCount} verts, maxDisp={maxDisp:0.000}m)."
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(bakedMesh);
                UnityEngine.Object.DestroyImmediate(instance);
            }

            UnitVatData vat = AssetDatabase.LoadAssetAtPath<UnitVatData>(vatPath);
            if (vat == null)
            {
                vat = ScriptableObject.CreateInstance<UnitVatData>();
                AssetDatabase.CreateAsset(vat, vatPath);
            }
            vat.ClipCount = clipCount;
            vat.FramesPerClip = framesPerClip;
            vat.FrameCount = totalFrames;
            vat.VertexCount = vertexCount;
            vat.Fps = DefaultFps;
            vat.Positions = positions;
            vat.Normals = normals;
            EditorUtility.SetDirty(vat);
            AssetDatabase.SaveAssets();
            return vat;
        }

        private static List<AnimationClip> FindClips(string animsPath)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(animsPath);
            var clips = new List<AnimationClip>();
            foreach (UnityEngine.Object obj in assets)
            {
                if (
                    obj is AnimationClip clip
                    && !clip.name.StartsWith("__preview__", StringComparison.Ordinal)
                )
                {
                    clips.Add(clip);
                }
            }
            clips.Sort(
                (a, b) =>
                {
                    int cmp = ClipPriority(a.name).CompareTo(ClipPriority(b.name));
                    return cmp != 0 ? cmp : string.CompareOrdinal(a.name, b.name);
                }
            );
            return clips;
        }

        private static int ClipPriority(string name)
        {
            if (name.EndsWith("_walk") || name.EndsWith("_trot") || name.EndsWith("_roll"))
            {
                return 0;
            }
            if (name.EndsWith("_strike") || name.EndsWith("_rear") || name.EndsWith("_fire"))
            {
                return 1;
            }
            if (name.EndsWith("_bash") || name.EndsWith("_slash") || name.EndsWith("_heave"))
            {
                return 2;
            }
            if (name.EndsWith("_flurry") || name.EndsWith("_charge") || name.EndsWith("_salvo"))
            {
                return 3;
            }
            if (name.EndsWith("_death"))
            {
                return 4;
            }
            if (name.EndsWith("_collapse"))
            {
                return 5;
            }
            return 10;
        }
    }
}
