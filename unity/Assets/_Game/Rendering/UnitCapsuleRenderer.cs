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
        private static readonly int ExtrasId = Shader.PropertyToID("_UnitExtras");
        private static readonly int PaletteId = Shader.PropertyToID("_Palette");
        private static readonly int CellColorsId = Shader.PropertyToID("_CellColors");
        private static readonly int VatPositionsId = Shader.PropertyToID("_VatPositions");
        private static readonly int VatNormalsId = Shader.PropertyToID("_VatNormals");
        private static readonly int VatVertexCountId = Shader.PropertyToID("_VatVertexCount");
        private static readonly int VatFrameCountId = Shader.PropertyToID("_VatFrameCount");
        private static readonly int VatClipCountId = Shader.PropertyToID("_VatClipCount");
        private static readonly int BaseInstanceId = Shader.PropertyToID("_BaseInstance");
        private static readonly int IsGhostId = Shader.PropertyToID("_IsGhost");

        private readonly Mesh _capsule;
        private readonly Mesh[] _typeMeshes;
        private readonly GraphicsBuffer[] _vatPositions;
        private readonly GraphicsBuffer[] _vatNormals;
        private readonly int[] _vatVertexCounts;
        private readonly int[] _vatFrameCounts;
        private readonly int[] _vatClipCounts;
        private readonly GraphicsBuffer _emptyVat;
        private readonly Material _material;
        private readonly GraphicsBuffer _units;
        private readonly GraphicsBuffer _shapes;
        private readonly GraphicsBuffer _extras;
        private readonly GraphicsBuffer _palette;
        private readonly GraphicsBuffer _cellColors;
        private readonly GraphicsBuffer _args;
        private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] _argsData;
        private readonly Vector4[] _unitData;
        private readonly Vector4[] _shapeData;
        private readonly Vector4[] _extraData;
        private readonly Vector4[] _scratchUnits;
        private readonly Vector4[] _scratchShapes;
        private readonly Vector4[] _scratchExtras;
        private readonly Color[] _basePaletteGamma;
        private readonly Color[] _displayPaletteGamma;
        private readonly Vector4[] _livePaletteLinear;
        private bool _paletteDirty;
        private readonly int[] _scratchTypes;
        private readonly Vector4[] _typeShapes;
        private readonly int[] _typeCounts;
        private readonly int[] _typeOffsets;
        private readonly int[] _typeCursors;
        private readonly MaterialPropertyBlock[] _typeProperties;
        private readonly Side[] _sides;
        private readonly float[] _facingX;
        private readonly float[] _facingZ;
        private readonly bool[] _hasFacing;
        private readonly float[] _animPhase;
        private readonly float[] _moveActivity;
        private readonly float[] _combatBlend;
        private readonly int[] _combatClip;
        private readonly int[] _strikeCount;
        private readonly float[] _combatSpeed;
        private readonly bool[] _wasAlive;
        private readonly float[] _deathTimer;
        private readonly int[] _deathClip;
        private readonly bool[] _rangedType;
        private readonly bool[] _siegeType;
        private readonly bool[] _crewType;
        private readonly float[] _regMinX;
        private readonly float[] _regMaxX;
        private readonly float[] _regMinZ;
        private readonly float[] _regMaxZ;
        private readonly bool[] _regHasUnits;
        private readonly bool _isGhost;
        private readonly Bounds _bounds;
        private int _lastTick = -1;

        public UnitCapsuleRenderer(Shader shader, BattleHost host)
            : this(shader, host, null, null) { }

        public UnitCapsuleRenderer(
            Shader shader,
            BattleHost host,
            Mesh[] typeMeshes,
            UnitVatData[] typeVats = null
        )
            : this(
                shader,
                CapacityOf(host),
                host.Rules,
                host.RegimentSides,
                host.Map,
                null,
                typeMeshes,
                typeVats
            ) { }

        private static int CapacityOf(BattleHost host)
        {
            host.Read(out _, out var current, out _);
            return current.X.Length;
        }

        /// <summary>
        /// A renderer for <paramref name="capacity"/> unit slots of a battle on <paramref name="map"/>. With a
        /// <paramref name="tint"/>, every regiment is drawn in that colour (the planning view's ghost of the previous plan).
        /// When <paramref name="typeMeshes"/> is supplied, any non-null slot draws that authored mesh oriented along the unit's
        /// facing direction instead of a capsule, and optional <paramref name="typeVats"/> drive per-soldier animation.
        /// </summary>
        public UnitCapsuleRenderer(
            Shader shader,
            int capacity,
            GodGame.Sim.Rules.Ruleset rules,
            Side[] sides,
            MapData map,
            Color? tint,
            Mesh[] typeMeshes = null,
            UnitVatData[] typeVats = null
        )
        {
            if (shader == null)
            {
                throw new ArgumentNullException(nameof(shader));
            }
            int typeCount = Math.Max(1, rules.UnitTypes.Count);
            int safeCapacity = Math.Max(1, capacity);

            var primitive = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _capsule = UnityEngine.Object.Instantiate(
                primitive.GetComponent<MeshFilter>().sharedMesh
            );
            UnityEngine.Object.DestroyImmediate(primitive);
            int vCount = _capsule.vertexCount;
            var whites = new Color32[vCount];
            var white = new Color32(255, 255, 255, 255);
            for (int v = 0; v < vCount; v++)
            {
                whites[v] = white;
            }
            _capsule.colors32 = whites;

            _typeMeshes = new Mesh[typeCount];
            if (typeMeshes != null)
            {
                int copyCount = Math.Min(typeCount, typeMeshes.Length);
                for (int t = 0; t < copyCount; t++)
                {
                    _typeMeshes[t] = typeMeshes[t];
                }
            }

            _vatPositions = new GraphicsBuffer[typeCount];
            _vatNormals = new GraphicsBuffer[typeCount];
            _vatVertexCounts = new int[typeCount];
            _vatFrameCounts = new int[typeCount];
            _vatClipCounts = new int[typeCount];
            _emptyVat = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, 16);
            _emptyVat.SetData(new[] { Vector4.zero });

            if (typeVats != null)
            {
                int copyCount = Math.Min(typeCount, typeVats.Length);
                for (int t = 0; t < copyCount; t++)
                {
                    UnitVatData vat = typeVats[t];
                    if (
                        vat == null
                        || vat.FrameCount <= 0
                        || vat.VertexCount <= 0
                        || vat.Positions == null
                        || vat.Positions.Length < vat.FrameCount * vat.VertexCount
                    )
                    {
                        continue;
                    }
                    int countFloats = vat.FrameCount * vat.VertexCount;
                    _vatVertexCounts[t] = vat.VertexCount;
                    _vatFrameCounts[t] = vat.FrameCount;
                    _vatClipCounts[t] = Math.Max(1, vat.ClipCount);
                    _vatPositions[t] = new GraphicsBuffer(
                        GraphicsBuffer.Target.Structured,
                        countFloats,
                        16
                    );
                    _vatPositions[t].SetData(vat.Positions, 0, 0, countFloats);
                    _vatNormals[t] = new GraphicsBuffer(
                        GraphicsBuffer.Target.Structured,
                        countFloats,
                        16
                    );
                    if (vat.Normals != null && vat.Normals.Length >= countFloats)
                    {
                        _vatNormals[t].SetData(vat.Normals, 0, 0, countFloats);
                    }
                }
            }

            _material = new Material(shader) { enableInstancing = true };
            _isGhost = tint.HasValue;
            _sides = sides ?? Array.Empty<Side>();
            int regCount = Math.Max(1, _sides.Length);
            _regMinX = new float[regCount];
            _regMaxX = new float[regCount];
            _regMinZ = new float[regCount];
            _regMaxZ = new float[regCount];
            _regHasUnits = new bool[regCount];

            _unitData = new Vector4[safeCapacity];
            _shapeData = new Vector4[safeCapacity];
            _extraData = new Vector4[safeCapacity];
            _scratchUnits = new Vector4[safeCapacity];
            _scratchShapes = new Vector4[safeCapacity];
            _scratchExtras = new Vector4[safeCapacity];
            _scratchTypes = new int[safeCapacity];
            _facingX = new float[safeCapacity];
            _facingZ = new float[safeCapacity];
            _hasFacing = new bool[safeCapacity];
            _animPhase = new float[safeCapacity];
            _moveActivity = new float[safeCapacity];
            _combatBlend = new float[safeCapacity];
            _combatClip = new int[safeCapacity];
            _strikeCount = new int[safeCapacity];
            _combatSpeed = new float[safeCapacity];
            _wasAlive = new bool[safeCapacity];
            _deathTimer = new float[safeCapacity];
            _deathClip = new int[safeCapacity];
            for (int u = 0; u < safeCapacity; u++)
            {
                _animPhase[u] = (u * 0.618034f) % 1f;
                _moveActivity[u] = 0.6f;
                _combatClip[u] = 1 + ((u * 7) % 3);
                _combatSpeed[u] = 1.15f + 0.80f * (((u * 31) % 100) / 100f);
                _deathClip[u] = 4 + ((u * 11) % 2);
            }

            _units = new GraphicsBuffer(GraphicsBuffer.Target.Structured, safeCapacity, 16);
            _shapes = new GraphicsBuffer(GraphicsBuffer.Target.Structured, safeCapacity, 16);
            _extras = new GraphicsBuffer(GraphicsBuffer.Target.Structured, safeCapacity, 16);

            _typeCounts = new int[typeCount];
            _typeOffsets = new int[typeCount];
            _typeCursors = new int[typeCount];
            _typeProperties = new MaterialPropertyBlock[typeCount];
            for (int t = 0; t < typeCount; t++)
            {
                _typeProperties[t] = new MaterialPropertyBlock();
            }

            _argsData = new GraphicsBuffer.IndirectDrawIndexedArgs[typeCount];
            _args = new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                typeCount,
                GraphicsBuffer.IndirectDrawIndexedArgs.size
            );

            _rangedType = new bool[typeCount];
            _siegeType = new bool[typeCount];
            _crewType = new bool[typeCount];
            _typeShapes = new Vector4[typeCount];
            for (int t = 0; t < rules.UnitTypes.Count; t++)
            {
                string uid = rules.UnitTypes[t].Id;
                bool isCrew = string.Equals(uid, "crew", StringComparison.OrdinalIgnoreCase);
                _rangedType[t] = string.Equals(uid, "ranged", StringComparison.OrdinalIgnoreCase);
                _siegeType[t] = string.Equals(uid, "siege", StringComparison.OrdinalIgnoreCase);
                _crewType[t] = isCrew;
                float radiusM = rules.UnitTypes[t].RadiusMm / 1000f;
                _typeShapes[t] = new Vector4(
                    isCrew ? Math.Max(0.42f, radiusM) : radiusM,
                    HeightOf(uid),
                    isCrew ? 1f : 0f,
                    0f
                );
            }

            int palCount = Math.Max(1, _sides.Length);
            _basePaletteGamma = new Color[palCount];
            _displayPaletteGamma = new Color[palCount];
            _livePaletteLinear = new Vector4[palCount];
            for (int r = 0; r < _sides.Length; r++)
            {
                // Golden-ratio steps through a narrow band of each side's hue, so neighbouring regiments differ.
                float step = (r * 0.618034f) % 1f;
                Color c;
                if (tint.HasValue)
                {
                    c = tint.Value;
                }
                else if (_sides[r] == Side.Neutral)
                {
                    c = Color.HSVToRGB(
                        0.11f + 0.03f * step,
                        0.55f,
                        0.65f + 0.25f * ((r * 0.381966f) % 1f)
                    );
                }
                else
                {
                    float hue =
                        _sides[r] == Side.Attacker ? 0.03f + 0.07f * step : 0.55f + 0.08f * step;
                    c = Color.HSVToRGB(hue, 0.75f, 0.55f + 0.4f * ((r * 0.381966f) % 1f));
                }
                _basePaletteGamma[r] = c;
                _displayPaletteGamma[r] = c;
                Color linear = c.linear;
                _livePaletteLinear[r] = new Vector4(linear.r, linear.g, linear.b, 1f);
            }
            if (_sides.Length == 0)
            {
                Color baseFallback = tint ?? Color.white;
                _basePaletteGamma[0] = baseFallback;
                _displayPaletteGamma[0] = baseFallback;
                Color fallback = baseFallback.linear;
                _livePaletteLinear[0] = new Vector4(fallback.r, fallback.g, fallback.b, 1f);
            }
            _palette = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                _livePaletteLinear.Length,
                16
            );
            _palette.SetData(_livePaletteLinear);

            _cellColors = PaletteCells.CreateBuffer();

            var size = new Vector3(map.WidthMm / 1000f, 200f, map.DepthMm / 1000f);
            _bounds = new Bounds(size * 0.5f, size + Vector3.one * 50f);
        }

        /// <summary>Presentation-only body heights: a soldier, a crew member, a rider, an engine.</summary>
        private static float HeightOf(string unitType) =>
            unitType switch
            {
                "cavalry" => 2.4f,
                "siege" => 3.0f,
                "crew" => 1.55f,
                "cattle" => 1.50f,
                _ => 1.8f,
            };

        public int DrawnLastFrame { get; private set; }

        /// <summary>Gets the configured capsule/shape metadata for unit type <paramref name="typeIndex"/>.</summary>
        public Vector4 GetTypeShape(int typeIndex) =>
            (uint)typeIndex < (uint)_typeShapes.Length ? _typeShapes[typeIndex] : Vector4.zero;

        /// <summary>True when unit type <paramref name="typeIndex"/> renders with the distinct crew capsule style.</summary>
        public bool UsesCrewCapsuleStyle(int typeIndex) =>
            (uint)typeIndex < (uint)_crewType.Length
            && _crewType[typeIndex]
            && _typeMeshes[typeIndex] == null;

        /// <summary>Gets the current gamma-space display tint for regiment <paramref name="regiment"/>.</summary>
        public Color GetDisplayColor(int regiment) =>
            (uint)regiment < (uint)_displayPaletteGamma.Length
                ? _displayPaletteGamma[regiment]
                : _displayPaletteGamma[0];

        /// <summary>
        /// Updates the per-regiment palette for strategic-zoom morale and routing tints while keeping attacker
        /// orange and defender blue colour-blind-safe.
        /// </summary>
        public void UpdateMoralePalette(
            RegimentSnapshot regiments,
            float cameraDistance,
            bool boostStrategic = false,
            HerdSnapshot herds = null
        )
        {
            if (_isGhost)
            {
                return;
            }
            float zoomWeight = boostStrategic
                ? 1f
                : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(60f, 240f, cameraDistance));
            bool changed = false;
            int regCount = _sides.Length;
            for (int r = 0; r < regCount; r++)
            {
                Color baseCol = _basePaletteGamma[r];
                Color target = baseCol;
                float weight = zoomWeight;
                if (
                    zoomWeight > 0.001f
                    && regiments != null
                    && r < regiments.Count
                    && (regiments.Wiped[r] || !regiments.Alive[r])
                )
                {
                    target = Color.Lerp(baseCol, new Color(0.24f, 0.24f, 0.26f), 0.72f);
                }
                else if (TryFindHerdForRegiment(herds, r, out int herdIdx))
                {
                    HerdMode mode = herds.Mode[herdIdx];
                    bool driven = herds.DrivenBy[herdIdx] >= 0;
                    target = mode switch
                    {
                        HerdMode.Agitated => new Color(1.00f, 0.78f, 0.14f),
                        HerdMode.Stampede => new Color(1.00f, 0.28f, 0.10f),
                        HerdMode.Scattered => new Color(0.58f, 0.56f, 0.50f),
                        _ => driven ? new Color(0.58f, 0.78f, 0.42f) : baseCol,
                    };
                    if (mode != HerdMode.Grazing || driven)
                    {
                        weight = Mathf.Max(zoomWeight, 0.85f);
                    }
                }
                else if (regiments != null && r < regiments.Count && zoomWeight > 0.001f)
                {
                    if (regiments.Routing[r] || regiments.Order[r] == RegimentOrder.Rout)
                    {
                        // Bright high-contrast washed side hue so routing columns pop immediately at strategic zoom.
                        target =
                            _sides[r] == Side.Attacker
                                ? new Color(1.00f, 0.86f, 0.65f)
                                : new Color(0.68f, 0.90f, 1.00f);
                    }
                    else
                    {
                        float morale01 = Mathf.Clamp01(regiments.Morale[r] / 1000f);
                        if (morale01 < 0.85f)
                        {
                            float t = morale01 / 0.85f;
                            target = Color.Lerp(
                                new Color(0.54f, 0.53f, 0.51f),
                                baseCol,
                                Mathf.Lerp(0.32f, 1f, t)
                            );
                        }
                    }
                }
                Color finalGamma = Color.Lerp(baseCol, target, weight);
                _displayPaletteGamma[r] = finalGamma;
                Color linear = finalGamma.linear;
                Vector4 next = new Vector4(linear.r, linear.g, linear.b, 1f);
                Vector4 prev = _livePaletteLinear[r];
                if (
                    Mathf.Abs(next.x - prev.x) > 0.002f
                    || Mathf.Abs(next.y - prev.y) > 0.002f
                    || Mathf.Abs(next.z - prev.z) > 0.002f
                )
                {
                    _livePaletteLinear[r] = next;
                    changed = true;
                }
            }
            if (changed || _paletteDirty)
            {
                _palette.SetData(_livePaletteLinear);
                _paletteDirty = false;
            }
        }

        private static bool TryFindHerdForRegiment(
            HerdSnapshot herds,
            int regiment,
            out int herdIndex
        )
        {
            if (herds != null)
            {
                for (int h = 0; h < herds.Count; h++)
                {
                    if (herds.Regiment[h] == regiment)
                    {
                        herdIndex = h;
                        return true;
                    }
                }
            }
            herdIndex = -1;
            return false;
        }

        public void Draw(BattleHost host)
        {
            host.Read(out var previous, out var current, out float alpha);
            Draw(previous, current, alpha, null, null, 0f);
        }

        /// <summary>Draws every live soldier of <paramref name="current"/>, blended from <paramref name="previous"/> by <paramref name="alpha"/>.</summary>
        public void Draw(
            BattleHost.UnitSnapshot previous,
            BattleHost.UnitSnapshot current,
            float alpha,
            TrackReader reader = null,
            PlaybackTrack track = null,
            float scrubSeconds = 0f
        )
        {
            float exactTick = scrubSeconds * SimConstants.TicksPerSecond;
            bool checkContinuousLaunches =
                reader != null && track != null && reader.HasAnyActiveLaunchAt(track, exactTick);
            int typeCount = _typeCounts.Length;
            Array.Clear(_typeCounts, 0, typeCount);
            bool tickChanged = current.Tick != _lastTick;
            float dt = Mathf.Clamp(Time.unscaledDeltaTime, 0.001f, 0.1f);
            int regCount = _sides.Length;
            for (int r = 0; r < regCount; r++)
            {
                _regMinX[r] = float.MaxValue;
                _regMaxX[r] = float.MinValue;
                _regMinZ[r] = float.MaxValue;
                _regMaxZ[r] = float.MinValue;
                _regHasUnits[r] = false;
            }
            for (int u = 0; u < current.Count; u++)
            {
                if (!current.Alive[u])
                {
                    continue;
                }
                int r = current.Regiment[u];
                if ((uint)r >= (uint)regCount)
                {
                    continue;
                }
                float ux = current.X[u];
                float uz = current.Z[u];
                if (ux < _regMinX[r])
                {
                    _regMinX[r] = ux;
                }
                if (ux > _regMaxX[r])
                {
                    _regMaxX[r] = ux;
                }
                if (uz < _regMinZ[r])
                {
                    _regMinZ[r] = uz;
                }
                if (uz > _regMaxZ[r])
                {
                    _regMaxZ[r] = uz;
                }
                _regHasUnits[r] = true;
            }

            int count = 0;

            for (int u = 0; u < current.Count && count < _scratchUnits.Length; u++)
            {
                int reg = current.Regiment[u];
                Side side = (uint)reg < (uint)_sides.Length ? _sides[reg] : Side.Attacker;
                int type = current.UnitType[u];
                if ((uint)type >= (uint)typeCount)
                {
                    type = 0;
                }

                if (!current.Alive[u])
                {
                    if (!_wasAlive[u] && !(u < previous.Count && previous.Alive[u]))
                    {
                        _wasAlive[u] = true;
                    }
                    _deathTimer[u] += dt;
                    const float DeathFallDuration = 1.15f;
                    const float CorpseLingerDuration = 18.0f;
                    const float CorpseSinkDuration = 2.0f;
                    if (_deathTimer[u] > CorpseLingerDuration + CorpseSinkDuration)
                    {
                        continue;
                    }
                    float dx = current.X[u];
                    float dy = current.Y[u];
                    float dz = current.Z[u];
                    if (_deathTimer[u] > CorpseLingerDuration)
                    {
                        dy -=
                            Mathf.SmoothStep(
                                0f,
                                1f,
                                (_deathTimer[u] - CorpseLingerDuration) / CorpseSinkDuration
                            ) * 0.65f;
                    }
                    if (!_hasFacing[u])
                    {
                        _facingX[u] = side == Side.Attacker ? 1f : -1f;
                        _facingZ[u] = 0f;
                        _hasFacing[u] = true;
                    }
                    float unitFallDur =
                        DeathFallDuration * (0.88f + 0.24f * (((u * 19) % 100) / 100f));
                    float deathPhase = Mathf.Clamp(_deathTimer[u] / unitFallDur, 0.0005f, 0.9995f);
                    float deathBlend = Mathf.Clamp01(_deathTimer[u] / 0.16f);
                    int dClip =
                        _vatClipCounts[type] >= 6
                            ? _deathClip[u]
                            : Math.Max(0, _vatClipCounts[type] - 1);
                    _scratchUnits[count] = new Vector4(dx, dy, dz, reg);
                    _scratchExtras[count] = Vector4.zero;
                    bool useMeshD = _typeMeshes[type] != null;
                    bool useVatD = useMeshD && _vatFrameCounts[type] > 0;
                    if (useVatD)
                    {
                        _scratchShapes[count] = new Vector4(
                            dClip + deathPhase,
                            -2.0f - deathBlend,
                            _facingX[u],
                            _facingZ[u]
                        );
                    }
                    else if (useMeshD)
                    {
                        _scratchShapes[count] = new Vector4(1f, 0f, _facingX[u], _facingZ[u]);
                    }
                    else
                    {
                        _scratchShapes[count] = _typeShapes[type];
                    }
                    _scratchTypes[count] = type;
                    _typeCounts[type]++;
                    count++;
                    continue;
                }

                _wasAlive[u] = true;
                _deathTimer[u] = 0f;
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
                float spinRad = blend
                    ? Mathf.LerpUnclamped(previous.SpinRad[u], current.SpinRad[u], alpha)
                    : current.SpinRad[u];
                bool airborne = (current.Flags[u] & (byte)UnitFlags.Launched) != 0;
                if (
                    checkContinuousLaunches
                    && reader.TryEvaluateActiveLaunch(
                        track,
                        u,
                        exactTick,
                        out float lx,
                        out float ly,
                        out float lz,
                        out float lSpin
                    )
                )
                {
                    x = lx;
                    y = ly;
                    z = lz;
                    spinRad = lSpin;
                    airborne = true;
                }

                float nearestDistSq = float.MaxValue;
                float enemyDirX = side == Side.Attacker ? 1f : -1f;
                float enemyDirZ = 0f;
                for (int r = 0; r < regCount; r++)
                {
                    if (!_regHasUnits[r] || _sides[r] == side)
                    {
                        continue;
                    }
                    float cx = Mathf.Clamp(x, _regMinX[r], _regMaxX[r]);
                    float cz = Mathf.Clamp(z, _regMinZ[r], _regMaxZ[r]);
                    float ex = cx - x;
                    float ez = cz - z;
                    float dSq = ex * ex + ez * ez;
                    if (dSq < 1e-4f)
                    {
                        float ctrX = (_regMinX[r] + _regMaxX[r]) * 0.5f;
                        float ctrZ = (_regMinZ[r] + _regMaxZ[r]) * 0.5f;
                        ex = ctrX - x;
                        ez = ctrZ - z;
                        float cLenSq = ex * ex + ez * ez;
                        if (cLenSq > 1e-4f)
                        {
                            float cInv = 1f / Mathf.Sqrt(cLenSq);
                            enemyDirX = ex * cInv;
                            enemyDirZ = ez * cInv;
                        }
                        nearestDistSq = 0f;
                        break;
                    }
                    if (dSq < nearestDistSq)
                    {
                        nearestDistSq = dSq;
                        float invD = 1f / Mathf.Sqrt(dSq);
                        enemyDirX = ex * invD;
                        enemyDirZ = ez * invD;
                    }
                }
                float enemyDist =
                    nearestDistSq < float.MaxValue ? Mathf.Sqrt(nearestDistSq) : 1000f;

                float stepDist = 0f;
                if (blend)
                {
                    float dx = current.X[u] - previous.X[u];
                    float dz = current.Z[u] - previous.Z[u];
                    float lenSq = dx * dx + dz * dz;
                    if (lenSq > 1e-8f)
                    {
                        stepDist = Mathf.Sqrt(lenSq);
                        float invLen = 1f / stepDist;
                        float tx = dx * invLen;
                        float tz = dz * invLen;
                        if (!_hasFacing[u])
                        {
                            _facingX[u] = tx;
                            _facingZ[u] = tz;
                            _hasFacing[u] = true;
                        }
                        else if (tickChanged)
                        {
                            float bx = Mathf.Lerp(_facingX[u], tx, 0.35f);
                            float bz = Mathf.Lerp(_facingZ[u], tz, 0.35f);
                            float bLenSq = bx * bx + bz * bz;
                            if (bLenSq > 1e-6f)
                            {
                                float bInv = 1f / Mathf.Sqrt(bLenSq);
                                _facingX[u] = bx * bInv;
                                _facingZ[u] = bz * bInv;
                            }
                            else
                            {
                                _facingX[u] = tx;
                                _facingZ[u] = tz;
                            }
                        }
                    }
                }
                if (!_hasFacing[u])
                {
                    _facingX[u] = side == Side.Attacker ? 1f : -1f;
                    _facingZ[u] = 0f;
                }

                if (tickChanged)
                {
                    _moveActivity[u] = Mathf.Lerp(
                        _moveActivity[u],
                        stepDist > 0.0015f ? 1f : 0f,
                        0.35f
                    );
                }

                float targetCombat = 0f;
                if (nearestDistSq < float.MaxValue)
                {
                    if (enemyDist < 5.5f)
                    {
                        targetCombat = 1.0f;
                    }
                    else if (enemyDist < 12.0f)
                    {
                        targetCombat = 0.85f;
                    }
                    else if (
                        (_rangedType[type] || _siegeType[type])
                        && _moveActivity[u] < 0.25f
                        && enemyDist < 190f
                    )
                    {
                        targetCombat = 1.0f;
                    }
                    else if (_moveActivity[u] < 0.25f && enemyDist < 60f)
                    {
                        targetCombat = 0.85f;
                    }
                }

                if (targetCombat > 0.35f && nearestDistSq < float.MaxValue)
                {
                    float turnAlpha = 1f - Mathf.Exp(-10f * dt);
                    float bx = Mathf.Lerp(_facingX[u], enemyDirX, turnAlpha);
                    float bz = Mathf.Lerp(_facingZ[u], enemyDirZ, turnAlpha);
                    float bLenSq = bx * bx + bz * bz;
                    if (bLenSq > 1e-6f)
                    {
                        float bInv = 1f / Mathf.Sqrt(bLenSq);
                        _facingX[u] = bx * bInv;
                        _facingZ[u] = bz * bInv;
                    }
                    else
                    {
                        _facingX[u] = enemyDirX;
                        _facingZ[u] = enemyDirZ;
                    }
                    _hasFacing[u] = true;
                }

                _combatBlend[u] = Mathf.MoveTowards(_combatBlend[u], targetCombat, dt * 5f);
                float moveRate = Mathf.Lerp(0.95f, 1.85f, _moveActivity[u]);
                float combatRate = _siegeType[type] ? _combatSpeed[u] * 0.72f : _combatSpeed[u];
                float phaseDelta =
                    dt * Mathf.Lerp(moveRate, combatRate, _combatBlend[u])
                    + (tickChanged && _combatBlend[u] < 0.5f ? stepDist * 0.45f : 0f);
                float nextPhase = _animPhase[u] + phaseDelta;
                if (nextPhase >= 1f)
                {
                    nextPhase %= 1f;
                    if (_combatBlend[u] > 0.25f)
                    {
                        _strikeCount[u]++;
                        int numCombatClips = Math.Max(1, _vatClipCounts[type]) - 1;
                        if (numCombatClips > 0)
                        {
                            _combatClip[u] = 1 + ((u * 7 + _strikeCount[u] * 13) % numCombatClips);
                        }
                        int hash = ((u * 31 + _strikeCount[u] * 17) & 0x7FFFFFFF) % 100;
                        _combatSpeed[u] = 1.15f + 0.80f * (hash / 100f);
                    }
                }
                _animPhase[u] = nextPhase;

                _scratchUnits[count] = new Vector4(x, y, z, reg);
                _scratchExtras[count] = new Vector4(
                    spinRad,
                    airborne ? 1f : 0f,
                    current.Flags[u],
                    (u * 0.618034f) % 1f
                );
                bool useMesh = _typeMeshes[type] != null;
                bool useVat = useMesh && _vatFrameCounts[type] > 0;
                if (useVat)
                {
                    float clampedPhase = Mathf.Clamp(_animPhase[u], 0.0005f, 0.9995f);
                    float animBlend = Mathf.Lerp(0.65f, 1.0f, _moveActivity[u]);
                    float packedX =
                        _vatClipCounts[type] > 1 ? _combatClip[u] + clampedPhase : clampedPhase;
                    float packedY =
                        _combatBlend[u] > 0.001f ? (-1.0f - _combatBlend[u]) : -animBlend;
                    _scratchShapes[count] = new Vector4(packedX, packedY, _facingX[u], _facingZ[u]);
                }
                else if (useMesh)
                {
                    _scratchShapes[count] = new Vector4(1f, 0f, _facingX[u], _facingZ[u]);
                }
                else
                {
                    _scratchShapes[count] = _typeShapes[type];
                }
                _scratchTypes[count] = type;
                _typeCounts[type]++;
                count++;
            }

            _lastTick = current.Tick;
            DrawnLastFrame = count;
            if (count == 0)
            {
                return;
            }

            int running = 0;
            for (int t = 0; t < typeCount; t++)
            {
                _typeOffsets[t] = running;
                _typeCursors[t] = running;
                running += _typeCounts[t];
            }

            for (int i = 0; i < count; i++)
            {
                int dst = _typeCursors[_scratchTypes[i]]++;
                _unitData[dst] = _scratchUnits[i];
                _shapeData[dst] = _scratchShapes[i];
                _extraData[dst] = _scratchExtras[i];
            }

            _units.SetData(_unitData, 0, 0, count);
            _shapes.SetData(_shapeData, 0, 0, count);
            _extras.SetData(_extraData, 0, 0, count);

            for (int t = 0; t < typeCount; t++)
            {
                int typeCountThis = _typeCounts[t];
                if (typeCountThis == 0)
                {
                    continue;
                }
                Mesh mesh = _typeMeshes[t] != null ? _typeMeshes[t] : _capsule;
                _argsData[t].indexCountPerInstance = mesh.GetIndexCount(0);
                _argsData[t].instanceCount = (uint)typeCountThis;
                _argsData[t].startIndex = mesh.GetIndexStart(0);
                _argsData[t].baseVertexIndex = mesh.GetBaseVertex(0);
                _argsData[t].startInstance = 0;
            }
            _args.SetData(_argsData);

            float isGhost = _isGhost ? 1f : 0f;
            for (int t = 0; t < typeCount; t++)
            {
                if (_typeCounts[t] == 0)
                {
                    continue;
                }
                Mesh mesh = _typeMeshes[t] != null ? _typeMeshes[t] : _capsule;
                MaterialPropertyBlock props = _typeProperties[t];
                props.SetBuffer(UnitsId, _units);
                props.SetBuffer(ShapesId, _shapes);
                props.SetBuffer(ExtrasId, _extras);
                props.SetBuffer(PaletteId, _palette);
                props.SetBuffer(CellColorsId, _cellColors);
                props.SetBuffer(
                    VatPositionsId,
                    _vatPositions[t] != null ? _vatPositions[t] : _emptyVat
                );
                props.SetBuffer(VatNormalsId, _vatNormals[t] != null ? _vatNormals[t] : _emptyVat);
                props.SetInt(VatVertexCountId, _vatVertexCounts[t]);
                props.SetInt(VatFrameCountId, _vatFrameCounts[t]);
                props.SetInt(VatClipCountId, _vatClipCounts[t]);
                props.SetInt(BaseInstanceId, _typeOffsets[t]);
                props.SetFloat(IsGhostId, isGhost);

                var parameters = new RenderParams(_material)
                {
                    worldBounds = _bounds,
                    matProps = props,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                };
                Graphics.RenderMeshIndirect(parameters, mesh, _args, 1, t);
            }
        }

        public void Dispose()
        {
            _units.Dispose();
            _shapes.Dispose();
            _extras.Dispose();
            _palette.Dispose();
            _cellColors.Dispose();
            _args.Dispose();
            _emptyVat.Dispose();
            for (int t = 0; t < _vatPositions.Length; t++)
            {
                _vatPositions[t]?.Dispose();
                _vatNormals[t]?.Dispose();
            }
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(_capsule);
                UnityEngine.Object.Destroy(_material);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(_capsule);
                UnityEngine.Object.DestroyImmediate(_material);
            }
        }
    }
}
