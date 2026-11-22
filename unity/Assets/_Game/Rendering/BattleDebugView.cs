using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GodGame.Bridge;
using GodGame.Sim;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Terrain = GodGame.Sim.Terrain;

namespace GodGame.Rendering
{
    /// <summary>
    /// The M2 debug view: a repo battle run by <see cref="BattleHost"/>, every soldier as an instanced capsule, the
    /// ground with switchable overlays (costs per class, the attackers' flow field, distance to lethal cells), the
    /// reachability check's guaranteed route and the attackers' predicted paths, and the simulation's fields: water
    /// as a see-through surface with flow streaks, wet and muddy ground, or the foot speed the fields leave (M3). Keys:
    /// O overlay, F fields, R routes, Space pause, +/- speed. Presentation only: it reads the simulation and never
    /// writes to it. The overlays come from a dry navigation of its own; the F view shows the battle's own speeds.
    /// <para>
    /// For the frame-rate exit check, a player started with <c>-measure-fps &lt;file&gt;</c> waits 5 s, measures
    /// <c>-measure-seconds</c> (default 20) and writes the frame statistics, then quits. To check the picture,
    /// <c>-screenshot &lt;file&gt; -screenshot-at &lt;game seconds&gt;</c> saves one frame then and quits; <c>-speed</c>
    /// sets the starting speed and <c>-fields 0|1|2</c> the field view.
    /// </para>
    /// </summary>
    public sealed class BattleDebugView : MonoBehaviour
    {
        [SerializeField]
        private string _mapId = "m3-river";

        [SerializeField]
        private string _planFile = "m3-river-two-thousand.json";

        [SerializeField]
        private ulong _seed = 1;

        [SerializeField]
        private float _speed = 1f;

        [SerializeField]
        private Shader _unitShader;

        [SerializeField]
        private Shader _groundShader;

        [SerializeField]
        private Shader _waterShader;

        [SerializeField]
        private Shader _lineShader;

        [SerializeField]
        private Shader _deckShader;

        [SerializeField]
        private Shader _markerShader;

        [SerializeField]
        private Shader _rainShader;

        [SerializeField]
        private Shader _skyShader;

        [SerializeField]
        private Shader _skirtShader;

        [SerializeField]
        private Shader _shadowShader;

        [SerializeField]
        private Shader _bannerShader;

        [SerializeField]
        private PanelSettings _panelSettings;

        [SerializeField]
        private StrategicCamera _camera;

        [SerializeField]
        private Mesh[] _unitMeshes;

        [SerializeField]
        private Mesh[] _unitMeshesLod2;

        [SerializeField]
        private UnitVatData[] _unitVats;

        [SerializeField]
        private Mesh _wreckMesh;

        [SerializeField]
        private Mesh _treeMesh;

        [SerializeField]
        private Mesh _felledTrunkMesh;

        [SerializeField]
        private Mesh _fireMesh;

        [SerializeField]
        private Mesh _boulderMesh;

        [SerializeField]
        private Mesh _hedgerowMesh;

        [SerializeField]
        private Mesh _wallDrystoneMesh;

        [SerializeField]
        private Mesh _herdPenMesh;

        [SerializeField]
        private Mesh _woodenBridgeMesh;

        [SerializeField]
        private Mesh _footbridgeMesh;

