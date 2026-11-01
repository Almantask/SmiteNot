using System;
using System.Collections.Generic;
using System.Globalization;

namespace GodGame.Sim
{
    /// <summary>
    /// The one place the tick's systems are listed (plan, section 3.4). Every step has exactly one system, in
    /// <see cref="TickStep"/> order; a step that has no behaviour yet gets an <see cref="EmptySystem"/>.
    /// Changing a system, its order or a rate changes outcomes and needs a SimVersion bump.
    /// </summary>
    public static class TickOrder
    {
        /// <summary>Fields step at 5 Hz, staggered by tile (plan, section 3.4: a starting point, tuned later).</summary>
        public static readonly TickRate FieldsRate = TickRate.FromHertz(5);

        /// <summary>Regiments think at 4 Hz, staggered by regiment id (plan, section 3.4: a starting point).</summary>
        public static readonly TickRate RegimentAiRate = TickRate.FromHertz(4);

        /// <summary>The M1 order: every step registered, none with behaviour yet.</summary>
        public static ISimSystem[] CreateDefault()
        {
            var systems = new ISimSystem[]
            {
                new EmptySystem(TickStep.PlanEvents, TickRate.EveryTick),
                // Fields and regiment AI run every tick and stagger their tiles and regiments inside.
                new EmptySystem(TickStep.Fields, TickRate.EveryTick),
                new EmptySystem(TickStep.StructuresAndLoad, TickRate.EveryTick),
                new EmptySystem(TickStep.RegimentAi, TickRate.EveryTick),
                new EmptySystem(TickStep.Navigation, TickRate.EveryTick),
                new EmptySystem(TickStep.Movement, TickRate.EveryTick),
                new EmptySystem(TickStep.BallisticsAndImpacts, TickRate.EveryTick),
                new EmptySystem(TickStep.Combat, TickRate.EveryTick),
                new EmptySystem(TickStep.Hazards, TickRate.EveryTick),
                new EmptySystem(TickStep.Morale, TickRate.EveryTick),
                new EmptySystem(TickStep.Bookkeeping, TickRate.EveryTick),
            };
            Validate(systems);
            return systems;
        }

        /// <summary>Exactly one system per step, in step order.</summary>
        public static void Validate(IReadOnlyList<ISimSystem> systems)
        {
            var steps = (TickStep[])Enum.GetValues(typeof(TickStep));
            if (systems.Count != steps.Length)
            {
                throw new ArgumentException(
                    "Expected one system per tick step ("
                        + steps.Length.ToString(CultureInfo.InvariantCulture)
                        + "), got "
                        + systems.Count.ToString(CultureInfo.InvariantCulture)
                        + ".",
                    nameof(systems)
                );
            }
            for (int i = 0; i < systems.Count; i++)
            {
                if (systems[i] == null || systems[i].Step != (TickStep)i)
                {
                    throw new ArgumentException(
                        "System "
                            + i.ToString(CultureInfo.InvariantCulture)
                            + " must be the "
                            + (TickStep)i
                            + " step.",
                        nameof(systems)
                    );
                }
                if (systems[i].Rate.Period < 1)
                {
                    // default(TickRate) has period 0 and would divide by zero on the first tick.
                    throw new ArgumentException(
                        "The " + (TickStep)i + " system has no rate.",
                        nameof(systems)
                    );
                }
            }
        }
    }
}
