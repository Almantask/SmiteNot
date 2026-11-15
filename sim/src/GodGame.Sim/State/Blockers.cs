using System;
using System.Collections.Generic;
using GodGame.Sim.Math;

namespace GodGame.Sim
{
    /// <summary>What a battle-made blocker is (spec S2 13.2).</summary>
    public enum BlockerKind : byte
    {
        /// <summary>A siege engine smashed to 0 structure, or abandoned with no crew left (S2 2.1).</summary>
        Wreck = 0,

        /// <summary>A tree lightning felled across the ground (S2 12.1, 13.2): trunk cells and crown cells.</summary>
        FelledTree = 1,

        /// <summary>A fire v0 patch (S2 12.2): burning cells, closed and lethal until it goes out.</summary>
        Fire = 2,
    }

    /// <summary>
    /// Blockers the battle made (spec S2 13.2: wrecks, felled trees): each one's kind, its cells with a profile per cell
    /// (0 the trunk or the wreck, 1 a tree's crown), the cause event that made it, and whether it has been cleared. They
    /// skip the lock-in route check (brief, 2026-10-09) and stay clearable. Kept in the order they were made; hashed and
    /// snapshotted with the world; navigation stamps them onto the terrain as they are made and again after a restore.
    /// </summary>
    public sealed class Blockers
    {
        /// <summary>A blocker covers at most this many cells (an 18 m tree with its crown is about 20).</summary>
        public const int MaxCells = 256;

        /// <summary>A battle makes at most this many (a guard for snapshots, not a rule).</summary>
        public const int MaxBlockers = 4_096;

        private readonly List<BlockerKind> _kind = new List<BlockerKind>();
        private readonly List<int[]> _cells = new List<int[]>();
        private readonly List<byte[]> _profile = new List<byte[]>();
        private readonly List<int> _event = new List<int>();
        private readonly List<bool> _cleared = new List<bool>();
        private readonly List<bool> _closesRoute = new List<bool>();
        private readonly List<int> _clearer = new List<int>();
        private readonly List<int> _progress = new List<int>();

        public int Count => _kind.Count;

        public BlockerKind Kind(int b) => _kind[b];

        public int[] Cells(int b) => _cells[b];

        /// <summary>Per cell: 0 the trunk or the wreck, 1 a tree's crown.</summary>
        public byte[] Profile(int b) => _profile[b];

        /// <summary>The cause event that made it (a wrecked or abandoned engine, a felled tree), or none.</summary>
        public int Event(int b) => _event[b];

        public bool IsCleared(int b) => _cleared[b];

        /// <summary>Whether it closed some army's last route for some class when it was made (D14: foot regiments clear it).</summary>
        public bool ClosesRoute(int b) => _closesRoute[b];

        /// <summary>The foot regiment clearing it (D14), or -1.</summary>
        public int Clearer(int b) => _clearer[b];

        /// <summary>Ticks of clearing the foot regiment has done.</summary>
        public int Progress(int b) => _progress[b];

        public void SetClearer(int b, int regiment, int progress)
        {
            _clearer[b] = regiment;
            _progress[b] = progress;
        }

        public void SetClosesRoute(int b, bool closes) => _closesRoute[b] = closes;

        /// <summary>Adds a blocker and returns its index, or -1 when the battle has made as many as it may.</summary>
        public int Add(BlockerKind kind, int[] cells, byte[] profile, int causeEvent)
        {
            if (cells == null || profile == null || cells.Length != profile.Length)
            {
                throw new ArgumentException("One profile per cell.", nameof(profile));
            }
            if (cells.Length == 0 || cells.Length > MaxCells)
            {
                throw new ArgumentOutOfRangeException(nameof(cells));
            }
            if (_kind.Count >= MaxBlockers)
            {
                return -1;
            }
            _kind.Add(kind);
            _cells.Add((int[])cells.Clone());
            _profile.Add((byte[])profile.Clone());
            _event.Add(causeEvent);
            _cleared.Add(false);
            _closesRoute.Add(false);
            _clearer.Add(-1);
            _progress.Add(0);
            return _kind.Count - 1;
        }