        private GodGame.Sim.Rules.Ruleset _rules;
        private BattlePlan _plan;
        private BattleHost _host;
        private UnitCapsuleRenderer _units;
        private DeckRenderer _deckRenderer;
        private WorldMarkerRenderer _worldMarkers;
        private StructureSnapshot _structures;
        private RegimentSnapshot _regiments;
        private BlockerSnapshot _blockers;
        private StrikeSnapshot _strikes;
        private HerdSnapshot _herds;
        private readonly List<WreckCellInstance> _wreckInstances = new List<WreckCellInstance>();
        private readonly List<StoneLandingInstance> _salvoInstances =
            new List<StoneLandingInstance>();
        private readonly List<TreePropInstance> _treeInstances = new List<TreePropInstance>();
        private readonly List<FelledTreeInstance> _felledTreeInstances =
            new List<FelledTreeInstance>();
        private readonly List<FireCellInstance> _fireInstances = new List<FireCellInstance>();
        private readonly List<LightningStrikeInstance> _strikeInstances =
            new List<LightningStrikeInstance>();
        private readonly List<HerdMarkerInstance> _herdInstances = new List<HerdMarkerInstance>();
        private readonly List<BoulderPropInstance> _boulderInstances =
            new List<BoulderPropInstance>();
        private readonly List<LineBarrierInstance> _barrierInstances =
            new List<LineBarrierInstance>();
        private readonly List<HerdPenPropInstance> _herdPenInstances =
            new List<HerdPenPropInstance>();
        private readonly List<RainCastInstance> _rainInstances = new List<RainCastInstance>();
        private readonly List<FlyingStoneInstance> _flyingStoneInstances =
            new List<FlyingStoneInstance>();
        private readonly List<ArrowVolleyInstance> _arrowInstances =
            new List<ArrowVolleyInstance>();
        private readonly List<RegimentBannerInstance> _bannerInstances =
            new List<RegimentBannerInstance>();
        private readonly Dictionary<int, int> _crushCountsScratch = new Dictionary<int, int>();
        private MapData _map;
        private Navigation _navigation;
        private FlowField _attackerFlow;
        private Texture2D _overlay;
        private Texture2D _terrainMask;
        private Color32[] _pixels;
        private Material _groundMaterial;
        private Material _waterMaterial;
        private Material _skirtMaterial;
        private Material _skyMaterial;
        private Material _previousSkybox;
        private GameObject _ground;
        private GameObject _water;
        private GameObject _skirt;
        private FieldUpload _fields;
        private FieldView _fieldView = FieldView.WetAndMud;
        private Mesh _groundMeshAsset;
        private Mesh _skirtMeshAsset;
        private readonly List<GameObject> _lines = new List<GameObject>();
        private OverlayMode _mode = OverlayMode.None;
        private bool _showRoutes = true;
        private Label _hud;
        private float _smoothedFrameMs = 16f;
        private float _nextHud;

        // Frame-rate measurement (players only).
        private string _measureFile;
        private float _measureSeconds = 20f;
        private float _measureStart = -1f;
        private readonly List<float> _frameMs = new List<float>();

        // Screenshots (players only): -screenshot <file> at -screenshot-at <game seconds>, then quit.
        private string _screenshotFile;
        private float _screenshotAt = 30f;
        private int _screenshotState; // 0 waiting, 1 capture requested, 2 done

        public BattleHost Host => _host;

        public int DrawnUnits => _units?.DrawnLastFrame ?? 0;

