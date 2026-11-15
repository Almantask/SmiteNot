using System;
using System.Collections.Generic;
using GodGame.Bridge;
using GodGame.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace GodGame.Rendering
{
    /// <summary>Optional authored FBX prop meshes wired by scene setup; omitted slots use procedural fallbacks.</summary>
    public readonly struct WorldMarkerMeshes
    {
        public WorldMarkerMeshes(Mesh wreckMesh, Mesh treeMesh, Mesh felledTrunkMesh, Mesh fireMesh)
        {
            WreckMesh = wreckMesh;
            TreeMesh = treeMesh;
            FelledTrunkMesh = felledTrunkMesh;
            FireMesh = fireMesh;
        }

        public Mesh WreckMesh { get; }
        public Mesh TreeMesh { get; }
        public Mesh FelledTrunkMesh { get; }
        public Mesh FireMesh { get; }
    }

    /// <summary>
    /// Indirectly draws battle-made engine wrecks, siege salvo landings, grove/falling/felled trees, fire patches,
    /// lightning strikes, and herd mood markers without GameObjects per event.
    /// </summary>
    public sealed class WorldMarkerRenderer : IDisposable
    {
        private static readonly int InstancesId = Shader.PropertyToID("_MarkerInstances");
        private static readonly int StatesId = Shader.PropertyToID("_MarkerStates");
        private static readonly int CellColorsId = Shader.PropertyToID("_CellColors");
        private static readonly int IsAuthoredFbxId = Shader.PropertyToID("_IsAuthoredFbx");
        private static readonly int TimeSecondsId = Shader.PropertyToID("_TimeSeconds");

        private sealed class MarkerBatch : IDisposable
        {
            private readonly Mesh _mesh;
            private readonly bool _ownsMesh;
            private readonly MaterialPropertyBlock _props = new MaterialPropertyBlock();
            private readonly GraphicsBuffer _args;
            private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _argsData =
                new GraphicsBuffer.IndirectDrawIndexedArgs[1];

            private GraphicsBuffer _instances;
            private GraphicsBuffer _states;
            public Vector4[] InstanceData;
            public Vector4[] StateData;

            public MarkerBatch(Mesh mesh, bool ownsMesh, bool isAuthoredFbx, int initialCapacity)
            {
                _mesh = mesh;
                _ownsMesh = ownsMesh;
                IsAuthoredFbx = isAuthoredFbx;
                _args = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments,
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size
                );
                Allocate(Math.Max(1, initialCapacity));
            }

            public bool IsAuthoredFbx { get; }

            public void EnsureCapacity(int count)
            {
                if (count > InstanceData.Length)
                {
                    Allocate(Math.Max(count, InstanceData.Length * 2));
                }
            }

            public void Draw(
                Material material,
                Bounds bounds,
                GraphicsBuffer cellColors,
                float timeSeconds,
                int count
            )
            {
                if (count <= 0 || _mesh == null)
                {
                    return;
                }
                _instances.SetData(InstanceData, 0, 0, count);
                _states.SetData(StateData, 0, 0, count);

                _argsData[0].indexCountPerInstance = _mesh.GetIndexCount(0);
                _argsData[0].instanceCount = (uint)count;
                _argsData[0].startIndex = _mesh.GetIndexStart(0);
                _argsData[0].baseVertexIndex = _mesh.GetBaseVertex(0);
                _argsData[0].startInstance = 0;
                _args.SetData(_argsData);

                _props.SetBuffer(InstancesId, _instances);
                _props.SetBuffer(StatesId, _states);
                _props.SetBuffer(CellColorsId, cellColors);
                _props.SetFloat(IsAuthoredFbxId, IsAuthoredFbx ? 1f : 0f);
                _props.SetFloat(TimeSecondsId, timeSeconds);

                var rp = new RenderParams(material)
                {
                    worldBounds = bounds,
                    matProps = _props,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                };
                Graphics.RenderMeshIndirect(rp, _mesh, _args, 1, 0);
            }

            private void Allocate(int capacity)
            {
                _instances?.Dispose();
                _states?.Dispose();
                _instances = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 16);
                _states = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 16);
                InstanceData = new Vector4[capacity];
                StateData = new Vector4[capacity];
            }

            public void Dispose()
            {
                _instances?.Dispose();
                _states?.Dispose();
                _args?.Dispose();
                if (_ownsMesh && _mesh != null)
                {
                    if (Application.isPlaying)
                    {
                        UnityEngine.Object.Destroy(_mesh);
                    }
                    else
                    {
                        UnityEngine.Object.DestroyImmediate(_mesh);
                    }
                }
            }
        }

        private readonly Material _material;
        private readonly GraphicsBuffer _cellColors;
        private readonly Bounds _bounds;

        private readonly MarkerBatch _wrecks;
        private readonly MarkerBatch _salvos;
        private readonly MarkerBatch _trees;
        private readonly MarkerBatch _felledTrees;
        private readonly MarkerBatch _fires;
        private readonly MarkerBatch _strikes;
        private readonly MarkerBatch _herds;

        public WorldMarkerRenderer(MapData map, Shader shader)
            : this(map, shader, default) { }

        public WorldMarkerRenderer(MapData map, Shader shader, WorldMarkerMeshes meshes)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }
            if (shader == null)
            {
                shader = Shader.Find("GodGame/DebugMarkers");
            }
            if (shader == null)
            {
                throw new ArgumentNullException(
                    nameof(shader),
                    "GodGame/DebugMarkers shader not found."
                );
            }

            _material = new Material(shader) { enableInstancing = true };
            _cellColors = PaletteCells.CreateBuffer();

            _wrecks = new MarkerBatch(
                meshes.WreckMesh != null ? meshes.WreckMesh : BuildWreckMesh(),
                ownsMesh: meshes.WreckMesh == null,
                isAuthoredFbx: meshes.WreckMesh != null,
                64
            );
            _salvos = new MarkerBatch(
                BuildStoneImpactMesh(),
                ownsMesh: true,
                isAuthoredFbx: false,
                128
            );
            _trees = new MarkerBatch(
                meshes.TreeMesh != null ? meshes.TreeMesh : BuildFallbackTreeMesh(),
                ownsMesh: meshes.TreeMesh == null,
                isAuthoredFbx: meshes.TreeMesh != null,
                128
            );
            _felledTrees = new MarkerBatch(
                meshes.FelledTrunkMesh != null
                    ? meshes.FelledTrunkMesh
                    : BuildFallbackFelledTrunkMesh(),
                ownsMesh: meshes.FelledTrunkMesh == null,
                isAuthoredFbx: meshes.FelledTrunkMesh != null,
                32
            );
            _fires = new MarkerBatch(
                meshes.FireMesh != null ? meshes.FireMesh : BuildFallbackFireMesh(),
                ownsMesh: meshes.FireMesh == null,
                isAuthoredFbx: meshes.FireMesh != null,
                64
            );
            _strikes = new MarkerBatch(
                BuildLightningMesh(),
                ownsMesh: true,
                isAuthoredFbx: false,
                16
            );
            _herds = new MarkerBatch(
                BuildHerdMarkerMesh(),
                ownsMesh: true,
                isAuthoredFbx: false,
                16
            );

            var size = new Vector3(map.WidthMm / 1000f, 160f, map.DepthMm / 1000f);
            _bounds = new Bounds(size * 0.5f, size + Vector3.one * 50f);
        }

        public int DrawnWrecksLastFrame { get; private set; }
        public int DrawnSalvosLastFrame { get; private set; }
        public int DrawnTreesLastFrame { get; private set; }
        public int DrawnFelledTreesLastFrame { get; private set; }
        public int DrawnFiresLastFrame { get; private set; }
        public int DrawnStrikesLastFrame { get; private set; }
        public int DrawnHerdsLastFrame { get; private set; }

        public bool UsesAuthoredWreckMesh => _wrecks.IsAuthoredFbx;
        public bool UsesAuthoredTreeMesh => _trees.IsAuthoredFbx;
        public bool UsesAuthoredFelledTrunkMesh => _felledTrees.IsAuthoredFbx;
        public bool UsesAuthoredFireMesh => _fires.IsAuthoredFbx;

        public void Draw(
            IReadOnlyList<WreckCellInstance> wrecks,
            IReadOnlyList<StoneLandingInstance> salvos
        ) => Draw(wrecks, salvos, null, null, null, null, null, 0f);

        public void Draw(
            IReadOnlyList<WreckCellInstance> wrecks,
            IReadOnlyList<StoneLandingInstance> salvos,
            IReadOnlyList<TreePropInstance> trees = null,
            IReadOnlyList<FelledTreeInstance> felledTrees = null,
            IReadOnlyList<FireCellInstance> fires = null,
            IReadOnlyList<LightningStrikeInstance> strikes = null,
            IReadOnlyList<HerdMarkerInstance> herds = null,
            float timeSeconds = 0f
        )
        {
            DrawnWrecksLastFrame = DrawWrecks(wrecks, timeSeconds);
            DrawnSalvosLastFrame = DrawSalvos(salvos, timeSeconds);
            DrawnTreesLastFrame = DrawTrees(trees, timeSeconds);
            DrawnFelledTreesLastFrame = DrawFelledTrees(felledTrees, timeSeconds);
            DrawnFiresLastFrame = DrawFires(fires, timeSeconds);
            DrawnStrikesLastFrame = DrawStrikes(strikes, timeSeconds);
            DrawnHerdsLastFrame = DrawHerds(herds, timeSeconds);
        }

        private int DrawWrecks(IReadOnlyList<WreckCellInstance> wrecks, float timeSeconds)
        {
            int count = wrecks != null ? wrecks.Count : 0;
            if (count == 0)
            {
                return 0;
            }
            _wrecks.EnsureCapacity(count);
            float scale = _wrecks.IsAuthoredFbx ? 0.68f : 1.0f;
            for (int i = 0; i < count; i++)
            {
                WreckCellInstance w = wrecks[i];
                _wrecks.InstanceData[i] = new Vector4(w.WorldX, w.WorldY, w.WorldZ, w.YawRad);
                _wrecks.StateData[i] = new Vector4(
                    scale,
                    w.Cleared ? 1.0f : 0.0f,
                    0.0f,
                    w.Cleared ? 0.30f : 1.0f
                );
            }
            _wrecks.Draw(_material, _bounds, _cellColors, timeSeconds, count);
            return count;
        }

        private int DrawSalvos(IReadOnlyList<StoneLandingInstance> salvos, float timeSeconds)
        {
            int count = salvos != null ? salvos.Count : 0;
            if (count == 0)
            {
                return 0;
            }
            _salvos.EnsureCapacity(count);
            for (int i = 0; i < count; i++)
            {
                StoneLandingInstance s = salvos[i];
                float yaw = ((s.EventIndex * 29) & 15) * (MathF.PI * 2f / 16f);
                _salvos.InstanceData[i] = new Vector4(s.WorldX, s.WorldY + 0.03f, s.WorldZ, yaw);
                _salvos.StateData[i] = new Vector4(
                    s.AgeSeconds,
                    s.CrushedCount,
                    s.CrushedCount > 0 ? 2.0f : 1.0f,
                    0.0f
                );
            }
            _salvos.Draw(_material, _bounds, _cellColors, timeSeconds, count);
            return count;
        }

        private int DrawTrees(IReadOnlyList<TreePropInstance> trees, float timeSeconds)
        {
            int count = trees != null ? trees.Count : 0;
            if (count == 0)
            {
                return 0;
            }
            _trees.EnsureCapacity(count);
            for (int i = 0; i < count; i++)
            {
                TreePropInstance t = trees[i];
                _trees.InstanceData[i] = new Vector4(t.WorldX, t.WorldY, t.WorldZ, t.YawRad);
                _trees.StateData[i] = new Vector4(t.Scale, 0.0f, 3.0f, t.FallProgress);
            }
            _trees.Draw(_material, _bounds, _cellColors, timeSeconds, count);
            return count;
        }

        private int DrawFelledTrees(
            IReadOnlyList<FelledTreeInstance> felledTrees,
            float timeSeconds
        )
        {
            int count = felledTrees != null ? felledTrees.Count : 0;
            if (count == 0)
            {
                return 0;
            }
            _felledTrees.EnsureCapacity(count);
            for (int i = 0; i < count; i++)
            {
                FelledTreeInstance t = felledTrees[i];
                _felledTrees.InstanceData[i] = new Vector4(t.WorldX, t.WorldY, t.WorldZ, t.YawRad);
                _felledTrees.StateData[i] = new Vector4(
                    t.Scale,
                    t.Cleared ? 1.0f : 0.0f,
                    4.0f,
                    t.Cleared ? 0.30f : 1.0f
                );
            }
            _felledTrees.Draw(_material, _bounds, _cellColors, timeSeconds, count);
            return count;
        }

        private int DrawFires(IReadOnlyList<FireCellInstance> fires, float timeSeconds)
        {
            int count = fires != null ? fires.Count : 0;
            if (count == 0)
            {
                return 0;
            }
            _fires.EnsureCapacity(count);
            for (int i = 0; i < count; i++)
            {
                FireCellInstance f = fires[i];
                float yaw = ((f.CellIndex * 19) & 15) * (MathF.PI * 2f / 16f);
                _fires.InstanceData[i] = new Vector4(f.WorldX, f.WorldY, f.WorldZ, yaw);
                _fires.StateData[i] = new Vector4(
                    f.Scale,
                    f.Phase,
                    5.0f,
                    f.Extinguished ? 1.0f : 0.0f
                );
            }
            _fires.Draw(_material, _bounds, _cellColors, timeSeconds, count);
            return count;
        }

        private int DrawStrikes(IReadOnlyList<LightningStrikeInstance> strikes, float timeSeconds)
        {
            int count = strikes != null ? strikes.Count : 0;
            if (count == 0)
            {
                return 0;
            }
            _strikes.EnsureCapacity(count);
            for (int i = 0; i < count; i++)
            {
                LightningStrikeInstance s = strikes[i];
                _strikes.InstanceData[i] = new Vector4(s.WorldX, s.WorldY + 0.04f, s.WorldZ, 0f);
                _strikes.StateData[i] = new Vector4(s.AgeSeconds, 0f, 6.0f, 0f);
            }
            _strikes.Draw(_material, _bounds, _cellColors, timeSeconds, count);
            return count;
        }

        private int DrawHerds(IReadOnlyList<HerdMarkerInstance> herds, float timeSeconds)
        {
            int count = herds != null ? herds.Count : 0;
            if (count == 0)
            {
                return 0;
            }
            _herds.EnsureCapacity(count);
            for (int i = 0; i < count; i++)
            {
                HerdMarkerInstance h = herds[i];
                _herds.InstanceData[i] = new Vector4(
                    h.WorldX,
                    h.WorldY + 0.05f,
                    h.WorldZ,
                    h.RadiusM
                );
                _herds.StateData[i] = new Vector4(
                    (float)h.Mode,
                    h.DrivenByRegiment >= 0 ? 1.0f : 0.0f,
                    7.0f,
                    h.YawRad
                );
            }
            _herds.Draw(_material, _bounds, _cellColors, timeSeconds, count);
            return count;
        }

        private static Mesh BuildWreckMesh()
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();

            Color timberDark = new Color(0.29f, 0.19f, 0.12f, 1f);
            Color timberChar = new Color(0.18f, 0.14f, 0.12f, 1f);
            Color iron = new Color(0.45f, 0.48f, 0.52f, 1f);

            AddBox(
                verts,
                cols,
                tris,
                new Vector3(0f, 0.24f, 0f),
                new Vector3(1.15f, 0.34f, 0.80f),
                Quaternion.Euler(4f, 12f, -9f),
                timberDark
            );
            AddBox(
                verts,
                cols,
                tris,
                new Vector3(-0.12f, 0.68f, 0.08f),
                new Vector3(1.25f, 0.20f, 0.22f),
                Quaternion.Euler(14f, -24f, 34f),
                timberChar
            );
            AddBox(
                verts,
                cols,
                tris,
                new Vector3(0.18f, 0.52f, -0.14f),
                new Vector3(0.22f, 0.92f, 0.20f),
                Quaternion.Euler(-18f, 16f, -28f),
                timberDark
            );
            AddBox(
                verts,
                cols,
                tris,
                new Vector3(0.36f, 0.28f, 0.30f),
                new Vector3(0.62f, 0.62f, 0.16f),
                Quaternion.Euler(22f, -8f, 12f),
                iron
            );

            return FinalizeMesh("WreckMarkerMesh", verts, cols, tris);
        }

        private static Mesh BuildStoneImpactMesh()
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();

            Color stoneCol = new Color(0.49f, 0.50f, 0.53f, 1f);
            Color ringMarker = new Color(0.60f, 0.55f, 0.48f, 0.45f);
            Color crushMarker = new Color(0.90f, 0.20f, 0.15f, 1f);

            AddBox(
                verts,
                cols,
                tris,
                new Vector3(0f, 0.28f, 0f),
                new Vector3(0.68f, 0.54f, 0.64f),
                Quaternion.Euler(14f, 22f, 9f),
                stoneCol
            );
            AddRing(verts, cols, tris, 0.85f, 1.38f, 0.08f, 14, ringMarker);
            AddBox(
                verts,
                cols,
                tris,
                new Vector3(0f, 0.95f, 0f),
                new Vector3(0.26f, 1.6f, 0.26f),
                Quaternion.identity,
                crushMarker
            );

            return FinalizeMesh("StoneImpactMarkerMesh", verts, cols, tris);
        }

        private static Mesh BuildFallbackTreeMesh()
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            Color bark = new Color(0.33f, 0.23f, 0.15f, 1f);
            Color crown = new Color(0.29f, 0.48f, 0.20f, 1f);
            AddBox(
                verts,
                cols,
                tris,
                new Vector3(0f, 3.5f, 0f),
                new Vector3(0.7f, 7.0f, 0.7f),
                Quaternion.identity,
                bark
            );
            AddBox(
                verts,
                cols,
                tris,
                new Vector3(0f, 9.5f, 0f),
                new Vector3(4.2f, 6.2f, 4.2f),
                Quaternion.Euler(0f, 25f, 0f),
                crown
            );
            return FinalizeMesh("TreeFallbackMesh", verts, cols, tris);
        }

        private static Mesh BuildFallbackFelledTrunkMesh()
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            Color bark = new Color(0.33f, 0.23f, 0.15f, 1f);
            Color crown = new Color(0.42f, 0.44f, 0.20f, 1f);
            AddBox(
                verts,
                cols,
                tris,
                new Vector3(0f, 0.42f, 4.5f),
                new Vector3(0.75f, 0.75f, 9.0f),
                Quaternion.identity,
                bark
            );
            AddBox(
                verts,
                cols,
                tris,
                new Vector3(0f, 0.95f, 11.2f),
                new Vector3(3.6f, 1.8f, 4.2f),
                Quaternion.identity,
                crown
            );
            return FinalizeMesh("FelledTrunkFallbackMesh", verts, cols, tris);
        }

        private static Mesh BuildFallbackFireMesh()
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();
            Color ember = new Color(0.72f, 0.20f, 0.08f, 1f);
            Color flame = new Color(0.95f, 0.56f, 0.14f, 1f);
            AddBox(
                verts,
                cols,
                tris,
                new Vector3(0f, 0.10f, 0f),
                new Vector3(1.25f, 0.20f, 1.25f),
                Quaternion.identity,
                ember
            );
            AddBox(
                verts,
                cols,
                tris,
                new Vector3(0f, 0.72f, 0f),
                new Vector3(0.45f, 1.25f, 0.45f),
                Quaternion.Euler(8f, 35f, -6f),
                flame
            );
            return FinalizeMesh("FireFallbackMesh", verts, cols, tris);
        }

        private static Mesh BuildLightningMesh()
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();

            Color bolt = new Color(0.98f, 0.94f, 0.65f, 1f);
            Color ring = new Color(1.0f, 0.85f, 0.25f, 0.45f);
            AddBox(
                verts,
                cols,
                tris,
                new Vector3(0f, 5.0f, 0f),
                new Vector3(0.45f, 10.5f, 0.45f),
                Quaternion.Euler(6f, 15f, -8f),
                bolt
            );
            AddBox(
                verts,
                cols,
                tris,
                new Vector3(0.55f, 14.5f, -0.30f),
                new Vector3(0.38f, 10.5f, 0.38f),
                Quaternion.Euler(-8f, 40f, 10f),
                bolt
            );
            AddBox(
                verts,
                cols,
                tris,
                new Vector3(-0.35f, 24.0f, 0.20f),
                new Vector3(0.30f, 10.5f, 0.30f),
                Quaternion.Euler(7f, -20f, -9f),
                bolt
            );
            AddRing(verts, cols, tris, 0.7f, 1.2f, 0.12f, 16, ring);
            return FinalizeMesh("LightningMarkerMesh", verts, cols, tris);
        }

        private static Mesh BuildHerdMarkerMesh()
        {
            var verts = new List<Vector3>();
            var cols = new List<Color>();
            var tris = new List<int>();

            Color ring = new Color(0.85f, 0.75f, 0.45f, 0.45f);
            Color chevron = new Color(0.98f, 0.45f, 0.15f, 1f);
            AddRing(verts, cols, tris, 0.92f, 1.00f, 0.14f, 24, ring);
            AddBox(
                verts,
                cols,
                tris,
                new Vector3(-0.48f, 0.28f, 0.65f),
                new Vector3(0.36f, 0.22f, 1.45f),
                Quaternion.Euler(0f, 34f, 0f),
                chevron
            );
            AddBox(
                verts,
                cols,
                tris,
                new Vector3(0.48f, 0.28f, 0.65f),
                new Vector3(0.36f, 0.22f, 1.45f),
                Quaternion.Euler(0f, -34f, 0f),
                chevron
            );
            return FinalizeMesh("HerdMarkerMesh", verts, cols, tris);
        }

        private static Mesh FinalizeMesh(
            string name,
            List<Vector3> verts,
            List<Color> cols,
            List<int> tris
        )
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        private static void AddBox(
            List<Vector3> verts,
            List<Color> cols,
            List<int> tris,
            Vector3 center,
            Vector3 size,
            Quaternion rot,
            Color color
        )
        {
            Vector3 h = size * 0.5f;
            Vector3[] corners =
            {
                new Vector3(-h.x, -h.y, -h.z),
                new Vector3(h.x, -h.y, -h.z),
                new Vector3(h.x, h.y, -h.z),
                new Vector3(-h.x, h.y, -h.z),
                new Vector3(-h.x, -h.y, h.z),
                new Vector3(h.x, -h.y, h.z),
                new Vector3(h.x, h.y, h.z),
                new Vector3(-h.x, h.y, h.z),
            };
            int b = verts.Count;
            for (int i = 0; i < corners.Length; i++)
            {
                verts.Add(center + rot * corners[i]);
                cols.Add(color);
            }
            int[] idx =
            {
                0,
                2,
                1,
                0,
                3,
                2,
                1,
                2,
                6,
                1,
                6,
                5,
                5,
                6,
                7,
                5,
                7,
                4,
                4,
                7,
                3,
                4,
                3,
                0,
                3,
                7,
                6,
                3,
                6,
                2,
                4,
                0,
                1,
                4,
                1,
                5,
            };
            for (int i = 0; i < idx.Length; i++)
            {
                tris.Add(b + idx[i]);
            }
        }

        private static void AddRing(
            List<Vector3> verts,
            List<Color> cols,
            List<int> tris,
            float innerR,
            float outerR,
            float y,
            int segments,
            Color color
        )
        {
            int baseIdx = verts.Count;
            for (int s = 0; s < segments; s++)
            {
                float a = s * (MathF.PI * 2f / segments);
                float c = MathF.Cos(a);
                float sn = MathF.Sin(a);
                verts.Add(new Vector3(c * innerR, y, sn * innerR));
                verts.Add(new Vector3(c * outerR, y, sn * outerR));
                cols.Add(color);
                cols.Add(color);
            }
            for (int s = 0; s < segments; s++)
            {
                int next = (s + 1) % segments;
                int i0 = baseIdx + s * 2;
                int o0 = i0 + 1;
                int i1 = baseIdx + next * 2;
                int o1 = i1 + 1;
                tris.Add(i0);
                tris.Add(o0);
                tris.Add(o1);
                tris.Add(i0);
                tris.Add(o1);
                tris.Add(i1);
            }
        }

        public void Dispose()
        {
            _wrecks.Dispose();
            _salvos.Dispose();
            _trees.Dispose();
            _felledTrees.Dispose();
            _fires.Dispose();
            _strikes.Dispose();
            _herds.Dispose();
            _cellColors?.Dispose();
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(_material);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(_material);
            }
        }
    }
}
