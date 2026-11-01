using System;
using System.Collections.Generic;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim.Rules
{
    /// <summary>
    /// Builds a <see cref="Ruleset"/> from rules files. The simulation reads no files itself: the host reads
    /// <c>data/rules/*.json</c> and passes (file name, text) pairs. v0 knows <c>unit-types.json</c> only.
    /// </summary>
    public static class RulesetLoader
    {
        public const int UnitTypesVersion = 0;
        public const string UnitTypesFile = "unit-types.json";

        public static Ruleset Load(IReadOnlyList<KeyValuePair<string, string>> files)
        {
            if (files == null)
            {
                throw new ArgumentNullException(nameof(files));
            }
            var parsed = new List<KeyValuePair<string, JsonValue>>();
            JsonValue? unitTypesJson = null;
            foreach (var file in files)
            {
                var value = JsonReader.Parse(file.Value, file.Key);
                parsed.Add(new KeyValuePair<string, JsonValue>(file.Key, value));
                if (string.Equals(file.Key, UnitTypesFile, StringComparison.Ordinal))
                {
                    unitTypesJson = value;
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

            bool placeholders = false;
            var unitTypes = ReadUnitTypes(unitTypesJson, ref placeholders);
            return new Ruleset(unitTypes, RulesetHash.Of(parsed), placeholders);
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
                item.OnlyKeys("id", "movementClass", "health", "morale");
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
                        item.Int("morale", 0, 1_000_000)
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