        private void OnEnable()
        {
            string repo = CommandLineValue("-repo") ?? RepoLoader.EditorRepoRoot;
            _mapId = CommandLineValue("-map") ?? _mapId;
            _planFile = CommandLineValue("-plan") ?? _planFile;
            _measureFile = CommandLineValue("-measure-fps");
            _screenshotFile = CommandLineValue("-screenshot");
            if (
                float.TryParse(
                    CommandLineValue("-screenshot-at"),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float at
                )
            )
            {
                _screenshotAt = at;
            }
            if (_screenshotFile != null || _measureFile != null)
            {
                Application.runInBackground = true; // unattended runs keep going without the focus
            }
            if (CommandLineValue("-speed") is string speed)
            {
                _speed = float.Parse(speed, CultureInfo.InvariantCulture);
            }
            if (CommandLineValue("-fields") is string view)
            {
                _fieldView = (FieldView)int.Parse(view, CultureInfo.InvariantCulture);
            }
            if (
                float.TryParse(
                    CommandLineValue("-measure-seconds"),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float seconds
                )
            )
            {
                _measureSeconds = seconds;
            }

            _rules = RepoLoader.LoadRules(repo);
            _map = RepoLoader.LoadMap(repo, _mapId);
            _plan = RepoLoader.LoadPlan(repo, _planFile, _map, _rules);
            var battle = new Battle(_rules, _map, _plan, _seed);
            _host = new BattleHost(battle) { Speed = _speed };
            _units = new UnitCapsuleRenderer(
                _unitShader,
                _host,
                _unitMeshes,
                _unitVats,
                _unitMeshesLod2,
                _shadowShader
            );
            _deckRenderer = new DeckRenderer(
                battle.PlayedMap,
                _deckShader != null ? _deckShader : Shader.Find("GodGame/DebugDeck"),
                _woodenBridgeMesh,
                _footbridgeMesh
            );
            _worldMarkers = new WorldMarkerRenderer(
                battle.PlayedMap,
                _markerShader != null ? _markerShader : Shader.Find("GodGame/DebugMarkers"),
                new WorldMarkerMeshes(
                    _wreckMesh,
                    _treeMesh,
                    _felledTrunkMesh,
                    _fireMesh,
                    _boulderMesh,
                    _hedgerowMesh,
                    _wallDrystoneMesh,
                    _herdPenMesh
                ),
                _rainShader,
                _shadowShader,
                _bannerShader
            );
            Shader resolvedSky =
                _skyShader != null ? _skyShader : Shader.Find("GodGame/StylizedSky");
            if (resolvedSky != null)
            {
                _previousSkybox = RenderSettings.skybox;
                _skyMaterial = new Material(resolvedSky);
                RenderSettings.skybox = _skyMaterial;
            }
            _structures = new StructureSnapshot();
            _regiments = new RegimentSnapshot();
            _blockers = new BlockerSnapshot();
            _strikes = new StrikeSnapshot();
            _herds = new HerdSnapshot();
            _fields = new FieldUpload(_map.Columns, _map.Rows);

            BuildNavigationViews(battle);
            BuildGround(battle.PlayedMap);
            BuildHud();
            if (_camera != null)
            {
                _camera.Frame(_map.WidthMm / 1000f, _map.DepthMm / 1000f);
                if (
                    TryParseVec3(CommandLineValue("-cam-pos"), out Vector3 camPos)
                    && TryParseVec3(CommandLineValue("-cam-look"), out Vector3 camLook)
                )
                {
                    _camera.SetCustomPose(camPos, camLook);
                }
            }
            _host.Start();
        }

        private void OnDisable()
        {
            _host?.Dispose();
            _units?.Dispose();
            _deckRenderer?.Dispose();
            _deckRenderer = null;
            _worldMarkers?.Dispose();
            _worldMarkers = null;
            foreach (var line in _lines)
            {
                Destroy(line.GetComponent<LineRenderer>().sharedMaterial);
                Destroy(line);
            }
            _lines.Clear();
            if (_ground != null)
            {
                Destroy(_ground);
            }
            if (_water != null)
            {
                Destroy(_water);
            }
            if (_skirt != null)
            {
                Destroy(_skirt);
            }
            if (_waterMaterial != null)
            {
                Destroy(_waterMaterial);
            }
            if (_skirtMaterial != null)
            {
                Destroy(_skirtMaterial);
            }
            if (_skyMaterial != null)
            {
                RenderSettings.skybox = _previousSkybox;
                Destroy(_skyMaterial);
                _skyMaterial = null;
            }
            _fields?.Dispose();
            _fields = null;
            if (_overlay != null)
            {
                Destroy(_overlay);
            }
            if (_terrainMask != null)
            {
                Destroy(_terrainMask);
                _terrainMask = null;
            }
            if (_groundMaterial != null)
            {
                Destroy(_groundMaterial);
            }
            if (_groundMeshAsset != null)
            {
                Destroy(_groundMeshAsset);
            }
            if (_skirtMeshAsset != null)
            {
                Destroy(_skirtMeshAsset);
            }
            _host = null;
            _units = null;
        }

