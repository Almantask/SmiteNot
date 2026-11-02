using System;
using System.Collections.Generic;
using GodGame.Sim.Math;

namespace GodGame.Sim
{
    /// <summary>
    /// The attacker's predicted path, drawn from the flow fields (tech notes, section 3: "the same computation produces
    /// the player's route preview"). Run the flow field with a proposed change applied (<see cref="FlowField.Repair"/>
    /// after the edit), trace from a regiment, and draw the line before the player commits. Planning-time output: it
    /// reads the fields and decides nothing.
    /// </summary>
    public static class RoutePreview
    {
        /// <summary>A trace gives up after this many cells (a route through every cell of a big map is far shorter).</summary>
        public const int MaxSteps = 100_000;

        /// <summary>
        /// The path from <paramref name="start"/> down the flow field to its goal, as cell centres, keeping only the
        /// corners (where the direction changes) and both ends; empty when the start can't reach the goal.
        /// </summary>
        public static IReadOnlyList<FixVec2> Trace(FlowField field, FixVec2 start)
        {
            if (field == null)
            {
                throw new ArgumentNullException(nameof(field));
            }
            var terrain = field.Terrain;
            terrain.CellOf(start, out int x, out int y);
            var directions = field.Directions;
            var points = new List<FixVec2>();
            byte d = directions[terrain.Index(x, y)];
            if (d == FlowField.NoDirection)
            {
                return points;
            }
            points.Add(Centre(terrain, x, y));
            byte previous = d;
            for (int step = 0; step < MaxSteps && d < FlowField.AtGoal; step++)
            {
                var (dx, dy) = FlowField.Offset(d);
                x += dx;
                y += dy;
                d = directions[terrain.Index(x, y)];
                if (d != previous || d >= FlowField.AtGoal)
                {
                    points.Add(Centre(terrain, x, y)); // a corner, or the goal
                }
                previous = d;
            }
            return points;
        }

        /// <summary>The predicted path of every attacking regiment, from its officer, in regiment slot order (empty for one that holds).</summary>
        public static IReadOnlyList<FixVec2>[] AttackerRoutes(Navigation navigation, World world)
        {
            if (navigation == null)
            {
                throw new ArgumentNullException(nameof(navigation));
            }
            if (world == null)
            {
                throw new ArgumentNullException(nameof(world));
            }
            navigation.AssignRegiments(world, new SerialRunner());
            var routes = new IReadOnlyList<FixVec2>[world.Regiments.HighWater];
            for (int r = 0; r < routes.Length; r++)
            {
                var field = world.Regiments.IsAlive(r) ? navigation.FieldOf(r) : null;
                int anchor = world.Regiments.IsAlive(r) ? world.RegimentAnchor[r] : -1;
                routes[r] =
                    field == null || anchor < 0
                        ? Array.Empty<FixVec2>()
                        : Trace(field, world.Position[anchor]);
            }
            return routes;
        }

        private static FixVec2 Centre(Terrain terrain, int x, int y) =>
            new FixVec2(
                Fix.FromRatio((long)x * terrain.CellSizeMm + terrain.CellSizeMm / 2, 1000),
                Fix.FromRatio((long)y * terrain.CellSizeMm + terrain.CellSizeMm / 2, 1000)
            );
    }
}
