using System;
using System.Collections.Generic;
using GodGame.Bridge;
using GodGame.Sim;
using GodGame.Sim.Rules;
using UnityEngine;
using Terrain = GodGame.Sim.Terrain;

namespace GodGame.Rendering
{
    /// <summary>The debug overlays, cycled with a key.</summary>
    public enum OverlayMode
    {
        None = 0,
        CostFoot,
        CostHorse,
        CostWheeled,
        FlowField,
        LethalDistance,
    }

    /// <summary>
    /// Paints one texel per simulation cell for the debug overlays, straight from the simulation's own fields (what the
    /// player sees is what the units feel). Pure: same fields in, same pixels out.
    /// </summary>
    public static class OverlayPainter
    {
        private static readonly Color32 Closed = new Color32(20, 20, 20, 230);
        public static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        public static void Paint(
            OverlayMode mode,
            Navigation navigation,
            FlowField flow,
            Color32[] pixels
        )
        {
            var terrain = navigation.Terrain;
            if (pixels.Length != terrain.CellCount)
            {
                throw new ArgumentException("One pixel per cell.", nameof(pixels));
            }
            switch (mode)
            {
                case OverlayMode.None:
                    Array.Fill(pixels, Clear);
                    break;
                case OverlayMode.CostFoot:
                    PaintCost(navigation.Costs.Of(MovementClass.Foot), pixels);
                    break;
                case OverlayMode.CostHorse:
                    PaintCost(navigation.Costs.Of(MovementClass.Horse), pixels);
                    break;
                case OverlayMode.CostWheeled:
                    PaintCost(navigation.Costs.Of(MovementClass.Wheeled), pixels);
                    break;
                case OverlayMode.FlowField:
                    PaintFlow(flow, pixels);
                    break;
                case OverlayMode.LethalDistance:
                    PaintLethal(navigation.Lethal.Distances, pixels);
                    break;
            }
        }

        /// <summary>Base cost clear, rising cost from yellow to red, closed cells near black.</summary>
        public static void PaintCost(ReadOnlySpan<ushort> costs, Color32[] pixels)
        {
            int flat = int.MaxValue;
            for (int i = 0; i < costs.Length; i++)
            {
                if (costs[i] != CostFields.Impassable && costs[i] < flat)
                {
                    flat = costs[i];
                }
            }
            for (int i = 0; i < costs.Length; i++)
            {
                ushort c = costs[i];
                if (c == CostFields.Impassable)
                {
                    pixels[i] = Closed;
                    continue;
                }
                // Up to 4× the cheapest cell: from clear to opaque, yellow to red.
                float t = Mathf.Clamp01((c - flat) / (3f * Mathf.Max(1, flat)));
                pixels[i] =
                    t <= 0
                        ? Clear
                        : (Color32)new Color(1f, 1f - t * 0.85f, 0.1f, 0.25f + 0.55f * t);
            }
        }

        /// <summary>Each direction its own hue, goal cells white, cells that can't reach the goal near black.</summary>
        public static void PaintFlow(FlowField flow, Color32[] pixels)
        {
            if (flow == null)
            {
                Array.Fill(pixels, Clear);
                return;
            }
            var directions = flow.Directions;
            for (int i = 0; i < directions.Length; i++)
            {
                byte d = directions[i];
                pixels[i] =
                    d == FlowField.NoDirection ? Closed
                    : d == FlowField.AtGoal ? new Color32(255, 255, 255, 160)
                    : WithAlpha(Color.HSVToRGB(d / 8f, 0.6f, 0.95f), 0.45f);
            }
        }

        private static Color32 WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        /// <summary>Red at lethal cells fading to clear at 20 m.</summary>
        public static void PaintLethal(ReadOnlySpan<int> distancesMm, Color32[] pixels)
        {
            for (int i = 0; i < distancesMm.Length; i++)
            {
                float t = 1f - Mathf.Clamp01(distancesMm[i] / 20_000f);
                pixels[i] = t <= 0 ? Clear : (Color32)new Color(0.9f, 0.1f, 0.15f, 0.7f * t);
            }
        }

