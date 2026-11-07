using System;
using GodGame.Sim.Math;

namespace GodGame.Sim
{
    /// <summary>What has become of a planned cast.</summary>
    public enum CastStatus : byte
    {
        /// <summary>Its tick hasn't come yet.</summary>
        Pending = 0,

        /// <summary>It passed the range rule at its tick: winding up, writing, or done (by the clock).</summary>
        Fired = 1,

        /// <summary>It failed the range rule at its tick and writes nothing.</summary>
        Fizzled = 2,
    }

    /// <summary>Why a cast fizzled (spec S1, section 8); both can apply.</summary>
    [Flags]
    public enum FizzleReasons : byte
    {
        None = 0,
        TooCloseToAttacker = 1 << 0,
        TooCloseToDefender = 1 << 1,
    }

    /// <summary>
    /// The world's record of the plan's casts, in the plan's cast order (<see cref="BattlePlan.Casts"/>): each one's
    /// status, why it fizzled, and the nearest outline of each army when it was checked (regiment slot and distance from
    /// the area's edge, negative when inside), which the preview's red markers read. Also the remainder each write
    /// carries between field steps, so a rate that doesn't divide evenly adds up exactly. Hashed and snapshotted.
    /// </summary>
    public sealed class CastStates
    {
        /// <summary>No regiment of that side had an outline when the cast was checked.</summary>
        public const int NoRegiment = -1;

        public CastStates(int casts, int writes)
        {
            if (casts < 0 || writes < 0)
            {
                throw new ArgumentOutOfRangeException(casts < 0 ? nameof(casts) : nameof(writes));
            }
            Status = new CastStatus[casts];
            Reasons = new FizzleReasons[casts];
            NearestAttacker = new int[casts];
            AttackerDistanceMm = new int[casts];
            NearestDefender = new int[casts];
            DefenderDistanceMm = new int[casts];
            Carry = new int[writes];
            for (int c = 0; c < casts; c++)
            {
                NearestAttacker[c] = NoRegiment;
                NearestDefender[c] = NoRegiment;
            }
        }

        public int Count => Status.Length;

        public CastStatus[] Status { get; }

        public FizzleReasons[] Reasons { get; }

        /// <summary>The attacker regiment (slot) whose outline was nearest the area at the check, or <see cref="NoRegiment"/>.</summary>
        public int[] NearestAttacker { get; }

        /// <summary>From the area's edge to that outline, in mm (negative when the outline reaches into the area).</summary>
        public int[] AttackerDistanceMm { get; }

        public int[] NearestDefender { get; }

        public int[] DefenderDistanceMm { get; }

        /// <summary>Per write of every cast, in plan order: the amount × ticks-per-second not yet written.</summary>
        public int[] Carry { get; }

        internal void WriteTo(ByteWriter writer)
        {
            writer.WriteInt32(Count);
            writer.WriteInt32(Carry.Length);
            for (int c = 0; c < Count; c++)
            {
                writer.WriteByte((byte)Status[c]);
                writer.WriteByte((byte)Reasons[c]);
                writer.WriteInt32(NearestAttacker[c]);
                writer.WriteInt32(AttackerDistanceMm[c]);
                writer.WriteInt32(NearestDefender[c]);
                writer.WriteInt32(DefenderDistanceMm[c]);
            }
            foreach (int carry in Carry)
            {
                writer.WriteInt32(carry);
            }
        }

        internal void ReadFrom(ByteReader reader, int regimentCapacity)
        {
            if (reader.ReadInt32() != Count || reader.ReadInt32() != Carry.Length)
            {
                throw new FormatException("The snapshot's casts don't match this battle's plan.");
            }
            for (int c = 0; c < Count; c++)
            {
                byte status = reader.ReadByte();
                byte reasons = reader.ReadByte();
                if (
                    status > (byte)CastStatus.Fizzled
                    || (reasons & ~3) != 0
                    || ((status == (byte)CastStatus.Fizzled) != (reasons != 0))
                )
                {
                    throw new FormatException("Snapshot cast state is invalid.");
                }
                Status[c] = (CastStatus)status;
                Reasons[c] = (FizzleReasons)reasons;
                NearestAttacker[c] = ReadRegiment(reader, regimentCapacity);
                AttackerDistanceMm[c] = reader.ReadInt32();
                NearestDefender[c] = ReadRegiment(reader, regimentCapacity);
                DefenderDistanceMm[c] = reader.ReadInt32();
            }
            for (int w = 0; w < Carry.Length; w++)
            {
                int carry = reader.ReadInt32();
                if (carry < 0 || carry >= SimConstants.TicksPerSecond)
                {
                    throw new FormatException("Snapshot cast remainder is out of range.");
                }
                Carry[w] = carry;
            }
        }

        private static int ReadRegiment(ByteReader reader, int regimentCapacity)
        {
            int regiment = reader.ReadInt32();
            if (regiment < NoRegiment || regiment >= regimentCapacity)
            {
                throw new FormatException("Snapshot cast regiment is out of range.");
            }
            return regiment;
        }

        internal void AppendTo(SimHash hash)
        {
            for (int c = 0; c < Count; c++)
            {
                hash.AppendInt32((int)Status[c]);
                hash.AppendInt32((int)Reasons[c]);
                hash.AppendInt32(NearestAttacker[c]);
                hash.AppendInt32(AttackerDistanceMm[c]);
                hash.AppendInt32(NearestDefender[c]);
                hash.AppendInt32(DefenderDistanceMm[c]);
            }
            foreach (int carry in Carry)
            {
                hash.AppendInt32(carry);
            }
        }
    }
}
