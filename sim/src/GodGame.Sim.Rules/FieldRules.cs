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

    /// <summary>The field framework's rules: the fields, their tiles and the interaction table.</summary>
    public sealed class FieldRules
    {
        public FieldRules(
            int tileCells,
            int sleepAfterSteps,
            IReadOnlyList<FieldDefinition> fields,
            IReadOnlyList<FieldInteraction> interactions
        )
        {
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
