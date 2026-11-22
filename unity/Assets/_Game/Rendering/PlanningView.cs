using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GodGame.Bridge;
using GodGame.Sim;
using GodGame.Sim.Math;
using GodGame.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Terrain = GodGame.Sim.Terrain;

namespace GodGame.Rendering
{
    /// <summary>
    /// Planning UI v0 (plan, M4): the plan edited in place while the preview worker fills its future in the background,
    /// and the future scrubbed from the playback track. Unity writes only the plan (through <see cref="PlanEditor"/>, whose
    /// edits the simulation's loader checks) and draws what the simulation computed.
    /// <list type="bullet">
    /// <item>Timeline bar (bottom): the buffer bar shows how far the future is filled; click or drag it to scrub; casts are
    /// markers you drag along it, red with their reason when the range rule fails at their tick. Space plays and pauses.</item>
    /// <item>Tools: 1 move regiments (drag), 2 rain (click to cast at the scrub time; [ and ] change its radius), 3 boulder,
    /// 4 channel (two clicks), Delete removes the selected cast or piece.</item>
    /// <item>Held keys: V the foot-speed view, N the no-cast rings (100 m around every regiment at the scrub time). G toggles
    /// the ghost of the plan before the last edit.</item>
    /// <item>Right-click a regiment: the causality trace of its latest losses.</item>
    /// </list>
    /// </summary>
    public sealed class PlanningView : MonoBehaviour
    {
        [SerializeField]
        private string _mapId = "m3-river";

        [SerializeField]
        private string _planFile = "m3-river-two-thousand.json";

        [SerializeField]
        private string _levelFile = "";

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

        private enum Tool
        {
            Move,
            Rain,
            Boulder,
            Channel,
            Lightning,
        }

        private GodGame.Sim.Rules.Ruleset _rules;
        private MapData _map;
        private LevelData _level;
        private Terrain _terrain;
        private PlanEditor _editor;

        /// <summary>The plan as loaded from disk: [R] puts it back.</summary>
        private BattlePlan _loadedPlan;
        private PreviewWorker _worker;
        private TrackReader _reader;
        private TrackReader _ghostReader;
        private PlaybackTrack _ghostTrack;
        private BattlePlan _shownPlan;
        private UnitCapsuleRenderer _units;
        private UnitCapsuleRenderer _ghost;
        private DeckRenderer _deckRenderer;
        private WorldMarkerRenderer _worldMarkers;
        private StructureSnapshot _structures;
        private RegimentSnapshot _regiments;
        private BlockerSnapshot _blockers;
        private StrikeSnapshot _strikes;
        private HerdSnapshot _herds;
        private HerdSnapshot _scrubHerds;
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
        private readonly Dictionary<int, int> _crushCountsScratch = new Dictionary<int, int>();
        private FieldUpload _fields;
        private uint[] _fieldCells;
        private int _fieldTick = -1;
        private PlaybackTrack _fieldTrack;
        private Texture2D _overlayTexture;
        private Texture2D _terrainMask;
        private Color32[] _overlayPixels;
        private bool _overlayDirty = true;
        private int _lastOverlayTick = int.MinValue;
        private int _lastOverlayCauses = -1;
        private PlaybackTrack _lastOverlayTrack;
        private bool _lastOverlayLanding;
        private bool _lastOverlayStrain;
        private Material _groundMaterial;
        private Material _waterMaterial;
        private Material _skirtMaterial;
        private Material _skyMaterial;
        private Material _previousSkybox;
        private GameObject _ground;
        private GameObject _water;
        private GameObject _skirt;
        private Mesh _mesh;
        private Mesh _skirtMesh;
        private LineRenderer _objectiveLine;
        private readonly List<LineRenderer> _rings = new List<LineRenderer>();
        private readonly List<LineRenderer> _castCircles = new List<LineRenderer>();

        private Tool _tool = Tool.Rain;
        private float _scrubSeconds;
        private bool _playing;
        private bool _showGhost = true;
        private bool _showStrainOverlay;
        private bool _boostMoraleTints;
        private int _radiusMm = 80_000;
        private string _selectedCast;
        private string _selectedPiece;
        private string _dragRegiment;
        private bool _draggingCastOrPiece;
        private int _dragCastStartXMm;
        private int _dragCastStartYMm;
        private Vector3? _channelStart;
        private string _trace = "";

        private Label _hud;
        private Label _tracePanel;
        private ResultsScreen _results;
        private PlaybackTrack _resultsTrack;
        private int _resultsCausesCount = -1;
        private BattleOutcomeKind _resultsOutcomeStatus = BattleOutcomeKind.Ongoing;
        private int _resultsAutoShownForEdits = -1;
        private VisualElement _bar;
        private VisualElement _filled;
        private VisualElement _cursor;
        private readonly List<VisualElement> _markers = new List<VisualElement>();
        private string _dragMarker;
        private bool _scrubbing;

        // Unattended checks (players only): -measure-preview <file> times a cast edit's refill and 5 s of scrubbing.
        private string _measureFile;
        private int _measureStage;
        private float _measureStart;
        private readonly List<float> _frameMs = new List<float>();

        // -screenshot <file> [-scrub seconds] [-rings]: once the future is filled, scrub there, capture, quit.
        private string _screenshotFile;
        private float _screenshotScrub = 120f;
        private bool _forceRings;
        private int _screenshotState;

        private void OnEnable()
        {
            string repo = CommandLineValue("-repo") ?? RepoLoader.EditorRepoRoot;
            _mapId = CommandLineValue("-map") ?? _mapId;
            _planFile = CommandLineValue("-plan") ?? _planFile;
            _levelFile = CommandLineValue("-level") ?? _levelFile;
            _measureFile = CommandLineValue("-measure-preview");
            _screenshotFile = CommandLineValue("-screenshot");
            _forceRings = Array.IndexOf(Environment.GetCommandLineArgs(), "-rings") >= 0;
            if (
                float.TryParse(
                    CommandLineValue("-scrub"),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out float scrub
                )
            )
            {
                _screenshotScrub = scrub;
            }
            if (_screenshotFile != null)
            {
                Application.runInBackground = true;
            }
            if (_measureFile != null)
            {
                Application.runInBackground = true;
            }
            _rules = RepoLoader.LoadRules(repo);
            BattlePlan plan;
            int seed = 1;
            if (!string.IsNullOrEmpty(_levelFile))
            {
                var header = RepoLoader.LoadLevel(repo, _levelFile, null, _rules);
                _mapId = header.MapId;
                _map = RepoLoader.LoadMap(repo, _mapId);
                _level = RepoLoader.LoadLevel(repo, _levelFile, _map, _rules);
                _planFile = _level.PlanFile;
                seed = _level.Seed;
                plan = RepoLoader.LoadPlanForLevel(repo, _level, _map, _rules);
            }
            else
            {
                _map = RepoLoader.LoadMap(repo, _mapId);
                _level = null;
                plan = RepoLoader.LoadPlan(repo, _planFile, _map, _rules);
            }
            _loadedPlan = plan;
            _editor = new PlanEditor(plan, _map, _rules);
            var battle = new Battle(_rules, _map, plan, (ulong)seed);
            int endTick = _level != null ? _level.Outcome.SunsetTick : PreviewWorker.DefaultEndTick;
            _worker = new PreviewWorker(
                battle,
                new ThreadedRunner(Math.Max(1, SystemInfo.processorCount - 2)),
                new KeyframeStore(512L << 20),
                endTick
            );
            if (_level != null)
            {
                _worker.SetOutcomeSpec(_level.Outcome);
            }
            _worker.Start();
            BuildScene(battle);
            BuildUi();
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
        }

