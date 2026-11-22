using GodGame.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using Terrain = GodGame.Sim.Terrain;

namespace GodGame.Rendering
{
    /// <summary>
    /// The debug ground: a grid mesh over the terrain's heights (every <c>step</c> cells, so a 1.2 × 0.8 km map stays
    /// near 100,000 vertices) with UVs that put one overlay texel on each simulation cell.
    /// </summary>
    public static class GroundMesh
    {
        public static Mesh Build(
            Terrain terrain,
            int step,
            out float minHeight,
            out float maxHeight
        )
        {
            int columns = terrain.Columns / step + 1;
            int rows = terrain.Rows / step + 1;
            float cell = terrain.CellSizeMm / 1000f;
            var vertices = new Vector3[columns * rows];
            var uvs = new Vector2[vertices.Length];
            minHeight = float.MaxValue;
            maxHeight = float.MinValue;
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < columns; i++)
                {
                    int x = Mathf.Min(i * step, terrain.Columns);
                    int y = Mathf.Min(j * step, terrain.Rows);
                    // Vertices sit on cell corners: the corner's height is the mean of the cells around it.
                    float h =
                        (
                            terrain.HeightMm(x - 1, y - 1)
                            + terrain.HeightMm(x, y - 1)
                            + terrain.HeightMm(x - 1, y)
                            + terrain.HeightMm(x, y)
                        ) / 4000f;
                    minHeight = Mathf.Min(minHeight, h);
                    maxHeight = Mathf.Max(maxHeight, h);
                    vertices[j * columns + i] = new Vector3(x * cell, h, y * cell);
                    uvs[j * columns + i] = new Vector2(
                        x / (float)terrain.Columns,
                        y / (float)terrain.Rows
                    );
                }
            }
            var triangles = new int[(columns - 1) * (rows - 1) * 6];
            int t = 0;
            for (int j = 0; j < rows - 1; j++)
            {
                for (int i = 0; i < columns - 1; i++)
                {
                    int a = j * columns + i;
                    triangles[t++] = a;
                    triangles[t++] = a + columns;
                    triangles[t++] = a + 1;
                    triangles[t++] = a + 1;
                    triangles[t++] = a + columns;
                    triangles[t++] = a + columns + 1;
                }
            }
            var mesh = new Mesh { name = "Debug ground", indexFormat = IndexFormat.UInt32 };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static readonly (string Prop, string ResourcePath)[] TerrainTextureBindings =
        {
            ("_GrassTex", "Terrain/T_terrain_grass_a"),
            ("_RoadTex", "Terrain/T_terrain_road_a"),
            ("_MudTex", "Terrain/T_terrain_mud_a"),
            ("_RiverbedTex", "Terrain/T_terrain_riverbed_a"),
            ("_WoodlandTex", "Terrain/T_terrain_woodland_a"),
        };

        /// <summary>
        /// Loads the seamless ground tile textures generated in Resources/Terrain and binds any available ones onto a
        /// ground material using <c>GodGame/DebugGround</c>.
        /// </summary>
        public static void BindTerrainTextures(Material material)
        {
            if (material == null)
            {
                return;
            }
            for (int i = 0; i < TerrainTextureBindings.Length; i++)
            {
                var (prop, resourcePath) = TerrainTextureBindings[i];
                if (material.HasProperty(prop))
                {
                    var tex = Resources.Load<Texture2D>(resourcePath);
                    if (tex != null)
                    {
                        material.SetTexture(prop, tex);
                    }
                }
            }
        }

