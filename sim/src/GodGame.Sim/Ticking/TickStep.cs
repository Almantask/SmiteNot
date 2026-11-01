namespace GodGame.Sim
{
    /// <summary>
    /// The fixed order of a tick (plan, section 3.4), one system per step. The numbers are the order: changing
    /// them changes outcomes, so it needs a SimVersion bump.
    /// </summary>
    public enum TickStep
    {
        /// <summary>Casts reaching their tick are checked against the range rule; failures fizzle with a reason.</summary>
        PlanEvents = 0,

        /// <summary>Fields at a sub-rate on active tiles, in the fixed interaction order.</summary>
        Fields = 1,

        /// <summary>Bridges, ice, dams, mines and snow slopes compare load with strength.</summary>
        StructuresAndLoad = 2,

        /// <summary>Goals, waiting, rerouting, garrisons, specialist actions, morale state; staggered by regiment.</summary>
        RegimentAi = 3,

        /// <summary>Rebuild dirty flow-field tiles, cost fields and the distance-to-lethal field.</summary>
        Navigation = 4,

        /// <summary>Flow fields, steering, separation, crowd pressure, teetering and lethal edges.</summary>
        Movement = 5,

        /// <summary>Launched soldiers and rolling boulders; impacts and landings.</summary>
        BallisticsAndImpacts = 6,

        /// <summary>Melee contacts and regiment volleys.</summary>
        Combat = 7,

        /// <summary>Swept and drowning, burning, reaching safe ground; every death records its cause.</summary>
        Hazards = 8,

        /// <summary>Casualty shock, contagion, routs; fleeing off the map counts as wiped.</summary>
        Morale = 9,

        /// <summary>Cause events, playback samples, launch events, state hash, keyframes.</summary>
        Bookkeeping = 10,
    }
}
