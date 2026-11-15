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
    }
}
