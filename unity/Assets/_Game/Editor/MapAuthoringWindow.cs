using System;
using System.IO;
using GodGame.Bridge;
using GodGame.Sim;
using UnityEditor;
using UnityEngine;

namespace GodGame.Editor
{
    /// <summary>
    /// Map authoring v0 (GodGame → Map Authoring): import a heightmap (8- or 16-bit PNG, or 16-bit RAW), set the map's
    /// size and cells, paint the two deployment zones by dragging their corners in the Scene view (they snap to whole
    /// metres), and export data/maps/&lt;id&gt; through <see cref="MapExport"/>, which checks it with the simulation's
    /// loader first. Opening an existing map loads its size and zones.
    /// </summary>
    public sealed class MapAuthoringWindow : EditorWindow
    {
        [SerializeField]
        private string _id = "new-map";

        [SerializeField]
        private int _widthM = 1200;

        [SerializeField]
        private int _depthM = 800;

        [SerializeField]
        private int _cellSizeMm = 1500;

        [SerializeField]
        private string _heightmapPath = "";

        [SerializeField]
        private int _rawWidth = 1025;

        [SerializeField]
        private int _rawHeight = 1025;

        [SerializeField]
        private int _lowMm;

        [SerializeField]
        private int _highMm = 20_000;

        [SerializeField]
        private string _comment = "";

        [SerializeField]
        private Rect _attacker = new Rect(0, 0, 200, 800);

        [SerializeField]
        private Rect _defender = new Rect(1000, 0, 200, 800);

        [SerializeField]
        private string _status = "";

        [MenuItem("GodGame/Map Authoring")]
        public static void Open() => GetWindow<MapAuthoringWindow>("Map Authoring");

        private void OnEnable() => SceneView.duringSceneGui += OnSceneGui;

        private void OnDisable() => SceneView.duringSceneGui -= OnSceneGui;

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Map", EditorStyles.boldLabel);
            _id = EditorGUILayout.TextField("Id (a-z, 0-9, _ -)", _id);
            _widthM = EditorGUILayout.IntField("Width (m, east)", _widthM);
            _depthM = EditorGUILayout.IntField("Depth (m, north)", _depthM);
            _cellSizeMm = EditorGUILayout.IntField("Cell size (mm)", _cellSizeMm);
            _comment = EditorGUILayout.TextField("Comment", _comment);
            if (GUILayout.Button("Load existing map " + _id))
            {
                LoadExisting();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Heightmap (row 0 is the south edge; empty means flat)",
                EditorStyles.boldLabel
            );
            using (new EditorGUILayout.HorizontalScope())
            {
                _heightmapPath = EditorGUILayout.TextField("File", _heightmapPath);
                if (GUILayout.Button("…", GUILayout.Width(28)))
                {
                    string picked = EditorUtility.OpenFilePanel("Heightmap", "", "png,raw,r16");
                    if (!string.IsNullOrEmpty(picked))
                    {
                        _heightmapPath = picked;
                    }
                }
            }
            if (IsRaw(_heightmapPath))
            {
                _rawWidth = EditorGUILayout.IntField("RAW width (px)", _rawWidth);
                _rawHeight = EditorGUILayout.IntField("RAW height (px)", _rawHeight);
            }
            _lowMm = EditorGUILayout.IntField("Black is (mm)", _lowMm);
            _highMm = EditorGUILayout.IntField("White is (mm)", _highMm);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(
                "Deployment zones (m; drag the corners in the Scene view)",
                EditorStyles.boldLabel
            );
            _attacker = EditorGUILayout.RectField("Attacker", _attacker);
            _defender = EditorGUILayout.RectField("Defender", _defender);
            if (
                GUILayout.Button("Frame the map in the Scene view")
                && SceneView.lastActiveSceneView != null
            )
            {
                SceneView.lastActiveSceneView.Frame(
                    new Bounds(
                        new Vector3(_widthM / 2f, 0, _depthM / 2f),
                        new Vector3(_widthM, 10, _depthM)
                    ),
                    false
                );
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Export to data/maps/" + _id, GUILayout.Height(28)))
            {
                Export();
            }
            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.HelpBox(_status, MessageType.Info);
            }
        }

        private void OnSceneGui(SceneView view)
        {
            Handles.color = Color.white;
            DrawRect(new Rect(0, 0, _widthM, _depthM));
            _attacker = ZoneHandles(_attacker, new Color(1f, 0.55f, 0.1f));
            _defender = ZoneHandles(_defender, new Color(0.2f, 0.6f, 1f));
        }

