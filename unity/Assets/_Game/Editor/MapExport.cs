using System;
using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Text;
using GodGame.Sim;

namespace GodGame.Editor
{
    /// <summary>A deployment zone in millimetres, as map.json stores it.</summary>
    [Serializable]
    public struct ZoneMm
    {
        public int MinX;
        public int MinY;
        public int MaxX;
        public int MaxY;

        public ZoneMm(int minX, int minY, int maxX, int maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }
    }

    /// <summary>
    /// Map authoring v0's file side: turns a heightmap and two zones into data/maps/&lt;id&gt;/map.json and height.bin
    /// (map format v0), checked by the simulation's own loader before anything is written. Pure apart from
    /// <see cref="Write"/>.
    /// </summary>
    public static class MapExport
    {
        /// <summary>
        /// Resamples a grayscale heightmap (values 0 to 1, row 0 at the south edge) to the map's cells, bilinear at
        /// cell centres, then maps 0 to 1 onto <paramref name="lowMm"/> to <paramref name="highMm"/>.
        /// </summary>
        public static int[] Resample(
            float[] source,
            int sourceWidth,
            int sourceHeight,
            int columns,
            int rows,
            int lowMm,
            int highMm
        )
        {
            if (source.Length != sourceWidth * sourceHeight || sourceWidth < 1 || sourceHeight < 1)
            {
                throw new ArgumentException(
                    "The heightmap's size doesn't match its data.",
                    nameof(source)
                );
            }
            var heights = new int[columns * rows];
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    double u = (x + 0.5) / columns * sourceWidth - 0.5;
                    double v = (y + 0.5) / rows * sourceHeight - 0.5;
                    int fx = (int)Math.Floor(u);
                    int fy = (int)Math.Floor(v);
                    int x0 = Clamp(fx, sourceWidth);
                    int y0 = Clamp(fy, sourceHeight);
                    int x1 = Clamp(fx + 1, sourceWidth); // past the edge both samples are the edge pixel
                    int y1 = Clamp(fy + 1, sourceHeight);
                    double tx = Math.Min(1, Math.Max(0, u - Math.Floor(u)));
                    double ty = Math.Min(1, Math.Max(0, v - Math.Floor(v)));
                    double south = Lerp(
                        source[y0 * sourceWidth + x0],
                        source[y0 * sourceWidth + x1],
                        tx
                    );
                    double north = Lerp(
                        source[y1 * sourceWidth + x0],
                        source[y1 * sourceWidth + x1],
                        tx
                    );
                    double t = Lerp(south, north, ty);
                    heights[y * columns + x] = (int)Math.Round(lowMm + (highMm - lowMm) * t);
                }
            }
            return heights;
        }

        /// <summary>16-bit little-endian RAW (the common heightmap export) to 0..1 values.</summary>
        public static float[] FromRaw16(byte[] bytes, int width, int height)
        {
            if (bytes.Length != width * height * 2)
            {
                throw new ArgumentException(
                    "A 16-bit RAW of "
                        + width
                        + " × "
                        + height
                        + " needs "
                        + width * height * 2
                        + " bytes."
                );
            }
            var values = new float[width * height];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] =
                    BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(i * 2, 2)) / 65535f;
            }
            return values;
        }

        public static string MapJson(
            string id,
            int widthMm,
            int depthMm,
            int cellSizeMm,
            ZoneMm attacker,
            ZoneMm defender,
            string comment
        )
        {
            var b = new StringBuilder();
            b.Append("{\n");
            if (!string.IsNullOrEmpty(comment))
            {
                b.Append("  \"$comment\": \"")
                    .Append(comment.Replace("\\", "\\\\").Replace("\"", "\\\""))
                    .Append("\",\n");
            }
            b.Append("  \"version\": 0,\n");
            b.Append("  \"id\": \"").Append(id).Append("\",\n");
            b.Append(Field("widthMm", widthMm))
                .Append(Field("depthMm", depthMm))
                .Append(Field("cellSizeMm", cellSizeMm));
            b.Append("  \"deployment\": [\n");
            b.Append(Zone("attacker", attacker)).Append(",\n");
            b.Append(Zone("defender", defender)).Append("\n");
            b.Append("  ]\n}\n");
            return b.ToString();
        }

        public static byte[] HeightBytes(int[] heightsMm)
        {
            var bytes = new byte[heightsMm.Length * 4];
            for (int i = 0; i < heightsMm.Length; i++)
            {
                BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(i * 4, 4), heightsMm[i]);
            }
            return bytes;
        }

        /// <summary>Loads the files with the simulation's own loader: throws exactly what a battle would refuse.</summary>
        public static MapData Validate(string mapJson, byte[] height) =>
            MapData.Load(mapJson, height);

        /// <summary>Validates, then writes map.json and height.bin into data/maps/&lt;id&gt;.</summary>
        public static string Write(string repoRoot, string mapJson, byte[] height)
        {
            var map = Validate(mapJson, height);
            string dir = Path.Combine(repoRoot, "data", "maps", map.Id);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "map.json"), mapJson.Replace("\r\n", "\n"));
            File.WriteAllBytes(Path.Combine(dir, "height.bin"), height);
            return dir;
        }

        private static string Field(string name, int value) =>
            "  \"" + name + "\": " + value.ToString(CultureInfo.InvariantCulture) + ",\n";

        private static string Zone(string side, ZoneMm z) =>
            string.Format(
                CultureInfo.InvariantCulture,
                "    {{ \"side\": \"{0}\", \"minXMm\": {1}, \"minYMm\": {2}, \"maxXMm\": {3}, \"maxYMm\": {4} }}",
                side,
                z.MinX,
                z.MinY,
                z.MaxX,
                z.MaxY
            );

        private static int Clamp(int v, int count) =>
            v < 0 ? 0
            : v >= count ? count - 1
            : v;

        private static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}
