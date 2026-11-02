using System;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// The battle's flow fields, one per (movement class, goal) that some regiment needs, kept in the order they were
    /// first asked for (no dictionary, so iteration order is fixed). Builds and repairs run one field per parallel
    /// chunk: each field owns its arrays and scratch, so chunks never share writes.
    /// </summary>
    public sealed class FlowFieldSet
    {
        private readonly CostFields _costs;
        private FlowField[] _fields = new FlowField[8];

        public FlowFieldSet(CostFields costs)
        {
            _costs = costs ?? throw new ArgumentNullException(nameof(costs));
        }

        public int Count { get; private set; }

        public FlowField this[int index] =>
            index >= 0 && index < Count
                ? _fields[index]
                : throw new ArgumentOutOfRangeException(nameof(index));

        /// <summary>The index of the field for this class and goal, or -1.</summary>
        public int IndexOf(MovementClass movementClass, CellRect goal)
        {
            for (int i = 0; i < Count; i++)
            {
                var f = _fields[i];
                if (
                    f.MovementClass == movementClass
                    && f.Goal.MinX == goal.MinX
                    && f.Goal.MinY == goal.MinY
                    && f.Goal.MaxX == goal.MaxX
                    && f.Goal.MaxY == goal.MaxY
                )
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>The field's index, adding an unbuilt field if there is none yet; call <see cref="BuildPending"/> before reading it.</summary>
        public int GetOrAdd(MovementClass movementClass, CellRect goal)
        {
            int index = IndexOf(movementClass, goal);
            if (index >= 0)
            {
                return index;
            }
            if (Count == _fields.Length)
            {
                Array.Resize(ref _fields, _fields.Length * 2);
            }
            _fields[Count] = new FlowField(_costs, movementClass, goal);
            return Count++;
        }

        /// <summary>Builds every field that has never been built.</summary>
        public void BuildPending(IParallelRunner runner)
        {
            if (runner == null)
            {
                throw new ArgumentNullException(nameof(runner));
            }
            runner.For(Count, 1, new BuildKernel(_fields, pendingOnly: true));
        }

        /// <summary>Rebuilds every field from scratch.</summary>
        public void RebuildAll(IParallelRunner runner)
        {
            if (runner == null)
            {
                throw new ArgumentNullException(nameof(runner));
            }
            runner.For(Count, 1, new BuildKernel(_fields, pendingOnly: false));
        }

        /// <summary>Repairs every field after the cost fields were rebuilt over <paramref name="dirtyCosts"/>.</summary>
        public void Repair(CellRect dirtyCosts, IParallelRunner runner)
        {
            if (runner == null)
            {
                throw new ArgumentNullException(nameof(runner));
            }
            runner.For(Count, 1, new RepairKernel(_fields, dirtyCosts));
        }

        private readonly struct BuildKernel : IChunkKernel
        {
            private readonly FlowField[] _fields;
            private readonly bool _pendingOnly;

            public BuildKernel(FlowField[] fields, bool pendingOnly)
            {
                _fields = fields;
                _pendingOnly = pendingOnly;
            }

            public void Execute(int start, int end)
            {
                for (int i = start; i < end; i++)
                {
                    if (!_pendingOnly || !_fields[i].IsBuilt)
                    {
                        _fields[i].Rebuild();
                    }
                }
            }
        }

        private readonly struct RepairKernel : IChunkKernel
        {
            private readonly FlowField[] _fields;
            private readonly CellRect _dirty;

            public RepairKernel(FlowField[] fields, CellRect dirty)
            {
                _fields = fields;
                _dirty = dirty;
            }

            public void Execute(int start, int end)
            {
                for (int i = start; i < end; i++)
                {
                    _fields[i].Repair(_dirty);
                }
            }
        }
    }
}