        public void Clear(int b) => _cleared[b] = true;

        internal void WriteTo(ByteWriter writer)
        {
            writer.WriteInt32(Count);
            for (int b = 0; b < Count; b++)
            {
                writer.WriteByte((byte)_kind[b]);
                writer.WriteInt32(_event[b]);
                writer.WriteByte(_cleared[b] ? (byte)1 : (byte)0);
                writer.WriteByte(_closesRoute[b] ? (byte)1 : (byte)0);
                writer.WriteInt32(_clearer[b]);
                writer.WriteInt32(_progress[b]);
                writer.WriteInt32(_cells[b].Length);
                for (int k = 0; k < _cells[b].Length; k++)
                {
                    writer.WriteInt32(_cells[b][k]);
                    writer.WriteByte(_profile[b][k]);
                }
            }
        }

        internal void ReadFrom(ByteReader reader, int cellCount)
        {
            _kind.Clear();
            _cells.Clear();
            _profile.Clear();
            _event.Clear();
            _cleared.Clear();
            _closesRoute.Clear();
            _clearer.Clear();
            _progress.Clear();
            int n = reader.ReadInt32();
            if (n < 0 || n > MaxBlockers)
            {
                throw new FormatException("Snapshot blocker count is out of range.");
            }
            for (int b = 0; b < n; b++)
            {
                byte kind = reader.ReadByte();
                if (kind > (byte)BlockerKind.Fire)
                {
                    throw new FormatException("Snapshot blocker kind is out of range.");
                }
                int cause = reader.ReadInt32();
                if (cause < -1)
                {
                    throw new FormatException("Snapshot blocker cause is invalid.");
                }
                byte cleared = reader.ReadByte();
                if (cleared > 1)
                {
                    throw new FormatException("Snapshot blocker cleared flag is invalid.");
                }
                byte closes = reader.ReadByte();
                int clearer = reader.ReadInt32();
                int progress = reader.ReadInt32();
                if (closes > 1 || clearer < -1 || progress < 0)
                {
                    throw new FormatException("Snapshot blocker clearing is invalid.");
                }
                _closesRoute.Add(closes == 1);
                _clearer.Add(clearer);
                _progress.Add(progress);
                int length = reader.ReadInt32();
                if (length < 1 || length > MaxCells)
                {
                    throw new FormatException("Snapshot blocker size is out of range.");
                }
                var cells = new int[length];
                var profile = new byte[length];
                for (int k = 0; k < length; k++)
                {
                    cells[k] = reader.ReadInt32();
                    profile[k] = reader.ReadByte();
                    if (cells[k] < 0 || cells[k] >= cellCount || profile[k] > 1)
                    {
                        throw new FormatException("Snapshot blocker cell is out of range.");
                    }
                }
                _kind.Add((BlockerKind)kind);
                _event.Add(cause);
                _cleared.Add(cleared == 1);
                _cells.Add(cells);
                _profile.Add(profile);
            }
        }

        internal void AppendTo(SimHash hash)
        {
            hash.AppendInt32(Count);
            for (int b = 0; b < Count; b++)
            {
                hash.AppendInt32((int)_kind[b]);
                hash.AppendInt32(_event[b]);
                hash.AppendInt32(_cleared[b] ? 1 : 0);
                hash.AppendInt32(_closesRoute[b] ? 1 : 0);
                hash.AppendInt32(_clearer[b]);
                hash.AppendInt32(_progress[b]);
                hash.AppendInt32(_cells[b].Length);
                for (int k = 0; k < _cells[b].Length; k++)
                {
                    hash.AppendInt32(_cells[b][k]);
                    hash.AppendInt32(_profile[b][k]);
                }
            }
        }
    }
}
