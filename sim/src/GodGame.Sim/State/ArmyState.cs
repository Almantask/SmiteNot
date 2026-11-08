using System;
using GodGame.Sim.Math;

namespace GodGame.Sim
{
    /// <summary>What a regiment is doing (spec S2, section 5's targeting default until the regiment AI, M7).</summary>
    public enum RegimentOrder : byte
    {
        /// <summary>The navigation default: attackers march on the defender zone, defenders hold where they stand.</summary>
        March = 0,

        /// <summary>Close with <see cref="ArmyState.Target"/> and fight it hand to hand.</summary>
        Engage = 1,

        /// <summary>Stand and shoot volleys at <see cref="ArmyState.Target"/>.</summary>
        Shoot = 2,

        /// <summary>Walk to <see cref="ArmyState.Point"/>, away from the enemy melee that came too close.</summary>
        FallBack = 3,

        /// <summary>Run for the side's retreat edge; soldiers who reach it flee the map.</summary>
        Rout = 4,

        /// <summary>A specialist walking to or standing at <see cref="ArmyState.Point"/>, where its action works.</summary>
        Work = 5,
    }

    /// <summary>Where a specialist's action stands (spec S2, section 14.1).</summary>
    public enum SpecialistJob : byte
    {
        /// <summary>Not started: waiting for its trigger (or a type with no action).</summary>
        Idle = 0,

        /// <summary>Working, or paused by a threat (its order says which).</summary>
        Active = 1,

        /// <summary>Finished, or out of charges.</summary>
        Done = 2,

        /// <summary>Wiped or routed: its unspent charges are gone (brief 2026-10-10).</summary>
        Lost = 3,
    }

    /// <summary>
    /// Per-regiment combat and morale state (spec S2, sections 5 to 7): the order and its target, ammunition and reload,
    /// the volley in flight, and the morale bookkeeping (losses seen, the last loss, rallies left, safe time, wiped).
    /// Indexed by regiment slot, hashed and snapshotted with the world; a dead regiment slot is zero (target -1).
    /// </summary>
    public sealed class ArmyState
    {
        public ArmyState(int regimentCapacity)
        {
            Order = new RegimentOrder[regimentCapacity];
            Target = new int[regimentCapacity];
            Point = new FixVec2[regimentCapacity];
            FallBackReady = new int[regimentCapacity];
            Ammo = new int[regimentCapacity];
            ReloadReady = new int[regimentCapacity];
            VolleyLands = new int[regimentCapacity];
            VolleyTarget = new int[regimentCapacity];
            VolleyAim = new FixVec2[regimentCapacity];
            VolleyHits = new int[regimentCapacity];
            LossesSeen = new int[regimentCapacity];
            LastLoss = new int[regimentCapacity];
            RalliesLeft = new int[regimentCapacity];
            SafeTicks = new int[regimentCapacity];
            Wiped = new bool[regimentCapacity];
            Job = new SpecialistJob[regimentCapacity];
            Spent = new int[regimentCapacity];
            WorkProgress = new int[regimentCapacity];
            WorkStep = new int[regimentCapacity];
            ClearTicks = new int[regimentCapacity];
            JobCells = new int[]?[regimentCapacity];
            JobTargets = new int[]?[regimentCapacity];
            for (int r = 0; r < regimentCapacity; r++)
            {
                Target[r] = -1;
                VolleyTarget[r] = -1;
            }
        }

        public int Capacity => Order.Length;

        public RegimentOrder[] Order { get; }

        /// <summary>The enemy regiment slot an Engage or Shoot order is aimed at, or -1.</summary>
        public int[] Target { get; }

        /// <summary>Where a FallBack order walks to.</summary>
        public FixVec2[] Point { get; }

        /// <summary>The first tick a ranged regiment may fall back again.</summary>
        public int[] FallBackReady { get; }

        /// <summary>Volleys left.</summary>
        public int[] Ammo { get; }

        /// <summary>The first tick the next volley may be shot.</summary>
        public int[] ReloadReady { get; }

        /// <summary>The tick the volley in flight lands, or 0 when none is.</summary>
        public int[] VolleyLands { get; }

        public int[] VolleyTarget { get; }

        public FixVec2[] VolleyAim { get; }

        public int[] VolleyHits { get; }

        /// <summary>Losses the morale step has already charged, in half soldiers (a swept soldier counts as half a loss).</summary>
        public int[] LossesSeen { get; }

        /// <summary>The tick of the last loss, for the recovery delay.</summary>
        public int[] LastLoss { get; }

        public int[] RalliesLeft { get; }

        /// <summary>Ticks a routing regiment has spent with no enemy near, away from lethal ground.</summary>
        public int[] SafeTicks { get; }

        /// <summary>Below about 10% of its starting size: the remnant routs for good and no longer counts (S2 7.3).</summary>
        public bool[] Wiped { get; }

        public SpecialistJob[] Job { get; }

        /// <summary>Charges used: fascine bundles laid.</summary>
        public int[] Spent { get; }

