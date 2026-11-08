using System;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// The battle's navigation: terrain, cost fields, the distance to lethal cells, the flow fields regiments follow,
    /// and which field each regiment follows. All of it is derived from the map, the rules and the world (regiment
    /// side and unit type, and the hashed <see cref="FieldSpeed"/> codes), so it needs no place in the state hash or
    /// snapshots: <see cref="AssignRegiments"/> recomputes the assignment every tick, <see cref="RefreshFieldCosts"/>
    /// refreshes the codes from water and mud every <see cref="Ruleset.FieldCostRefreshTicks"/> and rebuilds what they
    /// changed, and <see cref="RebuildFromWorld"/> rebuilds everything after a restore.
    /// <para>
    /// M2 goals are a working default until regiment AI arrives (M7, ai-engineer): attacking regiments march to the
    /// far quarter of the defender's deployment zone, defending regiments hold where they stand.
    /// </para>
    /// </summary>
    public sealed class Navigation
    {
        private int[] _regimentField = new int[0];
        private int[] _codes = new int[0]; // scratch: freshly computed speed codes
        private bool _builtFromWorld; // false until the derived data has been built from the world's stored codes

        public Navigation(MapData map, Ruleset rules, IParallelRunner runner)
        {
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            Terrain = new Terrain(map);
            Costs = new CostFields(Terrain, rules, runner);
            Lethal = new LethalDistance(Terrain, rules.Hazards.LethalDropMm);
            FlowFields = new FlowFieldSet(Costs);
        }

        public MapData Map { get; }

        public Ruleset Rules { get; }

        public Terrain Terrain { get; }

        public CostFields Costs { get; }

        public FlowFieldSet FlowFields { get; }

        /// <summary>Distance to the nearest lethal cell (cliff tops and the map edge for now).</summary>
        public LethalDistance Lethal { get; }

        /// <summary>
        /// On refresh ticks, computes every cell's <see cref="FieldSpeed"/> codes from the current water and mud (in
        /// parallel rows), writes the changed ones to the world's hashed codes, and rebuilds costs, flow fields and
        /// lethal distances over the rectangle that changed. A world without the fields keeps dry costs.
        /// </summary>
        public void RefreshFieldCosts(TickContext context)
        {
            var fields = context.World.Fields;
            if (
                fields == null
                || fields.Rules.IndexOf(FieldSpeed.FieldId) < 0
                || fields.Rules.Water == null
                || context.Tick % Rules.FieldCostRefreshTicks != 0
            )
            {
                return;
            }
            var stored = fields.Get(FieldSpeed.FieldId).Values;
            if (_codes.Length != stored.Length)
            {
                _codes = new int[stored.Length];
            }
            context.Runner.For(
                Terrain.Rows,
                CostFields.RowsPerChunk,
                new CodeKernel(fields, Rules, Terrain.CellSizeMm, _codes, Terrain.Columns)
            );
            // Serial, in cell order: copy what changed and grow the rectangle around it.
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = -1;
            int maxY = -1;
            var log = context.World.Causes;
            var depthGrid = fields.Get(WaterModel.Depth);
            for (int y = 0; y < Terrain.Rows; y++)
            {
                int row = y * Terrain.Columns;
                for (int x = 0; x < Terrain.Columns; x++)
                {
                    int code = _codes[row + x];
                    if (
                        !FieldSpeed.Significant(
                            stored[row + x],
                            code,
                            Rules.FieldCostHysteresisPermille
                        )
                    )
                    {
                        continue; // S1's cost hysteresis: small speed drifts wait for a real change
                    }
                    // Tick 0 only learns the map's settled water: a river that already sweeps soldiers didn't overflow.
                    if (
                        log != null
                        && context.Tick > 0
                        && FieldSpeed.Code(code, MovementClass.Foot) == FieldSpeed.Swept
                        && FieldSpeed.Code(stored[row + x], MovementClass.Foot) != FieldSpeed.Swept
                    )
                    {
                        // The first cell of a water tile to sweep foot soldiers: the river overflowed here (S1 12).
                        int tile = depthGrid.TileOf(x, y);
                        if (log.TileOverflowed[tile] == CauseLog.None)
                        {
                            log.TileOverflowed[tile] = log.Add(
                                CauseKind.RiverOverflowed,
                                context.Tick,
                                tile,
                                0,
                                log.TileTag[tile]
                            );
                        }
                    }
                    stored[row + x] = code;
                    minX = System.Math.Min(minX, x);
                    maxX = System.Math.Max(maxX, x);
                    minY = System.Math.Min(minY, y);
                    maxY = y;
                }
            }
            if (maxX < 0)
            {
                return;
            }
            var dirty = new CellRect(minX, minY, maxX, maxY);
            Costs.UseFieldCodes(stored);
            Lethal.UseFieldCodes(stored);
            Costs.Rebuild(dirty, context.Runner);
            FlowFields.Repair(dirty, context.Runner);
            Lethal.Rebuild(dirty);
        }

        /// <summary>
        /// Rebuilds costs, lethal distances and every flow field from the world's stored speed codes: after a snapshot
        /// restore, when the codes may differ from what the derived data was built from.
        /// </summary>
        public void RebuildFromWorld(World world, IParallelRunner runner)
        {
            _builtFromWorld = true;
            var fields = world.Fields;
            int[]? codes =
                fields != null && fields.Rules.IndexOf(FieldSpeed.FieldId) >= 0
                    ? fields.Get(FieldSpeed.FieldId).Values
                    : null;
            Costs.UseFieldCodes(codes);
            Lethal.UseFieldCodes(codes);
            Costs.RebuildAll(runner);
            FlowFields.RebuildAll(runner);
            Lethal.Rebuild(Terrain.All);
        }

        /// <summary>Builds from the world's codes once, the first time navigation runs on it.</summary>
        public void EnsureBuiltFromWorld(World world, IParallelRunner runner)
        {
            if (!_builtFromWorld)
            {
                RebuildFromWorld(world, runner);
            }
        }

        private readonly struct CodeKernel : IChunkKernel
        {
            private readonly FieldStore _fields;
            private readonly FieldGrid _depth;
            private readonly FieldGrid _mud;
            private readonly Ruleset _rules;
            private readonly int _cellSizeMm;
            private readonly int _dryDepthUm;
            private readonly int[] _codes;
            private readonly int _columns;

            public CodeKernel(
                FieldStore fields,
                Ruleset rules,
                int cellSizeMm,
                int[] codes,
                int columns
            )
            {
                _fields = fields;
                _depth = fields.Get(WaterModel.Depth);
                _mud = fields.Get(GroundModel.Mud);
                _rules = rules;
                _cellSizeMm = cellSizeMm;
                _dryDepthUm = fields.Rules.Water!.DryDepthUm;
                _codes = codes;
                _columns = columns;
            }

            // Each chunk writes only its own rows.
            public void Execute(int start, int end)
            {
                for (int y = start; y < end; y++)
                {
                    for (int x = 0; x < _columns; x++)
                    {
                        _codes[y * _columns + x] = FieldSpeed.CellCodes(
                            _fields,
                            _depth,
                            _mud,
                            _rules,
                            _cellSizeMm,
                            _dryDepthUm,
                            x,
                            y
                        );
                    }
                }
            }
        }

        /// <summary>The flow field a regiment follows, or null when it holds its ground.</summary>
        public FlowField? FieldOf(int regiment)
        {
            int index = regiment < _regimentField.Length ? _regimentField[regiment] : -1;
            return index < 0 ? null : FlowFields[index];
        }

        /// <summary>
        /// Gives every live regiment its goal and builds any flow field not built yet (in parallel, one field per
        /// chunk). Regiments are visited in slot order, so new fields are added in the same order everywhere.
        /// </summary>
        public void AssignRegiments(World world, IParallelRunner runner)
        {
            if (_regimentField.Length < world.Regiments.Capacity)
            {
                _regimentField = new int[world.Regiments.Capacity];
            }
            for (int r = 0; r < _regimentField.Length; r++)
            {
                _regimentField[r] = -1;
            }
            bool hasTarget = TryZone(Side.Defender, out var target);
            var goal = hasTarget ? Terrain.CellsOf(FarQuarter(target)) : default;
            for (int r = 0; r < world.Regiments.HighWater; r++)
            {
                if (!world.Regiments.IsAlive(r))
                {
                    continue;
                }
                var movementClass = Rules.UnitTypes[world.RegimentUnitType[r]].MovementClass;
                if (world.Army.Order[r] == RegimentOrder.Rout)
                {
                    // Routed: run for the side's retreat edge, the map edge nearest its zone (S2 7.2).
                    _regimentField[r] = FlowFields.GetOrAdd(
                        movementClass,
                        RetreatEdge(world.RegimentSide[r])
                    );
                    continue;
                }
                if (world.RegimentSide[r] != Side.Attacker || !hasTarget)
                {
                    continue;
                }
                // A far quarter this class can't stand in at all (a lake, a cliff) would leave the field without a
                // goal: march for the whole zone instead, which the reachability check measures too.
                var classGoal = AnyPassable(movementClass, goal) ? goal : Terrain.CellsOf(target);
                _regimentField[r] = FlowFields.GetOrAdd(movementClass, classGoal);
            }
            FlowFields.BuildPending(runner);
        }

        /// <summary>
        /// The quarter of the defender's zone farthest from the attacker's (along the axis the zones are furthest
        /// apart on): the attackers march through the zone rather than stopping at its edge, so arriving regiments
        /// spread into it instead of piling up at the border and backing up the road. A working default until regiment
        /// AI chooses real targets (M7).
        /// </summary>
        private RectMm FarQuarter(RectMm target)
        {
            if (!TryZone(Side.Attacker, out var from))
            {
                return target;
            }
            long dx = ((long)target.MinX + target.MaxX) - ((long)from.MinX + from.MaxX);
            long dy = ((long)target.MinY + target.MaxY) - ((long)from.MinY + from.MaxY);
            int quarterX = System.Math.Max(1, target.Width / 4);
            int quarterY = System.Math.Max(1, target.Depth / 4);
            if (System.Math.Abs(dx) >= System.Math.Abs(dy))
            {
                return dx >= 0
                    ? new RectMm(target.MaxX - quarterX, target.MinY, target.MaxX, target.MaxY)
                    : new RectMm(target.MinX, target.MinY, target.MinX + quarterX, target.MaxY);
            }
            return dy >= 0
                ? new RectMm(target.MinX, target.MaxY - quarterY, target.MaxX, target.MaxY)
                : new RectMm(target.MinX, target.MinY, target.MaxX, target.MinY + quarterY);
        }

        /// <summary>
        /// The one-cell band along the map edge nearest the side's deployment zone (west, east, south, north on ties,
        /// as the reachability check picks it); the west edge for a side without a zone.
        /// </summary>
        public CellRect RetreatEdge(Side side)
        {
            int edge = 0;
            if (TryZone(side, out var zone))
            {
                long best = zone.MinX;
                long east = Map.WidthMm - zone.MaxX;
                long south = zone.MinY;
                long north = Map.DepthMm - zone.MaxY;
                if (east < best)
                {
                    edge = 1;
                    best = east;
                }
                if (south < best)
                {
                    edge = 2;
                    best = south;
                }
                if (north < best)
                {
                    edge = 3;
                }
            }
            int maxX = Terrain.Columns - 1;
            int maxY = Terrain.Rows - 1;
            switch (edge)
            {
                case 0:
                    return new CellRect(0, 0, 0, maxY);
                case 1:
                    return new CellRect(maxX, 0, maxX, maxY);
                case 2:
                    return new CellRect(0, 0, maxX, 0);
                default:
                    return new CellRect(0, maxY, maxX, maxY);
            }
        }

        private bool AnyPassable(MovementClass movementClass, CellRect cells)
        {
            for (int y = cells.MinY; y <= cells.MaxY; y++)
            {
                for (int x = cells.MinX; x <= cells.MaxX; x++)
                {
                    if (Costs.Cost(movementClass, x, y) != CostFields.Impassable)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool TryZone(Side side, out RectMm area)
        {
            foreach (var zone in Map.DeploymentZones)
            {
                if (zone.Side == side)
                {
                    area = zone.Area;
                    return true;
                }
            }
            area = default;
            return false;
        }
    }

    /// <summary>The navigation step: regiment goals and flow fields, before movement reads them.</summary>
    public sealed class NavigationSystem : ISimSystem, IRestoreAware
    {
        private readonly Navigation _navigation;

        public NavigationSystem(Navigation navigation)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        }

        public TickStep Step => TickStep.Navigation;

        public TickRate Rate => TickRate.EveryTick;

        public void Run(TickContext context)
        {
            // The first run treats the start like a restore: whatever codes the world starts with, the costs match them.
            _navigation.EnsureBuiltFromWorld(context.World, context.Runner);
            _navigation.RefreshFieldCosts(context);
            _navigation.AssignRegiments(context.World, context.Runner);
        }

        public void AfterRestore(TickContext context) =>
            _navigation.RebuildFromWorld(context.World, context.Runner);
    }
}
