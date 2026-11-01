using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// Turns a map, a plan and a ruleset into a starting <see cref="World"/>. Regiments deploy in the plan's id order,
    /// and their soldiers in slot order, so the same inputs give the same world everywhere.
    /// </summary>
    public static class BattleSetup
    {
        /// <summary>
        /// M1 placeholder layout until formations arrive with M2 ("Armies that march"): a square block, rows across
        /// the facing direction, soldiers one field cell (1.5 m) apart. No spec has set formation spacing yet.
        /// </summary>
        public const int PlaceholderSpacingMm = 1_500;

        public static World Deploy(
            MapData map,
            BattlePlan plan,
            Ruleset rules,
            int extraUnitCapacity = 0
        )
        {
            int soldiers = 0;
            foreach (var regiment in plan.Army)
            {
                soldiers += regiment.Soldiers;
            }
            var world = new World(
                soldiers + extraUnitCapacity,
                System.Math.Max(1, plan.Army.Count)
            );
            Fix spacing = Fix.FromRatio(PlaceholderSpacingMm, 1000);
            Fix maxX = map.Width - Fix.FromRaw(1);
            Fix maxY = map.Depth - Fix.FromRaw(1);

            foreach (var planned in plan.Army)
            {
                var type = rules.UnitType(planned.UnitType);
                var regiment = world.AddRegiment(planned.Side, type.Index, type.Morale);
                int columns = SquareSide(planned.Soldiers);
                int rows = (planned.Soldiers + columns - 1) / columns;
                var facing = Angle.FromCentiDegrees(planned.FacingCentiDegrees);
                var across = FixVec2.FromAngle(facing + Angle.QuarterTurn, spacing); // along a rank
                var back = FixVec2.FromAngle(facing + Angle.HalfTurn, spacing); // from one rank to the next
                var centre = new FixVec2(
                    Fix.FromRatio(planned.XMm, 1000),
                    Fix.FromRatio(planned.YMm, 1000)
                );
                // Offsets are counted in half-spacings from the block's centre, so the block centres on the plan point.
                for (int s = 0; s < planned.Soldiers; s++)
                {
                    int column = s % columns;
                    int row = s / columns;
                    var offset =
                        across * (2 * column - (columns - 1)) / 2
                        + back * (2 * row - (rows - 1)) / 2;
                    var p = centre + offset;
                    var clamped = new FixVec2(
                        Fix.Clamp(p.X, Fix.Zero, maxX),
                        Fix.Clamp(p.Y, Fix.Zero, maxY)
                    );
                    world.SpawnUnit(regiment, clamped, type.Health, type.Morale);
                }
            }
            return world;
        }

        private static int SquareSide(int count)
        {
            int side = (int)IntMath.Sqrt((ulong)count);
            return side * side < count ? side + 1 : side;
        }
    }
}
