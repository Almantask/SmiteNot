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

        /// <summary>The slowest allowed refresh of field-driven costs: once every 10 s.</summary>
        private const int SimRefreshMaxTicks = 200;
        public const string MovementClassesFile = "movement-classes.json";
        public const int HazardsVersion = 0;
        public const string HazardsFile = "hazards.json";
        public const int RoutesVersion = 0;
        public const string RoutesFile = "routes.json";
        public const int FieldsVersion = 0;

        public const string PowersFile = "powers.json";

        public const string TerrainFile = "terrain.json";

        public const int TerrainVersion = 0;

        public const int PowersVersion = 0;

        /// <summary>Ticks per field step (the simulation's 5 Hz fields): power timings are whole field steps.</summary>
        public const int FieldStepTicks = 4;

        /// <summary>
        /// Fields a power may write: only water, whose model takes outside additions at its own step (powers write before
        /// it). Other fields keep invariants their models own (outflows match depth, mud follows wetness).
        /// </summary>
        private static readonly string[] PowerWritableFields = { "water" };

        /// <summary>No cast may be queued later than an hour in (the plan's hard battle cap is 15 minutes).</summary>
        public const int MaxCastTick = 72_000;
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
            JsonValue? powersJson = null;
            JsonValue? terrainJson = null;
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
                else if (string.Equals(file.Key, PowersFile, StringComparison.Ordinal))
                {
                    powersJson = value;
                }
                else if (string.Equals(file.Key, TerrainFile, StringComparison.Ordinal))
                {
                    terrainJson = value;
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
            if (powersJson == null)
            {
                throw new DataFormatException(PowersFile + " is missing.");
            }
            if (terrainJson == null)
            {
                throw new DataFormatException(TerrainFile + " is missing.");
            }

            bool placeholders = false;
            var unitTypes = ReadUnitTypes(unitTypesJson, ref placeholders);
            var movement = ReadMovementClasses(movementJson, ref placeholders, out int refresh);
            var hazards = ReadHazards(hazardsJson, ref placeholders);
            var routes = ReadRoutes(routesJson, ref placeholders);
            var fields = ReadFields(fieldsJson, ref placeholders);
            var powers = ReadPowers(powersJson, fields, ref placeholders, out int latestCastTick);
            var terrain = ReadTerrain(terrainJson, hazards, ref placeholders);
            return new Ruleset(
                unitTypes,
                movement,
                hazards,
                routes,
                fields,
                RulesetHash.Of(parsed),
                placeholders
            )
            {
                FieldCostRefreshTicks = refresh,
                Powers = powers,
                LatestCastTick = latestCastTick,
                Terrain = terrain,
            };
        }

        private static MovementClassRules[] ReadMovementClasses(
            JsonValue root,
            ref bool placeholders,
            out int refreshTicks
        )
        {
            root.OnlyKeys("$comment", "version", "status", "fieldCostRefreshTicks", "classes");
            if (root.Int("version") != MovementClassesVersion)
            {
                throw new DataFormatException(root.Path + ": version must be 0.");
            }
            refreshTicks = root.Int("fieldCostRefreshTicks", 1, SimRefreshMaxTicks);
            placeholders |= ReadStatus(root);
            var classes = new MovementClassRules?[4];
            foreach (var item in root.Array("classes"))
            {
                item.OnlyKeys(
                    "$comment",
                    "id",
                    "baseCost",
                    "slopeCost",
                    "maxSlopePermille",
                    "water",
                    "mudSpeed"
                );
                var movementClass = ParseMovementClass(item["id"]);
                if (classes[(int)movementClass] != null)
                {
                    throw new DataFormatException(item.Path + ": the class is listed twice.");
                }
                classes[(int)movementClass] = new MovementClassRules(
                    movementClass,
                    item.Int("baseCost", 1, MaxCost),
                    item.Int("slopeCost", 0, MaxCost),
                    item.Int("maxSlopePermille", 0, 100_000),
                    ReadWaterResponse(item["water"]),
                    ReadCurve(item["mudSpeed"], 65_535)
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

        private static WaterResponse ReadWaterResponse(JsonValue item)
        {
            item.OnlyKeys(
                "speed",
                "maxEnterDepthMm",
                "sweptDepthMm",
                "sweptDepthSpeed",
                "sweptMinDepthMm",
                "carriedPermille",
                "paddleMmPerS",
                "drowningTicks",
                "regainPermille"
            );
            return new WaterResponse(
                ReadCurve(item["speed"], 100_000),
                item.Int("maxEnterDepthMm", 0, 100_000),
                item.Int("sweptDepthMm", 1, 100_000),
                item.Int("sweptDepthSpeed", 0, 100_000_000),
                item.Int("sweptMinDepthMm", 0, 100_000)
            )
            {
                CarriedPermille = item.Int("carriedPermille", 0, 2_000),
                PaddleMmPerS = item.Int("paddleMmPerS", 0, 5_000),
                DrowningTicks = item.Int("drowningTicks", 1, 12_000),
                RegainPermille = item.Int("regainPermille", 1, 1_000),
            };
        }

        /// <summary>[[x, permille], …] with x ascending from at most <paramref name="maxX"/> and speeds 1 to 1000 ‰.</summary>
        private static SpeedCurve ReadCurve(JsonValue value, int maxX)
        {
            var points = value.Items;
            if (points.Count == 0 || points.Count > 32)
            {
                throw new DataFormatException(value.Path + ": 1 to 32 points.");
            }
            var x = new int[points.Count];
            var speed = new int[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                var pair = points[i].Items;
                if (pair.Count != 2)
                {
                    throw new DataFormatException(points[i].Path + ": a point is [x, permille].");
                }
                long px = pair[0].AsInteger;
                long ps = pair[1].AsInteger;
                if (px < 0 || px > maxX || ps < 1 || ps > 1_000 || (i > 0 && px <= x[i - 1]))
                {
                    throw new DataFormatException(
                        points[i].Path
                            + ": x must rise from 0 to "
                            + maxX
                            + ", speed 1 to 1000 permille."
                    );
                }
                x[i] = (int)px;
                speed[i] = (int)ps;
            }
            return new SpeedCurve(x, speed);
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
                case "code":
                    min = 0;
                    max = 0xFF_FFFF;
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
                "water",
                "ground"
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
                ReadWater(root["water"]),
                ReadGround(root["ground"])
            );
        }

        private static GroundRules ReadGround(JsonValue item)
        {
            item.OnlyKeys(
                "$comment",
                "unitsPerUm",
                "infiltrationUmPerS",
                "dryingPerS",
                "mudBaseStart",
                "mudCapStart",
                "mudCapNum",
                "mudCapDen",
                "churnAtFullWetness",
                "churnWeight",
                "soils"
            );
            var weights = item["churnWeight"];
            weights.OnlyKeys("foot", "horse", "wheeled", "water");
            var churnWeight = new[]
            {
                weights.Int("foot", 0, 10_000),
                weights.Int("horse", 0, 10_000),
                weights.Int("wheeled", 0, 10_000),
                weights.Int("water", 0, 10_000),
            };
            var soils = new List<SoilRules>();
            foreach (var soil in item.Array("soils"))
            {
                soil.OnlyKeys("$comment", "id", "churnPermille", "capPermille");
                string id = DataIds.Require(soil["id"]);
                if (soils.Exists(s => string.Equals(s.Id, id, StringComparison.Ordinal)))
                {
                    throw new DataFormatException(soil.Path + ": duplicate soil \"" + id + "\".");
                }
                soils.Add(
                    new SoilRules(
                        soils.Count,
                        id,
                        soil.Int("churnPermille", 0, 10_000),
                        soil.Int("capPermille", 0, 1_000)
                    )
                );
            }
            if (soils.Count == 0 || soils.Count > 255)
            {
                throw new DataFormatException(
                    item.Path + ": 1 to 255 soils, the first being the default."
                );
            }
            int capDen = item.Int("mudCapDen", 1, 1_000);
            return new GroundRules(
                item.Int("unitsPerUm", 1, 1_000),
                item.Int("infiltrationUmPerS", 0, 1_000_000),
                item.Int("dryingPerS", 0, 65_535),
                item.Int("mudBaseStart", 0, 65_535),
                item.Int("mudCapStart", 0, 65_535),
                item.Int("mudCapNum", 0, 1_000),
                capDen,
                item.Int("churnAtFullWetness", 0, 65_535),
                churnWeight,
                soils
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

        private static PowerRules[] ReadPowers(
            JsonValue root,
            FieldRules fields,
            ref bool placeholders,
            out int latestCastTick
        )
        {
            root.OnlyKeys("$comment", "version", "status", "latestCastTick", "powers");
            if (root.Int("version") != PowersVersion)
            {
                throw new DataFormatException(root.Path + ": version must be 0.");
            }
            placeholders |= ReadStatus(root);
            latestCastTick = root.Int("latestCastTick", 0, MaxCastTick);
            var list = new List<PowerRules>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.Array("powers"))
            {
                item.OnlyKeys(
                    "$comment",
                    "id",
                    "status",
                    "area",
                    "rangeRule",
                    "windUpTicks",
                    "durationTicks",
                    "writes"
                );
                string id = DataIds.Require(item["id"]);
                if (!ids.Add(id))
                {
                    throw new DataFormatException(item.Path + ": the power is listed twice.");
                }
                placeholders |= ReadStatus(item);
                var area = item["area"];
                area.OnlyKeys("$comment", "shape", "minRadiusMm", "maxRadiusMm", "defaultRadiusMm");
                if (!string.Equals(area.Str("shape"), "circle", StringComparison.Ordinal))
                {
                    throw new DataFormatException(area.Path + ": only circles in ruleset v0.");
                }
                int min = area.Int("minRadiusMm", 1_000, 1_000_000);
                int max = area.Int("maxRadiusMm", min, 1_000_000);
                int byDefault = area.Int("defaultRadiusMm", min, max);
                var range = item["rangeRule"];
                range.OnlyKeys("$comment", "minDistanceMm", "minSoldiersPerCoarseCell");
                var writes = new List<PowerWrite>();
                foreach (var write in item.Array("writes"))
                {
                    write.OnlyKeys("$comment", "field", "ratePerS");
                    string field = write.Str("field");
                    if (fields.IndexOf(field) < 0 || Array.IndexOf(PowerWritableFields, field) < 0)
                    {
                        throw new DataFormatException(
                            write.Path
                                + ": powers may write only water, and fields.json must define it (\""
                                + field
                                + "\")."
                        );
                    }
                    writes.Add(new PowerWrite(field, write.Int("ratePerS", 1, 100_000_000)));
                }
                list.Add(
                    new PowerRules(
                        id,
                        list.Count,
                        min,
                        max,
                        byDefault,
                        range.Int("minDistanceMm", 0, 1_000_000),
                        range.Int("minSoldiersPerCoarseCell", 1, 1_000),
                        WholeFieldSteps(item, "windUpTicks", 0),
                        WholeFieldSteps(item, "durationTicks", FieldStepTicks),
                        writes.AsReadOnly()
                    )
                );
            }
            return list.ToArray();
        }

        private static TerrainRules ReadTerrain(
            JsonValue root,
            HazardRules hazards,
            ref bool placeholders
        )
        {
            root.OnlyKeys("$comment", "version", "status", "bufferFromDeploymentMm", "pieces");
            if (root.Int("version") != TerrainVersion)
            {
                throw new DataFormatException(root.Path + ": version must be 0.");
            }
            placeholders |= ReadStatus(root);
            int buffer = root.Int("bufferFromDeploymentMm", 0, 1_000_000);
            var list = new List<PieceRules>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.Array("pieces"))
            {
                string id = DataIds.Require(item["id"]);
                if (!ids.Add(id))
                {
                    throw new DataFormatException(item.Path + ": the piece is listed twice.");
                }
                string kindName = item.Str("kind");
                PieceRules piece;
                switch (kindName)
                {
                    case "channel":
                        item.OnlyKeys(
                            "$comment",
                            "id",
                            "kind",
                            "minPoints",
                            "maxPoints",
                            "minLengthMm",
                            "maxLengthMm",
                            "minWidthMm",
                            "maxWidthMm",
                            "minDepthMm",
                            "maxDepthMm",
                            "sideCells",
                            "sideDepthPermille"
                        );
                        piece = Polyline(item, id, PieceKind.Channel);
                        piece.MinWidthMm = item.Int("minWidthMm", 1, 100_000);
                        piece.MaxWidthMm = item.Int("maxWidthMm", piece.MinWidthMm, 100_000);
                        // Never a cliff: a lethal drop needs the ruleset's lethal height.
                        piece.MinDepthMm = item.Int("minDepthMm", 1, hazards.LethalDropMm - 1);
                        piece.MaxDepthMm = item.Int(
                            "maxDepthMm",
                            piece.MinDepthMm,
                            hazards.LethalDropMm - 1
                        );
                        piece.SideCells = item.Int("sideCells", 0, 16);
                        piece.SideDepthPermille = item.Int("sideDepthPermille", 0, 1_000);
                        break;
                    case "ridge":
                    case "hollow":
                        bool ridge = kindName == "ridge";
                        string size = ridge ? "Height" : "Depth";
                        item.OnlyKeys(
                            "$comment",
                            "id",
                            "kind",
                            "minPoints",
                            "maxPoints",
                            "minLengthMm",
                            "maxLengthMm",
                            "min" + size + "Mm",
                            "max" + size + "Mm",
                            "topWidthMm",
                            "sideSlopePermille"
                        );
                        piece = Polyline(item, id, ridge ? PieceKind.Ridge : PieceKind.Hollow);
                        // Never a cliff: a lethal drop needs the ruleset's lethal height.
                        int min = item.Int("min" + size + "Mm", 1, hazards.LethalDropMm - 1);
                        int max = item.Int("max" + size + "Mm", min, hazards.LethalDropMm - 1);
                        if (ridge)
                        {
                            piece.MinHeightMm = min;
                            piece.MaxHeightMm = max;
                        }
                        else
                        {
                            piece.MinDepthMm = min;
                            piece.MaxDepthMm = max;
                        }
                        piece.TopWidthMm = item.Int("topWidthMm", 0, 100_000);
                        piece.SideSlopePermille = item.Int("sideSlopePermille", 1, 100_000);
                        break;
                    case "spring":
                        item.OnlyKeys(
                            "$comment",
                            "id",
                            "kind",
                            "sourceBlockCells",
                            "minDischargeLPerS",
                            "maxDischargeLPerS",
                            "defaultDischargeLPerS"
                        );
                        piece = new PieceRules(id, PieceKind.Spring);
                        piece.SourceBlockCells = item.Int("sourceBlockCells", 1, 64);
                        piece.MinDischargeLPerS = item.Int("minDischargeLPerS", 1, 1_000_000);
                        piece.MaxDischargeLPerS = item.Int(
                            "maxDischargeLPerS",
                            piece.MinDischargeLPerS,
                            1_000_000
                        );
                        piece.DefaultDischargeLPerS = item.Int(
                            "defaultDischargeLPerS",
                            piece.MinDischargeLPerS,
                            piece.MaxDischargeLPerS
                        );
                        break;
                    case "boulder":
                        item.OnlyKeys("$comment", "id", "kind", "sizeMm", "boulderHeightMm");
                        piece = new PieceRules(id, PieceKind.Boulder);
                        piece.SizeMm = item.Int("sizeMm", 1, 100_000);
                        piece.BoulderHeightMm = item.Int("boulderHeightMm", 1, hazards.LethalDropMm - 1);
                        break;
                    default:
                        throw new DataFormatException(
                            item.Path + ".kind: channel, ridge, hollow, spring or boulder."
                        );
                }
                list.Add(piece);
            }
            return new TerrainRules(buffer, list.AsReadOnly());
        }

        private static PieceRules Polyline(JsonValue item, string id, PieceKind kind)
        {
            var piece = new PieceRules(id, kind);
            piece.MinPoints = item.Int("minPoints", 2, 64);
            piece.MaxPoints = item.Int("maxPoints", piece.MinPoints, 64);
            piece.MinLengthMm = item.Int("minLengthMm", 1, 1_000_000);
            piece.MaxLengthMm = item.Int("maxLengthMm", piece.MinLengthMm, 1_000_000);
            return piece;
        }

        /// <summary>A timing in whole field steps, so the number of writes never depends on the cast tick's phase.</summary>
        private static int WholeFieldSteps(JsonValue item, string key, int min)
        {
            int ticks = item.Int(key, min, MaxCastTick);
            if (ticks % FieldStepTicks != 0)
            {
                throw new DataFormatException(
                    item.Path + "." + key + ": a multiple of " + FieldStepTicks + " ticks (one field step)."
                );
            }
            return ticks;
        }

        private static HazardRules ReadHazards(JsonValue root, ref bool placeholders)
        {
            root.OnlyKeys(
                "$comment",
                "version",
                "status",
                "lethalDropMm",
                "teeterTicks",
                "teeterPressureMm",
                "bankExitStunTicks"
            );
            if (root.Int("version") != HazardsVersion)
            {
                throw new DataFormatException(root.Path + ": version must be 0.");
            }
            placeholders |= ReadStatus(root);
            return new HazardRules(
                root.Int("lethalDropMm", 100, 1_000_000),
                root.Int("teeterTicks", 1, 1_200),
                root.Int("teeterPressureMm", 1, 10_000),
                root.Int("bankExitStunTicks", 0, 1_200)
            );
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
