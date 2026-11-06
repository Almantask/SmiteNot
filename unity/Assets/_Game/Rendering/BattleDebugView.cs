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
        private PanelSettings _panelSettings;

        [SerializeField]
        private StrategicCamera _camera;

        private BattleHost _host;
        private UnitCapsuleRenderer _units;
        private Navigation _navigation;
        private FlowField _attackerFlow;
        private Texture2D _overlay;
        private Color32[] _pixels;
        private Material _groundMaterial;
        private Material _waterMaterial;
        private GameObject _ground;
        private GameObject _water;
        private FieldUpload _fields;
        private FieldView _fieldView = FieldView.WetAndMud;
        private Mesh _groundMeshAsset;
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

            var rules = RepoLoader.LoadRules(repo);
            var map = RepoLoader.LoadMap(repo, _mapId);
            var plan = RepoLoader.LoadPlan(repo, _planFile, map, rules);
            var battle = new Battle(rules, map, plan, _seed);
            _host = new BattleHost(battle) { Speed = _speed };
            _units = new UnitCapsuleRenderer(_unitShader, _host);
            _fields = new FieldUpload(map.Columns, map.Rows);

            BuildNavigationViews(battle);
            BuildGround(map);
            BuildHud();
            if (_camera != null)
            {
                _camera.Frame(map.WidthMm / 1000f, map.DepthMm / 1000f);
            }
            _host.Start();
        }

        private void OnDisable()
        {
            _host?.Dispose();
            _units?.Dispose();
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
            if (_waterMaterial != null)
            {
                Destroy(_waterMaterial);
            }
            _fields?.Dispose();
            _fields = null;
            if (_overlay != null)
            {
                Destroy(_overlay);
            }
            if (_groundMaterial != null)
            {
                Destroy(_groundMaterial);
            }
            if (_groundMeshAsset != null)
            {
                Destroy(_groundMeshAsset);
            }
            _host = null;
            _units = null;
        }

        private void BuildNavigationViews(Battle battle)
        {
            // A navigation of its own for the overlays (derived data; the battle's lives on the worker thread).
            _navigation = new Navigation(battle.Map, battle.Rules, new SerialRunner());
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
                    battle.Map,
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
            _hud.style.color = Color.white;
            _hud.style.backgroundColor = new Color(0, 0, 0, 0.55f);
            _hud.style.paddingLeft = 8;
            _hud.style.paddingRight = 8;
            _hud.style.paddingTop = 4;
            _hud.style.paddingBottom = 4;
            _hud.style.position = Position.Absolute;
            _hud.style.left = 8;
            _hud.style.top = 8;
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
            _units.Draw(_host);
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
    }
}
