using System;
using System.Collections.Generic;

namespace GodGame.Sim.Rules
{
    /// <summary>One field a power writes while it is active, in the field's own unit per second.</summary>
    public sealed class PowerWrite
    {
        public PowerWrite(string field, int ratePerSecond)
        {
            Field = field;
            RatePerSecond = ratePerSecond;
        }

        /// <summary>A field id from <c>fields.json</c> (Rain writes "water").</summary>
        public string Field { get; }

        /// <summary>Added to every cell of the area each second (µm/s for water).</summary>
        public int RatePerSecond { get; }
    }

    /// <summary>
    /// A god power from <c>data/rules/powers.json</c> (spec S1, section 8 for Rain): a circle the player places with a
    /// radius in bounds, checked against the range rule at the cast tick (brief, 2026-10-08; S1 D1), then a wind-up,
    /// then writes to the world's fields for its duration. Powers never touch soldiers (pillar 1).
    /// </summary>
    public sealed class PowerRules
    {
        public PowerRules(
            string id,
            int index,
            int minRadiusMm,
            int maxRadiusMm,
            int defaultRadiusMm,
            int rangeMm,
            int minSoldiersPerCoarseCell,
            int windUpTicks,
            int durationTicks,
            IReadOnlyList<PowerWrite> writes
        )
        {
            Id = id;
            Index = index;
            MinRadiusMm = minRadiusMm;
            MaxRadiusMm = maxRadiusMm;
            DefaultRadiusMm = defaultRadiusMm;
            RangeMm = rangeMm;
            MinSoldiersPerCoarseCell = minSoldiersPerCoarseCell;
            WindUpTicks = windUpTicks;
            DurationTicks = durationTicks;
            Writes = writes ?? throw new ArgumentNullException(nameof(writes));
        }

        public string Id { get; }

        /// <summary>Position in <see cref="Ruleset.Powers"/>.</summary>
        public int Index { get; }

        public int MinRadiusMm { get; }

        public int MaxRadiusMm { get; }

        public int DefaultRadiusMm { get; }

        /// <summary>The range rule: the whole area must be at least this far from both armies' outlines (100 m).</summary>
        public int RangeMm { get; }

        /// <summary>A coarse cell counts toward a regiment's outline with at least this many of its soldiers (strays don't).</summary>
        public int MinSoldiersPerCoarseCell { get; }

        /// <summary>Ticks from the cast (and its range check) to the first write: the visible warning.</summary>
        public int WindUpTicks { get; }

        /// <summary>Ticks the power writes once the wind-up is over.</summary>
        public int DurationTicks { get; }

        public IReadOnlyList<PowerWrite> Writes { get; }

        /// <summary>Rot's effect on wooden segments (spec S2 12.3), or null for a power that writes fields.</summary>
        public RotRules? Rot { get; internal set; }
    }
}
