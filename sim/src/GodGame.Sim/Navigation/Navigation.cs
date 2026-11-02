using System;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// The battle's navigation: terrain, cost fields, the distance to lethal cells, the flow fields regiments follow,
    /// and which field each regiment follows. All of it is derived from the map, the rules and the world (regiment
    /// side and unit type), so it needs no place in the state hash or snapshots: <see cref="AssignRegiments"/>
    /// recomputes the assignment every tick.
    /// <para>
    /// M2 goals are a working default until regiment AI arrives (M7, ai-engineer): attacking regiments march to the
    /// defender's deployment zone, defending regiments hold where they stand.
    /// </para>
    /// </summary>
    public sealed class Navigation
    {
        private int[] _regimentField = new int[0];

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
            var goal = hasTarget ? Terrain.CellsOf(target) : default;
            for (int r = 0; r < world.Regiments.HighWater; r++)
            {
                if (
                    !world.Regiments.IsAlive(r)
                    || world.RegimentSide[r] != Side.Attacker
                    || !hasTarget
                )
                {
                    continue;
                }
                var movementClass = Rules.UnitTypes[world.RegimentUnitType[r]].MovementClass;
                _regimentField[r] = FlowFields.GetOrAdd(movementClass, goal);
            }
            FlowFields.BuildPending(runner);
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
    public sealed class NavigationSystem : ISimSystem
    {
        private readonly Navigation _navigation;

        public NavigationSystem(Navigation navigation)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
        }

        public TickStep Step => TickStep.Navigation;

        public TickRate Rate => TickRate.EveryTick;

        public void Run(TickContext context) =>
            _navigation.AssignRegiments(context.World, context.Runner);
    }
}
