using System;
using System.Collections.Generic;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim
{
    /// <summary>One regiment in the army layer: where it deploys, which way it faces, what it is.</summary>
    public sealed class PlannedRegiment
    {
        public PlannedRegiment(
            string id,
            Side side,
            string unitType,
            int soldiers,
            int xMm,
            int yMm,
            int facingCentiDegrees
        )
        {
            Id = id;
            Side = side;
            UnitType = unitType;
            Soldiers = soldiers;
            XMm = xMm;
            YMm = yMm;
            FacingCentiDegrees = facingCentiDegrees;
        }

        /// <summary>Stable across edits, so a moved regiment is still the same regiment in the preview and replays.</summary>
        public string Id { get; }

        public Side Side { get; }

        public string UnitType { get; }

        public int Soldiers { get; }

        /// <summary>The regiment's centre.</summary>
        public int XMm { get; }

        public int YMm { get; }

        /// <summary>0 faces east (+X), counter-clockwise.</summary>
        public int FacingCentiDegrees { get; }
    }

    /// <summary>
    /// One timed god power in the casts layer (spec S1, section 8): which power, the tick it is cast (its range check
    /// and the start of its wind-up), and the circle it covers.
    /// </summary>
    public sealed class PlannedCast
    {
        public PlannedCast(string id, PowerRules power, int tick, int xMm, int yMm, int radiusMm)
        {
            Id = id;
            Power = power;
            Tick = tick;
            XMm = xMm;
            YMm = yMm;
            RadiusMm = radiusMm;
        }

        /// <summary>Stable across edits.</summary>
        public string Id { get; }

        public PowerRules Power { get; }

        /// <summary>The queued tick: the range check runs here and the wind-up starts (S1 D1).</summary>
        public int Tick { get; }

        /// <summary>The circle's centre.</summary>
        public int XMm { get; }

        public int YMm { get; }

        public int RadiusMm { get; }
    }

    /// <summary>
    /// Plan format v0 (plan, section 3.8): the three layers (army, terrain, casts) with stable ids. v0 reads the army
    /// and casts layers; terrain pieces (M3 item 8) must be an empty list until then. Regiments and casts are kept in
    /// ordinal id order, whatever order the file lists them in, so nothing depends on file order. A cast that can't be
    /// placed (an unknown power, a radius out of bounds, a circle not wholly on the map, a tick before 0 or after the
    /// battle cap) is refused here, while planning: it is never a fizzle (S1, section 8).
    /// </summary>
    public sealed class BattlePlan
    {
        public const int FormatVersion = 0;

        /// <summary>Up to 10,000 units (brief, pillar 3).</summary>
        public const int MaxSoldiers = 10_000;

        private BattlePlan(
            string mapId,
            PlannedRegiment[] army,
            PlannedCast[] casts,
            ulong hash,
            string canonicalJson
        )
        {
            CanonicalJson = canonicalJson;
            MapId = mapId;
            Army = army;
            Casts = casts;
            Hash = hash;
        }

        public string MapId { get; }

        /// <summary>The plan as canonical JSON (sorted keys, no whitespace): what replays embed.</summary>
        public string CanonicalJson { get; }

        /// <summary>In ordinal id order.</summary>
        public IReadOnlyList<PlannedRegiment> Army { get; }

        /// <summary>In ordinal id order; <see cref="World.Casts"/> keeps their state in the same order.</summary>
        public IReadOnlyList<PlannedCast> Casts { get; }

        /// <summary>Over the canonical plan; replays record it.</summary>
        public ulong Hash { get; }

        public static BattlePlan Load(string planJson, MapData map, Ruleset rules)
        {
            var root = JsonReader.Parse(planJson, "plan.json");
            root.OnlyKeys("$comment", "version", "map", "army", "terrain", "casts");
            if (root.Int("version") != FormatVersion)
            {
                throw new DataFormatException("plan.json: version must be 0.");
            }
            string mapId = root.Str("map");
            if (!string.Equals(mapId, map.Id, StringComparison.Ordinal))
            {
                throw new DataFormatException(
                    "plan.json: made for map \"" + mapId + "\", not \"" + map.Id + "\"."
                );
            }
            if (root.Array("terrain").Count != 0)
            {
                throw new DataFormatException(
                    "plan.json: terrain pieces arrive in M3; plan v0 needs an empty list."
                );
            }
            var casts = ReadCasts(root, map, rules);

            var army = new List<PlannedRegiment>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.Array("army"))
            {
                item.OnlyKeys(
                    "id",
                    "side",
                    "unitType",
                    "soldiers",
                    "xMm",
                    "yMm",
                    "facingCentiDegrees"
                );
                string id = DataIds.Require(item["id"]);
                if (!ids.Add(id))
                {
                    throw new DataFormatException(
                        item.Path + ": ids must be unique and not empty (\"" + id + "\")."
                    );
                }
                var side = SideNames.Parse(item["side"]);
                var type = rules.UnitType(item.Str("unitType"));
                string unitType = type.Id;
                int x = item.Int("xMm", 0, map.WidthMm - 1);
                int y = item.Int("yMm", 0, map.DepthMm - 1);
                RectMm zone = map.ZoneOf(side).Area;
                if (x < zone.MinX || x >= zone.MaxX || y < zone.MinY || y >= zone.MaxY)
                {
                    throw new DataFormatException(
                        item.Path + ": the centre is outside the " + side + " deployment zone."
                    );
                }
                army.Add(
                    new PlannedRegiment(
                        id,
                        side,
                        unitType,
                        item.Int(
                            "soldiers",
                            type.Formation.MinSoldiers,
                            type.Formation.MaxSoldiers
                        ),
                        x,
                        y,
                        item.Int("facingCentiDegrees", 0, 35_999)
                    )
                );
            }
            long soldiers = 0;
            foreach (var regiment in army)
            {
                soldiers += regiment.Soldiers;
            }
            if (soldiers > MaxSoldiers)
            {
                throw new DataFormatException(
                    "plan.json: more than 10,000 soldiers in the army layer."
                );
            }
            // Ids are unique, so the ordinal sort is total.
            army.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));

            // The canonical plan lists the army in id order too, so two files that deploy the same way hash the same.
            var sortedItems = new List<KeyValuePair<string, JsonValue>>();
            foreach (var item in root.Array("army"))
            {
                sortedItems.Add(new KeyValuePair<string, JsonValue>(item.Str("id"), item));
            }
            sortedItems.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            var text = new System.Text.StringBuilder("{\"army\":[");
            for (int i = 0; i < sortedItems.Count; i++)
            {
                text.Append(i == 0 ? "" : ",").Append(JsonCanonical.ToText(sortedItems[i].Value));
            }
            var sortedCasts = new List<KeyValuePair<string, JsonValue>>();
            foreach (var item in root.Array("casts"))
            {
                sortedCasts.Add(new KeyValuePair<string, JsonValue>(item.Str("id"), item));
            }
            sortedCasts.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            // Keys in ordinal order: army, casts, map, terrain, version.
            text.Append("],\"casts\":[");
            for (int i = 0; i < sortedCasts.Count; i++)
            {
                text.Append(i == 0 ? "" : ",").Append(JsonCanonical.ToText(sortedCasts[i].Value));
            }
            text.Append(']');
            text.Append(",\"map\":").Append(JsonCanonical.ToText(root["map"]));
            text.Append(",\"terrain\":").Append(JsonCanonical.ToText(root["terrain"]));
            text.Append(",\"version\":").Append(JsonCanonical.ToText(root["version"])).Append('}');
            string canonical = text.ToString();
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(canonical);

            var hash = new SimHash();
            hash.AppendInt32(bytes.Length);
            hash.Append(bytes);
            return new BattlePlan(mapId, army.ToArray(), casts, hash.Digest(), canonical);
        }

        private static PlannedCast[] ReadCasts(JsonValue root, MapData map, Ruleset rules)
        {
            var casts = new List<PlannedCast>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in root.Array("casts"))
            {
                item.OnlyKeys("id", "power", "tick", "xMm", "yMm", "radiusMm");
                string id = DataIds.Require(item["id"]);
                if (!ids.Add(id))
                {
                    throw new DataFormatException(
                        item.Path + ": ids must be unique and not empty (\"" + id + "\")."
                    );
                }
                string powerId = item.Str("power");
                var power =
                    rules.Power(powerId)
                    ?? throw new DataFormatException(
                        item.Path + ": no power \"" + powerId + "\" in powers.json."
                    );
                int tick = item.Int("tick", 0, rules.LatestCastTick);
                int radius = item.Int("radiusMm", power.MinRadiusMm, power.MaxRadiusMm);
                int x = item.Int("xMm", radius, map.WidthMm - radius);
                int y = item.Int("yMm", radius, map.DepthMm - radius);
                casts.Add(new PlannedCast(id, power, tick, x, y, radius));
            }
            // Ids are unique, so the ordinal sort is total.
            casts.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return casts.ToArray();
        }
    }
}