        private Rect ZoneHandles(Rect zone, Color color)
        {
            Handles.color = color;
            DrawRect(zone);
            var min = Corner(new Vector3(zone.xMin, 0, zone.yMin));
            var max = Corner(new Vector3(zone.xMax, 0, zone.yMax));
            var result = Rect.MinMaxRect(
                Mathf.Clamp(Mathf.Min(min.x, max.x), 0, _widthM),
                Mathf.Clamp(Mathf.Min(min.z, max.z), 0, _depthM),
                Mathf.Clamp(Mathf.Max(min.x, max.x), 0, _widthM),
                Mathf.Clamp(Mathf.Max(min.z, max.z), 0, _depthM)
            );
            if (result != zone)
            {
                Repaint();
            }
            return result;
        }

        private static Vector3 Corner(Vector3 at)
        {
            float size = HandleUtility.GetHandleSize(at) * 0.15f;
            var moved = Handles.Slider2D(
                at,
                Vector3.up,
                Vector3.right,
                Vector3.forward,
                size,
                Handles.DotHandleCap,
                Vector2.one
            );
            return new Vector3(Mathf.Round(moved.x), 0, Mathf.Round(moved.z));
        }

        private static void DrawRect(Rect r)
        {
            Handles.DrawPolyLine(
                new Vector3(r.xMin, 0, r.yMin),
                new Vector3(r.xMax, 0, r.yMin),
                new Vector3(r.xMax, 0, r.yMax),
                new Vector3(r.xMin, 0, r.yMax),
                new Vector3(r.xMin, 0, r.yMin)
            );
        }

        private void LoadExisting()
        {
            try
            {
                var map = RepoLoader.LoadMap(RepoLoader.EditorRepoRoot, _id);
                _widthM = map.WidthMm / 1000;
                _depthM = map.DepthMm / 1000;
                _cellSizeMm = map.CellSizeMm;
                foreach (var zone in map.DeploymentZones)
                {
                    var r = Rect.MinMaxRect(
                        zone.Area.MinX / 1000f,
                        zone.Area.MinY / 1000f,
                        zone.Area.MaxX / 1000f,
                        zone.Area.MaxY / 1000f
                    );
                    if (zone.Side == Side.Attacker)
                    {
                        _attacker = r;
                    }
                    else
                    {
                        _defender = r;
                    }
                }
                _status =
                    "Loaded "
                    + _id
                    + " (heights stay in its height.bin until you export a new heightmap).";
            }
            catch (Exception e)
            {
                _status = "Could not load " + _id + ": " + e.Message;
            }
        }

        private void Export()
        {
            try
            {
                int columns = (_widthM * 1000 + _cellSizeMm - 1) / _cellSizeMm;
                int rows = (_depthM * 1000 + _cellSizeMm - 1) / _cellSizeMm;
                int[] heights = string.IsNullOrEmpty(_heightmapPath)
                    ? new int[columns * rows]
                    : MapExport.Resample(
                        ReadHeightmap(out int w, out int h),
                        w,
                        h,
                        columns,
                        rows,
                        _lowMm,
                        _highMm
                    );
                string json = MapExport.MapJson(
                    _id,
                    _widthM * 1000,
                    _depthM * 1000,
                    _cellSizeMm,
                    Mm(_attacker),
                    Mm(_defender),
                    _comment
                );
                string dir = MapExport.Write(
                    RepoLoader.EditorRepoRoot,
                    json,
                    MapExport.HeightBytes(heights)
                );
                _status = "Exported " + columns + " × " + rows + " cells to " + dir;
            }
            catch (Exception e)
            {
                _status = "Not exported: " + e.Message;
            }
        }

        private float[] ReadHeightmap(out int width, out int height)
        {
            byte[] bytes = File.ReadAllBytes(_heightmapPath);
            if (IsRaw(_heightmapPath))
            {
                width = _rawWidth;
                height = _rawHeight;
                return MapExport.FromRaw16(bytes, width, height);
            }
            var texture = new Texture2D(2, 2, TextureFormat.RGBA64, false, true);
            try
            {
                if (!texture.LoadImage(bytes, false))
                {
                    throw new InvalidOperationException("Not a PNG this editor can read.");
                }
                width = texture.width;
                height = texture.height;
                var pixels = texture.GetPixels();
                var values = new float[pixels.Length];
                for (int i = 0; i < pixels.Length; i++)
                {
                    values[i] = pixels[i].r; // GetPixels runs row by row from the bottom: row 0 is the south edge
                }
                return values;
            }
            finally
            {
                DestroyImmediate(texture);
            }
        }

        private static bool IsRaw(string path) =>
            path.EndsWith(".raw", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".r16", StringComparison.OrdinalIgnoreCase);

        private static ZoneMm Mm(Rect r) =>
            new ZoneMm(
                (int)(r.xMin * 1000),
                (int)(r.yMin * 1000),
                (int)(r.xMax * 1000),
                (int)(r.yMax * 1000)
            );
    }
}