        private void OnDisable()
        {
            _worker?.Dispose();
            _units?.Dispose();
            _ghost?.Dispose();
            _deckRenderer?.Dispose();
            _worldMarkers?.Dispose();
            _fields?.Dispose();
            if (_objectiveLine != null)
            {
                Destroy(_objectiveLine.gameObject);
            }
            foreach (var line in _rings)
            {
                if (line != null) // already gone when the application quits
                {
                    Destroy(line.gameObject);
                }
            }
            foreach (var line in _castCircles)
            {
                if (line != null) // already gone when the application quits
                {
                    Destroy(line.gameObject);
                }
            }
            _rings.Clear();
            _castCircles.Clear();
            _objectiveLine = null;
            _deckRenderer = null;
            _worldMarkers = null;
            _results = null;
            Destroy(_ground);
            Destroy(_water);
            if (_skirt != null)
            {
                Destroy(_skirt);
            }
            Destroy(_overlayTexture);
            if (_terrainMask != null)
            {
                Destroy(_terrainMask);
                _terrainMask = null;
            }
            Destroy(_groundMaterial);
            Destroy(_waterMaterial);
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
            Destroy(_mesh);
            if (_skirtMesh != null)
            {
                Destroy(_skirtMesh);
            }
            _worker = null;
        }

        private void BuildScene(Battle battle)
        {
            _terrain = new Terrain(battle.PlayedMap);
            int step = _terrain.CellCount > 200_000 ? 2 : 1;
            _mesh = GroundMesh.Build(_terrain, step, out float minH, out float maxH);
            Shader resolvedSky =
                _skyShader != null ? _skyShader : Shader.Find("GodGame/StylizedSky");
            if (resolvedSky != null)
            {
                _previousSkybox = RenderSettings.skybox;
                _skyMaterial = new Material(resolvedSky);
                RenderSettings.skybox = _skyMaterial;
            }
            _groundMaterial = new Material(_groundShader);
            GroundMesh.BindTerrainTextures(_groundMaterial);
            _terrainMask = GroundMesh.BuildTerrainMask(battle.PlayedMap);
            _groundMaterial.SetTexture("_TerrainMask", _terrainMask);
            _overlayTexture = new Texture2D(_map.Columns, _map.Rows, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            _overlayPixels = new Color32[_map.Columns * _map.Rows];
            Array.Fill(_overlayPixels, OverlayPainter.Clear);
            _overlayTexture.SetPixels32(_overlayPixels);
            _overlayTexture.Apply(false, false);
            _groundMaterial.SetTexture("_Overlay", _overlayTexture);
            _groundMaterial.SetVector(
                "_HeightRange",
                new Vector4(minH, Mathf.Max(maxH, minH + 1), 0, 0)
            );
            _ground = new GameObject("Planning ground");
            _ground.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _ground.AddComponent<MeshRenderer>().sharedMaterial = _groundMaterial;
            _ground.AddComponent<MeshCollider>().sharedMesh = _mesh; // for pointing at the map, never for gameplay
            _fields = new FieldUpload(_map.Columns, _map.Rows);
            _fieldCells = new uint[_map.Columns * _map.Rows * FieldUpload.Stride];
            _fields.Bind(_groundMaterial);
            _waterMaterial = new Material(_waterShader);
            _waterMaterial.SetFloat(
                "_DryDepthUm",
                _rules.Fields.Water != null ? _rules.Fields.Water.DryDepthUm : 5000
            );
            _fields.Bind(_waterMaterial);
            _water = new GameObject("Planning water");
            _water.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _water.AddComponent<MeshRenderer>().sharedMaterial = _waterMaterial;
            Shader resolvedSkirt =
                _skirtShader != null ? _skirtShader : Shader.Find("GodGame/MapSkirt");
            if (resolvedSkirt != null)
            {
                _skirtMesh = GroundMesh.BuildSkirt(_terrain, step, minH);
                _skirtMaterial = new Material(resolvedSkirt);
                _skirt = new GameObject("Planning skirt");
                _skirt.AddComponent<MeshFilter>().sharedMesh = _skirtMesh;
                var skirtRenderer = _skirt.AddComponent<MeshRenderer>();
                skirtRenderer.sharedMaterial = _skirtMaterial;
                skirtRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
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
                _shadowShader
            );
            _structures = new StructureSnapshot();
            _regiments = new RegimentSnapshot();
            _blockers = new BlockerSnapshot();
            _strikes = new StrikeSnapshot();
            _herds = new HerdSnapshot();
            _scrubHerds = new HerdSnapshot();
            if (
                _level != null
                && _level.Outcome.Variant == LevelOutcomeVariant.Objective
                && _level.Outcome.ObjectivePointCount >= 3
            )
            {
                _objectiveLine = NewLine("Objective Zone", new Color(1f, 0.85f, 0.2f, 0.9f), 2.0f);
                int ptCount = _level.Outcome.ObjectivePointCount;
                _objectiveLine.positionCount = ptCount;
                for (int i = 0; i < ptCount; i++)
                {
                    int xMm = _level.Outcome.ObjectivePolygonMm[i * 2];
                    int yMm = _level.Outcome.ObjectivePolygonMm[i * 2 + 1];
                    var p = new FixVec2(Fix.FromRatio(xMm, 1000), Fix.FromRatio(yMm, 1000));
                    _objectiveLine.SetPosition(
                        i,
                        new Vector3(xMm / 1000f, _terrain.HeightMmAt(p) / 1000f + 1.8f, yMm / 1000f)
                    );
                }
            }
            ResetReaders(battle);
        }

        private void ResetReaders(Battle battle)
        {
            var reader = new TrackReader(battle);
            _units?.Dispose();
            _units = new UnitCapsuleRenderer(
                _unitShader,
                reader.Capacity,
                _rules,
                reader.RegimentSides,
                _map,
                null,
                _unitMeshes,
                _unitVats,
                _unitMeshesLod2,
                _shadowShader
            );
            _reader = reader;
            if (_groundMaterial != null && battle.PlayedMap != null)
            {
                if (_terrainMask != null)
                {
                    Destroy(_terrainMask);
                }
                _terrainMask = GroundMesh.BuildTerrainMask(battle.PlayedMap);
                _groundMaterial.SetTexture("_TerrainMask", _terrainMask);
            }
        }

        private void BuildUi()
        {
            if (_panelSettings == null)
            {
                return;
            }
            var document =
                gameObject.GetComponent<UIDocument>() ?? gameObject.AddComponent<UIDocument>();
            document.panelSettings = _panelSettings;
            var root = document.rootVisualElement;
            _hud = Panel(new Label(), 8, 8);
            root.Add(_hud);
            _tracePanel = Panel(new Label(), 8, 120);
            _tracePanel.style.maxWidth = 560;
            _tracePanel.style.whiteSpace = WhiteSpace.Normal;
            root.Add(_tracePanel);

            _bar = new VisualElement();
            _bar.style.position = Position.Absolute;
            _bar.style.left = 16;
            _bar.style.right = 16;
            _bar.style.bottom = 16;
            _bar.style.height = 28;
            _bar.style.backgroundColor = new Color(0, 0, 0, 0.55f);
            _filled = new VisualElement();
            _filled.style.position = Position.Absolute;
            _filled.style.left = 0;
            _filled.style.top = 0;
            _filled.style.bottom = 0;
            _filled.style.backgroundColor = new Color(0.35f, 0.55f, 0.85f, 0.6f);
            _cursor = new VisualElement();
            _cursor.style.position = Position.Absolute;
            _cursor.style.top = -4;
            _cursor.style.bottom = -4;
            _cursor.style.width = 3;
            _cursor.style.backgroundColor = Color.white;
            _bar.Add(_filled);
            _bar.Add(_cursor);
            _bar.RegisterCallback<PointerDownEvent>(e =>
            {
                _scrubbing = true;
                _bar.CapturePointer(e.pointerId);
                Scrub(e.localPosition.x);
            });
            _bar.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (_dragMarker != null)
                {
                    MoveCastMarker(e.localPosition.x);
                }
                else if (_scrubbing)
                {
                    Scrub(e.localPosition.x);
                }
            });
            _bar.RegisterCallback<PointerUpEvent>(e =>
            {
                if (_dragMarker != null)
                {
                    CommitEdit();
                }
                _dragMarker = null;
                _scrubbing = false;
                _bar.ReleasePointer(e.pointerId);
            });
            root.Add(_bar);
            _results = new ResultsScreen(root);
        }

