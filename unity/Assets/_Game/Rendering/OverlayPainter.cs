using System;
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
        private static readonly Color32 Clear = new Color32(0, 0, 0, 0);

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
    }
}
