using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using GodGame.Bridge;
using GodGame.Sim;
using GodGame.Sim.Math;
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
        private Shader _unitShader;

        [SerializeField]
        private Shader _groundShader;

        [SerializeField]
        private Shader _waterShader;

        [SerializeField]
        private Shader _lineShader;

        [SerializeField]
        private PanelSettings _panelSettings;

        [SerializeField]
        private StrategicCamera _camera;

        private enum Tool
        {
            Move,
            Rain,
            Boulder,
            Channel,
        }

        private GodGame.Sim.Rules.Ruleset _rules;
        private MapData _map;
        private Terrain _terrain;
        private PlanEditor _editor;
        private PreviewWorker _worker;
        private TrackReader _reader;
        private TrackReader _ghostReader;
        private PlaybackTrack _ghostTrack;
        private BattlePlan _shownPlan;
        private UnitCapsuleRenderer _units;
        private UnitCapsuleRenderer _ghost;
        private FieldUpload _fields;
        private uint[] _fieldCells;
        private int _fieldTick = -1;
        private PlaybackTrack _fieldTrack;
        private Material _groundMaterial;
        private Material _waterMaterial;
        private GameObject _ground;
        private GameObject _water;
        private Mesh _mesh;
        private readonly List<LineRenderer> _rings = new List<LineRenderer>();
        private readonly List<LineRenderer> _castCircles = new List<LineRenderer>();

        private Tool _tool = Tool.Rain;
        private float _scrubSeconds;
        private bool _playing;
        private bool _showGhost = true;
        private int _radiusMm = 80_000;
        private string _selectedCast;
        private string _selectedPiece;
        private string _dragRegiment;
        private Vector3? _channelStart;
        private string _trace = "";

        private Label _hud;
        private Label _tracePanel;
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
            _measureFile = CommandLineValue("-measure-preview");
            _screenshotFile = CommandLineValue("-screenshot");
            _forceRings = Array.IndexOf(Environment.GetCommandLineArgs(), "-rings") >= 0;
            if (float.TryParse(CommandLineValue("-scrub"), NumberStyles.Float, CultureInfo.InvariantCulture, out float scrub))
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
            _map = RepoLoader.LoadMap(repo, _mapId);
            var plan = RepoLoader.LoadPlan(repo, _planFile, _map, _rules);
            _editor = new PlanEditor(plan, _map, _rules);
            var battle = new Battle(_rules, _map, plan, 1);
            _worker = new PreviewWorker(
                battle,
                new ThreadedRunner(Math.Max(1, SystemInfo.processorCount - 2)),
                new KeyframeStore(512L << 20)
            );
            _worker.Start();
            BuildScene(battle);
            BuildUi();
            if (_camera != null)
            {
                _camera.Frame(_map.WidthMm / 1000f, _map.DepthMm / 1000f);
            }
        }

        private void OnDisable()
        {
            _worker?.Dispose();
            _units?.Dispose();
            _ghost?.Dispose();
            _fields?.Dispose();
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
            Destroy(_ground);
            Destroy(_water);
            Destroy(_groundMaterial);
            Destroy(_waterMaterial);
            Destroy(_mesh);
            _worker = null;
        }

        private void BuildScene(Battle battle)
        {
            _terrain = new Terrain(battle.PlayedMap);
            _mesh = GroundMesh.Build(
                _terrain,
                _terrain.CellCount > 200_000 ? 2 : 1,
                out float minH,
                out float maxH
            );
            _groundMaterial = new Material(_groundShader);
            _groundMaterial.SetTexture("_Overlay", Texture2D.blackTexture);
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
                null
            );
            _reader = reader;
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
                            new Color(0.6f, 0.6f, 0.6f)
                        );
                    }
                    ResetReaders(new Battle(_rules, _map, plan, 1));
                }
                _shownPlan = plan;
            }

            var track = _worker.Track;
            if (
                _reader.Read(
                    track,
                    _scrubSeconds,
                    out var previous,
                    out var current,
                    out float alpha
                )
            )
            {
                _units.Draw(previous, current, alpha);
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
                _ghost.Draw(gp, gc, ga);
            }
            UploadFields(track);
            DrawRings(track);
            DrawCasts();
            UpdateUi();
            Measure();
            Screenshot();
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
            bool show = _forceRings || (Keyboard.current != null && Keyboard.current.nKey.isPressed);
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
                if (n[r] == 0)
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
                var color =
                    red ? new Color(0.95f, 0.15f, 0.15f)
                    : cast.Id == _selectedCast ? Color.white
                    : new Color(0.3f, 0.75f, 1f);
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
                "{0}  {1} / {2}  filled {3:0}%{4}{5}\ntool [1-4] {6}  radius [ ] {7} m  play [Space]  ghost [G] {8}  hold [V] speed  hold [N] no-cast rings  trace [right-click]  {9:0} fps{10}{11}",
                _mapId,
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
                _tool,
                _radiusMm / 1000,
                _showGhost ? "on" : "off",
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
                _markers[i].style.backgroundColor =
                    red ? new Color(0.95f, 0.15f, 0.15f)
                    : cast.Id == _selectedCast ? Color.white
                    : new Color(0.3f, 0.75f, 1f);
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
            if (keyboard.spaceKey.wasPressedThisFrame)
                _playing = !_playing;
            if (keyboard.gKey.wasPressedThisFrame)
                _showGhost = !_showGhost;
            if (keyboard.leftBracketKey.wasPressedThisFrame)
                ChangeRadius(-10_000);
            if (keyboard.rightBracketKey.wasPressedThisFrame)
                ChangeRadius(10_000);
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
                    if (mouse.leftButton.wasPressedThisFrame)
                    {
                        var hit = _editor.Casts.Find(c =>
                            (long)(c.XMm - xMm) * (c.XMm - xMm)
                                + (long)(c.YMm - yMm) * (c.YMm - yMm)
                            < (long)c.RadiusMm * c.RadiusMm
                        );
                        if (hit != null)
                        {
                            _selectedCast = hit.Id;
                            break;
                        }
                        var cast = new PlanEditor.Cast
                        {
                            Id = _editor.NewId("rain"),
                            Power = "rain",
                            Tick = (int)(_scrubSeconds * SimConstants.TicksPerSecond),
                            XMm = Mathf.Clamp(xMm, _radiusMm, _map.WidthMm - _radiusMm),
                            YMm = Mathf.Clamp(yMm, _radiusMm, _map.DepthMm - _radiusMm),
                            RadiusMm = _radiusMm,
                        };
                        _editor.Casts.Add(cast);
                        _selectedCast = cast.Id;
                        CommitEdit();
                    }
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

        private void ChangeRadius(int deltaMm)
        {
            _radiusMm = Mathf.Clamp(_radiusMm + deltaMm, 40_000, 100_000);
            var cast = _editor.Casts.Find(c => c.Id == _selectedCast);
            if (cast != null)
            {
                cast.RadiusMm = _radiusMm;
                CommitEdit();
            }
        }

        /// <summary>Hands the edited plan to the worker if the loader accepts it; a refusal shows its reason instead.</summary>
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
            int drowned = 0;
            int latest = -1;
            for (int e = 0; e < events.Length; e++)
            {
                var ev = events[e];
                if (ev.Tick > tick || ev.Subject != regiment)
                {
                    continue;
                }
                if (ev.Kind == CauseKind.SoldierDrowned)
                {
                    drowned++;
                    latest = e;
                }
                else if (ev.Kind == CauseKind.RegimentSwept && latest < 0)
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
                + drowned
                + " drowned by "
                + CauseTrace.Clock(tick)
                + ".\n";
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
                        if (e.Kind == CauseKind.SoldierDrowned && e.Tick <= _scrubSeconds * SimConstants.TicksPerSecond)
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
    }
}