        private void BuildNavigationViews(Battle battle)
        {
            // A navigation of its own for the overlays (derived data; the battle's lives on the worker thread).
            _navigation = new Navigation(battle.PlayedMap, battle.Rules, new SerialRunner());
            var deployed = battle.Start(new SerialRunner()).World;
            var routes = RoutePreview.AttackerRoutes(_navigation, deployed);
            for (int r = 0; r < routes.Length && _attackerFlow == null; r++)
            {
                if (routes[r].Count > 0)
                {
                    _attackerFlow = _navigation.FieldOf(r);
                }
            }
            foreach (var route in routes)
            {
                AddLine(route, new Color(1f, 0.85f, 0.2f), 0.6f);
            }
            try
            {
                var check = new Reachability(
                    _navigation.Costs,
                    battle.PlayedMap,
                    battle.Rules.Routes
                ).Check(new SerialRunner());
                for (int i = 0; i < check.Checks.Count; i++)
                {
                    var c = check.Checks[i];
                    if (
                        c.Side == Side.Attacker
                        && c.MovementClass == MovementClass.Foot
                        && c.Target == RouteTarget.Enemy
                    )
                    {
                        AddLine(check.Route(i), c.Ok ? new Color(0.2f, 1f, 0.4f) : Color.red, 2f);
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // A map without both zones has no guaranteed route to draw.
            }
        }

        private void AddLine(IReadOnlyList<FixVec2> points, Color color, float width)
        {
            if (points.Count < 2)
            {
                return;
            }
            var go = new GameObject("Route");
            var line = go.AddComponent<LineRenderer>();
            line.positionCount = points.Count;
            for (int i = 0; i < points.Count; i++)
            {
                line.SetPosition(i, Ground(points[i]) + Vector3.up * 1.5f);
            }
            line.widthMultiplier = width;
            line.material = new Material(_lineShader) { color = color };
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lines.Add(go);
        }

        private Vector3 Ground(FixVec2 p) =>
            new Vector3(
                p.X.Raw / (float)Fix.OneRaw,
                _navigation.Terrain.HeightMmAt(p) / 1000f,
                p.Y.Raw / (float)Fix.OneRaw
            );

        private void BuildGround(MapData map)
        {
            var terrain = _navigation.Terrain;
            // Every cell on maps up to about 200,000 cells, every other one beyond (the working map).
            int step = terrain.CellCount > 200_000 ? 2 : 1;
            var mesh = GroundMesh.Build(terrain, step, out float minH, out float maxH);
            _groundMeshAsset = mesh;
            _overlay = new Texture2D(terrain.Columns, terrain.Rows, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            _pixels = new Color32[terrain.CellCount];
            _groundMaterial = new Material(_groundShader);
            GroundMesh.BindTerrainTextures(_groundMaterial);
            _terrainMask = GroundMesh.BuildTerrainMask(map);
            _groundMaterial.SetTexture("_TerrainMask", _terrainMask);
            _groundMaterial.SetTexture("_Overlay", _overlay);
            _groundMaterial.SetVector(
                "_HeightRange",
                new Vector4(minH, Mathf.Max(maxH, minH + 1), 0, 0)
            );
            _ground = new GameObject("Debug ground");
            _ground.AddComponent<MeshFilter>().sharedMesh = mesh;
            _ground.AddComponent<MeshRenderer>().sharedMaterial = _groundMaterial;
            _fields.Bind(_groundMaterial);
            if (_waterShader != null)
            {
                _waterMaterial = new Material(_waterShader);
                var water = _host.Rules.Fields.Water;
                _waterMaterial.SetFloat("_DryDepthUm", water != null ? water.DryDepthUm : 5000);
                _fields.Bind(_waterMaterial);
                _water = new GameObject("Debug water");
                _water.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = _water.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _waterMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            Shader resolvedSkirt =
                _skirtShader != null ? _skirtShader : Shader.Find("GodGame/MapSkirt");
            if (resolvedSkirt != null)
            {
                _skirtMeshAsset = GroundMesh.BuildSkirt(terrain, step, minH);
                _skirtMaterial = new Material(resolvedSkirt);
                _skirt = new GameObject("Map skirt");
                _skirt.AddComponent<MeshFilter>().sharedMesh = _skirtMeshAsset;
                var skirtRenderer = _skirt.AddComponent<MeshRenderer>();
                skirtRenderer.sharedMaterial = _skirtMaterial;
                skirtRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            ApplyFieldView();
            RepaintOverlay();
        }

        private void RepaintOverlay()
        {
            OverlayPainter.Paint(_mode, _navigation, _attackerFlow, _pixels);
            _overlay.SetPixels32(_pixels);
            _overlay.Apply(false);
        }

        private void BuildHud()
        {
            if (_panelSettings == null)
            {
                return;
            }
            var document =
                gameObject.GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
            document.panelSettings = _panelSettings;
            _hud = new Label();
            _hud.style.color = new Color(0.95f, 0.93f, 0.87f, 1f);
            _hud.style.backgroundColor = new Color(0.07f, 0.09f, 0.13f, 0.86f);
            _hud.style.borderLeftWidth = 3;
            _hud.style.borderLeftColor = new Color(0.80f, 0.64f, 0.34f, 0.95f);
            _hud.style.borderTopWidth = 1;
            _hud.style.borderRightWidth = 1;
            _hud.style.borderBottomWidth = 1;
            _hud.style.borderTopColor = new Color(0.80f, 0.64f, 0.34f, 0.45f);
            _hud.style.borderRightColor = new Color(0.80f, 0.64f, 0.34f, 0.45f);
            _hud.style.borderBottomColor = new Color(0.80f, 0.64f, 0.34f, 0.45f);
            _hud.style.borderTopLeftRadius = 6;
            _hud.style.borderTopRightRadius = 6;
            _hud.style.borderBottomLeftRadius = 6;
            _hud.style.borderBottomRightRadius = 6;
            _hud.style.paddingLeft = 12;
            _hud.style.paddingRight = 12;
            _hud.style.paddingTop = 7;
            _hud.style.paddingBottom = 7;
            _hud.style.position = Position.Absolute;
            _hud.style.left = 12;
            _hud.style.top = 12;
            document.rootVisualElement.Add(_hud);
        }

        private void Update()
        {
            if (_host == null || _units == null)
            {
                return; // set-up failed (see the console)
            }
            HandleKeys();
            _fields.Update(_host);
            _host.ReadStructures(_structures);
            _host.ReadRegiments(_regiments);
            _host.ReadStrikes(_strikes);
            _host.ReadHerds(_herds);
            float camDist = _camera != null ? _camera.Distance : 900f;
            _units.UpdateMoralePalette(_regiments, camDist, false, _herds);
            _units.Draw(_host, camDist);
            _deckRenderer?.Draw(_structures, _host.Map, false);
            _host.ReadBlockers(_blockers);
            var causes = _host.ReadCauses();
            _host.Read(out _, out var latestUnits, out _);
            int markerTick = Math.Max(latestUnits.Tick, _blockers.Tick);
            int fallTicks = _rules?.Power("lightning")?.Lightning?.FallTicks ?? 40;
            SalvoAndBlockerProjection.CollectWrecks(
                _blockers,
                causes,
                _host.Map,
                _navigation?.Terrain,
                markerTick,
                filterByTime: false,
                includeCleared: false,
                _wreckInstances
            );
            SalvoAndBlockerProjection.CollectStoneLandings(
                causes,
                _host.Map,
                _navigation?.Terrain,
                markerTick,
                windowSeconds: 12f,
                _salvoInstances,
                _crushCountsScratch
            );
            SalvoAndBlockerProjection.CollectTrees(
                _host.Map,
                _navigation?.Terrain,
                _strikes,
                causes,
                markerTick,
                filterByTime: false,
                fallTicks,
                _treeInstances
            );
            SalvoAndBlockerProjection.CollectFelledTrees(
                _blockers,
                _host.Map,
                _navigation?.Terrain,
                _strikes,
                causes,
                markerTick,
                filterByTime: false,
                includeCleared: false,
                _felledTreeInstances
            );
            SalvoAndBlockerProjection.CollectFires(
                _blockers,
                _host.Map,
                _navigation?.Terrain,
                _strikes,
                causes,
                markerTick,
                filterByTime: false,
                includeExtinguished: false,
                _fireInstances
            );
            SalvoAndBlockerProjection.CollectLightningStrikes(
                _strikes,
                causes,
                _host.Map,
                _navigation?.Terrain,
                markerTick,
                windowSeconds: 1.5f,
                _strikeInstances
            );
            SalvoAndBlockerProjection.CollectHerdMarkers(
                _herds,
                latestUnits,
                _host.Map,
                _navigation?.Terrain,
                causes,
                markerTick,
                filterByTime: false,
                _herdInstances
            );
            _worldMarkers?.Draw(
                _wreckInstances,
                _salvoInstances,
                _treeInstances,
                _felledTreeInstances,
                _fireInstances,
                _strikeInstances,
                _herdInstances,
                Time.unscaledTime
            );
            SalvoAndBlockerProjection.CollectBoulders(
                _host.Map,
                _navigation?.Terrain,
                _boulderInstances
            );
            SalvoAndBlockerProjection.CollectLineBarriers(
                _plan,
                _host.Map,
                _navigation?.Terrain,
                _barrierInstances
            );
            SalvoAndBlockerProjection.CollectHerdPens(
                _host.Map,
                _navigation?.Terrain,
                _herdPenInstances
            );
            _worldMarkers?.DrawTerrainProps(
                _boulderInstances,
                _barrierInstances,
                _herdPenInstances,
                Time.unscaledTime
            );
            SalvoAndBlockerProjection.CollectActiveRainCasts(
                _plan,
                causes,
                _host.Map,
                _navigation?.Terrain,
                markerTick,
                _rainInstances
            );
            _worldMarkers?.DrawRain(_rainInstances, Time.unscaledTime);
            SalvoAndBlockerProjection.CollectFlyingStones(
                causes,
                latestUnits,
                _plan,
                _host.Map,
                _navigation?.Terrain,
                markerTick,
                _flyingStoneInstances
            );
            SalvoAndBlockerProjection.CollectArrowVolleys(
                _plan,
                causes,
                latestUnits,
                _host.Map,
                _navigation?.Terrain,
                markerTick,
                _arrowInstances
            );
            _worldMarkers?.DrawProjectiles(
                _flyingStoneInstances,
                _arrowInstances,
                Time.unscaledTime
            );
            SalvoAndBlockerProjection.CollectRegimentBanners(
                latestUnits,
                _regiments,
                _plan,
                _host.Map,
                _bannerInstances
            );
            _worldMarkers?.DrawBanners(_bannerInstances, Time.unscaledTime);
            float ms = Time.unscaledDeltaTime * 1000f;
            _smoothedFrameMs = Mathf.Lerp(_smoothedFrameMs, ms, 0.05f);
            if (_hud != null && Time.unscaledTime >= _nextHud)
            {
                _nextHud = Time.unscaledTime + 0.25f; // a few times a second: formatting allocates
                _host.Read(out _, out var current, out _);
                _hud.text = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}  tick {1} ({2:0.0} s)  units {3}\n{4:0} fps ({5:0.0} ms)  tick {6:0.00} ms  speed {7}x\noverlay [O] {8}  fields [F] {11}  routes [R] {9}  pause [Space]  speed [+/-]{10}",
                    _mapId,
                    current.Tick,
                    current.Tick / (float)SimConstants.TicksPerSecond,
                    DrawnUnits,
                    1000f / _smoothedFrameMs,
                    _smoothedFrameMs,
                    _host.LastTickMs,
                    _host.Speed,
                    _mode,
                    _showRoutes ? "on" : "off",
                    _host.Failure != null ? "\nSIMULATION STOPPED: " + _host.Failure.Message : "",
                    _fieldView
                );
            }
            Measure(ms);
            Screenshot();
        }

        private void HandleKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }
            if (keyboard.oKey.wasPressedThisFrame)
            {
                _mode = (OverlayMode)(
                    ((int)_mode + 1) % Enum.GetValues(typeof(OverlayMode)).Length
                );
                RepaintOverlay();
            }
            if (keyboard.fKey.wasPressedThisFrame)
            {
                _fieldView = (FieldView)(((int)_fieldView + 1) % 3);
                ApplyFieldView();
            }
            if (keyboard.rKey.wasPressedThisFrame)
            {
                _showRoutes = !_showRoutes;
                foreach (var line in _lines)
                {
                    line.SetActive(_showRoutes);
                }
            }
            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                _host.Speed = _host.Speed > 0 ? 0 : _speed;
            }
            if (
                keyboard.equalsKey.wasPressedThisFrame || keyboard.numpadPlusKey.wasPressedThisFrame
            )
            {
                _speed = Mathf.Min(_speed * 2, 16);
                _host.Speed = _speed;
            }
            if (
                keyboard.minusKey.wasPressedThisFrame || keyboard.numpadMinusKey.wasPressedThisFrame
            )
            {
                _speed = Mathf.Max(_speed / 2, 0.25f);
                _host.Speed = _speed;
            }
        }

