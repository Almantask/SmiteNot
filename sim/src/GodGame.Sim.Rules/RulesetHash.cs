using System;
using GodGame.Sim.Math;

namespace GodGame.Sim.Rules
{
    /// <summary>
    /// Fingerprint of a ruleset, stamped on replays and checked at PvP lock-in.
    /// M0 placeholder: hashes the raw rules files in the order given. The M1 loader hashes the parsed,
    /// canonical form instead, so formatting changes don't change the hash.
    /// </summary>
    public static class RulesetHash
    {
        public static ulong Of(params byte[][] files)
        {
            if (files == null)
            {
                throw new ArgumentNullException(nameof(files));
            }
            var hash = new SimHash();
            hash.AppendInt32(files.Length);
            foreach (byte[] file in files)
            {
                hash.AppendInt32(file.Length);
                hash.Append(file);
            }
            return hash.Digest();
        }
    }
}
