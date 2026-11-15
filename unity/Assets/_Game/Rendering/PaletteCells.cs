using UnityEngine;

namespace GodGame.Rendering
{
    /// <summary>
    /// Shared 16x16 palette lookup matching <c>art/palettes/palette.json</c> (cy * 16 + cx, v=0 at cy=0) uploaded as
    /// linear-space RGBA entries for <c>_CellColors</c> in unit and world-marker shaders.
    /// </summary>
    internal static class PaletteCells
    {
        public static GraphicsBuffer CreateBuffer()
        {
            var buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 256, 16);
            buffer.SetData(BuildLinear());
            return buffer;
        }

        public static Vector4[] BuildLinear()
        {
            var cells = new Vector4[256];
            Color fallback = Color.gray.linear;
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = new Vector4(fallback.r, fallback.g, fallback.b, 1f);
            }
            SetCell(cells, 0, 0, "#E8B48A"); // skin
            SetCell(cells, 1, 0, "#6E4E37"); // cloth_brown
            SetCell(cells, 2, 0, "#8C96A0"); // metal
            SetCell(cells, 3, 0, "#9C6B3B"); // wood
            SetCell(cells, 4, 0, "#4A3222"); // leather
            SetCell(cells, 5, 0, "#7A3E24"); // horse_bay
            SetCell(cells, 6, 0, "#26201D"); // horse_mane
            SetCell(cells, 7, 0, "#D9B45B"); // straw
            SetCell(cells, 8, 0, "#D4A038"); // brass
            SetCell(cells, 9, 0, "#E6DEC8"); // cloth_cream
            SetCell(cells, 10, 0, "#787B80"); // stone
            SetCell(cells, 0, 1, "#2F5928"); // foliage_dark
            SetCell(cells, 1, 1, "#4B7B34"); // foliage_mid
            SetCell(cells, 2, 1, "#9C8A3C"); // foliage_dry
            SetCell(cells, 3, 1, "#543B27"); // bark_dark
            SetCell(cells, 4, 1, "#2B2523"); // charred
            SetCell(cells, 5, 1, "#B83214"); // ember
            SetCell(cells, 6, 1, "#EE6E1A"); // flame_orange
            SetCell(cells, 7, 1, "#F7C631"); // flame_yellow
            SetCell(cells, 8, 1, "#8B4E2B"); // cattle_hide
            SetCell(cells, 9, 1, "#3A271D"); // cattle_dark
            SetCell(cells, 10, 1, "#DCD3BE"); // horn_bone
            SetCell(cells, 0, 15, "#D94B2B"); // team_tabard
            return cells;
        }

        private static void SetCell(Vector4[] cells, int cx, int cy, string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color c))
            {
                Color linear = c.linear;
                cells[cy * 16 + cx] = new Vector4(linear.r, linear.g, linear.b, 1f);
            }
        }
    }
}
