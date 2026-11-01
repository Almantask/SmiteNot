using System;
using System.Collections.Generic;
using System.Text;
using GodGame.Sim.Math;
using GodGame.Sim.Rules.Json;

namespace GodGame.Sim.Rules
{
    /// <summary>
    /// Fingerprint of a ruleset, stamped on replays and checked at PvP lock-in (plan, section 3.8). It hashes each
    /// file's name and canonical form (<see cref="JsonCanonical"/>), in ordinal file-name order, so whitespace, key
    /// order and the order files were read in don't change it, while any change of meaning does.
    /// </summary>
    public static class RulesetHash
    {
        public static ulong Of(IReadOnlyList<KeyValuePair<string, JsonValue>> files)
        {
            if (files == null)
            {
                throw new ArgumentNullException(nameof(files));
            }
            var sorted = new List<KeyValuePair<string, JsonValue>>(files);
            sorted.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            for (int i = 1; i < sorted.Count; i++)
            {
                if (string.Equals(sorted[i - 1].Key, sorted[i].Key, StringComparison.Ordinal))
                {
                    throw new DataFormatException(
                        "Rules file \"" + sorted[i].Key + "\" is listed twice."
                    );
                }
            }
            var hash = new SimHash();
            hash.AppendInt32(sorted.Count);
            foreach (var file in sorted)
            {
                byte[] name = Encoding.UTF8.GetBytes(file.Key);
                byte[] body = JsonCanonical.ToUtf8(file.Value);
                hash.AppendInt32(name.Length);
                hash.Append(name);
                hash.AppendInt32(body.Length);
                hash.Append(body);
            }
            return hash.Digest();
        }
    }
}
