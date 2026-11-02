using System;
using GodGame.Bridge;
using GodGame.Sim;
using UnityEngine;
using UnityEngine.Rendering;

namespace GodGame.Rendering
{
    /// <summary>
    /// The M2 debug renderer: every soldier as a capsule, all in one indirect instanced draw. Each frame copies the
    /// blended positions into a GPU buffer (no GameObject or Update per soldier). Attackers are oranges and defenders
    /// blues (a colour-blind-safe pair), each regiment a different shade of its side's hue.
    /// </summary>
    public sealed class UnitCapsuleRenderer : IDisposable
    {
        private static readonly int UnitsId = Shader.PropertyToID("_Units");
        private static readonly int ShapesId = Shader.PropertyToID("_Shapes");
        private static readonly int PaletteId = Shader.PropertyToID("_Palette");

        private readonly Mesh _capsule;
        private readonly Material _material;
        private readonly GraphicsBuffer _units;
        private readonly GraphicsBuffer _shapes;
        private readonly GraphicsBuffer _palette;
        private readonly GraphicsBuffer _args;
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _argsData =
            new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        private readonly Vector4[] _unitData;
        private readonly Vector4[] _shapeData;
        private readonly Vector4[] _typeShapes;
        private readonly MaterialPropertyBlock _properties = new MaterialPropertyBlock();
        private readonly Bounds _bounds;

        public UnitCapsuleRenderer(Shader shader, BattleHost host)
        {
            if (shader == null)
            {
                throw new ArgumentNullException(nameof(shader));
            }
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _capsule = primitive.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(primitive);
            _material = new Material(shader) { enableInstancing = true };

            host.Read(out _, out var current, out _);
            int capacity = current.X.Length;
            _unitData = new Vector4[capacity];
            _shapeData = new Vector4[capacity];
            _units = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                Math.Max(1, capacity),
                16
            );
            _shapes = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                Math.Max(1, capacity),
                16
            );
            _args = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                1,
                GraphicsBuffer.IndirectDrawIndexedArgs.size
            );

            var rules = host.Rules;
            _typeShapes = new Vector4[rules.UnitTypes.Count];
            for (int t = 0; t < _typeShapes.Length; t++)
            {
                _typeShapes[t] = new Vector4(
                    rules.UnitTypes[t].RadiusMm / 1000f,
                    HeightOf(rules.UnitTypes[t].Id),
                    0,
                    0
                );
            }

            var sides = host.RegimentSides;
            var palette = new Vector4[Math.Max(1, sides.Length)];
            for (int r = 0; r < sides.Length; r++)
            {
                // Golden-ratio steps through a narrow band of each side's hue, so neighbouring regiments differ.
                float step = (r * 0.618034f) % 1f;
                float hue = sides[r] == Side.Attacker ? 0.03f + 0.07f * step : 0.55f + 0.08f * step;
                Color c = Color.HSVToRGB(hue, 0.75f, 0.55f + 0.4f * ((r * 0.381966f) % 1f));
                palette[r] = new Vector4(c.r, c.g, c.b, 1);
            }
            _palette = new GraphicsBuffer(GraphicsBuffer.Target.Structured, palette.Length, 16);
            _palette.SetData(palette);

            var map = host.Map;
            var size = new Vector3(map.WidthMm / 1000f, 200f, map.DepthMm / 1000f);
            _bounds = new Bounds(size * 0.5f, size + Vector3.one * 50f);
        }

        /// <summary>Presentation-only body heights: a soldier, a rider, an engine.</summary>
        private static float HeightOf(string unitType) =>
            unitType switch
            {
                "cavalry" => 2.4f,
                "siege" => 3.0f,
                _ => 1.8f,
            };

        public int DrawnLastFrame { get; private set; }

        public void Draw(BattleHost host)
        {
            host.Read(out var previous, out var current, out float alpha);
            int count = 0;
            for (int u = 0; u < current.Count; u++)
            {
                if (!current.Alive[u])
                {
                    continue;
                }
                bool blend = u < previous.Count && previous.Alive[u];
                float x = blend
                    ? Mathf.LerpUnclamped(previous.X[u], current.X[u], alpha)
                    : current.X[u];
                float y = blend
                    ? Mathf.LerpUnclamped(previous.Y[u], current.Y[u], alpha)
                    : current.Y[u];
                float z = blend
                    ? Mathf.LerpUnclamped(previous.Z[u], current.Z[u], alpha)
                    : current.Z[u];
                _unitData[count] = new Vector4(x, y, z, current.Regiment[u]);
                _shapeData[count] = _typeShapes[current.UnitType[u]];
                count++;
            }
            DrawnLastFrame = count;
            if (count == 0)
            {
                return;
            }
            _units.SetData(_unitData, 0, 0, count);
            _shapes.SetData(_shapeData, 0, 0, count);
            _argsData[0].indexCountPerInstance = _capsule.GetIndexCount(0);
            _argsData[0].instanceCount = (uint)count;
            _argsData[0].startIndex = _capsule.GetIndexStart(0);
            _argsData[0].baseVertexIndex = _capsule.GetBaseVertex(0);
            _argsData[0].startInstance = 0;
            _args.SetData(_argsData);

            _properties.SetBuffer(UnitsId, _units);
            _properties.SetBuffer(ShapesId, _shapes);
            _properties.SetBuffer(PaletteId, _palette);
            var parameters = new RenderParams(_material)
            {
                worldBounds = _bounds,
                matProps = _properties,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };
            Graphics.RenderMeshIndirect(parameters, _capsule, _args);
        }

        public void Dispose()
        {
            _units.Dispose();
            _shapes.Dispose();
            _palette.Dispose();
            _args.Dispose();
            UnityEngine.Object.Destroy(_material);
        }
    }
}
