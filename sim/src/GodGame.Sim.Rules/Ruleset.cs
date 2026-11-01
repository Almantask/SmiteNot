using System;
using System.Collections.Generic;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim.Rules
{
    /// <summary>How a unit type moves; at most four at launch (data rules): foot, horse, wheeled and one water class.</summary>
    public enum MovementClass
    {
        Foot = 0,
        Horse = 1,
        Wheeled = 2,
        Water = 3,
    }

    /// <summary>One unit type from <c>data/rules/unit-types.json</c>. Units are documented in data/schema/unit-types.schema.json.</summary>
    public sealed class UnitTypeRules
    {
        public UnitTypeRules(
            int index,
            string id,
            MovementClass movementClass,
            int health,
            int morale
        )
        {
            Index = index;
            Id = id;
            MovementClass = movementClass;
            Health = health;
            Morale = morale;
        }

        /// <summary>Position in the ruleset, which is what <c>World.UnitType</c> stores.</summary>
        public int Index { get; }

        /// <summary>Stable id used by plans, maps and replays.</summary>
        public string Id { get; }

        public MovementClass MovementClass { get; }

        /// <summary>Starting hit points.</summary>
        public int Health { get; }

        /// <summary>Starting morale.</summary>
        public int Morale { get; }
    }

    /// <summary>
    /// The immutable rules a battle runs on, with a hash over their canonical form (plan, section 3.8): replays
    /// record it and PvP checks it at lock-in, so two machines can't fight with different rules.
    /// </summary>
    public sealed class Ruleset
    {
        private readonly Dictionary<string, UnitTypeRules> _unitTypesById;

        internal Ruleset(IReadOnlyList<UnitTypeRules> unitTypes, ulong hash, bool hasPlaceholders)
        {
            UnitTypes = unitTypes;
            Hash = hash;
            HasPlaceholders = hasPlaceholders;
            _unitTypesById = new Dictionary<string, UnitTypeRules>(StringComparer.Ordinal);
            foreach (var type in unitTypes)
            {
                _unitTypesById.Add(type.Id, type);
            }
        }

        /// <summary>In file order; <see cref="UnitTypeRules.Index"/> is the position here.</summary>
        public IReadOnlyList<UnitTypeRules> UnitTypes { get; }

        public ulong Hash { get; }

        /// <summary>True while any file is marked <c>"status": "placeholder"</c>: numbers no spec has set yet.</summary>
        public bool HasPlaceholders { get; }

        public UnitTypeRules UnitType(string id) =>
            _unitTypesById.TryGetValue(id, out var type)
                ? type
                : throw new DataFormatException("Unknown unit type \"" + id + "\".");
    }
}
