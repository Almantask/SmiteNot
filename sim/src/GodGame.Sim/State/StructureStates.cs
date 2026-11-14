using System;
using GodGame.Sim.Math;

namespace GodGame.Sim
{
    /// <summary>Strain stages (spec S2 8.3): for the player's eyes only, regiments never read them.</summary>
    public enum StrainStage : byte
    {
        Quiet = 0,
        Creak = 1,
        Crack = 2,
        Sag = 3,
    }

    /// <summary>
    /// Each deck segment's state during the battle (spec S2 section 8), by segment index in <see cref="DeckLayout"/>:
    /// strength in ‰ of its maximum (Rot and lost support lower it), whether it has failed, its consecutive checks over
    /// strength, its strain stage and load ratio for presentation, and the Rot under way. Hashed and snapshotted.
    /// </summary>
    public sealed class StructureStates
    {
        /// <summary>How many segments a battle may build on top of the map's (trestles, pontoon decks).</summary>
        public const int MaxBattleSegments = 256;

        /// <summary>A sanity bound on structure kinds in a snapshot (the ruleset has a handful).</summary>
        public const int MaxKinds = 64;

        /// <summary>The map's segments plus room for battle-made ones.</summary>
        public StructureStates(int staticSegments)
            : this(staticSegments, staticSegments + MaxBattleSegments) { }

        private StructureStates(int staticSegments, int segments)
        {
            StaticCount = staticSegments;
            Count = staticSegments;
            BattleKind = new int[segments];
            BattleCells = new int[]?[segments];
            BattleDeckHeight = new int[segments];
            BattleMaxStrength = new long[segments];
            BattlePrevious = new int[segments];
            Strength = new int[segments];
            Broken = new bool[segments];
            OverChecks = new int[segments];
            Stage = new StrainStage[segments];
            LoadPermille = new int[segments];
            RotFrom = new int[segments];
            RotTo = new int[segments];
            RotStart = new int[segments];
            RotTicks = new int[segments];
            RotCause = new int[segments];
            for (int s = 0; s < segments; s++)
            {
                Strength[s] = 1000;
                RotCause[s] = CauseLog.None;
            }
        }

        /// <summary>Segments in use: the map's, then the battle-made ones so far.</summary>
        public int Count { get; private set; }

        public int StaticCount { get; }

        public int Capacity => Strength.Length;

        // A battle-made segment's definition (index ≥ StaticCount): kind index in load.json, cells, deck height, strength
        // when sound, and the segment it was built on from (its neighbour), or -1.
        public int[] BattleKind { get; }

        public int[]?[] BattleCells { get; }

        public int[] BattleDeckHeight { get; }

        public long[] BattleMaxStrength { get; }

        public int[] BattlePrevious { get; }

        /// <summary>Records a battle-made segment; its index, or -1 when the battle has built as many as it may.</summary>
        public int AddBattleSegment(
            int kind,
            int[] cells,
            int deckHeightMm,
            long maxStrengthKg,
            int previous
        )
        {
            if (Count == Capacity)
            {
                return -1;
            }
            int s = Count++;
            BattleKind[s] = kind;
            BattleCells[s] = cells;
            BattleDeckHeight[s] = deckHeightMm;
            BattleMaxStrength[s] = maxStrengthKg;
            BattlePrevious[s] = previous;
            Strength[s] = 1000;
            Broken[s] = false;
            OverChecks[s] = 0;
            Stage[s] = StrainStage.Quiet;
            LoadPermille[s] = 0;
            RotStart[s] = 0;
            RotCause[s] = CauseLog.None;
            return s;
        }

        /// <summary>A clean copy's shape, for a blank world to restore into.</summary>
        internal StructureStates CreateBlank() => new StructureStates(StaticCount, Capacity);

        /// <summary>Current strength, ‰ of the segment's maximum.</summary>
        public int[] Strength { get; }

        public bool[] Broken { get; }

        public int[] OverChecks { get; }

        public StrainStage[] Stage { get; }

        /// <summary>Load ÷ strength at the last check, in ‰ (capped): the strain overlay's reading.</summary>
        public int[] LoadPermille { get; }

        /// <summary>A Rot under way: strength falls from <see cref="RotFrom"/> to <see cref="RotTo"/> over its ticks.</summary>
        public int[] RotFrom { get; }

        public int[] RotTo { get; }

        /// <summary>The tick the Rot began, or 0 when none is under way (no Rot starts at tick 0: casts wind up first).</summary>
        public int[] RotStart { get; }

        public int[] RotTicks { get; }

        /// <summary>The cast event behind the latest Rot, for the collapse's cause chain.</summary>
        public int[] RotCause { get; }

