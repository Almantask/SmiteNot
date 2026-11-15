using System;
using System.Collections.Generic;
using GodGame.Sim.Math;

namespace GodGame.Sim
{
    /// <summary>
    /// What Lightning v0 left in the world (spec S2 12.1–12.2): each strike (its point, tick and cause event, a fear
    /// source for herds), each tree it felled (the tree, the fall heading, the tick it lands and whether it has), and each
    /// fire v0 patch (its blocker, the tick it goes out and whether it has). In the order they happened; hashed and
    /// snapshotted with the world.
    /// </summary>
    public sealed class StrikeStates
    {
        public const int MaxEntries = 4_096;

        private readonly List<int> _strikeX = new List<int>();
        private readonly List<int> _strikeY = new List<int>();
        private readonly List<int> _strikeTick = new List<int>();
        private readonly List<int> _strikeEvent = new List<int>();
        private readonly List<int> _fallTree = new List<int>();
        private readonly List<int> _fallHeading = new List<int>();
        private readonly List<int> _fallTick = new List<int>();
        private readonly List<int> _fallCause = new List<int>();
        private readonly List<bool> _fallLanded = new List<bool>();
        private readonly List<int> _fireBlocker = new List<int>();
        private readonly List<int> _fireEnds = new List<int>();
        private readonly List<int> _fireEvent = new List<int>();
        private readonly List<bool> _fireOut = new List<bool>();

        public int StrikeCount => _strikeTick.Count;

        public int StrikeXMm(int s) => _strikeX[s];

        public int StrikeYMm(int s) => _strikeY[s];

        public int StrikeTick(int s) => _strikeTick[s];

        public int StrikeEvent(int s) => _strikeEvent[s];

        public void AddStrike(int xMm, int yMm, int tick, int causeEvent)
        {
            Guard(_strikeTick.Count);
            _strikeX.Add(xMm);
            _strikeY.Add(yMm);
            _strikeTick.Add(tick);
            _strikeEvent.Add(causeEvent);
        }

        public int FallCount => _fallTree.Count;

        public int FallTree(int f) => _fallTree[f];

        public int FallHeading(int f) => _fallHeading[f];

        public int FallTick(int f) => _fallTick[f];

        public int FallCause(int f) => _fallCause[f];

        public bool FallLanded(int f) => _fallLanded[f];

        public void Land(int f) => _fallLanded[f] = true;

        /// <summary>Whether a tree has been struck already (falling or fallen): it can't be felled twice.</summary>
        public bool IsFelled(int tree) => _fallTree.Contains(tree);

        public void AddFall(int tree, int headingUnits, int landTick, int causeEvent)
        {
            Guard(_fallTree.Count);
            _fallTree.Add(tree);
            _fallHeading.Add(headingUnits);
            _fallTick.Add(landTick);
            _fallCause.Add(causeEvent);
            _fallLanded.Add(false);
        }

        public int FireCount => _fireBlocker.Count;

        public int FireBlocker(int f) => _fireBlocker[f];

        public int FireEnds(int f) => _fireEnds[f];

        public int FireEvent(int f) => _fireEvent[f];

        public bool FireOut(int f) => _fireOut[f];

        public void PutOut(int f) => _fireOut[f] = true;

        public void AddFire(int blocker, int endsTick, int causeEvent)
        {
            Guard(_fireBlocker.Count);
            _fireBlocker.Add(blocker);
            _fireEnds.Add(endsTick);
            _fireEvent.Add(causeEvent);
            _fireOut.Add(false);
        }

        private static void Guard(int count)
        {
            if (count >= MaxEntries)
            {
                throw new InvalidOperationException(
                    "A battle holds at most 4,096 strikes, falls or fires."
                );
            }
        }

