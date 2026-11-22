using System;
using GodGame.Bridge;
using GodGame.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace GodGame.Rendering
{
    /// <summary>
    /// Indirectly draws static and battle-built bridge/ice decks at <see cref="StructureSnapshot.DeckHeightM"/>,
    /// rendering authored timber cart bridges (<c>P_bridge_wood</c>) and plank footbridges (<c>P_bridge_foot</c>) per
    /// intact segment when wired, and per-cell blocks for stone bridges, ice patches, or fallback decks. Visualizes
    /// Rot greying, Creak/Crack/Sag strain stages, physical droop and collapsed gaps.
    /// </summary>
    public sealed class DeckRenderer : IDisposable
    {
        private static readonly int InstancesId = Shader.PropertyToID("_DeckInstances");
        private static readonly int StatesId = Shader.PropertyToID("_DeckStates");
        private static readonly int ExtraId = Shader.PropertyToID("_DeckExtra");
        private static readonly int CellColorsId = Shader.PropertyToID("_CellColors");
        private static readonly int CellSizeMId = Shader.PropertyToID("_CellSizeM");
        private static readonly int ShowStrainOverlayId = Shader.PropertyToID("_ShowStrainOverlay");
        private static readonly int IsAuthoredBridgeId = Shader.PropertyToID("_IsAuthoredBridge");
        private static readonly int DeckTopOffsetId = Shader.PropertyToID("_DeckTopOffset");

        // Authored mesh dimensions from tools/blender/make_m5_assets.py (Blender Z -> Unity Y, Blender Y -> Unity Z).
        private const float WoodBridgeMeshWidthM = 2.96f;
        private const float WoodBridgeMeshLengthM = 4.42f;
        private const float WoodBridgeDeckTopM = 0.755f;

        private const float FootBridgeMeshWidthM = 1.76f;
        private const float FootBridgeMeshLengthM = 2.92f;
        private const float FootBridgeDeckTopM = 0.575f;

        private sealed class DeckBatch : IDisposable
        {
            private readonly MaterialPropertyBlock _props = new MaterialPropertyBlock();
            private readonly GraphicsBuffer _args;
            private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _argsData =
                new GraphicsBuffer.IndirectDrawIndexedArgs[1];

            private GraphicsBuffer _instances;
            private GraphicsBuffer _states;
            private GraphicsBuffer _extra;
            public Vector4[] InstanceData;
            public Vector4[] StateData;
            public Vector4[] ExtraData;

            public DeckBatch(int initialCapacity)
            {
                _args = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments,
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size
                );
                Allocate(Math.Max(16, initialCapacity));
            }

            public void EnsureCapacity(int required)
            {
                if (required > InstanceData.Length)
                {
                    Allocate(Math.Max(required, InstanceData.Length * 2));
                }
            }

            public void Draw(
                Mesh mesh,
                Material material,
                Bounds bounds,
                GraphicsBuffer cellColors,
                float cellSizeM,
                bool showStrainOverlay,
                bool isAuthoredBridge,
                float deckTopOffset,
                int count
            )
            {
                if (count <= 0 || mesh == null)
                {
                    return;
                }
                _instances.SetData(InstanceData, 0, 0, count);
                _states.SetData(StateData, 0, 0, count);
                _extra.SetData(ExtraData, 0, 0, count);

                _argsData[0].indexCountPerInstance = mesh.GetIndexCount(0);
                _argsData[0].instanceCount = (uint)count;
                _argsData[0].startIndex = mesh.GetIndexStart(0);
                _argsData[0].baseVertexIndex = mesh.GetBaseVertex(0);
                _argsData[0].startInstance = 0;
                _args.SetData(_argsData);

                _props.SetBuffer(InstancesId, _instances);
                _props.SetBuffer(StatesId, _states);
                _props.SetBuffer(ExtraId, _extra);
                _props.SetBuffer(CellColorsId, cellColors);
                _props.SetFloat(CellSizeMId, cellSizeM);
                _props.SetFloat(ShowStrainOverlayId, showStrainOverlay ? 1f : 0f);
                _props.SetFloat(IsAuthoredBridgeId, isAuthoredBridge ? 1f : 0f);
                _props.SetFloat(DeckTopOffsetId, deckTopOffset);

                var parameters = new RenderParams(material)
                {
                    worldBounds = bounds,
                    matProps = _props,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                };
                Graphics.RenderMeshIndirect(parameters, mesh, _args, 1, 0);
            }

            private void Allocate(int capacity)
            {
                _instances?.Dispose();
                _states?.Dispose();
                _extra?.Dispose();
                InstanceData = new Vector4[capacity];
                StateData = new Vector4[capacity];
                ExtraData = new Vector4[capacity];
                _instances = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 16);
                _states = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 16);
                _extra = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 16);
            }

            public void Dispose()
            {
                _instances?.Dispose();
                _states?.Dispose();
                _extra?.Dispose();
                _args?.Dispose();
            }
        }

        private readonly Mesh _cube;
        private readonly Mesh _woodenBridgeMesh;
        private readonly Mesh _footbridgeMesh;
        private readonly Material _material;
        private readonly GraphicsBuffer _cellColors;
        private readonly Bounds _bounds;
        private readonly float _cellSizeM;

        private readonly DeckBatch _cellBatch;
        private readonly DeckBatch _woodBridgeBatch;
        private readonly DeckBatch _footBridgeBatch;

        private Vector2[] _segmentCentroids = new Vector2[64];

        public DeckRenderer(MapData map, Shader shader)
            : this(map, shader, null, null) { }

        public DeckRenderer(MapData map, Shader shader, Mesh woodenBridgeMesh, Mesh footbridgeMesh)
        {
            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }
            if (shader == null)
            {
                shader = Shader.Find("GodGame/DebugDeck");
            }
            if (shader == null)
            {
                throw new ArgumentNullException(
                    nameof(shader),
                    "GodGame/DebugDeck shader not found."
                );
            }
            _cellSizeM = map.CellSizeMm / 1000f;
            _material = new Material(shader) { enableInstancing = true };
            _cellColors = PaletteCells.CreateBuffer();
            _woodenBridgeMesh = woodenBridgeMesh;
            _footbridgeMesh = footbridgeMesh;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cube = UnityEngine.Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
            UnityEngine.Object.DestroyImmediate(go);

            int initialCap = 256;
            if (map.Decks != null)
            {
                for (int s = 0; s < map.Decks.Segments.Count; s++)
                {
                    initialCap += map.Decks.Segments[s].Cells.Length;
                }
            }
            _cellBatch = new DeckBatch(Math.Max(256, initialCap + 256));
            _woodBridgeBatch = new DeckBatch(64);
            _footBridgeBatch = new DeckBatch(64);

            var size = new Vector3(map.WidthMm / 1000f, 120f, map.DepthMm / 1000f);
            _bounds = new Bounds(size * 0.5f, size + Vector3.one * 40f);
        }

        public int DrawnCellsLastFrame { get; private set; }
        public int DrawnBridgeSegmentsLastFrame { get; private set; }
        public bool UsesAuthoredWoodBridgeMesh => _woodenBridgeMesh != null;
        public bool UsesAuthoredFootbridgeMesh => _footbridgeMesh != null;

        public void Draw(StructureSnapshot snapshot, MapData map, bool showStrainOverlay)
        {
            if (snapshot == null || snapshot.Count == 0 || map == null)
            {
                DrawnCellsLastFrame = 0;
                DrawnBridgeSegmentsLastFrame = 0;
                return;
            }

            int cols = map.Columns;
            if (_segmentCentroids.Length < snapshot.Count)
            {
                _segmentCentroids = new Vector2[
                    Math.Max(snapshot.Count, _segmentCentroids.Length * 2)
                ];
            }

            int totalCells = 0;
            int woodSegments = 0;
            int footSegments = 0;

            for (int s = 0; s < snapshot.Count; s++)
            {
                int[] cells = snapshot.Cells[s];
                if (cells == null || cells.Length == 0)
                {
                    _segmentCentroids[s] = Vector2.zero;
                    continue;
                }
                float sx = 0f;
                float sz = 0f;
                for (int i = 0; i < cells.Length; i++)
                {
                    int cell = cells[i];
                    sx += (cell % cols + 0.5f) * _cellSizeM;
                    sz += (cell / cols + 0.5f) * _cellSizeM;
                }
                _segmentCentroids[s] = new Vector2(sx / cells.Length, sz / cells.Length);

                if (snapshot.Broken[s])
                {
                    continue;
                }
                string kindId = snapshot.KindIds[s];
                if (_footbridgeMesh != null && IsFootbridge(kindId))
                {
                    footSegments++;
                }
                else if (_woodenBridgeMesh != null && IsWoodBridge(kindId))
                {
                    woodSegments++;
                }
                else
                {
                    totalCells += cells.Length;
                }
            }

            if (totalCells == 0 && woodSegments == 0 && footSegments == 0)
            {
                DrawnCellsLastFrame = 0;
                DrawnBridgeSegmentsLastFrame = 0;
                return;
            }

            _cellBatch.EnsureCapacity(Math.Max(1, totalCells));
            _woodBridgeBatch.EnsureCapacity(Math.Max(1, woodSegments));
            _footBridgeBatch.EnsureCapacity(Math.Max(1, footSegments));

            int cellWrite = 0;
            int woodWrite = 0;
            int footWrite = 0;

            for (int s = 0; s < snapshot.Count; s++)
            {
                int[] cells = snapshot.Cells[s];
                if (snapshot.Broken[s] || cells == null || cells.Length == 0)
                {
                    continue;
                }

                float deckY = snapshot.DeckHeightM[s];
                float load01 = snapshot.LoadPermille[s] / 1000f;
                float strength01 = snapshot.StrengthPermille[s] / 1000f;
                float stageVal = (int)snapshot.Stage[s];
                string kindId = snapshot.KindIds[s];
                float kindVal = KindCode(kindId);

                bool useFoot = _footbridgeMesh != null && IsFootbridge(kindId);
                bool useWood = !useFoot && _woodenBridgeMesh != null && IsWoodBridge(kindId);

                if (useFoot || useWood)
                {
                    Vector2 center = _segmentCentroids[s];
                    ComputeSpanFrame(
                        s,
                        cells,
                        cols,
                        center,
                        snapshot,
                        map,
                        out float yawRad,
                        out float widthM,
                        out float lengthM
                    );

                    float baseW = useFoot ? FootBridgeMeshWidthM : WoodBridgeMeshWidthM;
                    float baseL = useFoot ? FootBridgeMeshLengthM : WoodBridgeMeshLengthM;
                    float scaleX = Mathf.Max(0.45f, widthM / baseW);
                    float scaleZ = Mathf.Max(0.45f, (lengthM * 1.04f) / baseL);

                    DeckBatch target = useFoot ? _footBridgeBatch : _woodBridgeBatch;
                    int idx = useFoot ? footWrite++ : woodWrite++;
                    target.InstanceData[idx] = new Vector4(
                        center.x,
                        deckY + 0.03f,
                        center.y,
                        yawRad
                    );
                    target.StateData[idx] = new Vector4(load01, strength01, stageVal, kindVal);
                    target.ExtraData[idx] = new Vector4(scaleX, 1.0f, scaleZ, 0f);
                }
                else
                {
                    float thickness = kindVal > 1.5f ? 0.14f : 0.42f;
                    for (int i = 0; i < cells.Length; i++)
                    {
                        int cell = cells[i];
                        int cx = cell % cols;
                        int cz = cell / cols;
                        float wx = (cx + 0.5f) * _cellSizeM;
                        float wz = (cz + 0.5f) * _cellSizeM;
                        _cellBatch.InstanceData[cellWrite] = new Vector4(
                            wx,
                            deckY + 0.04f,
                            wz,
                            thickness
                        );
                        _cellBatch.StateData[cellWrite] = new Vector4(
                            load01,
                            strength01,
                            stageVal,
                            kindVal
                        );
                        _cellBatch.ExtraData[cellWrite] = Vector4.one;
                        cellWrite++;
                    }
                }
            }

            DrawnCellsLastFrame = cellWrite;
            DrawnBridgeSegmentsLastFrame = woodWrite + footWrite;

            _cellBatch.Draw(
                _cube,
                _material,
                _bounds,
                _cellColors,
                _cellSizeM,
                showStrainOverlay,
                isAuthoredBridge: false,
                deckTopOffset: 0f,
                cellWrite
            );
            _woodBridgeBatch.Draw(
                _woodenBridgeMesh,
                _material,
                _bounds,
                _cellColors,
                _cellSizeM,
                showStrainOverlay,
                isAuthoredBridge: true,
                deckTopOffset: WoodBridgeDeckTopM,
                woodWrite
            );
            _footBridgeBatch.Draw(
                _footbridgeMesh,
                _material,
                _bounds,
                _cellColors,
                _cellSizeM,
                showStrainOverlay,
                isAuthoredBridge: true,
                deckTopOffset: FootBridgeDeckTopM,
                footWrite
            );
        }

        private void ComputeSpanFrame(
            int segIndex,
            int[] cells,
            int cols,
            Vector2 center,
            StructureSnapshot snapshot,
            MapData map,
            out float yawRad,
            out float widthM,
            out float lengthM
        )
        {
            Vector2 spanDir = Vector2.zero;
            int staticCount = map.Decks != null ? map.Decks.Segments.Count : 0;

            if (segIndex < staticCount)
            {
                int[] neighbours = map.Decks.Segments[segIndex].Neighbours;
                if (neighbours != null && neighbours.Length > 0)
                {
                    if (neighbours.Length >= 2)
                    {
                        spanDir =
                            _segmentCentroids[neighbours[neighbours.Length - 1]]
                            - _segmentCentroids[neighbours[0]];
                    }
                    else
                    {
                        spanDir = _segmentCentroids[neighbours[0]] - center;
                    }
                }
            }
            else
            {
                // Battle-built segments (trestle / pontoon): align along nearest adjacent same-kind segment.
                float bestDistSq = 100f;
                for (int o = staticCount; o < snapshot.Count; o++)
                {
                    if (o == segIndex || snapshot.Cells[o] == null || snapshot.Cells[o].Length == 0)
                    {
                        continue;
                    }
                    if (
                        !string.Equals(
                            snapshot.KindIds[o],
                            snapshot.KindIds[segIndex],
                            StringComparison.Ordinal
                        )
                    )
                    {
                        continue;
                    }
                    Vector2 d = _segmentCentroids[o] - center;
                    float dSq = d.sqrMagnitude;
                    if (dSq > 0.25f && dSq < bestDistSq)
                    {
                        bestDistSq = dSq;
                        spanDir = d;
                    }
                }
            }

            if (spanDir.sqrMagnitude < 1e-4f)
            {
                // Fallback for isolated single-segment bridge: use axis with larger cell spread.
                float minX = float.MaxValue,
                    maxX = float.MinValue,
                    minZ = float.MaxValue,
                    maxZ = float.MinValue;
                for (int i = 0; i < cells.Length; i++)
                {
                    float wx = (cells[i] % cols + 0.5f) * _cellSizeM;
                    float wz = (cells[i] / cols + 0.5f) * _cellSizeM;
                    if (wx < minX)
                        minX = wx;
                    if (wx > maxX)
                        maxX = wx;
                    if (wz < minZ)
                        minZ = wz;
                    if (wz > maxZ)
                        maxZ = wz;
                }
                spanDir = (maxX - minX) >= (maxZ - minZ) ? Vector2.right : Vector2.up;
            }
            else
            {
                spanDir.Normalize();
            }

            Vector2 rightDir = new Vector2(spanDir.y, -spanDir.x);
            float minAlong = float.MaxValue,
                maxAlong = float.MinValue;
            float minAcross = float.MaxValue,
                maxAcross = float.MinValue;
            for (int i = 0; i < cells.Length; i++)
            {
                Vector2 p =
                    new Vector2(
                        (cells[i] % cols + 0.5f) * _cellSizeM,
                        (cells[i] / cols + 0.5f) * _cellSizeM
                    ) - center;
                float along = Vector2.Dot(p, spanDir);
                float across = Vector2.Dot(p, rightDir);
                if (along < minAlong)
                    minAlong = along;
                if (along > maxAlong)
                    maxAlong = along;
                if (across < minAcross)
                    minAcross = across;
                if (across > maxAcross)
                    maxAcross = across;
            }

            lengthM = Mathf.Max(_cellSizeM, (maxAlong - minAlong) + _cellSizeM);
            widthM = Mathf.Max(_cellSizeM, (maxAcross - minAcross) + _cellSizeM);
            // P_bridge_wood & P_bridge_foot span along local +Z; RotateY(v, yaw) sends (0,0,1) to (-sin(yaw), 0, cos(yaw)).
            yawRad = Mathf.Atan2(-spanDir.x, spanDir.y);
        }

        private static bool IsFootbridge(string kindId) =>
            !string.IsNullOrEmpty(kindId)
            && string.Equals(kindId, "footbridge", StringComparison.OrdinalIgnoreCase);

        private static bool IsWoodBridge(string kindId)
        {
            if (string.IsNullOrEmpty(kindId))
            {
                return false;
            }
            return string.Equals(kindId, "wooden-bridge", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kindId, "trestle", StringComparison.OrdinalIgnoreCase)
                || string.Equals(kindId, "pontoon", StringComparison.OrdinalIgnoreCase);
        }

        private static float KindCode(string kindId)
        {
            if (string.IsNullOrEmpty(kindId))
            {
                return 0f;
            }
            if (string.Equals(kindId, "ice", StringComparison.OrdinalIgnoreCase))
            {
                return 2f;
            }
            if (kindId.IndexOf("stone", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 1f;
            }
            return 0f;
        }

        public void Dispose()
        {
            _cellBatch?.Dispose();
            _woodBridgeBatch?.Dispose();
            _footBridgeBatch?.Dispose();
            _cellColors?.Dispose();
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(_cube);
                UnityEngine.Object.Destroy(_material);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(_cube);
                UnityEngine.Object.DestroyImmediate(_material);
            }
        }
    }
}
