using System;
using GodGame.Bridge;
using GodGame.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace GodGame.Rendering
{
    /// <summary>
    /// Indirectly draws static and battle-built bridge/ice decks at <see cref="StructureSnapshot.DeckHeightM"/>,
    /// visualizing Rot greying, Creak/Crack/Sag strain stages, physical droop and collapsed gaps.
    /// </summary>
    public sealed class DeckRenderer : IDisposable
    {
        private static readonly int InstancesId = Shader.PropertyToID("_DeckInstances");
        private static readonly int StatesId = Shader.PropertyToID("_DeckStates");
        private static readonly int CellSizeMId = Shader.PropertyToID("_CellSizeM");
        private static readonly int ShowStrainOverlayId = Shader.PropertyToID("_ShowStrainOverlay");

        private readonly Mesh _cube;
        private readonly Material _material;
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        private readonly Bounds _bounds;
        private readonly float _cellSizeM;

        private GraphicsBuffer _instances;
        private GraphicsBuffer _states;
        private GraphicsBuffer _args;
        private Vector4[] _instanceData;
        private Vector4[] _stateData;
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _argsData =
            new GraphicsBuffer.IndirectDrawIndexedArgs[1];

        public DeckRenderer(MapData map, Shader shader)
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
            AllocateBuffers(Math.Max(256, initialCap + 256));

            _args = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size
            );
            var size = new Vector3(map.WidthMm / 1000f, 120f, map.DepthMm / 1000f);
            _bounds = new Bounds(size * 0.5f, size + Vector3.one * 40f);
        }

        public int DrawnCellsLastFrame { get; private set; }

        public void Draw(StructureSnapshot snapshot, MapData map, bool showStrainOverlay)
        {
            if (snapshot == null || snapshot.Count == 0 || map == null)
            {
                DrawnCellsLastFrame = 0;
                return;
            }

            int totalCells = 0;
            for (int s = 0; s < snapshot.Count; s++)
            {
                if (!snapshot.Broken[s] && snapshot.Cells[s] != null)
                {
                    totalCells += snapshot.Cells[s].Length;
                }
            }
            if (totalCells == 0)
            {
                DrawnCellsLastFrame = 0;
                return;
            }
            if (totalCells > _instanceData.Length)
            {
                AllocateBuffers(Math.Max(totalCells, _instanceData.Length * 2));
            }

            int cols = map.Columns;
            int write = 0;
            for (int s = 0; s < snapshot.Count; s++)
            {
                if (snapshot.Broken[s] || snapshot.Cells[s] == null)
                {
                    continue;
                }
                float deckY = snapshot.DeckHeightM[s];
                float load01 = snapshot.LoadPermille[s] / 1000f;
                float strength01 = snapshot.StrengthPermille[s] / 1000f;
                float stageVal = (int)snapshot.Stage[s];
                float kindVal = KindCode(snapshot.KindIds[s]);
                float thickness = kindVal > 1.5f ? 0.14f : 0.36f;

                int[] cells = snapshot.Cells[s];
                for (int i = 0; i < cells.Length; i++)
                {
                    int cell = cells[i];
                    int cx = cell % cols;
                    int cz = cell / cols;
                    float wx = (cx + 0.5f) * _cellSizeM;
                    float wz = (cz + 0.5f) * _cellSizeM;
                    _instanceData[write] = new Vector4(wx, deckY + 0.04f, wz, thickness);
                    _stateData[write] = new Vector4(load01, strength01, stageVal, kindVal);
                    write++;
                }
            }

            DrawnCellsLastFrame = write;
            if (write == 0)
            {
                return;
            }

            _instances.SetData(_instanceData, 0, 0, write);
            _states.SetData(_stateData, 0, 0, write);

            _argsData[0].indexCountPerInstance = _cube.GetIndexCount(0);
            _argsData[0].instanceCount = (uint)write;
            _argsData[0].startIndex = _cube.GetIndexStart(0);
            _argsData[0].baseVertexIndex = _cube.GetBaseVertex(0);
            _argsData[0].startInstance = 0;
            _args.SetData(_argsData);

            _properties.SetBuffer(InstancesId, _instances);
            _properties.SetBuffer(StatesId, _states);
            _properties.SetFloat(CellSizeMId, _cellSizeM);
            _properties.SetFloat(ShowStrainOverlayId, showStrainOverlay ? 1f : 0f);

            var parameters = new RenderParams(_material)
            {
                worldBounds = _bounds,
                matProps = _properties,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };
            Graphics.RenderMeshIndirect(parameters, _cube, _args, 1, 0);
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

        private void AllocateBuffers(int capacity)
        {
            _instances?.Dispose();
            _states?.Dispose();
            _instanceData = new Vector4[capacity];
            _stateData = new Vector4[capacity];
            _instances = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 16);
            _states = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 16);
        }

        public void Dispose()
        {
            _instances?.Dispose();
            _states?.Dispose();
            _args?.Dispose();
            UnityEngine.Object.DestroyImmediate(_cube);
            UnityEngine.Object.DestroyImmediate(_material);
        }
    }
}