        private enum FieldView
        {
            Off = 0,
            WetAndMud = 1,
            FootSpeed = 2,
        }

        private void ApplyFieldView()
        {
            _groundMaterial.SetFloat("_FieldMode", (float)_fieldView);
            if (_water != null)
            {
                _water.SetActive(_fieldView != FieldView.Off);
            }
        }

        /// <summary>For checking the picture without a person at the screen: capture once the battle reaches the time, then quit.</summary>
        private void Screenshot()
        {
            if (_screenshotFile == null || _screenshotState == 2)
            {
                return;
            }
            if (_screenshotState == 1)
            {
                _screenshotState = 2;
                Application.Quit(0);
                return;
            }
            _host.Read(out _, out var current, out _);
            if (current.Tick >= _screenshotAt * SimConstants.TicksPerSecond)
            {
                ScreenCapture.CaptureScreenshot(_screenshotFile);
                _screenshotState = 1; // quit next frame, once the capture is written
            }
        }

        private void Measure(float ms)
        {
            if (_measureFile == null)
            {
                return;
            }
            float now = Time.realtimeSinceStartup;
            if (_measureStart < 0)
            {
                _measureStart = now + 5f; // warm-up
                return;
            }
            if (now < _measureStart)
            {
                return;
            }
            _frameMs.Add(ms);
            if (now - _measureStart < _measureSeconds)
            {
                return;
            }
            _frameMs.Sort();
            float total = 0;
            foreach (float f in _frameMs)
            {
                total += f;
            }
            float mean = total / _frameMs.Count;
            string report = string.Format(
                CultureInfo.InvariantCulture,
                "frames {0}\nmean_ms {1:0.00}\nmean_fps {2:0.0}\np95_ms {3:0.00}\nunits {4}\nresolution {5}x{6}\ngpu {7}\nlast_tick_ms {8:0.00}\n",
                _frameMs.Count,
                mean,
                1000f / mean,
                _frameMs[(int)(_frameMs.Count * 0.95f)],
                DrawnUnits,
                Screen.width,
                Screen.height,
                SystemInfo.graphicsDeviceName,
                _host.LastTickMs
            );
            File.WriteAllText(_measureFile, report);
            _measureFile = null;
            Application.Quit(0);
        }

        private static string CommandLineValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static bool TryParseVec3(string raw, out Vector3 value)
        {
            value = Vector3.zero;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }
            string[] parts = raw.Split(',');
            if (parts.Length != 3)
            {
                return false;
            }
            if (
                float.TryParse(
                    parts[0],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float x
                )
                && float.TryParse(
                    parts[1],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float y
                )
                && float.TryParse(
                    parts[2],
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float z
                )
            )
            {
                value = new Vector3(x, y, z);
                return true;
            }
            return false;
        }
    }
}
