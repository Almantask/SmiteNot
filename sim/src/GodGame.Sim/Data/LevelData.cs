using System;
using System.Collections.Generic;
using GodGame.Sim.Rules;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim
{
    /// <summary>Campaign battle win condition variant (spec S2 section 7.5; outcomes.json draft).</summary>
    public enum LevelOutcomeVariant
    {
        Survival = 0,
        Objective = 1,
    }

    /// <summary>Integer specification of how a battle or campaign level is won or lost.</summary>
    public sealed class LevelOutcomeSpec
    {
        public const int DefaultSunsetTick = 3_600;
        public const int MaxSunsetTick = 18_000;
        public const int DefaultObjectiveMinSoldiers = 50;
        public const int DefaultObjectiveHoldTicks = 600;
        public const int MaxObjectivePoints = 8;
        public const int DefaultArmyRoutPermille = 700;

        public LevelOutcomeSpec(
            LevelOutcomeVariant variant,
            int sunsetTick,
            int armyRoutPermille,
            int[]? objectivePolygonMm = null,
            int objectiveMinSoldiers = DefaultObjectiveMinSoldiers,
            int objectiveHoldTicks = DefaultObjectiveHoldTicks
        )
        {
            Variant = variant;
            SunsetTick = sunsetTick;
            ArmyRoutPermille = armyRoutPermille;
            ObjectivePolygonMm = objectivePolygonMm ?? Array.Empty<int>();
            ObjectiveMinSoldiers = objectiveMinSoldiers;
            ObjectiveHoldTicks = objectiveHoldTicks;
        }

        public LevelOutcomeVariant Variant { get; }

        public int SunsetTick { get; }

        public int ArmyRoutPermille { get; }

        /// <summary>Alternating [x0, y0, x1, y1, ...] in mm; empty unless <see cref="Variant"/> is <see cref="LevelOutcomeVariant.Objective"/>.</summary>
        public int[] ObjectivePolygonMm { get; }

        public int ObjectivePointCount => ObjectivePolygonMm.Length / 2;

        public int ObjectiveMinSoldiers { get; }

        public int ObjectiveHoldTicks { get; }

        public static LevelOutcomeSpec DefaultSurvival(int sunsetTick = DefaultSunsetTick) =>
            new LevelOutcomeSpec(LevelOutcomeVariant.Survival, sunsetTick, DefaultArmyRoutPermille);
    }

    /// <summary>One power slot in a level's hand and how many charges the player brings.</summary>
    public sealed class LevelPowerEntry
    {
        public LevelPowerEntry(string powerId, int charges, bool pending = false, string note = "")
        {
            PowerId = powerId;
            Charges = charges;
            Pending = pending;
            Note = note;
        }

        public string PowerId { get; }
        public int Charges { get; }
        public bool Pending { get; }
        public string Note { get; }
    }

    /// <summary>One optional side quest on a campaign level.</summary>
    public sealed class LevelSideQuest
    {
        public LevelSideQuest(string id, string description)
        {
            Id = id;
            Description = description;
        }

        public string Id { get; }
        public string Description { get; }
    }

    /// <summary>
    /// Level definition v0 (<c>data/levels/&lt;chapter&gt;/&lt;id&gt;.json</c>; plan section 3.8): links a map, a starting
    /// plan, the allowed terrain pieces, the hand of god powers (plus pending powers waiting on later milestones),
    /// outcome criteria (<see cref="LevelOutcomeSpec"/>), side quests and teaching flags.
    /// </summary>
    public sealed class LevelData
    {
        public const int FormatVersion = 0;

        private LevelData(
            string id,
            string chapter,
            string title,
            string mapId,
            string planFile,
            int seed,
            string[] allowedPieces,
            LevelPowerEntry[] hand,
            LevelPowerEntry[] pendingPowers,
            LevelOutcomeSpec outcome,
            LevelSideQuest[] sideQuests,
            string[] teachingFlags
        )
        {
            Id = id;
            Chapter = chapter;
            Title = title;
            MapId = mapId;
            PlanFile = planFile;
            Seed = seed;
            AllowedPieces = allowedPieces;
            Hand = hand;
            PendingPowers = pendingPowers;
            Outcome = outcome;
            SideQuests = sideQuests;
            TeachingFlags = teachingFlags;
        }

        public string Id { get; }
        public string Chapter { get; }
        public string Title { get; }
        public string MapId { get; }
        public string PlanFile { get; }
        public int Seed { get; }
        public IReadOnlyList<string> AllowedPieces { get; }
        public IReadOnlyList<LevelPowerEntry> Hand { get; }
        public IReadOnlyList<LevelPowerEntry> PendingPowers { get; }
        public LevelOutcomeSpec Outcome { get; }
        public IReadOnlyList<LevelSideQuest> SideQuests { get; }
        public IReadOnlyList<string> TeachingFlags { get; }

        public static LevelData Load(
            string json,
            MapData? map = null,
            Ruleset? rules = null,
            string fileName = "level.json"
        )
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }
            var root = JsonReader.Parse(json, fileName);
            root.OnlyKeys(
                "$comment",
                "version",
                "id",
                "chapter",
                "title",
                "map",
                "plan",
                "seed",
                "allowedPieces",
                "hand",
                "pendingPowers",
                "outcome",
                "sideQuests",
                "teachingFlags"
            );
            if (root.Int("version", 0, FormatVersion) != FormatVersion)
            {
                throw new DataFormatException("level.json: version must be 0.");
            }
            string id = DataIds.Require(root["id"]);
            string chapter = DataIds.Require(root["chapter"]);
            string title = root.Has("title") ? root.Str("title") : id;
            string mapId = DataIds.Require(root["map"]);
            if (map != null && !string.Equals(mapId, map.Id, StringComparison.Ordinal))
            {
                throw new DataFormatException(
                    "level.json: built for map \"" + mapId + "\", not \"" + map.Id + "\"."
                );
            }
            string planFile = root.Str("plan");
            if (string.IsNullOrWhiteSpace(planFile))
            {
                throw new DataFormatException("level.json: plan file cannot be empty.");
            }
            int seed = root.Has("seed") ? root.Int("seed", 0, int.MaxValue) : 1;

            var allowedPieces = ReadPieceIds(root, "allowedPieces", rules);
            var hand = ReadPowerList(root, "hand", rules, allowMissingPower: false);
            var pending = root.Has("pendingPowers")
                ? ReadPowerList(root, "pendingPowers", rules, allowMissingPower: true)
                : Array.Empty<LevelPowerEntry>();
            var outcome = ReadOutcome(root["outcome"], map);
            var quests = ReadSideQuests(root);
            var flags = ReadFlags(root);

            return new LevelData(
                id,
                chapter,
                title,
                mapId,
                planFile,
                seed,
                allowedPieces,
                hand,
                pending,
                outcome,
                quests,
                flags
            );
        }

        private static string[] ReadPieceIds(JsonValue root, string key, Ruleset? rules)
        {
            if (!root.Has(key))
            {
                return Array.Empty<string>();
            }
            var items = root.Array(key);
            var list = new string[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                string pieceId = DataIds.Require(items[i]);
                if (rules != null && rules.Terrain.Piece(pieceId) == null)
                {
                    throw new DataFormatException(
                        items[i].Path + ": unknown terrain piece \"" + pieceId + "\"."
                    );
                }
                list[i] = pieceId;
            }
            return list;
        }

        private static LevelPowerEntry[] ReadPowerList(
            JsonValue root,
            string key,
            Ruleset? rules,
            bool allowMissingPower
        )
        {
            var items = root.Array(key);
            var list = new LevelPowerEntry[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                var entry = items[i];
                entry.OnlyKeys("power", "charges", "note");
                string powerId = DataIds.Require(entry["power"]);
                if (!allowMissingPower && rules != null && rules.Power(powerId) == null)
                {
                    throw new DataFormatException(
                        entry.Path + ": unknown power \"" + powerId + "\"."
                    );
                }
                int charges = entry.Int("charges", 1, 99);
                string note = entry.Has("note") ? entry.Str("note") : string.Empty;
                list[i] = new LevelPowerEntry(powerId, charges, allowMissingPower, note);
            }
            return list;
        }

        private static LevelOutcomeSpec ReadOutcome(JsonValue obj, MapData? map)
        {
            obj.OnlyKeys(
                "$comment",
                "variant",
                "sunsetTick",
                "armyRoutPermille",
                "objectivePolygonMm",
                "objectiveMinSoldiers",
                "objectiveHoldTicks"
            );
            string variantName = obj.Str("variant");
            LevelOutcomeVariant variant;
            if (string.Equals(variantName, "survival", StringComparison.Ordinal))
            {
                variant = LevelOutcomeVariant.Survival;
            }
            else if (string.Equals(variantName, "objective", StringComparison.Ordinal))
            {
                variant = LevelOutcomeVariant.Objective;
            }
            else
            {
                throw new DataFormatException(
                    obj.Path + ".variant: must be \"survival\" or \"objective\"."
                );
            }
            int sunset = obj.Has("sunsetTick")
                ? obj.Int("sunsetTick", 1, LevelOutcomeSpec.MaxSunsetTick)
                : LevelOutcomeSpec.DefaultSunsetTick;
            int routPermille = obj.Has("armyRoutPermille")
                ? obj.Int("armyRoutPermille", 1, 1_000)
                : LevelOutcomeSpec.DefaultArmyRoutPermille;
            int minSoldiers = obj.Has("objectiveMinSoldiers")
                ? obj.Int("objectiveMinSoldiers", 1, BattlePlan.MaxSoldiers)
                : LevelOutcomeSpec.DefaultObjectiveMinSoldiers;
            int holdTicks = obj.Has("objectiveHoldTicks")
                ? obj.Int("objectiveHoldTicks", 1, LevelOutcomeSpec.MaxSunsetTick)
                : LevelOutcomeSpec.DefaultObjectiveHoldTicks;
            int[]? polygon = null;
            if (variant == LevelOutcomeVariant.Objective)
            {
                var pts = obj.Array("objectivePolygonMm");
                if (pts.Count < 3 || pts.Count > LevelOutcomeSpec.MaxObjectivePoints)
                {
                    throw new DataFormatException(
                        obj.Path
                            + ".objectivePolygonMm: objective needs 3 to "
                            + LevelOutcomeSpec.MaxObjectivePoints
                            + " points."
                    );
                }
                polygon = new int[pts.Count * 2];
                for (int i = 0; i < pts.Count; i++)
                {
                    var pair = pts[i].Items;
                    if (pair.Count != 2)
                    {
                        throw new DataFormatException(pts[i].Path + ": point must be [xMm, yMm].");
                    }
                    long x = pair[0].AsInteger;
                    long y = pair[1].AsInteger;
                    if (map != null && (x < 0 || y < 0 || x >= map.WidthMm || y >= map.DepthMm))
                    {
                        throw new DataFormatException(pts[i].Path + ": point is off the map.");
                    }
                    polygon[2 * i] = (int)x;
                    polygon[2 * i + 1] = (int)y;
                }
            }
            return new LevelOutcomeSpec(
                variant,
                sunset,
                routPermille,
                polygon,
                minSoldiers,
                holdTicks
            );
        }

        private static LevelSideQuest[] ReadSideQuests(JsonValue root)
        {
            if (!root.Has("sideQuests"))
            {
                return Array.Empty<LevelSideQuest>();
            }
            var items = root.Array("sideQuests");
            var list = new LevelSideQuest[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                item.OnlyKeys("id", "description");
                list[i] = new LevelSideQuest(DataIds.Require(item["id"]), item.Str("description"));
            }
            return list;
        }

        private static string[] ReadFlags(JsonValue root)
        {
            if (!root.Has("teachingFlags"))
            {
                return Array.Empty<string>();
            }
            var items = root.Array("teachingFlags");
            var list = new string[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                list[i] = items[i].AsString;
            }
            return list;
        }
    }
}