        /// <summary>
        /// Paints deck/ice strain stages across structure cells (Quiet/Creak/Crack/Sag/Broken + Rot tint).
        /// </summary>
        public static void PaintStrain(
            StructureSnapshot structures,
            Color32[] pixels,
            bool clearFirst = true
        )
        {
            if (clearFirst)
            {
                Array.Fill(pixels, Clear);
            }
            if (structures == null || structures.Count == 0)
            {
                return;
            }
            for (int s = 0; s < structures.Count; s++)
            {
                int[] cells = structures.Cells[s];
                if (cells == null || cells.Length == 0)
                {
                    continue;
                }
                Color32 col = StrainColor(
                    structures.Stage[s],
                    structures.LoadPermille[s],
                    structures.StrengthPermille[s],
                    structures.Broken[s]
                );
                if (col.a == 0)
                {
                    continue;
                }
                for (int i = 0; i < cells.Length; i++)
                {
                    int cell = cells[i];
                    if ((uint)cell < (uint)pixels.Length && col.a >= pixels[cell].a)
                    {
                        pixels[cell] = col;
                    }
                }
            }
        }

        /// <summary>Canonical ground-overlay color for a deck/ice strain state.</summary>
        public static Color32 StrainColor(
            StrainStage stage,
            ushort loadPermille,
            ushort strengthPermille,
            bool broken
        )
        {
            if (broken)
            {
                return new Color32(48, 20, 20, 225);
            }
            switch (stage)
            {
                case StrainStage.Sag:
                    return new Color32(235, 36, 28, 225);
                case StrainStage.Crack:
                    return new Color32(246, 126, 24, 215);
                case StrainStage.Creak:
                    return new Color32(235, 192, 42, 195);
                default:
                    if (strengthPermille < 1000)
                    {
                        float rot = 1f - Mathf.Clamp01(strengthPermille / 1000f);
                        byte a = (byte)(60 + 110 * rot);
                        return new Color32(150, 148, 138, a);
                    }
                    if (loadPermille > 100)
                    {
                        float t = Mathf.Clamp01(loadPermille / 700f);
                        return new Color32(180, 210, 68, (byte)(50 + 110 * t));
                    }
                    return Clear;
            }
        }

        /// <summary>
        /// Paints the landing-spread hazard zone: amber where landings stun and red where any landing kills.
        /// Red overrides amber when spreads overlap a cell.
        /// </summary>
        public static void PaintLandingSpread(
            IReadOnlyList<LandingSpread> spreads,
            MapData map,
            int previewCellMm,
            Color32[] pixels,
            bool clearFirst = true
        )
        {
            if (clearFirst)
            {
                Array.Fill(pixels, Clear);
            }
            if (spreads == null || spreads.Count == 0 || map == null || previewCellMm <= 0)
            {
                return;
            }
            int mapCell = Math.Max(1, map.CellSizeMm);
            int cols = map.Columns;
            int rows = map.Rows;

            for (int i = 0; i < spreads.Count; i++)
            {
                var spread = spreads[i];
                if (spread == null || spread.Cells == null)
                {
                    continue;
                }
                for (int c = 0; c < spread.Cells.Count; c++)
                {
                    var cell = spread.Cells[c];
                    bool lethal = cell.Lethal > 0;
                    bool stunned = cell.Stunned > 0;
                    if (!lethal && !stunned)
                    {
                        continue;
                    }
                    long minXMm = (long)cell.X * previewCellMm;
                    long maxXMm = minXMm + previewCellMm - 1;
                    long minYMm = (long)cell.Y * previewCellMm;
                    long maxYMm = minYMm + previewCellMm - 1;

                    int c0 = Math.Max(0, (int)(minXMm / mapCell));
                    int c1 = Math.Min(cols - 1, (int)(maxXMm / mapCell));
                    int r0 = Math.Max(0, (int)(minYMm / mapCell));
                    int r1 = Math.Min(rows - 1, (int)(maxYMm / mapCell));

                    Color32 color = lethal
                        ? new Color32(236, 36, 36, 220)
                        : new Color32(245, 178, 38, 175);

                    for (int mr = r0; mr <= r1; mr++)
                    {
                        int rowOffset = mr * cols;
                        for (int mc = c0; mc <= c1; mc++)
                        {
                            int idx = rowOffset + mc;
                            if ((uint)idx >= (uint)pixels.Length)
                            {
                                continue;
                            }
                            // Red always wins over amber/empty.
                            if (lethal || pixels[idx].a < color.a)
                            {
                                pixels[idx] = color;
                            }
                        }
                    }
                }
            }
        }
    }
}