        private static Label Panel(Label label, float left, float top)
        {
            label.style.color = Color.white;
            label.style.backgroundColor = new Color(0, 0, 0, 0.55f);
            label.style.paddingLeft = 8;
            label.style.paddingRight = 8;
            label.style.paddingTop = 4;
            label.style.paddingBottom = 4;
            label.style.position = Position.Absolute;
            label.style.left = left;
            label.style.top = top;
            return label;
        }

        private float EndSeconds => _worker.EndTick / (float)SimConstants.TicksPerSecond;

        private void Scrub(float x)
        {
            float width = Mathf.Max(1, _bar.resolvedStyle.width);
            _scrubSeconds = Mathf.Clamp01(x / width) * EndSeconds;
        }

        private void MoveCastMarker(float x)
        {
            var cast = _editor.Casts.Find(c => c.Id == _dragMarker);
            if (cast == null)
            {
                return;
            }
            float width = Mathf.Max(1, _bar.resolvedStyle.width);
            cast.Tick = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Clamp01(x / width) * _worker.EndTick),
                0,
                _rules.LatestCastTick
            );
        }

        private void Update()
        {
            if (_worker == null)
            {
                return;
            }
            HandleInput();
            if (_playing)
            {
                _scrubSeconds = Mathf.Min(EndSeconds, _scrubSeconds + Time.deltaTime);
            }

            // The worker moved to a new plan: its first published track; the old one becomes the ghost.
            var plan = _worker.Plan;
            if (!ReferenceEquals(plan, _shownPlan))
            {
                if (_shownPlan != null)
                {
                    _ghostTrack = _fieldTrack;
                    _ghostReader = _reader;
                    if (_ghost == null)
                    {
                        _ghost = new UnitCapsuleRenderer(
                            _unitShader,
                            _reader.Capacity,
                            _rules,
                            _reader.RegimentSides,
                            _map,
                            new Color(0.6f, 0.6f, 0.6f),
                            _unitMeshes,
                            _unitVats
                        );
                    }
                    ResetReaders(new Battle(_rules, _map, plan, 1));
                }
                _shownPlan = plan;
            }

            var track = _worker.Track;
            int scrubTick = Mathf.RoundToInt(_scrubSeconds * SimConstants.TicksPerSecond);
            _worker.ReadBlockers(_blockers);
            _worker.ReadStrikes(_strikes);
            _worker.ReadHerds(_herds);
            var causes = _worker.ReadCauses();
            BuildScrubHerds(causes, scrubTick);
            _reader.ReadStructures(track, _scrubSeconds, _structures);
            _reader.ReadRegiments(track, _scrubSeconds, _regiments);
            float camDist = _camera != null ? _camera.Distance : 900f;
            _units.UpdateMoralePalette(_regiments, camDist, _boostMoraleTints, _scrubHerds);
            bool hasCurrent = _reader.Read(
                track,
                _scrubSeconds,
                out var previous,
                out var current,
                out float alpha
            );
            if (hasCurrent)
            {
                _units.Draw(previous, current, alpha, _reader, track, _scrubSeconds, camDist);
            }
            if (
                _showGhost
                && _ghostReader != null
                && _ghost != null
                && _ghostReader.Read(
                    _ghostTrack,
                    _scrubSeconds,
                    out var gp,
                    out var gc,
                    out float ga
                )
            )
            {
                _ghost.Draw(gp, gc, ga, _ghostReader, _ghostTrack, _scrubSeconds, camDist);
            }
            MapData activeMap = _reader.Map ?? _map;
            Terrain activeTerrain = _reader.Terrain ?? _terrain;
            int fallTicks = _rules.Power("lightning")?.Lightning?.FallTicks ?? 40;
            _deckRenderer?.Draw(_structures, activeMap, _showStrainOverlay);
            SalvoAndBlockerProjection.CollectWrecks(
                _blockers,
                causes,
                activeMap,
                activeTerrain,
                scrubTick,
                filterByTime: true,
                includeCleared: false,
                _wreckInstances
            );
            SalvoAndBlockerProjection.CollectStoneLandings(
                causes,
                activeMap,
                activeTerrain,
                scrubTick,
                windowSeconds: 12f,
                _salvoInstances,
                _crushCountsScratch
            );
            SalvoAndBlockerProjection.CollectTrees(
                activeMap,
                activeTerrain,
                _strikes,
                causes,
                scrubTick,
                filterByTime: true,
                fallTicks,
                _treeInstances
            );
            SalvoAndBlockerProjection.CollectFelledTrees(
                _blockers,
                activeMap,
                activeTerrain,
                _strikes,
                causes,
                scrubTick,
                filterByTime: true,
                includeCleared: false,
                _felledTreeInstances
            );
            SalvoAndBlockerProjection.CollectFires(
                _blockers,
                activeMap,
                activeTerrain,
                _strikes,
                causes,
                scrubTick,
                filterByTime: true,
                includeExtinguished: false,
                _fireInstances
            );
            SalvoAndBlockerProjection.CollectLightningStrikes(
                _strikes,
                causes,
                activeMap,
                activeTerrain,
                scrubTick,
                windowSeconds: 1.5f,
                _strikeInstances
            );
            SalvoAndBlockerProjection.CollectHerdMarkers(
                _herds,
                hasCurrent ? current : null,
                activeMap,
                activeTerrain,
                causes,
                scrubTick,
                filterByTime: true,
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
            SalvoAndBlockerProjection.CollectBoulders(activeMap, activeTerrain, _boulderInstances);
            SalvoAndBlockerProjection.CollectLineBarriers(
                plan,
                activeMap,
                activeTerrain,
                _barrierInstances
            );
            SalvoAndBlockerProjection.CollectHerdPens(activeMap, activeTerrain, _herdPenInstances);
            _worldMarkers?.DrawTerrainProps(
                _boulderInstances,
                _barrierInstances,
                _herdPenInstances,
                Time.unscaledTime
            );
            SalvoAndBlockerProjection.CollectActiveRainCasts(
                plan,
                causes,
                activeMap,
                activeTerrain,
                scrubTick,
                _rainInstances
            );
            _worldMarkers?.DrawRain(_rainInstances, Time.unscaledTime);
            SalvoAndBlockerProjection.CollectFlyingStones(
                causes,
                hasCurrent ? current : null,
                plan,
                activeMap,
                activeTerrain,
                scrubTick,
                _flyingStoneInstances
            );
            SalvoAndBlockerProjection.CollectArrowVolleys(
                plan,
                causes,
                hasCurrent ? current : null,
                activeMap,
                activeTerrain,
                scrubTick,
                _arrowInstances
            );
            _worldMarkers?.DrawProjectiles(
                _flyingStoneInstances,
                _arrowInstances,
                Time.unscaledTime
            );
            UploadFields(track);
            UpdateOverlay(track);
            DrawRings(track);
            DrawCasts();
            UpdateResults(track, plan);
            UpdateUi();
            Measure();
            Screenshot();
        }

        private void UpdateOverlay(PlaybackTrack track)
        {
            if (_overlayTexture == null || _overlayPixels == null)
            {
                return;
            }
            bool showLanding =
                (Keyboard.current != null && Keyboard.current.lKey.isPressed)
                || _dragMarker != null
                || _dragRegiment != null
                || _channelStart != null
                || _draggingCastOrPiece;
            bool showStrain = _showStrainOverlay;
            int tickBucket = (int)(_scrubSeconds * SimConstants.TicksPerSecond) / 4;
            var spreads = showLanding ? _worker.ReadLandingSpreads() : Array.Empty<LandingSpread>();
            int causeCount = spreads.Length;
            if (
                !_overlayDirty
                && ReferenceEquals(track, _lastOverlayTrack)
                && tickBucket == _lastOverlayTick
                && causeCount == _lastOverlayCauses
                && showLanding == _lastOverlayLanding
                && showStrain == _lastOverlayStrain
            )
            {
                return;
            }
            Array.Fill(_overlayPixels, OverlayPainter.Clear);
            if (showStrain)
            {
                OverlayPainter.PaintStrain(_structures, _overlayPixels, clearFirst: false);
            }
            if (showLanding)
            {
                OverlayPainter.PaintLandingSpread(
                    spreads,
                    _map,
                    _rules.Launches.PreviewCellMm,
                    _overlayPixels,
                    clearFirst: false
                );
            }
            _overlayTexture.SetPixels32(_overlayPixels);
            _overlayTexture.Apply(false, false);
            _overlayDirty = false;
            _lastOverlayTrack = track;
            _lastOverlayTick = tickBucket;
            _lastOverlayCauses = causeCount;
            _lastOverlayLanding = showLanding;
            _lastOverlayStrain = showStrain;
        }

        private void UpdateResults(PlaybackTrack track, BattlePlan plan)
        {
            if (_results == null)
            {
                return;
            }
            var outcome = _worker.ReadOutcome();
            var causes = _worker.ReadCauses();
            var progress = _worker.ReadProgress();
            var status = outcome != null ? outcome.Kind : BattleOutcomeKind.Ongoing;
            if (
                !ReferenceEquals(track, _resultsTrack)
                || causes.Length != _resultsCausesCount
                || status != _resultsOutcomeStatus
            )
            {
                _results.SetData(outcome, causes, plan, _level);
                _resultsTrack = track;
                _resultsCausesCount = causes.Length;
                _resultsOutcomeStatus = status;
            }
            int scrubTick = (int)(_scrubSeconds * SimConstants.TicksPerSecond);
            int decidedTick =
                outcome != null && outcome.DecidedTick > 0
                    ? outcome.DecidedTick
                    : (outcome?.EvaluatedTick ?? int.MaxValue);
            if (
                _playing
                && _screenshotFile == null
                && _measureFile == null
                && outcome != null
                && outcome.Kind != BattleOutcomeKind.Ongoing
                && scrubTick >= decidedTick
                && _resultsAutoShownForEdits != progress.Edits
            )
            {
                _playing = false;
                _results.Show();
                _resultsAutoShownForEdits = progress.Edits;
            }
        }

        private void UploadFields(PlaybackTrack track)
        {
            int tick = (int)(_scrubSeconds * SimConstants.TicksPerSecond);
            int frameTick = tick / PlaybackTrack.FrameTicks * PlaybackTrack.FrameTicks;
            if (
                track == null
                || (
                    frameTick == _fieldTick
                    && ReferenceEquals(track, _fieldTrack)
                    && track.FrameCount * PlaybackTrack.FrameTicks > frameTick
                )
            )
            {
                return;
            }
            if (track.FieldsAt(tick, _fieldCells) >= 0)
            {
                _fields.Upload(_fieldCells);
                _fieldTick = frameTick;
            }
            _fieldTrack = track;
            bool speed = Keyboard.current != null && Keyboard.current.vKey.isPressed;
            _groundMaterial.SetFloat("_FieldMode", speed ? 2 : 1);
        }

        private void DrawRings(PlaybackTrack track)
        {
            bool show =
                _forceRings || (Keyboard.current != null && Keyboard.current.nKey.isPressed);
            var sample = track?.SampleAtOrBefore(
                (int)(_scrubSeconds * SimConstants.TicksPerSecond)
            );
            int regiments = _reader.RegimentSides.Length;
            while (_rings.Count < regiments)
            {
                _rings.Add(NewLine("No-cast ring", new Color(0.9f, 0.2f, 0.2f, 0.8f), 1.2f));
            }
            for (int r = 0; r < _rings.Count; r++)
            {
                _rings[r].gameObject.SetActive(false);
            }
            if (
                !show
                || sample == null
                || !_reader.Read(track, _scrubSeconds, out _, out var current, out _)
            )
            {
                return;
            }
            // Each regiment's centre and spread at the scrub time; the ring is 100 m beyond its outer soldiers.
            var sumX = new float[regiments];
            var sumZ = new float[regiments];
            var n = new int[regiments];
            for (int u = 0; u < current.Count; u++)
            {
                if (current.Alive[u])
                {
                    int r = current.Regiment[u];
                    sumX[r] += current.X[u];
                    sumZ[r] += current.Z[u];
                    n[r]++;
                }
            }
            var spread = new float[regiments];
            for (int u = 0; u < current.Count; u++)
            {
                if (current.Alive[u])
                {
                    int r = current.Regiment[u];
                    float dx = current.X[u] - sumX[r] / n[r];
                    float dz = current.Z[u] - sumZ[r] / n[r];
                    spread[r] = Mathf.Max(spread[r], Mathf.Sqrt(dx * dx + dz * dz));
                }
            }
            for (int r = 0; r < regiments; r++)
            {
                if (n[r] == 0 || _reader.RegimentSides[r] == Side.Neutral)
                {
                    continue;
                }
                float range =
                    (_rules.Powers.Count > 0 ? _rules.Powers[0].RangeMm : 100_000) / 1000f;
                Circle(_rings[r], new Vector2(sumX[r] / n[r], sumZ[r] / n[r]), spread[r] + range);
                _rings[r].gameObject.SetActive(true);
            }
        }

        private void DrawCasts()
        {
            var results = _worker.ReadCasts();
            while (_castCircles.Count < _editor.Casts.Count)
            {
                _castCircles.Add(NewLine("Cast area", Color.cyan, 1.5f));
            }
            for (int i = 0; i < _castCircles.Count; i++)
            {
                bool used = i < _editor.Casts.Count;
                _castCircles[i].gameObject.SetActive(used);
                if (!used)
                {
                    continue;
                }
                var cast = _editor.Casts[i];
                bool red = Array.Exists(results, c => c.Id == cast.Id && c.Fizzled);
                var color = CastColor(cast, red);
                _castCircles[i].startColor = _castCircles[i].endColor = color;
                _castCircles[i].material.color = color;
                Circle(
                    _castCircles[i],
                    new Vector2(cast.XMm / 1000f, cast.YMm / 1000f),
                    cast.RadiusMm / 1000f
                );
            }
        }

        private LineRenderer NewLine(string name, Color color, float width)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.loop = true;
            line.positionCount = 64;
            line.widthMultiplier = width;
            line.material = new Material(_lineShader) { color = color };
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return line;
        }

        private void Circle(LineRenderer line, Vector2 centre, float radius)
        {
            for (int k = 0; k < line.positionCount; k++)
            {
                float a = k * Mathf.PI * 2 / line.positionCount;
                float x = centre.x + Mathf.Cos(a) * radius;
                float z = centre.y + Mathf.Sin(a) * radius;
                var p = new FixVec2(
                    Fix.FromRatio((long)(x * 1000), 1000),
                    Fix.FromRatio((long)(z * 1000), 1000)
                );
                line.SetPosition(k, new Vector3(x, _terrain.HeightMmAt(p) / 1000f + 1.5f, z));
            }
        }

        private void UpdateUi()
        {
            if (_hud == null)
            {
                return;
            }
            var progress = _worker.ReadProgress();
            var results = _worker.ReadCasts();
            var outcome = _worker.ReadOutcome();
            string outcomeLabel =
                outcome == null || outcome.Kind == BattleOutcomeKind.Ongoing ? ""
                : outcome.Kind == BattleOutcomeKind.DefenderVictory
                    ? string.Format(
                        CultureInfo.InvariantCulture,
                        "  VICTORY ({0} @ {1})",
                        outcome.Reason,
                        CauseTrace.Clock(
                            outcome.DecidedTick > 0 ? outcome.DecidedTick : outcome.EvaluatedTick
                        )
                    )
                : string.Format(
                    CultureInfo.InvariantCulture,
                    "  DEFEAT ({0} @ {1})",
                    outcome.Reason,
                    CauseTrace.Clock(
                        outcome.DecidedTick > 0 ? outcome.DecidedTick : outcome.EvaluatedTick
                    )
                );
            string fizzles = "";
            foreach (var c in results)
            {
                if (c.Fizzled)
                {
                    fizzles +=
                        "\nRED: "
                        + c.Id
                        + " at "
                        + CauseTrace.Clock(c.Tick)
                        + ": "
                        + c.Reasons
                        + " ("
                        + (
                            Math.Min(
                                c.Reasons.HasFlag(FizzleReasons.TooCloseToAttacker)
                                    ? c.AttackerDistanceMm
                                    : int.MaxValue,
                                c.Reasons.HasFlag(FizzleReasons.TooCloseToDefender)
                                    ? c.DefenderDistanceMm
                                    : int.MaxValue
                            ) / 1000
                        )
                        + " m from the army)";
                }
            }
            _hud.text = string.Format(
                CultureInfo.InvariantCulture,
                "{0}  {1} / {2}  filled {3:0}%{4}{5}{6}\ntool [1-5] {7}  radius [ ] {8} m  play [Space]  reset [R]  ghost [G] {9}  strain [T] {10}  morale [M] {11}  hold [V] speed  hold [N] rings  hold [L] landing  results [Tab]  trace [right-click]  {12:0} fps{13}{14}",
                _level != null ? _level.Title : _mapId,
                CauseTrace.Clock((int)(_scrubSeconds * SimConstants.TicksPerSecond)),
                CauseTrace.Clock(_worker.EndTick),
                progress.Fraction * 100,
                progress.FirstWindowReady
                    ? string.Format(
                        CultureInfo.InvariantCulture,
                        "  first 30 s in {0:0} ms",
                        progress.FirstWindowMs
                    )
                    : "",
                progress.Complete
                    ? string.Format(
                        CultureInfo.InvariantCulture,
                        ", all in {0:0} ms",
                        progress.FillMs
                    )
                    : "",
                outcomeLabel,
                _tool,
                _radiusMm / 1000,
                _showGhost ? "on" : "off",
                _showStrainOverlay ? "on" : "off",
                _boostMoraleTints ? "on" : "auto",
                1f / Mathf.Max(0.0001f, Time.smoothDeltaTime),
                fizzles,
                _editor.Error != null ? "\nREFUSED: " + _editor.Error : ""
            );
            _tracePanel.text = _trace;
            _tracePanel.style.display = string.IsNullOrEmpty(_trace)
                ? DisplayStyle.None
                : DisplayStyle.Flex;

            float width = Mathf.Max(1, _bar.resolvedStyle.width);
            _filled.style.width = width * progress.Fraction;
            _cursor.style.left = width * Mathf.Clamp01(_scrubSeconds / EndSeconds) - 1;
            while (_markers.Count < _editor.Casts.Count)
            {
                var marker = new VisualElement();
                marker.style.position = Position.Absolute;
                marker.style.top = 2;
                marker.style.bottom = 2;
                marker.style.width = 8;
                marker.RegisterCallback<PointerDownEvent>(e =>
                {
                    _dragMarker = (string)marker.userData;
                    _selectedCast = _dragMarker;
                    _bar.CapturePointer(e.pointerId);
                    e.StopPropagation();
                });
                _bar.Add(marker);
                _markers.Add(marker);
            }
            for (int i = 0; i < _markers.Count; i++)
            {
                bool used = i < _editor.Casts.Count;
                _markers[i].style.display = used ? DisplayStyle.Flex : DisplayStyle.None;
                if (!used)
                {
                    continue;
                }
                var cast = _editor.Casts[i];
                bool red = Array.Exists(results, c => c.Id == cast.Id && c.Fizzled);
                _markers[i].userData = cast.Id;
                _markers[i].style.left = width * cast.Tick / (float)_worker.EndTick - 4;
                _markers[i].style.backgroundColor = CastColor(cast, red);
                _markers[i].tooltip = red ? cast.Id + ": fizzles" : cast.Id;
            }
        }

        private void HandleInput()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null)
            {
                return;
            }
            if (keyboard.digit1Key.wasPressedThisFrame)
                _tool = Tool.Move;
            if (keyboard.digit2Key.wasPressedThisFrame)
                _tool = Tool.Rain;
            if (keyboard.digit3Key.wasPressedThisFrame)
                _tool = Tool.Boulder;
            if (keyboard.digit4Key.wasPressedThisFrame)
                _tool = Tool.Channel;
            if (keyboard.digit5Key.wasPressedThisFrame)
                _tool = Tool.Lightning;
            if (keyboard.spaceKey.wasPressedThisFrame)
                _playing = !_playing;
            if (keyboard.rKey.wasPressedThisFrame)
                ResetPlan();
            if (keyboard.gKey.wasPressedThisFrame)
                _showGhost = !_showGhost;
            if (keyboard.tKey.wasPressedThisFrame)
            {
                _showStrainOverlay = !_showStrainOverlay;
                _overlayDirty = true;
            }
            if (keyboard.mKey.wasPressedThisFrame)
                _boostMoraleTints = !_boostMoraleTints;
            if (keyboard.tabKey.wasPressedThisFrame)
                _results?.Toggle();
            if (keyboard.leftBracketKey.wasPressedThisFrame)
                ChangeRadius(-10_000);
            if (keyboard.rightBracketKey.wasPressedThisFrame)
                ChangeRadius(10_000);
            if (_results != null && _results.IsVisible)
            {
                return;
            }
            if (keyboard.deleteKey.wasPressedThisFrame)
            {
                int removed =
                    _editor.Casts.RemoveAll(c => c.Id == _selectedCast)
                    + _editor.Terrain.RemoveAll(p => p.Id == _selectedPiece);
                if (removed > 0)
                {
                    CommitEdit();
                }
            }
            bool overUi =
                _bar != null
                && _bar.worldBound.Contains(
                    new Vector2(
                        mouse.position.ReadValue().x,
                        Screen.height - mouse.position.ReadValue().y
                    )
                );
            if (overUi || !PointOnMap(mouse.position.ReadValue(), out Vector3 point))
            {
                if (!mouse.leftButton.isPressed)
                {
                    _draggingCastOrPiece = false;
                }
                return;
            }
            int xMm = Mathf.RoundToInt(point.x * 1000);
            int yMm = Mathf.RoundToInt(point.z * 1000);
            if (mouse.rightButton.wasPressedThisFrame)
            {
                ShowTrace(point);
            }
            switch (_tool)
            {
                case Tool.Move:
                    if (mouse.leftButton.wasPressedThisFrame)
                    {
                        _dragRegiment = NearestRegiment(point);
                    }
                    else if (mouse.leftButton.wasReleasedThisFrame && _dragRegiment != null)
                    {
                        var r = _editor.Army.Find(a => a.Id == _dragRegiment);
                        r.XMm = xMm;
                        r.YMm = yMm;
                        _dragRegiment = null;
                        CommitEdit();
                    }
                    break;
                case Tool.Rain:
                    PlaceOrDragCast(mouse, xMm, yMm, "rain");
                    break;
                case Tool.Lightning:
                    PlaceOrDragCast(mouse, xMm, yMm, "lightning");
                    break;
                case Tool.Boulder:
                    if (mouse.leftButton.wasPressedThisFrame)
                    {
                        var piece = new PlanEditor.Piece
                        {
                            Id = _editor.NewId("boulder"),
                            Kind = "boulder",
                        };
                        piece.Points.Add((xMm, yMm));
                        _editor.Terrain.Add(piece);
                        _selectedPiece = piece.Id;
                        CommitEdit();
                    }
                    break;
                case Tool.Channel:
                    if (mouse.leftButton.wasPressedThisFrame)
                    {
                        if (_channelStart == null)
                        {
                            _channelStart = point;
                        }
                        else
                        {
                            var piece = new PlanEditor.Piece
                            {
                                Id = _editor.NewId("channel"),
                                Kind = "channel",
                                WidthMm = 4_500,
                                DepthMm = 1_500,
                            };
                            piece.Points.Add(
                                (
                                    Mathf.RoundToInt(_channelStart.Value.x * 1000),
                                    Mathf.RoundToInt(_channelStart.Value.z * 1000)
                                )
                            );
                            piece.Points.Add((xMm, yMm));
                            _editor.Terrain.Add(piece);
                            _selectedPiece = piece.Id;
                            _channelStart = null;
                            CommitEdit();
                        }
                    }
                    break;
            }
        }

        private void PlaceOrDragCast(Mouse mouse, int xMm, int yMm, string powerId)
        {
            if (mouse.leftButton.wasPressedThisFrame)
            {
                var hit = _editor.Casts.Find(c =>
                    (long)(c.XMm - xMm) * (c.XMm - xMm) + (long)(c.YMm - yMm) * (c.YMm - yMm)
                    < (long)c.RadiusMm * c.RadiusMm
                );
                if (hit != null)
                {
                    _selectedCast = hit.Id;
                    _dragCastStartXMm = hit.XMm;
                    _dragCastStartYMm = hit.YMm;
                    _draggingCastOrPiece = true;
                    return;
                }
                var power = _rules.Power(powerId);
                int radius =
                    power != null
                        ? Mathf.Clamp(
                            powerId == "lightning" ? power.DefaultRadiusMm : _radiusMm,
                            power.MinRadiusMm,
                            power.MaxRadiusMm
                        )
                        : _radiusMm;
                var cast = new PlanEditor.Cast
                {
                    Id = _editor.NewId(powerId),
                    Power = powerId,
                    Tick = (int)(_scrubSeconds * SimConstants.TicksPerSecond),
                    XMm = Mathf.Clamp(xMm, radius, _map.WidthMm - radius),
                    YMm = Mathf.Clamp(yMm, radius, _map.DepthMm - radius),
                    RadiusMm = radius,
                };
                _editor.Casts.Add(cast);
                _selectedCast = cast.Id;
                _dragCastStartXMm = cast.XMm;
                _dragCastStartYMm = cast.YMm;
                _draggingCastOrPiece = true;
                CommitEdit();
            }
            else if (mouse.leftButton.isPressed && _draggingCastOrPiece)
            {
                var selected = _editor.Casts.Find(c => c.Id == _selectedCast);
                if (selected != null)
                {
                    selected.XMm = Mathf.Clamp(
                        xMm,
                        selected.RadiusMm,
                        _map.WidthMm - selected.RadiusMm
                    );
                    selected.YMm = Mathf.Clamp(
                        yMm,
                        selected.RadiusMm,
                        _map.DepthMm - selected.RadiusMm
                    );
                }
            }
            else if (mouse.leftButton.wasReleasedThisFrame && _draggingCastOrPiece)
            {
                _draggingCastOrPiece = false;
                var selected = _editor.Casts.Find(c => c.Id == _selectedCast);
                if (
                    selected != null
                    && (selected.XMm != _dragCastStartXMm || selected.YMm != _dragCastStartYMm)
                )
                {
                    CommitEdit();
                }
            }
        }

        private Color CastColor(PlanEditor.Cast cast, bool fizzled)
        {
            if (fizzled)
            {
                return new Color(0.95f, 0.15f, 0.15f);
            }
            if (cast.Id == _selectedCast)
            {
                return Color.white;
            }
            return cast.Power == "lightning"
                ? new Color(1.0f, 0.85f, 0.2f)
                : new Color(0.3f, 0.75f, 1f);
        }

        private void BuildScrubHerds(CauseEvent[] causes, int scrubTick)
        {
            if (_herds == null || _herds.Count == 0)
            {
                if (_scrubHerds != null)
                {
                    _scrubHerds.Count = 0;
                }
                return;
            }
            _herds.CopyTo(_scrubHerds);
            for (int h = 0; h < _scrubHerds.Count; h++)
            {
                _scrubHerds.Mode[h] = SalvoAndBlockerProjection.ResolveHerdAtTick(
                    _herds,
                    causes,
                    h,
                    scrubTick,
                    filterByTime: true,
                    out int headingUnits,
                    out int fear
                );
                _scrubHerds.HeadingUnits[h] = headingUnits;
                _scrubHerds.Fear[h] = fear;
            }
        }

        private void ChangeRadius(int deltaMm)
        {
            _radiusMm = Mathf.Clamp(_radiusMm + deltaMm, 40_000, 100_000);
            var cast = _editor.Casts.Find(c => c.Id == _selectedCast);
            if (cast != null)
            {
                var power = _rules.Power(cast.Power);
                int minR = power != null ? power.MinRadiusMm : 40_000;
                int maxR = power != null ? power.MaxRadiusMm : 100_000;
                int next = Mathf.Clamp(cast.RadiusMm + deltaMm, minR, maxR);
                if (next != cast.RadiusMm)
                {
                    cast.RadiusMm = next;
                    CommitEdit();
                }
            }
        }

        /// <summary>Hands the edited plan to the worker if the loader accepts it; a refusal shows its reason instead.</summary>
        /// <summary>
        /// [R]: the plan as it was loaded, every edit dropped, back at 0:00 and paused. The preview re-runs it like any
        /// other edit (the simulation decides; this only hands it the plan).
        /// </summary>
        private void ResetPlan()
        {
            _editor = new PlanEditor(_loadedPlan, _map, _rules);
            _selectedCast = null;
            _selectedPiece = null;
            _dragRegiment = null;
            _channelStart = null;
            _draggingCastOrPiece = false;
            _playing = false;
            _scrubSeconds = 0f;
            _overlayDirty = true;
            _worker.Edit(_loadedPlan);
        }

        private void CommitEdit()
        {
            if (_editor.Commit())
            {
                _worker.Edit(_editor.Plan);
            }
        }

        private bool PointOnMap(Vector2 screen, out Vector3 point)
        {
            point = default;
            var camera = Camera.main;
            if (
                camera == null
                || !Physics.Raycast(camera.ScreenPointToRay(screen), out var hit, 10_000f)
            )
            {
                return false;
            }
            point = hit.point;
            return true;
        }

        private string NearestRegiment(Vector3 point)
        {
            string best = null;
            float bestSq = 30f * 30f;
            foreach (var r in _editor.Army)
            {
                float dx = r.XMm / 1000f - point.x;
                float dz = r.YMm / 1000f - point.z;
                if (dx * dx + dz * dz < bestSq)
                {
                    bestSq = dx * dx + dz * dz;
                    best = r.Id;
                }
            }
            return best;
        }

        /// <summary>The causality trace of the clicked regiment's latest loss up to the scrub time (M4 item 6).</summary>
        private void ShowTrace(Vector3 point)
        {
            if (!_reader.Read(_worker.Track, _scrubSeconds, out _, out var current, out _))
            {
                return;
            }
            int regiment = -1;
            float bestSq = 15f * 15f;
            for (int u = 0; u < current.Count; u++)
            {
                if (!current.Alive[u])
                {
                    continue;
                }
                float dx = current.X[u] - point.x;
                float dz = current.Z[u] - point.z;
                if (dx * dx + dz * dz < bestSq)
                {
                    bestSq = dx * dx + dz * dz;
                    regiment = current.Regiment[u];
                }
            }
            var events = _worker.ReadCauses();
            var plan = _worker.Plan;
            if (regiment < 0)
            {
                _trace = "";
                return;
            }
            int tick = (int)(_scrubSeconds * SimConstants.TicksPerSecond);
            if (
                regiment < _reader.RegimentSides.Length
                && _reader.RegimentSides[regiment] == Side.Neutral
            )
            {
                ShowHerdTrace(regiment, events, plan, tick);
                return;
            }
            int losses = 0;
            int latest = -1;
            for (int e = 0; e < events.Length; e++)
            {
                var ev = events[e];
                if (ev.Tick > tick || ev.Subject != regiment)
                {
                    continue;
                }
                if (CauseBreakdown.IsFatalEvent(ev))
                {
                    losses++;
                    latest = e;
                }
                else if (
                    (ev.Kind == CauseKind.RegimentSwept || ev.Kind == CauseKind.RegimentRouted)
                    && latest < 0
                )
                {
                    latest = e;
                }
            }
            if (latest < 0)
            {
                _trace =
                    CauseTrace.Regiment(regiment, plan)
                    + ": no losses by "
                    + CauseTrace.Clock(tick)
                    + ".";
                return;
            }
            var chain = CauseTrace.Walk(events, latest);
            var text =
                CauseTrace.Regiment(regiment, plan)
                + ": "
                + losses
                + " lost by "
                + CauseTrace.Clock(tick)
                + ".\n";
            for (int i = 0; i < chain.Count; i++)
            {
                text += (i == 0 ? "" : "\n  ← ") + CauseTrace.Describe(events[chain[i]], plan);
            }
            _trace = text;
        }

        private void ShowHerdTrace(int regiment, CauseEvent[] events, BattlePlan plan, int tick)
        {
            int herdIndex = -1;
            if (_herds != null)
            {
                for (int h = 0; h < _herds.Count; h++)
                {
                    if (_herds.Regiment[h] == regiment)
                    {
                        herdIndex = h;
                        break;
                    }
                }
            }
            int latest = -1;
            int incidents = 0;
            for (int e = 0; e < events.Length; e++)
            {
                CauseEvent ev = events[e];
                if (ev.Tick > tick)
                {
                    continue;
                }
                bool matchesHerd =
                    herdIndex >= 0
                    && ev.Subject == herdIndex
                    && (
                        ev.Kind == CauseKind.HerdAgitated
                        || ev.Kind == CauseKind.HerdStampeded
                        || ev.Kind == CauseKind.HerdScattered
                    );
                bool matchesRegiment = ev.Subject == regiment && CauseBreakdown.IsFatalEvent(ev);
                if (matchesHerd || matchesRegiment)
                {
                    incidents++;
                    latest = e;
                }
            }
            string label = herdIndex >= 0 ? "herd #" + herdIndex + " (cattle)" : "herd";
            if (latest < 0)
            {
                _trace = label + ": grazing calmly at " + CauseTrace.Clock(tick) + ".";
                return;
            }
            var chain = CauseTrace.Walk(events, latest);
            string text = label + ": " + incidents + " events by " + CauseTrace.Clock(tick) + ".\n";
            for (int i = 0; i < chain.Count; i++)
            {
                text += (i == 0 ? "" : "\n  ← ") + CauseTrace.Describe(events[chain[i]], plan);
            }
            _trace = text;
        }

        private void Screenshot()
        {
            if (_screenshotFile == null)
            {
                return;
            }
            if (_screenshotState == 0 && _worker.ReadProgress().Complete)
            {
                _scrubSeconds = _screenshotScrub;
                if (_reader.Read(_worker.Track, _scrubSeconds, out _, out var current, out _))
                {
                    // The trace of the regiment with the most losses, as a right-click on it would show.
                    var events = _worker.ReadCauses();
                    var losses = new Dictionary<int, int>();
                    int worst = -1;
                    foreach (var e in events)
                    {
                        if (
                            CauseBreakdown.IsFatalEvent(e)
                            && e.Tick <= _scrubSeconds * SimConstants.TicksPerSecond
                        )
                        {
                            losses.TryGetValue(e.Subject, out int n);
                            losses[e.Subject] = n + 1;
                            if (worst < 0 || losses[e.Subject] > losses[worst])
                            {
                                worst = e.Subject;
                            }
                        }
                    }
                    for (int u = 0; worst >= 0 && u < current.Count; u++)
                    {
                        if (current.Alive[u] && current.Regiment[u] == worst)
                        {
                            ShowTrace(new Vector3(current.X[u], 0, current.Z[u]));
                            break;
                        }
                    }
                }
                _screenshotState = 1;
            }
            else if (_screenshotState == 1)
            {
                _screenshotState = 2; // one frame for the UI to lay out
            }
            else if (_screenshotState == 2)
            {
                ScreenCapture.CaptureScreenshot(_screenshotFile);
                _screenshotState = 3;
            }
            else if (_screenshotState == 3)
            {
                Application.Quit(0);
            }
        }

        private void Measure()
        {
            if (_measureFile == null)
            {
                return;
            }
            var progress = _worker.ReadProgress();
            switch (_measureStage)
            {
                case 0 when progress.Complete:
                    // Move the first cast 10 s later (or add one), as a player dragging its marker would.
                    if (_editor.Casts.Count == 0)
                    {
                        _editor.Casts.Add(
                            new PlanEditor.Cast
                            {
                                Id = "measure",
                                Power = "rain",
                                Tick = 20,
                                XMm = _map.WidthMm / 2,
                                YMm = _map.DepthMm - 40_000,
                                RadiusMm = 40_000,
                            }
                        );
                    }
                    else
                    {
                        _editor.Casts[0].Tick += 200;
                    }
                    CommitEdit();
                    _measureStage = 1;
                    break;
                case 1 when progress.Edits == _worker.Posted && progress.Complete:
                    _measureStage = 2;
                    _measureStart = Time.realtimeSinceStartup;
                    _playing = false;
                    File.WriteAllText(
                        _measureFile,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "edit_resumed_at {0}\nfirst_30s_ms {1:0}\nfull_ms {2:0}\n",
                            progress.ResumedAt,
                            progress.FirstWindowMs,
                            progress.FillMs
                        )
                    );
                    break;
                case 2:
                    // 5 s of scrubbing back and forth across the whole future.
                    float t = Time.realtimeSinceStartup - _measureStart;
                    _scrubSeconds = Mathf.PingPong(t * 60f, EndSeconds);
                    if (t > 0.5f)
                    {
                        _frameMs.Add(Time.unscaledDeltaTime * 1000f);
                    }
                    if (t > 5.5f)
                    {
                        _frameMs.Sort();
                        float mean = 0;
                        foreach (float f in _frameMs)
                        {
                            mean += f / _frameMs.Count;
                        }
                        File.AppendAllText(
                            _measureFile,
                            string.Format(
                                CultureInfo.InvariantCulture,
                                "scrub_mean_fps {0:0.0}\nscrub_p95_ms {1:0.00}\nunits {2}\n",
                                1000f / mean,
                                _frameMs[(int)(_frameMs.Count * 0.95f)],
                                _reader.Capacity
                            )
                        );
                        _measureStage = 3;
                        Application.Quit(0);
                    }
                    break;
            }
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
