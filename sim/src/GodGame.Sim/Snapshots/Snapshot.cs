using System;
using System.Globalization;
using GodGame.Sim.Math;

namespace GodGame.Sim
{
    /// <summary>
    /// A versioned binary image of a simulation (plan, section 3.9). Header: magic, format version, the SimVersion that
    /// wrote it, the battle id (rules, map and plan; 0 for a simulation built by hand), the tick and the seed. Then the
    /// whole world, then an xxHash64 of everything before it. Restoring it into a simulation of the same battle
    /// continues exactly as the original would have, which keyframes, scrubbing and snapshot-restore tests rely on. The
    /// format has its own version, so an old keyframe is rejected rather than misread, and the checksum and the
    /// invariant checks reject a damaged one rather than restoring a broken world.
    /// </summary>
    public static class Snapshot
    {
        /// <summary>"GGSN", little-endian.</summary>
        public const int Magic = 0x4E534747;

        public const int FormatVersion = 2;

        internal static byte[] Write(ulong battleId, int tick, ulong seed, World world)
        {
            var writer = new ByteWriter(64 * 1024);
            writer.WriteInt32(Magic);
            writer.WriteInt32(FormatVersion);
            writer.WriteInt32(SimVersion.Current);
            writer.WriteUInt64(battleId);
            writer.WriteInt32(tick);
            writer.WriteUInt64(seed);
            world.WriteTo(writer);
            byte[] body = writer.ToArray();
            var hash = new SimHash();
            hash.Append(body);
            writer.WriteUInt64(hash.Digest());
            return writer.ToArray();
        }

        /// <summary>Reads a snapshot into <paramref name="world"/> and returns its header.</summary>
        internal static (ulong BattleId, int Tick, ulong Seed) Read(byte[] snapshot, World world)
        {
            if (snapshot.Length < 8)
            {
                throw new FormatException("Snapshot ends early.");
            }
            var hash = new SimHash();
            hash.Append(new ReadOnlySpan<byte>(snapshot, 0, snapshot.Length - 8));
            var trailer = new ByteReader(Slice(snapshot, snapshot.Length - 8, 8));
            if (trailer.ReadUInt64() != hash.Digest())
            {
                throw new FormatException(
                    "Snapshot checksum does not match: the snapshot is damaged."
                );
            }

            var reader = new ByteReader(Slice(snapshot, 0, snapshot.Length - 8));
            if (reader.ReadInt32() != Magic)
            {
                throw new FormatException("Not a simulation snapshot.");
            }
            if (reader.ReadInt32() != FormatVersion)
            {
                throw new FormatException(
                    "Snapshot format version "
                        + FormatVersion.ToString(CultureInfo.InvariantCulture)
                        + " expected."
                );
            }
            if (reader.ReadInt32() != SimVersion.Current)
            {
                throw new FormatException(
                    "The snapshot was written by another SimVersion; its future would differ."
                );
            }
            ulong battleId = reader.ReadUInt64();
            int tick = reader.ReadInt32();
            ulong seed = reader.ReadUInt64();
            if (tick < 0)
            {
                throw new FormatException("Snapshot tick is negative.");
            }
            world.ReadFrom(reader);
            if (!reader.AtEnd)
            {
                throw new FormatException("Snapshot has trailing bytes.");
            }
            world.CheckInvariants();
            return (battleId, tick, seed);
        }

        private static byte[] Slice(byte[] source, int start, int length)
        {
            var copy = new byte[length];
            Array.Copy(source, start, copy, 0, length);
            return copy;
        }
    }
}