        /// <summary>Work done toward the next step, in ticks × ‰ of the full regiment's rate.</summary>
        public int[] WorkProgress { get; }

        /// <summary>The next step: a ditch's next section.</summary>
        public int[] WorkStep { get; }

        /// <summary>Ticks a paused specialist has had no enemy near.</summary>
        public int[] ClearTicks { get; }

        /// <summary>The cells a fascine crossing fills, in fill order, or null.</summary>
        public int[]?[] JobCells { get; }

        /// <summary>Each crossing cell's target height, in mm.</summary>
        public int[]?[] JobTargets { get; }

        /// <summary>A new regiment's state: marching, full ammunition, rallies to come.</summary>
        public void Init(int r, int ammo, int rallies)
        {
            Clear(r);
            Ammo[r] = ammo;
            RalliesLeft[r] = rallies;
        }

        public void Clear(int r)
        {
            Order[r] = RegimentOrder.March;
            Target[r] = -1;
            Point[r] = FixVec2.Zero;
            FallBackReady[r] = 0;
            Ammo[r] = 0;
            ReloadReady[r] = 0;
            VolleyLands[r] = 0;
            VolleyTarget[r] = -1;
            VolleyAim[r] = FixVec2.Zero;
            VolleyHits[r] = 0;
            LossesSeen[r] = 0;
            LastLoss[r] = 0;
            RalliesLeft[r] = 0;
            SafeTicks[r] = 0;
            Wiped[r] = false;
            Job[r] = SpecialistJob.Idle;
            Spent[r] = 0;
            WorkProgress[r] = 0;
            WorkStep[r] = 0;
            ClearTicks[r] = 0;
            JobCells[r] = null;
            JobTargets[r] = null;
        }

        /// <summary>Whether a slot holds exactly what <see cref="Clear"/> leaves (dead regiment slots must).</summary>
        internal bool IsClear(int r) =>
            Order[r] == RegimentOrder.March
            && Target[r] == -1
            && Point[r] == FixVec2.Zero
            && FallBackReady[r] == 0
            && Ammo[r] == 0
            && ReloadReady[r] == 0
            && VolleyLands[r] == 0
            && VolleyTarget[r] == -1
            && VolleyAim[r] == FixVec2.Zero
            && VolleyHits[r] == 0
            && LossesSeen[r] == 0
            && LastLoss[r] == 0
            && RalliesLeft[r] == 0
            && SafeTicks[r] == 0
            && !Wiped[r]
            && Job[r] == SpecialistJob.Idle
            && Spent[r] == 0
            && WorkProgress[r] == 0
            && WorkStep[r] == 0
            && ClearTicks[r] == 0
            && JobCells[r] == null
            && JobTargets[r] == null;

        internal void WriteTo(ByteWriter writer)
        {
            writer.WriteInt32(Capacity);
            for (int r = 0; r < Capacity; r++)
            {
                writer.WriteByte((byte)Order[r]);
                writer.WriteInt32(Target[r]);
                writer.WriteInt64(Point[r].X.Raw);
                writer.WriteInt64(Point[r].Y.Raw);
                writer.WriteInt32(FallBackReady[r]);
                writer.WriteInt32(Ammo[r]);
                writer.WriteInt32(ReloadReady[r]);
                writer.WriteInt32(VolleyLands[r]);
                writer.WriteInt32(VolleyTarget[r]);
                writer.WriteInt64(VolleyAim[r].X.Raw);
                writer.WriteInt64(VolleyAim[r].Y.Raw);
                writer.WriteInt32(VolleyHits[r]);
                writer.WriteInt32(LossesSeen[r]);
                writer.WriteInt32(LastLoss[r]);
                writer.WriteInt32(RalliesLeft[r]);
                writer.WriteInt32(SafeTicks[r]);
                writer.WriteByte(Wiped[r] ? (byte)1 : (byte)0);
                writer.WriteByte((byte)Job[r]);
                writer.WriteInt32(Spent[r]);
                writer.WriteInt32(WorkProgress[r]);
                writer.WriteInt32(WorkStep[r]);
                writer.WriteInt32(ClearTicks[r]);
                WriteCells(writer, JobCells[r]);
                WriteCells(writer, JobTargets[r]);
            }
        }

