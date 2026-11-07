using System;
using System.Collections.Generic;

namespace GodGame.Sim
{
    /// <summary>
    /// Where keyframes go when the memory budget is full (tech notes, section 6: cap the in-memory total and spill older
    /// keyframes to disk). The simulation never touches files; hosts supply this (GodGame.Sim.Hosting has a directory one).
    /// </summary>
    public interface IKeyframeSpill
    {
        void Write(int tick, byte[] keyframe);

        byte[] Read(int tick);

        void Delete(int tick);
    }

    /// <summary>
    /// The timeline's keyframes by tick: the newest stay in memory up to <see cref="BudgetBytes"/>; older ones go to the
    /// spill (or are dropped when there is none, so the timeline re-simulates further back instead). Ticks are kept in
    /// ascending order in a list, never a hashed collection, so nothing here depends on iteration order.
    /// </summary>
    public sealed class KeyframeStore
    {
        private readonly List<int> _ticks = new List<int>();
        private readonly List<byte[]?> _inMemory = new List<byte[]?>();
        private readonly IKeyframeSpill? _spill;
        private long _memoryBytes;

        public KeyframeStore(long budgetBytes, IKeyframeSpill? spill = null)
        {
            if (budgetBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(budgetBytes));
            }
            BudgetBytes = budgetBytes;
            _spill = spill;
        }

        public long BudgetBytes { get; }

        public long MemoryBytes => _memoryBytes;

        public int Count => _ticks.Count;

        /// <summary>Keyframe ticks in ascending order.</summary>
        public IReadOnlyList<int> Ticks => _ticks;

        /// <summary>Adds a keyframe after every one held (ticks only grow between <see cref="DropFrom"/> calls).</summary>
        public void Add(int tick, byte[] keyframe)
        {
            if (_ticks.Count > 0 && tick <= _ticks[_ticks.Count - 1])
            {
                throw new ArgumentException("Keyframes are added in tick order.", nameof(tick));
            }
            _ticks.Add(tick);
            _inMemory.Add(keyframe);
            _memoryBytes += keyframe.Length;
            // Oldest first out of memory: they are the least likely to be resumed from after an edit.
            for (int i = 0; _memoryBytes > BudgetBytes && i < _ticks.Count - 1; i++)
            {
                var bytes = _inMemory[i];
                if (bytes == null)
                {
                    continue;
                }
                _spill?.Write(_ticks[i], bytes);
                _inMemory[i] = null;
                _memoryBytes -= bytes.Length;
            }
        }

        /// <summary>The latest keyframe at or before <paramref name="tick"/>, or false when none is held.</summary>
        public bool TryGetAtOrBefore(int tick, out int keyframeTick, out byte[] keyframe)
        {
            for (int i = _ticks.Count - 1; i >= 0; i--)
            {
                if (_ticks[i] > tick)
                {
                    continue;
                }
                var bytes = _inMemory[i];
                if (bytes == null && _spill == null)
                {
                    continue; // dropped for the budget: look further back
                }
                keyframeTick = _ticks[i];
                keyframe = bytes ?? _spill!.Read(_ticks[i]);
                return true;
            }
            keyframeTick = 0;
            keyframe = Array.Empty<byte>();
            return false;
        }

        /// <summary>Forgets every keyframe after <paramref name="tick"/> (an edit made them stale).</summary>
        public void DropAfter(int tick)
        {
            for (int i = _ticks.Count - 1; i >= 0 && _ticks[i] > tick; i--)
            {
                var bytes = _inMemory[i];
                if (bytes != null)
                {
                    _memoryBytes -= bytes.Length;
                }
                else
                {
                    _spill?.Delete(_ticks[i]);
                }
                _ticks.RemoveAt(i);
                _inMemory.RemoveAt(i);
            }
        }

        /// <summary>Forgets every keyframe at or after <paramref name="tick"/>.</summary>
        public void DropFrom(int tick) => DropAfter(tick - 1);
    }
}