        internal void WriteTo(ByteWriter writer)
        {
            writer.WriteInt32(StaticCount);
            writer.WriteInt32(Count);
            for (int s = 0; s < Count; s++)
            {
                if (s >= StaticCount)
                {
                    writer.WriteInt32(BattleKind[s]);
                    writer.WriteInt32(BattleDeckHeight[s]);
                    writer.WriteInt64(BattleMaxStrength[s]);
                    writer.WriteInt32(BattlePrevious[s]);
                    var cells = BattleCells[s]!;
                    writer.WriteInt32(cells.Length);
                    foreach (int c in cells)
                    {
                        writer.WriteInt32(c);
                    }
                }
                writer.WriteInt32(Strength[s]);
                writer.WriteByte(Broken[s] ? (byte)1 : (byte)0);
                writer.WriteInt32(OverChecks[s]);
                writer.WriteByte((byte)Stage[s]);
                writer.WriteInt32(LoadPermille[s]);
                writer.WriteInt32(RotFrom[s]);
                writer.WriteInt32(RotTo[s]);
                writer.WriteInt32(RotStart[s]);
                writer.WriteInt32(RotTicks[s]);
                writer.WriteInt32(RotCause[s]);
            }
        }

        internal void ReadFrom(ByteReader reader, int cellCount)
        {
            if (reader.ReadInt32() != StaticCount)
            {
                throw new FormatException(
                    "The snapshot's structures don't match this battle's decks."
                );
            }
            int count = reader.ReadInt32();
            if (count < StaticCount || count > Capacity)
            {
                throw new FormatException("The snapshot's battle-made decks are out of range.");
            }
            Count = count;
            for (int s = 0; s < Count; s++)
            {
                if (s >= StaticCount)
                {
                    BattleKind[s] = reader.ReadInt32();
                    if (BattleKind[s] < 0 || BattleKind[s] >= MaxKinds)
                    {
                        throw new FormatException(
                            "Snapshot battle-made deck kind is out of range."
                        );
                    }
                    BattleDeckHeight[s] = reader.ReadInt32();
                    BattleMaxStrength[s] = reader.ReadInt64();
                    BattlePrevious[s] = reader.ReadInt32();
                    int n = reader.ReadInt32();
                    if (
                        n < 0
                        || n > 100_000
                        || BattleMaxStrength[s] < 0
                        || BattlePrevious[s] < -1
                        || BattlePrevious[s] >= s
                    )
                    {
                        throw new FormatException("Snapshot battle-made deck is invalid.");
                    }
                    var cells = new int[n];
                    for (int k = 0; k < n; k++)
                    {
                        cells[k] = reader.ReadInt32();
                        if (cells[k] < 0 || cells[k] >= cellCount)
                        {
                            throw new FormatException(
                                "Snapshot battle-made deck cell is off the map."
                            );
                        }
                    }
                    BattleCells[s] = cells;
                }
                Strength[s] = Permille(reader.ReadInt32());
                byte broken = reader.ReadByte();
                if (broken > 1)
                {
                    throw new FormatException("Snapshot structure flag is invalid.");
                }
                Broken[s] = broken == 1;
                OverChecks[s] = NonNegative(reader.ReadInt32());
                byte stage = reader.ReadByte();
                if (stage > (byte)StrainStage.Sag)
                {
                    throw new FormatException("Snapshot strain stage is out of range.");
                }
                Stage[s] = (StrainStage)stage;
                LoadPermille[s] = NonNegative(reader.ReadInt32());
                RotFrom[s] = Permille(reader.ReadInt32());
                RotTo[s] = Permille(reader.ReadInt32());
                RotStart[s] = NonNegative(reader.ReadInt32());
                RotTicks[s] = NonNegative(reader.ReadInt32());
                RotCause[s] = reader.ReadInt32();
                if (RotCause[s] < CauseLog.None)
                {
                    throw new FormatException("Snapshot rot cause is invalid.");
                }
            }
        }

        private static int Permille(int v) =>
            v >= 0 && v <= 1000
                ? v
                : throw new FormatException("Snapshot strength is out of range.");

        private static int NonNegative(int v) =>
            v >= 0 ? v : throw new FormatException("Snapshot structure count is negative.");

        internal void AppendTo(SimHash hash)
        {
            hash.AppendInt32(StaticCount);
            hash.AppendInt32(Count);
            for (int s = 0; s < Count; s++)
            {
                if (s >= StaticCount)
                {
                    hash.AppendInt32(BattleKind[s]);
                    hash.AppendInt32(BattleDeckHeight[s]);
                    hash.AppendInt64(BattleMaxStrength[s]);
                    hash.AppendInt32(BattlePrevious[s]);
                    hash.AppendInt32s(BattleCells[s]!);
                }
                hash.AppendInt32(Strength[s]);
                hash.AppendInt32(Broken[s] ? 1 : 0);
                hash.AppendInt32(OverChecks[s]);
                hash.AppendInt32((int)Stage[s]);
                hash.AppendInt32(LoadPermille[s]);
                hash.AppendInt32(RotFrom[s]);
                hash.AppendInt32(RotTo[s]);
                hash.AppendInt32(RotStart[s]);
                hash.AppendInt32(RotTicks[s]);
                hash.AppendInt32(RotCause[s]);
            }
        }
    }
}
