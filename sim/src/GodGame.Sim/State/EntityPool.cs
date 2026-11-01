using System;
using System.Globalization;
using GodGame.Sim.Math;

namespace GodGame.Sim
{
    /// <summary>
    /// Fixed-capacity slots with generations and deterministic reuse. A freed slot goes on a stack and the next
    /// allocation takes the most recently freed slot; with no free slot it takes the next unused index. The same
    /// sequence of allocations and frees therefore gives the same indices on every machine, and the stack is
    /// part of the hashed state, so a different history shows up in the state hash. Because the stack is
    /// last-in-first-out, the order of frees is state too: never free from parallel code.
    /// </summary>
    public sealed class EntityPool
    {
        private readonly int[] _generation;
        private readonly bool[] _alive;
        private readonly int[] _freeStack;
        private int _freeCount;
        private int _highWater;
        private int _aliveCount;

        public EntityPool(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }
            _generation = new int[capacity];
            _alive = new bool[capacity];
            _freeStack = new int[capacity];
        }

        public int Capacity => _generation.Length;

        /// <summary>One past the highest slot ever used: loops over slots run from 0 to here and skip dead ones.</summary>
        public int HighWater => _highWater;

        public int AliveCount => _aliveCount;

        /// <summary>
        /// Bumped by every allocation and free, so derived data (the spatial hash) can tell it is out of date.
        /// It decides nothing in the battle, so it is not part of the hash.
        /// </summary>
        public int Version { get; private set; }

        public bool IsAlive(int index) => _alive[index];

        public bool IsAlive(EntityId id) =>
            !id.IsNone
            && (uint)id.Index < (uint)_highWater
            && _alive[id.Index]
            && _generation[id.Index] == id.Generation;

        /// <summary>The live handle for a slot. The slot must be alive.</summary>
        public EntityId IdOf(int index)
        {
            if (!_alive[index])
            {
                throw new InvalidOperationException(
                    "Slot " + index.ToString(CultureInfo.InvariantCulture) + " is not alive."
                );
            }
            return new EntityId(index, _generation[index]);
        }

        public EntityId Allocate()
        {
            int index;
            if (_freeCount > 0)
            {
                index = _freeStack[--_freeCount];
            }
            else if (_highWater < Capacity)
            {
                index = _highWater++;
            }
            else
            {
                throw new InvalidOperationException(
                    "Entity pool is full (" + Capacity.ToString(CultureInfo.InvariantCulture) + ")."
                );
            }
            // Generations start at 1, so EntityId.None never matches a live slot; wrapping past
            // int.MaxValue skips 0 for the same reason.
            int generation = unchecked(_generation[index] + 1);
            _generation[index] = generation <= 0 ? 1 : generation;
            _alive[index] = true;
            _aliveCount++;
            Version = unchecked(Version + 1);
            return new EntityId(index, _generation[index]);
        }

        public void Free(EntityId id)
        {
            if (!IsAlive(id))
            {
                throw new InvalidOperationException("Entity " + id + " is not alive.");
            }
            _alive[id.Index] = false;
            _freeStack[_freeCount++] = id.Index;
            _aliveCount--;
            Version = unchecked(Version + 1);
        }

        /// <summary>The whole pool, every slot up to capacity, for snapshots.</summary>
        internal void WriteTo(ByteWriter writer)
        {
            writer.WriteInt32(Capacity);
            writer.WriteInt32(_highWater);
            writer.WriteInt32(_freeCount);
            writer.WriteInt32(_aliveCount);
            writer.WriteInt32s(_generation);
            writer.WriteInt32s(_freeStack);
            for (int i = 0; i < Capacity; i++)
            {
                writer.WriteByte(_alive[i] ? (byte)1 : (byte)0);
            }
        }

        /// <summary>Replaces this pool's state with a snapshot's; the capacity must match.</summary>
        internal void ReadFrom(ByteReader reader)
        {
            if (reader.ReadInt32() != Capacity)
            {
                throw new FormatException("Snapshot pool capacity does not match the world.");
            }
            int highWater = reader.ReadInt32();
            int freeCount = reader.ReadInt32();
            int aliveCount = reader.ReadInt32();
            if (
                highWater < 0
                || highWater > Capacity
                || freeCount < 0
                || freeCount > highWater
                || aliveCount != highWater - freeCount
            )
            {
                throw new FormatException("Snapshot pool counts are inconsistent.");
            }
            reader.ReadInt32s(_generation);
            reader.ReadInt32s(_freeStack);
            for (int i = 0; i < Capacity; i++)
            {
                byte alive = reader.ReadByte();
                if (alive > 1)
                {
                    throw new FormatException("Snapshot alive flag is not 0 or 1.");
                }
                _alive[i] = alive == 1;
            }
            _highWater = highWater;
            _freeCount = freeCount;
            _aliveCount = aliveCount;
            Version = unchecked(Version + 1); // derived data built before the restore is now stale
            CheckInvariants();
        }

        /// <summary>
        /// Throws <see cref="FormatException"/> when the pool's internal state is inconsistent: what a damaged or
        /// hand-made snapshot could otherwise smuggle in (a free slot that is also alive hands one slot to two units).
        /// </summary>
        internal void CheckInvariants()
        {
            int alive = 0;
            for (int i = 0; i < Capacity; i++)
            {
                if (_alive[i])
                {
                    if (i >= _highWater)
                    {
                        throw new FormatException("A slot above the high-water mark is alive.");
                    }
                    if (_generation[i] <= 0)
                    {
                        throw new FormatException("A live slot has no generation.");
                    }
                    alive++;
                }
            }
            if (alive != _aliveCount)
            {
                throw new FormatException("The alive count does not match the alive flags.");
            }
            var seen = new bool[Capacity];
            for (int f = 0; f < _freeCount; f++)
            {
                int slot = _freeStack[f];
                if (slot < 0 || slot >= _highWater || _alive[slot] || seen[slot])
                {
                    throw new FormatException(
                        "The free stack names a live, repeated or out-of-range slot."
                    );
                }
                seen[slot] = true;
            }
        }

        /// <summary>Everything that decides future allocations, in a fixed order.</summary>
        public void AppendTo(SimHash hash)
        {
            hash.AppendInt32(_highWater);
            hash.AppendInt32(_freeCount);
            hash.AppendInt32s(_freeStack.AsSpan(0, _freeCount));
            hash.AppendInt32s(_generation.AsSpan(0, _highWater));
            for (int i = 0; i < _highWater; i++)
            {
                hash.AppendInt32(_alive[i] ? 1 : 0);
            }
        }
    }
}
