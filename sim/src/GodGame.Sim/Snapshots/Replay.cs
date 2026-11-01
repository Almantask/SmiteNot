using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using GodGame.Sim.Rules;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim
{
    /// <summary>
    /// A replay file, <c>.godreplay</c> v0 (plan, section 3.8): the simulation version, the ruleset hash, the map id and
    /// hash, the seed and the plan, plus how long to run and, optionally, the per-second state hashes it produced.
    /// Battles take no input, so this is all a replay needs. 64-bit values are written as 16-digit hex strings, since
    /// JSON integers in rules data are signed 64-bit.
    /// </summary>
    public sealed class Replay
    {
        public const int FormatVersion = 0;
        public const string Format = "godreplay";

        public Replay(
            int simVersion,
            ulong rulesetHash,
            string mapId,
            ulong mapHash,
            ulong seed,
            string planJson,
            int seconds,
            IReadOnlyList<ulong> perSecondHashes
        )
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds));
            }
            SimVersion = simVersion;
            RulesetHash = rulesetHash;
            MapId = mapId ?? throw new ArgumentNullException(nameof(mapId));
            MapHash = mapHash;
            Seed = seed;
            PlanJson = planJson ?? throw new ArgumentNullException(nameof(planJson));
            Seconds = seconds;
            PerSecondHashes =
                perSecondHashes ?? throw new ArgumentNullException(nameof(perSecondHashes));
        }

        public int SimVersion { get; }

        public ulong RulesetHash { get; }

        public string MapId { get; }

        public ulong MapHash { get; }

        public ulong Seed { get; }

        /// <summary>The plan's canonical JSON.</summary>
        public string PlanJson { get; }

        public int Seconds { get; }

        /// <summary>Empty when the replay was saved without results.</summary>
        public IReadOnlyList<ulong> PerSecondHashes { get; }

        /// <summary>
        /// Rebuilds the battle, refusing anything that would not reproduce it: another SimVersion, other rules, another
        /// map, or a plan that no longer loads.
        /// </summary>
        public Battle ToBattle(Ruleset rules, MapData map)
        {
            if (SimVersion != GodGame.Sim.SimVersion.Current)
            {
                throw new DataFormatException(
                    "The replay was recorded with SimVersion "
                        + SimVersion.ToString(CultureInfo.InvariantCulture)
                        + "; this build is "
                        + GodGame.Sim.SimVersion.Current.ToString(CultureInfo.InvariantCulture)
                        + "."
                );
            }
            if (rules.Hash != RulesetHash)
            {
                throw new DataFormatException(
                    "The replay was recorded with other rules (ruleset hash differs)."
                );
            }
            if (!string.Equals(map.Id, MapId, StringComparison.Ordinal) || map.Hash != MapHash)
            {
                throw new DataFormatException(
                    "The replay was recorded on another map, or the map has changed."
                );
            }
            return new Battle(rules, map, BattlePlan.Load(PlanJson, map, rules), Seed);
        }

        public string ToJson()
        {
            var b = new StringBuilder();
            b.Append("{\n");
            b.Append("  \"format\": \"").Append(Format).Append("\",\n");
            b.Append("  \"version\": ")
                .Append(FormatVersion.ToString(CultureInfo.InvariantCulture))
                .Append(",\n");
            b.Append("  \"simVersion\": ")
                .Append(SimVersion.ToString(CultureInfo.InvariantCulture))
                .Append(",\n");
            b.Append("  \"rulesetHash\": \"").Append(Hex(RulesetHash)).Append("\",\n");
            b.Append("  \"mapId\": ");
            AppendString(b, MapId);
            b.Append(",\n");
            b.Append("  \"mapHash\": \"").Append(Hex(MapHash)).Append("\",\n");
            b.Append("  \"seed\": \"").Append(Hex(Seed)).Append("\",\n");
            b.Append("  \"seconds\": ")
                .Append(Seconds.ToString(CultureInfo.InvariantCulture))
                .Append(",\n");
            b.Append("  \"perSecondHashes\": [");
            for (int i = 0; i < PerSecondHashes.Count; i++)
            {
                b.Append(i == 0 ? "\n    \"" : ",\n    \"")
                    .Append(Hex(PerSecondHashes[i]))
                    .Append('"');
            }
            b.Append(PerSecondHashes.Count == 0 ? "],\n" : "\n  ],\n");
            b.Append("  \"plan\": ").Append(PlanJson).Append('\n');
            b.Append("}\n");
            return b.ToString();
        }

        public static Replay Parse(string json)
        {
            var root = JsonReader.Parse(json, "replay");
            root.OnlyKeys(
                "format",
                "version",
                "simVersion",
                "rulesetHash",
                "mapId",
                "mapHash",
                "seed",
                "seconds",
                "perSecondHashes",
                "plan"
            );
            if (root.Str("format") != Format || root.Int("version") != FormatVersion)
            {
                throw new DataFormatException("replay: not a godreplay v0 file.");
            }
            var hashes = new List<ulong>();
            foreach (var item in root.Array("perSecondHashes"))
            {
                hashes.Add(ParseHex(item));
            }
            int seconds = root.Int("seconds", 0, 24 * 60 * 60);
            if (hashes.Count != 0 && hashes.Count != seconds)
            {
                throw new DataFormatException(
                    "replay: perSecondHashes must be empty or one per second."
                );
            }
            return new Replay(
                root.Int("simVersion", 1),
                ParseHex(root["rulesetHash"]),
                root.Str("mapId"),
                ParseHex(root["mapHash"]),
                ParseHex(root["seed"]),
                JsonCanonical.ToText(root["plan"]),
                seconds,
                hashes
            );
        }

        public static string Hex(ulong value) =>
            value.ToString("X16", CultureInfo.InvariantCulture);

        private static ulong ParseHex(JsonValue value)
        {
            string text = value.AsString;
            if (text.Length != 16)
            {
                throw new DataFormatException(value.Path + ": expected 16 hex digits.");
            }
            ulong result = 0;
            foreach (char c in text)
            {
                int digit =
                    c >= '0' && c <= '9' ? c - '0'
                    : c >= 'A' && c <= 'F' ? c - 'A' + 10
                    : c >= 'a' && c <= 'f' ? c - 'a' + 10
                    : -1;
                if (digit < 0)
                {
                    throw new DataFormatException(value.Path + ": expected 16 hex digits.");
                }
                result = (result << 4) | (uint)digit;
            }
            return result;
        }

        private static void AppendString(StringBuilder b, string text)
        {
            b.Append('"');
            foreach (char c in text)
            {
                if (c == '"' || c == '\\')
                {
                    b.Append('\\').Append(c);
                }
                else if (c < 0x20)
                {
                    b.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                }
                else
                {
                    b.Append(c);
                }
            }
            b.Append('"');
        }
    }
}