        internal void ReadFrom(ByteReader reader)
        {
            if (reader.ReadInt32() != Capacity)
            {
                throw new FormatException("The snapshot's army state doesn't match this world's.");
            }
            for (int r = 0; r < Capacity; r++)
            {
                byte order = reader.ReadByte();
                if (order > (byte)RegimentOrder.Work)
                {
                    throw new FormatException("Snapshot regiment order is out of range.");
                }
                Order[r] = (RegimentOrder)order;
                Target[r] = ReadSlot(reader);
                Point[r] = new FixVec2(
                    Fix.FromRaw(reader.ReadInt64()),
                    Fix.FromRaw(reader.ReadInt64())
                );
                FallBackReady[r] = reader.ReadInt32();
                Ammo[r] = NonNegative(reader.ReadInt32());
                ReloadReady[r] = reader.ReadInt32();
                VolleyLands[r] = NonNegative(reader.ReadInt32());
                VolleyTarget[r] = ReadSlot(reader);
                VolleyAim[r] = new FixVec2(
                    Fix.FromRaw(reader.ReadInt64()),
                    Fix.FromRaw(reader.ReadInt64())
                );
                VolleyHits[r] = NonNegative(reader.ReadInt32());
                LossesSeen[r] = NonNegative(reader.ReadInt32());
                LastLoss[r] = reader.ReadInt32();
                RalliesLeft[r] = NonNegative(reader.ReadInt32());
                SafeTicks[r] = NonNegative(reader.ReadInt32());
                byte wiped = reader.ReadByte();
                if (wiped > 1)
                {
                    throw new FormatException("Snapshot wiped flag is invalid.");
                }
                Wiped[r] = wiped == 1;
                byte job = reader.ReadByte();
                if (job > (byte)SpecialistJob.Lost)
                {
                    throw new FormatException("Snapshot specialist job is out of range.");
                }
                Job[r] = (SpecialistJob)job;
                Spent[r] = NonNegative(reader.ReadInt32());
                WorkProgress[r] = NonNegative(reader.ReadInt32());
                WorkStep[r] = NonNegative(reader.ReadInt32());
                ClearTicks[r] = NonNegative(reader.ReadInt32());
                JobCells[r] = ReadCells(reader);
                JobTargets[r] = ReadCells(reader);
                if (
                    (JobCells[r] == null) != (JobTargets[r] == null)
                    || (JobCells[r] != null && JobCells[r]!.Length != JobTargets[r]!.Length)
                )
                {
                    throw new FormatException("Snapshot crossing cells and targets don't match.");
                }
            }
        }

        private static void WriteCells(ByteWriter writer, int[]? cells)
        {
            writer.WriteInt32(cells == null ? -1 : cells.Length);
            if (cells != null)
            {
                foreach (int c in cells)
                {
                    writer.WriteInt32(c);
                }
            }
        }

        private static int[]? ReadCells(ByteReader reader)
        {
            int n = reader.ReadInt32();
            if (n < -1 || n > 100_000)
            {
                throw new FormatException("Snapshot crossing is implausibly long.");
            }
            if (n < 0)
            {
                return null;
            }
            var cells = new int[n];
            for (int i = 0; i < n; i++)
            {
                cells[i] = reader.ReadInt32();
                if (cells[i] < -MapData.MaxHeightMm || cells[i] > 100_000_000)
                {
                    // Cells and target heights share this reader: an index on the largest map, or a legal height.
                    throw new FormatException("Snapshot crossing value is out of range.");
                }
            }
            return cells;
        }

        private static void AppendCells(SimHash hash, int[]? cells)
        {
            hash.AppendInt32(cells == null ? -1 : cells.Length);
            if (cells != null)
            {
                foreach (int c in cells)
                {
                    hash.AppendInt32(c);
                }
            }
        }

        private int ReadSlot(ByteReader reader)
        {
            int slot = reader.ReadInt32();
            if (slot < -1 || slot >= Capacity)
            {
                throw new FormatException("Snapshot regiment target is out of range.");
            }
            return slot;
        }

        private static int NonNegative(int value) =>
            value >= 0
                ? value
                : throw new FormatException("Snapshot army state has a negative count.");

        /// <summary>Live regiments only, in slot order (a dead slot is cleared and a new regiment re-initialises it).</summary>
        internal void AppendTo(SimHash hash, EntityPool regiments)
        {
            for (int r = 0; r < regiments.HighWater; r++)
            {
                if (!regiments.IsAlive(r))
                {
                    continue;
                }
                hash.AppendInt32((int)Order[r]);
                hash.AppendInt32(Target[r]);
                hash.AppendInt64(Point[r].X.Raw);
                hash.AppendInt64(Point[r].Y.Raw);
                hash.AppendInt32(FallBackReady[r]);
                hash.AppendInt32(Ammo[r]);
                hash.AppendInt32(ReloadReady[r]);
                hash.AppendInt32(VolleyLands[r]);
                hash.AppendInt32(VolleyTarget[r]);
                hash.AppendInt64(VolleyAim[r].X.Raw);
                hash.AppendInt64(VolleyAim[r].Y.Raw);
                hash.AppendInt32(VolleyHits[r]);
                hash.AppendInt32(LossesSeen[r]);
                hash.AppendInt32(LastLoss[r]);
                hash.AppendInt32(RalliesLeft[r]);
                hash.AppendInt32(SafeTicks[r]);
                hash.AppendInt32(Wiped[r] ? 1 : 0);
                hash.AppendInt32((int)Job[r]);
                hash.AppendInt32(Spent[r]);
                hash.AppendInt32(WorkProgress[r]);
                hash.AppendInt32(WorkStep[r]);
                hash.AppendInt32(ClearTicks[r]);
                AppendCells(hash, JobCells[r]);
                AppendCells(hash, JobTargets[r]);
            }
        }
    }
}
