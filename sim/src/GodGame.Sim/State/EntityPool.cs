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