        /// <summary>
        /// Builds a per-cell biome and map-feature mask texture (<c>_TerrainMask</c>) from <see cref="MapData"/>:
        /// R = packed road soil, G = woodland/fuel floor, B = deployment zone (128 attacker, 255 defender),
        /// A = boulder scree/gravel base. Presentation only.
        /// </summary>
        public static Texture2D BuildTerrainMask(MapData map)
        {
            int cols = map.Columns;
            int rows = map.Rows;
            int cellSizeMm = System.Math.Max(1, map.CellSizeMm);
            var pixels = new Color32[cols * rows];

            if (map.SoilAreas != null)
            {
                for (int s = 0; s < map.SoilAreas.Count; s++)
                {
                    SoilArea area = map.SoilAreas[s];
                    if (
                        !string.Equals(area.Soil, "road", System.StringComparison.OrdinalIgnoreCase)
                    )
                    {
                        continue;
                    }
                    int c0 = Mathf.Clamp(area.Area.MinX / cellSizeMm, 0, cols - 1);
                    int c1 = Mathf.Clamp((area.Area.MaxX - 1) / cellSizeMm, 0, cols - 1);
                    int r0 = Mathf.Clamp(area.Area.MinY / cellSizeMm, 0, rows - 1);
                    int r1 = Mathf.Clamp((area.Area.MaxY - 1) / cellSizeMm, 0, rows - 1);
                    for (int r = r0; r <= r1; r++)
                    {
                        int rowOffset = r * cols;
                        for (int c = c0; c <= c1; c++)
                        {
                            pixels[rowOffset + c].r = 255;
                        }
                    }
                }
            }

            if (map.Fuel != null)
            {
                int len = System.Math.Min(pixels.Length, map.Fuel.Length);
                for (int i = 0; i < len; i++)
                {
                    if (map.Fuel[i])
                    {
                        pixels[i].g = 255;
                    }
                }
            }

            if (map.Trees != null)
            {
                for (int t = 0; t < map.Trees.Count; t++)
                {
                    int cell = map.Trees[t].Cell;
                    if ((uint)cell >= (uint)pixels.Length)
                    {
                        continue;
                    }
                    int tc = cell % cols;
                    int tr = cell / cols;
                    for (int dr = -2; dr <= 2; dr++)
                    {
                        int nr = tr + dr;
                        if ((uint)nr >= (uint)rows)
                        {
                            continue;
                        }
                        for (int dc = -2; dc <= 2; dc++)
                        {
                            int nc = tc + dc;
                            if ((uint)nc >= (uint)cols)
                            {
                                continue;
                            }
                            int distSq = dc * dc + dr * dr;
                            if (distSq > 5)
                            {
                                continue;
                            }
                            byte shade = distSq <= 1 ? (byte)255 : (byte)165;
                            int idx = nr * cols + nc;
                            if (shade > pixels[idx].g)
                            {
                                pixels[idx].g = shade;
                            }
                        }
                    }
                }
            }

            if (map.DeploymentZones != null)
            {
                for (int z = 0; z < map.DeploymentZones.Count; z++)
                {
                    DeploymentZone zone = map.DeploymentZones[z];
                    byte code =
                        zone.Side == Side.Attacker ? (byte)128
                        : zone.Side == Side.Defender ? (byte)255
                        : (byte)0;
                    if (code == 0)
                    {
                        continue;
                    }
                    int c0 = Mathf.Clamp(zone.Area.MinX / cellSizeMm, 0, cols - 1);
                    int c1 = Mathf.Clamp((zone.Area.MaxX - 1) / cellSizeMm, 0, cols - 1);
                    int r0 = Mathf.Clamp(zone.Area.MinY / cellSizeMm, 0, rows - 1);
                    int r1 = Mathf.Clamp((zone.Area.MaxY - 1) / cellSizeMm, 0, rows - 1);
                    for (int r = r0; r <= r1; r++)
                    {
                        int rowOffset = r * cols;
                        for (int c = c0; c <= c1; c++)
                        {
                            // Mark perimeter band (2 cells wide) so deployment zones read cleanly without tinting grass.
                            bool border =
                                (c - c0 < 2) || (c1 - c < 2) || (r - r0 < 2) || (r1 - r < 2);
                            if (border)
                            {
                                pixels[rowOffset + c].b = code;
                            }
                        }
                    }
                }
            }

            if (map.Boulders != null)
            {
                for (int b = 0; b < map.Boulders.Count; b++)
                {
                    int[] cells = map.Boulders[b].Cells;
                    if (cells == null)
                    {
                        continue;
                    }
                    for (int i = 0; i < cells.Length; i++)
                    {
                        int cell = cells[i];
                        if ((uint)cell >= (uint)pixels.Length)
                        {
                            continue;
                        }
                        int bc = cell % cols;
                        int br = cell / cols;
                        for (int dr = -1; dr <= 1; dr++)
                        {
                            int nr = br + dr;
                            if ((uint)nr >= (uint)rows)
                            {
                                continue;
                            }
                            for (int dc = -1; dc <= 1; dc++)
                            {
                                int nc = bc + dc;
                                if ((uint)nc >= (uint)cols)
                                {
                                    continue;
                                }
                                byte val = (dc == 0 && dr == 0) ? (byte)255 : (byte)150;
                                int idx = nr * cols + nc;
                                if (val > pixels[idx].a)
                                {
                                    pixels[idx].a = val;
                                }
                            }
                        }
                    }
                }
            }

            var tex = new Texture2D(cols, rows, TextureFormat.RGBA32, false)
            {
                name = "TerrainMask",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }
    }
}
