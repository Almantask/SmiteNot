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
    /// Plan format v0 (plan, section 3.8): the three layers (army, terrain, casts) with stable ids. v0 reads the army
    /// layer; terrain pieces (M3, M6) and casts (M3) must be empty lists until their milestones. Regiments are kept in
    /// ordinal id order, whatever order the file lists them in, so deployment never depends on file order.
    /// </summary>
    public sealed class BattlePlan
    {
        public const int FormatVersion = 0;

        /// <summary>Up to 10,000 units (brief, pillar 3).</summary>
        public const int MaxSoldiers = 10_000;

        private BattlePlan(string mapId, PlannedRegiment[] army, ulong hash, string canonicalJson)
        {
            CanonicalJson = canonicalJson;
            MapId = mapId;
            Army = army;
            Hash = hash;
        }

        public string MapId { get; }

        /// <summary>The plan as canonical JSON (sorted keys, no whitespace): what replays embed.</summary>
        public string CanonicalJson { get; }

        /// <summary>In ordinal id order.</summary>
        public IReadOnlyList<PlannedRegiment> Army { get; }

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
            if (root.Array("casts").Count != 0)
            {
                throw new DataFormatException(
                    "plan.json: casts arrive in M3; plan v0 needs an empty list."
                );
            }

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
                string unitType = rules.UnitType(item.Str("unitType")).Id;
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
                        item.Int("soldiers", 1, 10_000),
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
            // Keys in ordinal order: army, casts, map, terrain, version.
            text.Append("],\"casts\":").Append(JsonCanonical.ToText(root["casts"]));
            text.Append(",\"map\":").Append(JsonCanonical.ToText(root["map"]));
            text.Append(",\"terrain\":").Append(JsonCanonical.ToText(root["terrain"]));
            text.Append(",\"version\":").Append(JsonCanonical.ToText(root["version"])).Append('}');
            string canonical = text.ToString();
            byte[] bytes = System.Text.Encoding.ASCII.GetBytes(canonical);

            var hash = new SimHash();
            hash.AppendInt32(bytes.Length);
            hash.Append(bytes);
            return new BattlePlan(mapId, army.ToArray(), hash.Digest(), canonical);
        }
    }
}
