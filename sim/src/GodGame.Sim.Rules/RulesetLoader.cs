using System;
using System.Collections.Generic;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim.Rules
{
    /// <summary>
    /// Builds a <see cref="Ruleset"/> from rules files. The simulation reads no files itself: the host reads
    /// <c>data/rules/*.json</c> and passes (file name, text) pairs. v0 knows <c>unit-types.json</c> and
    /// <c>movement-classes.json</c>, and needs both.
    /// </summary>
    public static class RulesetLoader
    {
        public const int UnitTypesVersion = 0;
        public const string UnitTypesFile = "unit-types.json";
        public const int MovementClassesVersion = 0;
        public const string MovementClassesFile = "movement-classes.json";

        /// <summary>Costs fit a ushort with room for the impassable marker.</summary>
        public const int MaxCost = 10_000;

        /// <summary>20 m/s: a galloping horse is about 15.</summary>
        public const int MaxSpeedMmPerSecond = 20_000;

        /// <summary>Bodies from 10 cm to 5 m across the ground; the movement system's neighbour search assumes this.</summary>
        public const int MinRadiusMm = 100;

        public const int MaxRadiusMm = 2_500;

        public static Ruleset Load(IReadOnlyList<KeyValuePair<string, string>> files)
        {
            if (files == null)
            {
                throw new ArgumentNullException(nameof(files));
            }
            var parsed = new List<KeyValuePair<string, JsonValue>>();
            JsonValue? unitTypesJson = null;
            JsonValue? movementJson = null;
            foreach (var file in files)
            {
                var value = JsonReader.Parse(file.Value, file.Key);
                parsed.Add(new KeyValuePair<string, JsonValue>(file.Key, value));
                if (string.Equals(file.Key, UnitTypesFile, StringComparison.Ordinal))
                {
                    unitTypesJson = value;
                }
                else if (string.Equals(file.Key, MovementClassesFile, StringComparison.Ordinal))
                {
                    movementJson = value;
                }
                else
                {
                    throw new DataFormatException(
                        file.Key + ": not a known rules file in ruleset v0."
                    );
                }
            }
            if (unitTypesJson == null)
            {
                throw new DataFormatException(UnitTypesFile + " is missing.");
            }
            if (movementJson == null)
            {
                throw new DataFormatException(MovementClassesFile + " is missing.");
            }

            bool placeholders = false;
            var unitTypes = ReadUnitTypes(unitTypesJson, ref placeholders);
            var movement = ReadMovementClasses(movementJson, ref placeholders);
            return new Ruleset(unitTypes, movement, RulesetHash.Of(parsed), placeholders);
        }

        private static MovementClassRules[] ReadMovementClasses(
            JsonValue root,
            ref bool placeholders
        )
        {
            root.OnlyKeys("$comment", "version", "status", "classes");
            if (root.Int("version") != MovementClassesVersion)
            {
                throw new DataFormatException(root.Path + ": version must be 0.");
            }
            placeholders |= ReadStatus(root);
            var classes = new MovementClassRules?[4];
            foreach (var item in root.Array("classes"))
            {
                item.OnlyKeys("id", "baseCost", "slopeCost", "maxSlopePermille");
                var movementClass = ParseMovementClass(item["id"]);
                if (classes[(int)movementClass] != null)
                {
                    throw new DataFormatException(item.Path + ": the class is listed twice.");
                }
                classes[(int)movementClass] = new MovementClassRules(
                    movementClass,
                    item.Int("baseCost", 1, MaxCost),
                    item.Int("slopeCost", 0, MaxCost),
                    item.Int("maxSlopePermille", 0, 100_000)
                );
            }
            var result = new MovementClassRules[classes.Length];
            for (int i = 0; i < classes.Length; i++)
            {
                result[i] =
                    classes[i]
                    ?? throw new DataFormatException(
                        root.Path
                            + ": every movement class needs an entry (foot, horse, wheeled, water)."
                    );
            }
            return result;
        }

        private static List<UnitTypeRules> ReadUnitTypes(JsonValue root, ref bool placeholders)
        {
            root.OnlyKeys("$comment", "version", "status", "unitTypes");
            if (root.Int("version") != UnitTypesVersion)
            {
                throw new DataFormatException(root.Path + ": version must be 0.");
            }
            placeholders |= ReadStatus(root);
            var list = new List<UnitTypeRules>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.Array("unitTypes"))
            {
                item.OnlyKeys("id", "movementClass", "health", "morale", "speedMmPerS", "radiusMm");
                string id = DataIds.Require(item["id"]);
                if (!seen.Add(id))
                {
                    throw new DataFormatException(item.Path + ": duplicate id \"" + id + "\".");
                }
                list.Add(
                    new UnitTypeRules(
                        list.Count,
                        id,
                        ParseMovementClass(item["movementClass"]),
                        item.Int("health", 1, 1_000_000),
                        item.Int("morale", 0, 1_000_000),
                        item.Int("speedMmPerS", 1, MaxSpeedMmPerSecond),
                        item.Int("radiusMm", MinRadiusMm, MaxRadiusMm)
                    )
                );
            }
            if (list.Count == 0)
            {
                throw new DataFormatException(root.Path + ": unitTypes is empty.");
            }
            return list;
        }

        private static bool ReadStatus(JsonValue root)
        {
            if (!root.Has("status"))
            {
                return false;
            }
            string status = root.Str("status");
            if (status == "placeholder")
            {
                return true;
            }
            if (status == "settled")
            {
                return false;
            }
            throw new DataFormatException(
                root.Path + ": status must be \"placeholder\" or \"settled\"."
            );
        }

        private static MovementClass ParseMovementClass(JsonValue value)
        {
            switch (value.AsString)
            {
                case "foot":
                    return MovementClass.Foot;
                case "horse":
                    return MovementClass.Horse;
                case "wheeled":
                    return MovementClass.Wheeled;
                case "water":
                    return MovementClass.Water;
                default:
                    throw new DataFormatException(
                        value.Path + ": unknown movement class \"" + value.AsString + "\"."
                    );
            }
        }
    }
}
