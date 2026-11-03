using System;
using System.Collections.Generic;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim.Rules
{
    /// <summary>
    /// Builds a <see cref="Ruleset"/> from rules files. The simulation reads no files itself: the host reads
    /// <c>data/rules/*.json</c> and passes (file name, text) pairs. v0 knows <c>unit-types.json</c> and
    /// <c>movement-classes.json</c>, <c>hazards.json</c>, <c>routes.json</c> and <c>fields.json</c>, and needs all
    /// five.
    /// </summary>
    public static class RulesetLoader
    {
        public const int UnitTypesVersion = 0;
        public const string UnitTypesFile = "unit-types.json";
        public const int MovementClassesVersion = 0;
        public const string MovementClassesFile = "movement-classes.json";
        public const int HazardsVersion = 0;
        public const string HazardsFile = "hazards.json";
        public const int RoutesVersion = 0;
        public const string RoutesFile = "routes.json";
        public const int FieldsVersion = 0;
        public const string FieldsFile = "fields.json";

        /// <summary>Costs fit a ushort with room for the impassable marker.</summary>
        public const int MaxCost = 10_000;

        /// <summary>20 m/s: a galloping horse is about 15.</summary>
        public const int MaxSpeedMmPerSecond = 20_000;

        /// <summary>Bodies from 10 cm to 5 m across the ground; the movement system's neighbour search assumes this.</summary>
        public const int MinRadiusMm = 100;

        public const int MaxRadiusMm = 2_500;

        /// <summary>No regiment, of any type, above this; the plan's army cap (10,000) still applies on top.</summary>
        public const int MaxRegimentSoldiers = 10_000;

        /// <summary>A formation is at most 1 km wide and deep, so squared distances across it stay far inside a long.</summary>
        public const int MaxFormationMm = 1_000_000;

        /// <summary>Field values in mm stay within ±10 km, like ground heights.</summary>
        public const int MaxFieldMm = 10_000_000;

        public const int MaxFields = 16;

        public static Ruleset Load(IReadOnlyList<KeyValuePair<string, string>> files)
        {
            if (files == null)
            {
                throw new ArgumentNullException(nameof(files));
            }
            var parsed = new List<KeyValuePair<string, JsonValue>>();
            JsonValue? unitTypesJson = null;
            JsonValue? movementJson = null;
            JsonValue? hazardsJson = null;
            JsonValue? routesJson = null;
            JsonValue? fieldsJson = null;
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
                else if (string.Equals(file.Key, HazardsFile, StringComparison.Ordinal))
                {
                    hazardsJson = value;
                }
                else if (string.Equals(file.Key, RoutesFile, StringComparison.Ordinal))
                {
                    routesJson = value;
                }
                else if (string.Equals(file.Key, FieldsFile, StringComparison.Ordinal))
                {
                    fieldsJson = value;
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
            if (hazardsJson == null)
            {
                throw new DataFormatException(HazardsFile + " is missing.");
            }
            if (routesJson == null)
            {
                throw new DataFormatException(RoutesFile + " is missing.");
            }
            if (fieldsJson == null)
            {
                throw new DataFormatException(FieldsFile + " is missing.");
            }

            bool placeholders = false;
            var unitTypes = ReadUnitTypes(unitTypesJson, ref placeholders);
            var movement = ReadMovementClasses(movementJson, ref placeholders);
            var hazards = ReadHazards(hazardsJson, ref placeholders);
            var routes = ReadRoutes(routesJson, ref placeholders);
            var fields = ReadFields(fieldsJson, ref placeholders);
            return new Ruleset(
                unitTypes,
                movement,
                hazards,
                routes,
                fields,
                RulesetHash.Of(parsed),
                placeholders
            );
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
                item.OnlyKeys(
                    "id",
                    "movementClass",
                    "health",
                    "morale",
                    "speedMmPerS",
                    "radiusMm",
                    "formation"
                );
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
                        item.Int("radiusMm", MinRadiusMm, MaxRadiusMm),
                        ReadFormation(item["formation"])
                    )
                );
            }
            if (list.Count == 0)
            {
                throw new DataFormatException(root.Path + ": unitTypes is empty.");
            }
            return list;
        }

        /// <summary>Field units the data rules allow, with their value ranges.</summary>
        private static bool UnitRange(string unit, out int min, out int max)
        {
            switch (unit)
            {
                case "mm":
                    min = -MaxFieldMm;
                    max = MaxFieldMm;
                    return true;
                case "scale":
                    min = 0;
                    max = 65_535;
                    return true;
                case "centidegrees":
                    min = -10_000;
                    max = 10_000;
                    return true;
                case "cmPerS":
                    min = -10_000;
                    max = 10_000;
                    return true;
                case "um":
                case "umPerS":
                    min = -1_000_000_000;
                    max = 1_000_000_000;
                    return true;
                default:
                    min = max = 0;
                    return false;
            }
        }

        private static FieldRules ReadFields(JsonValue root, ref bool placeholders)
        {
            root.OnlyKeys(
                "$comment",
                "version",
                "status",
                "tileCells",
                "sleepAfterSteps",
                "fields",
                "interactions",
                "water"
            );
            if (root.Int("version") != FieldsVersion)
            {
                throw new DataFormatException(root.Path + ": version must be 0.");
            }
            placeholders |= ReadStatus(root);
            var fields = new List<FieldDefinition>();
            foreach (var item in root.Array("fields"))
            {
                item.OnlyKeys("$comment", "id", "unit", "min", "max", "initial");
                string id = DataIds.Require(item["id"]);
                foreach (var existing in fields)
                {
                    if (string.Equals(existing.Id, id, StringComparison.Ordinal))
                    {
                        throw new DataFormatException(
                            item.Path + ": duplicate field \"" + id + "\"."
                        );
                    }
                }
                string unit = item.Str("unit");
                if (!UnitRange(unit, out int lowest, out int highest))
                {
                    throw new DataFormatException(
                        item.Path + ": unit must be mm, um, umPerS, scale, centidegrees or cmPerS."
                    );
                }
                int min = item.Int("min", lowest, highest);
                int max = item.Int("max", lowest, highest);
                if (max < min)
                {
                    throw new DataFormatException(item.Path + ": max is below min.");
                }
                fields.Add(
                    new FieldDefinition(
                        fields.Count,
                        id,
                        unit,
                        min,
                        max,
                        item.Int("initial", min, max)
                    )
                );
            }
            if (fields.Count > MaxFields)
            {
                throw new DataFormatException(root.Path + ": at most 16 fields.");
            }
            var rules = new FieldRules(
                root.Int("tileCells", 4, 64),
                root.Int("sleepAfterSteps", 1, 100),
                fields,
                new List<FieldInteraction>(),
                null
            );
            var interactions = new List<FieldInteraction>();
            foreach (var item in root.Array("interactions"))
            {
                item.OnlyKeys("$comment", "id", "source", "target", "rate", "threshold");
                string id = DataIds.Require(item["id"]);
                if (interactions.Exists(r => string.Equals(r.Id, id, StringComparison.Ordinal)))
                {
                    throw new DataFormatException(
                        item.Path + ": duplicate interaction \"" + id + "\"."
                    );
                }
                int source = rules.IndexOf(item.Str("source"));
                int target = rules.IndexOf(item.Str("target"));
                if (source < 0 || target < 0)
                {
                    throw new DataFormatException(
                        item.Path + ": source and target must be fields listed above."
                    );
                }
                interactions.Add(
                    new FieldInteraction(
                        id,
                        source,
                        target,
                        item.Int("rate", -1_000_000, 1_000_000),
                        item.Has("threshold") ? item.Int("threshold", -MaxFieldMm, MaxFieldMm) : 0
                    )
                );
            }
            return new FieldRules(
                rules.TileCells,
                rules.SleepAfterSteps,
                fields,
                interactions,
                ReadWater(root["water"])
            );
        }

        private static WaterRules ReadWater(JsonValue item)
        {
            item.OnlyKeys(
                "$comment",
                "substepsPerFieldStep",
                "pipeGainPermille",
                "bedFrictionPpm",
                "fluxDeadbandUmPerS",
                "headDeadbandUm",
                "dryDepthUm"
            );
            return new WaterRules(
                item.Int("substepsPerFieldStep", 1, 16),
                item.Int("pipeGainPermille", 1, 10_000),
                item.Int("bedFrictionPpm", 0, 1_000_000),
                item.Int("fluxDeadbandUmPerS", 0, 1_000_000),
                item.Int("headDeadbandUm", 0, 1_000_000),
                item.Int("dryDepthUm", 0, 1_000_000)
            );
        }

        private static RouteRules ReadRoutes(JsonValue root, ref bool placeholders)
        {
            root.OnlyKeys(
                "$comment",
                "version",
                "status",
                "capPercent",
                "coarseCells",
                "clearanceCells"
            );
            if (root.Int("version") != RoutesVersion)
            {
                throw new DataFormatException(root.Path + ": version must be 0.");
            }
            placeholders |= ReadStatus(root);
            var clearance = root["clearanceCells"];
            clearance.OnlyKeys("foot", "horse", "wheeled");
            return new RouteRules(
                root.Int("capPercent", 100, 1_000),
                root.Int("coarseCells", 1, 16),
                clearance.Int("foot", 1, 8),
                clearance.Int("horse", 1, 8),
                clearance.Int("wheeled", 1, 8)
            );
        }

        private static HazardRules ReadHazards(JsonValue root, ref bool placeholders)
        {
            root.OnlyKeys("$comment", "version", "status", "lethalDropMm");
            if (root.Int("version") != HazardsVersion)
            {
                throw new DataFormatException(root.Path + ": version must be 0.");
            }
            placeholders |= ReadStatus(root);
            return new HazardRules(root.Int("lethalDropMm", 100, 1_000_000));
        }

        private static FormationRules ReadFormation(JsonValue item)
        {
            item.OnlyKeys("files", "fileSpacingMm", "rankSpacingMm", "minSoldiers", "maxSoldiers");
            int min = item.Int("minSoldiers", 1, MaxRegimentSoldiers);
            int max = item.Int("maxSoldiers", 1, MaxRegimentSoldiers);
            if (max < min)
            {
                throw new DataFormatException(item.Path + ": maxSoldiers is below minSoldiers.");
            }
            int files = item.Int("files", 1, 1_000);
            int fileSpacing = item.Int("fileSpacingMm", MinRadiusMm, 20_000);
            int rankSpacing = item.Int("rankSpacingMm", MinRadiusMm, 20_000);
            // The largest regiment's block must fit in a kilometre each way: movement squares distances across it.
            long depth = (long)((max - 1 + files - 1) / files) * rankSpacing;
            if ((long)files * fileSpacing > MaxFormationMm || depth > MaxFormationMm)
            {
                throw new DataFormatException(
                    item.Path + ": the largest regiment's formation is wider or deeper than 1 km."
                );
            }
            return new FormationRules(files, fileSpacing, rankSpacing, min, max);
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
