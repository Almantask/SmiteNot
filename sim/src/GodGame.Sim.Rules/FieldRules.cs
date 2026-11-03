using System;
using System.Collections.Generic;

namespace GodGame.Sim.Rules
{
    /// <summary>One world field from <c>data/rules/fields.json</c>: an integer grid in a documented unit.</summary>
    public sealed class FieldDefinition
    {
        public FieldDefinition(int index, string id, string unit, int min, int max, int initial)
        {
            Index = index;
            Id = id;
            Unit = unit;
            Min = min;
            Max = max;
            Initial = initial;
        }

        /// <summary>Position in the file: the order fields are stored, hashed and snapshotted.</summary>
        public int Index { get; }

        public string Id { get; }

        /// <summary>"mm", "scale" (0 to 65535), "centidegrees" or "cmPerS" (data rules: no floats, units documented).</summary>
        public string Unit { get; }

        public int Min { get; }

        public int Max { get; }

        /// <summary>Every cell's value at the start of a battle, unless the map or a terrain piece sets it.</summary>
        public int Initial { get; }
    }

    /// <summary>
    /// One row of the field interaction table (tech notes, section 6: interaction strengths are data, not code): an
    /// effect of one field on another, with integer parameters whose meaning the named rule's model documents.
    /// </summary>
    public sealed class FieldInteraction
    {
        public FieldInteraction(string id, int source, int target, int rate, int threshold)
        {
            Id = id;
            Source = source;
            Target = target;
            Rate = rate;
            Threshold = threshold;
        }

        /// <summary>The rule's name, which the model that applies it looks up.</summary>
        public string Id { get; }

        /// <summary>Field index read.</summary>
        public int Source { get; }

        /// <summary>Field index written.</summary>
        public int Target { get; }

        /// <summary>How much per field step, in the target's unit per the source's unit (the model documents the scale).</summary>
        public int Rate { get; }

        /// <summary>The source value below which the rule does nothing.</summary>
        public int Threshold { get; }
    }

    /// <summary>
    /// The water model's numbers (spec S1, section 2: virtual pipes on the heightfield). Depth is in µm, and each cell
    /// keeps four outflows (east, north, west, south) in µm/s of its own depth.
    /// </summary>
    public sealed class WaterRules
    {
        public WaterRules(
            int substepsPerFieldStep,
            int pipeGainPermille,
            int bedFrictionPpm,
            int fluxDeadbandUmPerS,
            int headDeadbandUm,
            int dryDepthUm
        )
        {
            HeadDeadbandUm = headDeadbandUm;
            SubstepsPerFieldStep = substepsPerFieldStep;
            PipeGainPermille = pipeGainPermille;
            BedFrictionPpm = bedFrictionPpm;
            FluxDeadbandUmPerS = fluxDeadbandUmPerS;
            DryDepthUm = dryDepthUm;
        }

        /// <summary>Water substeps per field step (2: water at 10 Hz, stable to about 5.7 m deep).</summary>
        public int SubstepsPerFieldStep { get; }

        /// <summary>Outflow gained per substep per µm of head difference, in µm/s per µm (‰): g·dt / cell.</summary>
        public int PipeGainPermille { get; }

        /// <summary>
        /// The bed friction coefficient in millionths (the shallow-water c_f: each substep slows a flow by
        /// c_f × speed × dt / depth), so rivers settle at speed √(g·depth·slope / c_f): deep water runs faster than a
        /// thin sheet on the same slope. 4,200 (0.0042) runs the M3 test river at about 0.7 m/s.
        /// </summary>
        public int BedFrictionPpm { get; }

        /// <summary>
        /// Outflows slower than this, with less than <see cref="HeadDeadbandUm"/> of head behind them, are dropped, so
        /// still water goes quiet and its tiles sleep.
        /// </summary>
        public int FluxDeadbandUmPerS { get; }

        /// <summary>The head difference below which a slow outflow counts as still water.</summary>
        public int HeadDeadbandUm { get; }

        /// <summary>Shallower water counts as dry for units and rendering.</summary>
        public int DryDepthUm { get; }
    }

    /// <summary>The field framework's rules: the fields, their tiles and the interaction table.</summary>
    public sealed class FieldRules
    {
        public FieldRules(
            int tileCells,
            int sleepAfterSteps,
            IReadOnlyList<FieldDefinition> fields,
            IReadOnlyList<FieldInteraction> interactions,
            WaterRules? water = null
        )
        {
            Water = water;
            TileCells = tileCells;
            SleepAfterSteps = sleepAfterSteps;
            Fields = fields ?? throw new ArgumentNullException(nameof(fields));
            Interactions = interactions ?? throw new ArgumentNullException(nameof(interactions));
        }

        /// <summary>Cells per side of an activity tile (16 is 24 m on 1.5 m cells).</summary>
        public int TileCells { get; }

        /// <summary>Field steps a tile stays awake after its last change before it sleeps.</summary>
        public int SleepAfterSteps { get; }

        /// <summary>In file order: the order fields are stored, hashed and snapshotted.</summary>
        public IReadOnlyList<FieldDefinition> Fields { get; }

        public IReadOnlyList<FieldInteraction> Interactions { get; }

        /// <summary>The water model's numbers (spec S1, section 2); null for field sets without water.</summary>
        public WaterRules? Water { get; }

        /// <summary>The field's index, or -1.</summary>
        public int IndexOf(string id)
        {
            for (int i = 0; i < Fields.Count; i++)
            {
                if (string.Equals(Fields[i].Id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The interaction row with this id, or null.</summary>
        public FieldInteraction? Interaction(string id)
        {
            foreach (var row in Interactions)
            {
                if (string.Equals(row.Id, id, StringComparison.Ordinal))
                {
                    return row;
                }
            }
            return null;
        }
    }
}
