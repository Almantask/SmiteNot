using GodGame.Sim.Math;
using GodGame.Sim.Rules;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim
{
    /// <summary>
    /// Turns a map, a plan and a ruleset into a starting <see cref="World"/>. Regiments deploy in the plan's id order,
    /// in formation (<see cref="Formation"/>: the officer in front, ranks behind), centred on the plan point, and
    /// their soldiers in slot order, so the same inputs give the same world everywhere.
    /// </summary>
    public static class BattleSetup
    {
        /// <summary>
        /// Puts the map's settled fields in place before tick 0 (spec S1, section 7), waking every tile that differs from
        /// the field's initial value so the models step it from the first tick.
        /// </summary>
        public static void ApplyInitialFields(FieldStore fields, MapData map)
        {
            foreach (var layer in map.InitialFields)
            {
                if (fields.Rules.IndexOf(layer.Key) < 0)
                {
                    throw new DataFormatException(
                        "Map "
                            + map.Id
                            + " starts the field \""
                            + layer.Key
                            + "\", which the rules don't define."
                    );
                }
                var grid = fields.Get(layer.Key);
                int[] values = layer.Value;
                for (int i = 0; i < values.Length; i++)
                {
                    int v = values[i];
                    if (v != grid.Clamp(v))
                    {
                        throw new DataFormatException(
                            "Map "
                                + map.Id
                                + ": a starting "
                                + layer.Key
                                + " value is out of the field's range."
                        );
                    }
                    grid.Values[i] = v;
                    if (v != grid.Definition.Initial)
                    {
                        grid.WakeAround(grid.TileOf(i % grid.Columns, i / grid.Columns));
                    }
                }
            }
        }

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
                soldiers += rules.UnitType(regiment.UnitType).UnitsFor(regiment.Soldiers);
            }
            int animals = 0;
            foreach (var herd in map.Herds)
            {
                animals += herd.Animals;
            }
            var world = new World(
                soldiers + animals + extraUnitCapacity,
                System.Math.Max(1, plan.Army.Count + map.Herds.Count)
            );
            world.AttachFields(new FieldStore(rules.Fields, map.Columns, map.Rows));
            ApplyInitialFields(world.Fields!, map);
            world.AttachCasts(CastSystem.StatesFor(plan));
            // Every battle can hold decks: the map's, and those its specialists build (trestles, pontoons).
            world.AttachStructures(new StructureStates(map.Decks?.Segments.Count ?? 0));
            world.AttachCauses(
                new CauseLog(
                    world.Fields!.Get(WaterModel.Depth).TileCount,
                    world.Regiments.Capacity,
                    world.Units.Capacity
                )
            );
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
                    rules.Morale.Start,
                    files,
                    facing
                );
                // Stones are per engine (S2 6.1): the regiment carries its engines' ammunition as one pool.
                world.Army.Init(
                    regiment.Index,
                    (type.Projectile?.Ammo ?? 0) * (type.Engine == null ? 1 : planned.Soldiers),
                    rules.Morale.RallyTimes
                );
                world.Army.Open[regiment.Index] = planned.Open;
                // Open order (S2 2.2) doubles both spacings from the start.
                int open = planned.Open ? rules.Combat.OpenSpacingPermille : 1000;
                Fix fileSpacing = Fix.FromRatio((long)formation.FileSpacingMm * open, 1_000_000);
                Fix rankSpacing = Fix.FromRatio((long)formation.RankSpacingMm * open, 1_000_000);
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
                var engine = type.Engine;
                if (engine == null)
                {
                    continue;
                }
                // Each engine's crew, in engine order, around it (S2 2.1, D4): formation slots after the engines.
                var crew = engine.CrewType;
                Fix ring = Fix.FromRatio(crew.SlotRadiusMm, 1000);
                for (int e = 0; e < planned.Soldiers; e++)
                {
                    var at =
                        officer + Formation.SlotOffset(e, files, fileSpacing, rankSpacing, facing);
                    for (int j = 0; j < engine.CrewPerEngine; j++)
                    {
                        var p = at + EngineCrews.CrewOffset(j, engine.CrewPerEngine, ring, facing);
                        var clamped = new FixVec2(
                            Fix.Clamp(p.X, Fix.Zero, maxX),
                            Fix.Clamp(p.Y, Fix.Zero, maxY)
                        );
                        world.SpawnUnit(regiment, clamped, crew.Health, crew.Morale, crew.Index);
                    }
                }
            }
            DeployHerds(world, map, rules, maxX, maxY);
            return world;
        }

        /// <summary>
        /// Each herd (spec S2 11.1) as a neutral regiment of animals after the army's regiments, in plan order, scattered
        /// over its grazing circle by a hash of the herd's id (area-uniform, a metre inside the edge), grazing.
        /// </summary>
        private static void DeployHerds(World world, MapData map, Ruleset rules, Fix maxX, Fix maxY)
        {
            var herds = rules.Herds;
            if (herds == null || map.Herds.Count == 0)
            {
                return;
            }
            var type = herds.AnimalType;
            var regiments = new int[map.Herds.Count];
            var sizes = new int[map.Herds.Count];
            for (int h = 0; h < map.Herds.Count; h++)
            {
                var spec = map.Herds[h];
                var regiment = world.AddRegiment(Side.Neutral, type.Index, type.Morale, 1);
                regiments[h] = regiment.Index;
                sizes[h] = spec.Animals;
                long reach = System.Math.Max(1, spec.RadiusMm - 1_000);
                for (int k = 0; k < spec.Animals; k++)
                {
                    var hash = new SimHash();
                    hash.AppendInt32(spec.Id.Length);
                    foreach (char c in spec.Id)
                    {
                        hash.AppendInt32(c);
                    }
                    hash.AppendInt32(k);
                    ulong bits = hash.Digest();
                    var angle = Angle.FromUnits((int)(bits % (ulong)Angle.UnitsPerTurn));
                    long r = IntMath.Sqrt((bits >> 20) % (ulong)(reach * reach));
                    var p =
                        new FixVec2(Fix.FromRatio(spec.XMm, 1000), Fix.FromRatio(spec.YMm, 1000))
                        + FixVec2.FromAngle(angle, Fix.FromRatio(r, 1000));
                    world.SpawnUnit(
                        regiment,
                        new FixVec2(Fix.Clamp(p.X, Fix.Zero, maxX), Fix.Clamp(p.Y, Fix.Zero, maxY)),
                        type.Health,
                        type.Morale
                    );
                }
            }
            world.Herds.Init(regiments, sizes);
            for (int h = 0; h < map.Herds.Count; h++)
            {
                // Each herd grazes around its circle's centre until something moves it (S2 11.1).
                world.Herds.Anchor[h] = new FixVec2(
                    Fix.FromRatio(map.Herds[h].XMm, 1000),
                    Fix.FromRatio(map.Herds[h].YMm, 1000)
                );
            }
        }
    }
}
