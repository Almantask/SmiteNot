using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// Turns a map, a plan and a ruleset into a starting <see cref="World"/>. Regiments deploy in the plan's id order,
    /// in formation (<see cref="Formation"/>: the officer in front, ranks behind), centred on the plan point, and
    /// their soldiers in slot order, so the same inputs give the same world everywhere.
    /// </summary>
    public static class BattleSetup
    {
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
            var fields = new FieldStore(rules.Fields, map.Columns, map.Rows);
            world.AttachFields(fields);
            // Wake the tiles under every water source, so rivers flow from the first field step.
            if (rules.Fields.IndexOf(WaterModel.Depth) >= 0)
            {
                var water = fields.Get(WaterModel.Depth);
                foreach (var source in map.WaterSources)
                {
                    var cells = new Terrain(map).CellsOf(source.Area);
                    for (int y = cells.MinY; y <= cells.MaxY; y += water.TileCells)
                    {
                        for (int x = cells.MinX; x <= cells.MaxX; x += water.TileCells)
                        {
                            water.WakeAround(water.TileOf(x, y));
                        }
                    }
                    water.WakeAround(water.TileOf(cells.MaxX, cells.MaxY));
                }
            }
            Fix maxX = map.Width - Fix.FromRaw(1);
            Fix maxY = map.Depth - Fix.FromRaw(1);

            foreach (var planned in plan.Army)
            {
                var type = rules.UnitType(planned.UnitType);
                var formation = type.Formation;
                int files = Formation.FilesFor(formation, planned.Soldiers);
                int ranks = Formation.Ranks(planned.Soldiers, files);
                var facing = Angle.FromCentiDegrees(planned.FacingCentiDegrees);
                var regiment = world.AddRegiment(
                    planned.Side,
                    type.Index,
                    type.Morale,
                    files,
                    facing
                );
                Fix fileSpacing = Fix.FromRatio(formation.FileSpacingMm, 1000);
                Fix rankSpacing = Fix.FromRatio(formation.RankSpacingMm, 1000);
                // The plan point is the middle of the block, from the officer to the last rank.
                var centre = new FixVec2(
                    Fix.FromRatio(planned.XMm, 1000),
                    Fix.FromRatio(planned.YMm, 1000)
                );
                var officer = centre + FixVec2.FromAngle(facing, rankSpacing * ranks / 2);
                for (int s = 0; s < planned.Soldiers; s++)
                {
                    var p =
                        officer + Formation.SlotOffset(s, files, fileSpacing, rankSpacing, facing);
                    var clamped = new FixVec2(
                        Fix.Clamp(p.X, Fix.Zero, maxX),
                        Fix.Clamp(p.Y, Fix.Zero, maxY)
                    );
                    world.SpawnUnit(regiment, clamped, type.Health, type.Morale);
                }
            }
            return world;
        }
    }
}