        internal void WriteTo(ByteWriter writer)
        {
            writer.WriteInt32(StrikeCount);
            for (int s = 0; s < StrikeCount; s++)
            {
                writer.WriteInt32(_strikeX[s]);
                writer.WriteInt32(_strikeY[s]);
                writer.WriteInt32(_strikeTick[s]);
                writer.WriteInt32(_strikeEvent[s]);
            }
            writer.WriteInt32(FallCount);
            for (int f = 0; f < FallCount; f++)
            {
                writer.WriteInt32(_fallTree[f]);
                writer.WriteInt32(_fallHeading[f]);
                writer.WriteInt32(_fallTick[f]);
                writer.WriteInt32(_fallCause[f]);
                writer.WriteByte(_fallLanded[f] ? (byte)1 : (byte)0);
            }
            writer.WriteInt32(FireCount);
            for (int f = 0; f < FireCount; f++)
            {
                writer.WriteInt32(_fireBlocker[f]);
                writer.WriteInt32(_fireEnds[f]);
                writer.WriteInt32(_fireEvent[f]);
                writer.WriteByte(_fireOut[f] ? (byte)1 : (byte)0);
            }
        }

        internal void ReadFrom(ByteReader reader)
        {
            _strikeX.Clear();
            _strikeY.Clear();
            _strikeTick.Clear();
            _strikeEvent.Clear();
            _fallTree.Clear();
            _fallHeading.Clear();
            _fallTick.Clear();
            _fallCause.Clear();
            _fallLanded.Clear();
            _fireBlocker.Clear();
            _fireEnds.Clear();
            _fireEvent.Clear();
            _fireOut.Clear();
            int strikes = Count(reader);
            for (int s = 0; s < strikes; s++)
            {
                _strikeX.Add(reader.ReadInt32());
                _strikeY.Add(reader.ReadInt32());
                _strikeTick.Add(reader.ReadInt32());
                _strikeEvent.Add(reader.ReadInt32());
            }
            int falls = Count(reader);
            for (int f = 0; f < falls; f++)
            {
                _fallTree.Add(reader.ReadInt32());
                int heading = reader.ReadInt32();
                if (heading < 0 || heading >= Angle.UnitsPerTurn)
                {
                    throw new FormatException("Snapshot fall heading is out of range.");
                }
                _fallHeading.Add(heading);
                _fallTick.Add(reader.ReadInt32());
                _fallCause.Add(reader.ReadInt32());
                _fallLanded.Add(Flag(reader));
            }
            int fires = Count(reader);
            for (int f = 0; f < fires; f++)
            {
                _fireBlocker.Add(reader.ReadInt32());
                _fireEnds.Add(reader.ReadInt32());
                _fireEvent.Add(reader.ReadInt32());
                _fireOut.Add(Flag(reader));
            }
        }

        private static int Count(ByteReader reader)
        {
            int n = reader.ReadInt32();
            if (n < 0 || n > MaxEntries)
            {
                throw new FormatException("Snapshot strike state is implausibly long.");
            }
            return n;
        }

        private static bool Flag(ByteReader reader)
        {
            byte b = reader.ReadByte();
            if (b > 1)
            {
                throw new FormatException("Snapshot strike flag is invalid.");
            }
            return b == 1;
        }

        internal void AppendTo(SimHash hash)
        {
            hash.AppendInt32(StrikeCount);
            for (int s = 0; s < StrikeCount; s++)
            {
                hash.AppendInt32(_strikeX[s]);
                hash.AppendInt32(_strikeY[s]);
                hash.AppendInt32(_strikeTick[s]);
                hash.AppendInt32(_strikeEvent[s]);
            }
            hash.AppendInt32(FallCount);
            for (int f = 0; f < FallCount; f++)
            {
                hash.AppendInt32(_fallTree[f]);
                hash.AppendInt32(_fallHeading[f]);
                hash.AppendInt32(_fallTick[f]);
                hash.AppendInt32(_fallCause[f]);
                hash.AppendInt32(_fallLanded[f] ? 1 : 0);
            }
            hash.AppendInt32(FireCount);
            for (int f = 0; f < FireCount; f++)
            {
                hash.AppendInt32(_fireBlocker[f]);
                hash.AppendInt32(_fireEnds[f]);
                hash.AppendInt32(_fireEvent[f]);
                hash.AppendInt32(_fireOut[f] ? 1 : 0);
            }
        }
    }
}
