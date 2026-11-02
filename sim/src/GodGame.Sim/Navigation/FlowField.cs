using System;
using System.Diagnostics;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// One flow field: for every cell, the cheapest cost to reach a goal area for one movement class, and the
    /// neighbour to step to next (tech notes, section 3). Thousands of soldiers sample the cell they stand in instead
    /// of each searching a path.
    /// <para>
    /// Costs come from <see cref="CostFields"/>. Steps go to the 8 neighbours; a step costs the mean of the two cells'
    /// costs times 10 east-west or north-south and times 14 diagonally (so √2 is 1.4), and a diagonal needs both cells
    /// it cuts past to be open. The distances are a pure function of the costs and the goal (Dijkstra's shortest
    /// distances are unique), and each direction is a pure function of its neighbours' distances, ties going to the
    /// first neighbour in <see cref="Offsets"/> order. So <see cref="Repair"/> after an edit gives exactly the field a
    /// full <see cref="Rebuild"/> would, which the tests check bit for bit.
    /// </para>
    /// <para>
    /// Derived state, like the cost fields: never hashed or snapshotted, rebuilt whenever the costs change.
    /// </para>
    /// </summary>
    public sealed class FlowField
    {
        /// <summary>Distance of a cell that can't reach the goal.</summary>
        public const long Unreachable = long.MaxValue;

        /// <summary>Direction of a goal cell: stay.</summary>
        public const byte AtGoal = 8;

        /// <summary>Direction of a cell that can't reach the goal (or is impassable itself).</summary>
        public const byte NoDirection = 255;

        /// <summary>Neighbour offsets, counter-clockwise from east; a direction is an index into this list.</summary>
        public static readonly (int Dx, int Dy)[] Offsets =
        {
            (1, 0),
            (1, 1),
            (0, 1),
            (-1, 1),
            (-1, 0),
            (-1, -1),
            (0, -1),
            (1, -1),
        };

        // Unit vectors for the eight directions; the diagonal component is √½ in Fix raw units, rounded.
        private const long Diagonal = 46341;
        private static readonly FixVec2[] Unit =
        {
            new FixVec2(Fix.One, Fix.Zero),
            new FixVec2(Fix.FromRaw(Diagonal), Fix.FromRaw(Diagonal)),
            new FixVec2(Fix.Zero, Fix.One),
            new FixVec2(Fix.FromRaw(-Diagonal), Fix.FromRaw(Diagonal)),
            new FixVec2(-Fix.One, Fix.Zero),
            new FixVec2(Fix.FromRaw(-Diagonal), Fix.FromRaw(-Diagonal)),
            new FixVec2(Fix.Zero, -Fix.One),
            new FixVec2(Fix.FromRaw(Diagonal), Fix.FromRaw(-Diagonal)),
        };

        private readonly CostFields _costs;
        private readonly Terrain _terrain;
        private readonly long[] _distance;
        private readonly byte[] _direction;

        // Scratch, reused by every rebuild: never read before it is written in the same rebuild.
        private readonly CellHeap _heap;
        private readonly int[] _mark;
        private readonly int[] _next;
        private readonly int[] _prev;
        private readonly int[] _delta;
        private int[] _bucketHead = new int[0];
        private int _stamp;
        private int[] _list;

        public FlowField(CostFields costs, MovementClass movementClass, CellRect goal)
        {
            _costs = costs ?? throw new ArgumentNullException(nameof(costs));
            _terrain = costs.Terrain;
            if (
                goal.IsEmpty
                || goal.MinX < 0
                || goal.MinY < 0
                || goal.MaxX >= _terrain.Columns
                || goal.MaxY >= _terrain.Rows
            )
            {
                throw new ArgumentOutOfRangeException(
                    nameof(goal),
                    "The goal must be a non-empty area on the map."
                );
            }
            MovementClass = movementClass;
            Goal = goal;
            int cells = _terrain.CellCount;
            _distance = new long[cells];
            _direction = new byte[cells];
            _mark = new int[cells];
            _next = new int[cells];
            _prev = new int[cells];
            _delta = new int[8];
            for (int d = 0; d < 8; d++)
            {
                _delta[d] = Offsets[d].Dy * _terrain.Columns + Offsets[d].Dx;
            }
            _list = new int[256];
            _heap = new CellHeap(1024);
        }

        public MovementClass MovementClass { get; }

        public CellRect Goal { get; }

        /// <summary>The terrain version the field last caught up with; Debug reads check it, like the cost fields.</summary>
        public int BuiltFromVersion { get; private set; } = -1;

        public Terrain Terrain => _terrain;

        /// <summary>Cost to the goal in tenths of a flat step's cost (a flat east step costs 1000 at base cost 100).</summary>
        public long Distance(int x, int y)
        {
            AssertFresh();
            return _distance[_terrain.Index(x, y)];
        }

        /// <summary>An index into <see cref="Offsets"/>, <see cref="AtGoal"/> or <see cref="NoDirection"/>.</summary>
        public byte Direction(int x, int y)
        {
            AssertFresh();
            return _direction[_terrain.Index(x, y)];
        }

        /// <summary>Row-major directions, for overlays and the route preview.</summary>
        public ReadOnlySpan<byte> Directions
        {
            get
            {
                AssertFresh();
                return _direction;
            }
        }

        /// <summary>Row-major distances.</summary>
        public ReadOnlySpan<long> Distances
        {
            get
            {
                AssertFresh();
                return _distance;
            }
        }

        /// <summary>The unit vector toward the next cell from the cell a position falls in; zero at the goal or when it is out of reach.</summary>
        public FixVec2 Sample(FixVec2 position)
        {
            _terrain.CellOf(position, out int x, out int y);
            return DirectionVector(Direction(x, y));
        }

        public static FixVec2 DirectionVector(byte direction) =>
            direction < AtGoal ? Unit[direction] : FixVec2.Zero;

        [Conditional("DEBUG")]
        private void AssertFresh() =>
            Debug.Assert(
                BuiltFromVersion == _costs.BuiltFromVersion
                    && _costs.BuiltFromVersion == _terrain.Version,
                "Flow field read after a cost change without Rebuild or Repair."
            );

        /// <summary>Recomputes every cell from the goal outward.</summary>
        public void Rebuild()
        {
            var costs = _costs.Of(MovementClass);
            int cells = _distance.Length;
            int maxCost = 0;
            for (int i = 0; i < cells; i++)
            {
                _distance[i] = Unreachable;
                int c = costs[i];
                if (c != CostFields.Impassable && c > maxCost)
                {
                    maxCost = c;
                }
            }
            // Dial's algorithm: a step costs at most maxCost × 2 × 7, so every queued distance lies within
            // [current, current + maxStep] and a ring of maxStep + 1 buckets, keyed by distance modulo its size, keeps
            // them in order. Each cell sits in at most one bucket (doubly linked), so a cheaper find just moves it.
            int ring = maxCost * 14 + 1;
            if (_bucketHead.Length < ring)
            {
                _bucketHead = new int[ring];
            }
            for (int b = 0; b < ring; b++)
            {
                _bucketHead[b] = -1;
            }
            int queued = 0;
            for (int y = Goal.MinY; y <= Goal.MaxY; y++)
            {
                for (int x = Goal.MinX; x <= Goal.MaxX; x++)
                {
                    int i = _terrain.Index(x, y);
                    if (costs[i] != CostFields.Impassable)
                    {
                        _distance[i] = 0;
                        Link(i, 0, ring);
                        queued++;
                    }
                }
            }
            int columns = _terrain.Columns;
            long current = 0;
            while (queued > 0)
            {
                int bucket = (int)(current % ring);
                int cell = _bucketHead[bucket];
                if (cell < 0)
                {
                    current++;
                    continue;
                }
                Unlink(cell, bucket);
                queued--;
                int cx = cell % columns;
                int cy = cell / columns;
                bool interior = IsInterior(cx, cy);
                for (int d = 0; d < 8; d++)
                {
                    long w = interior
                        ? InteriorStepCost(costs, cell, d)
                        : StepCost(costs, cx, cy, d);
                    if (w < 0)
                    {
                        continue;
                    }
                    int n = cell + _delta[d];
                    long nd = current + w;
                    long old = _distance[n];
                    if (nd < old)
                    {
                        // A finished cell has a distance at most current and is never improved, so a finite old
                        // distance here belongs to a queued cell: move it to its new bucket.
                        if (old != Unreachable)
                        {
                            Unlink(n, (int)(old % ring));
                        }
                        else
                        {
                            queued++;
                        }
                        _distance[n] = nd;
                        Link(n, nd, ring);
                    }
                }
            }
            for (int y = 0; y < _terrain.Rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    _direction[_terrain.Index(x, y)] = ComputeDirection(costs, x, y);
                }
            }
            BuiltFromVersion = _costs.BuiltFromVersion;
        }

        private void Link(int cell, long distance, int ring)
        {
            int bucket = (int)(distance % ring);
            int head = _bucketHead[bucket];
            _prev[cell] = -1;
            _next[cell] = head;
            if (head >= 0)
            {
                _prev[head] = cell;
            }
            _bucketHead[bucket] = cell;
        }

        private void Unlink(int cell, int bucket)
        {
            int prev = _prev[cell];
            int next = _next[cell];
            if (prev >= 0)
            {
                _next[prev] = next;
            }
            else
            {
                _bucketHead[bucket] = next;
            }
            if (next >= 0)
            {
                _prev[next] = prev;
            }
        }

        /// <summary>
        /// Brings the field up to date after the cost fields were rebuilt over <paramref name="dirtyCosts"/> (the
        /// rectangle passed to <see cref="CostFields.Rebuild"/>). Cells whose route ran through the changed area are
        /// cleared and refilled from their neighbours; cheaper routes through the area spread outward. Same result
        /// as <see cref="Rebuild"/>, at a cost that grows with the cells whose route changed, not with the map.
        /// </summary>
        public void Repair(CellRect dirtyCosts)
        {
            if (BuiltFromVersion < 0)
            {
                Rebuild();
                return;
            }
            if (dirtyCosts.IsEmpty)
            {
                BuiltFromVersion = _costs.BuiltFromVersion;
                return;
            }
            var costs = _costs.Of(MovementClass);
            // A cost change alters every step into or out of the cell and the diagonals that cut past it: one cell further.
            var changed = new CellRect(
                System.Math.Max(0, dirtyCosts.MinX - 1),
                System.Math.Max(0, dirtyCosts.MinY - 1),
                System.Math.Min(_terrain.Columns - 1, dirtyCosts.MaxX + 1),
                System.Math.Min(_terrain.Rows - 1, dirtyCosts.MaxY + 1)
            );

            // 1. Clear the changed cells and everything whose next step leads into a cleared cell.
            int invalidStamp = NextStamp();
            int count = 0;
            for (int y = changed.MinY; y <= changed.MaxY; y++)
            {
                for (int x = changed.MinX; x <= changed.MaxX; x++)
                {
                    int i = _terrain.Index(x, y);
                    _mark[i] = invalidStamp;
                    count = Append(count, i);
                }
            }
            for (int head = 0; head < count; head++)
            {
                int cell = _list[head];
                int cx = cell % _terrain.Columns;
                int cy = cell / _terrain.Columns;
                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + Offsets[d].Dx;
                    int ny = cy + Offsets[d].Dy;
                    if (!_terrain.InBounds(nx, ny))
                    {
                        continue;
                    }
                    int n = _terrain.Index(nx, ny);
                    // The neighbour's next step is this cell when its direction is the opposite of d.
                    if (_mark[n] != invalidStamp && _direction[n] == ((d + 4) & 7))
                    {
                        _mark[n] = invalidStamp;
                        count = Append(count, n);
                    }
                }
            }
            for (int k = 0; k < count; k++)
            {
                _distance[_list[k]] = Unreachable;
            }

            // 2. Seed each cleared cell from its neighbours (or as a goal), then spread as in a full build.
            _heap.Clear();
            for (int k = 0; k < count; k++)
            {
                int cell = _list[k];
                if (costs[cell] == CostFields.Impassable)
                {
                    continue;
                }
                int cx = cell % _terrain.Columns;
                int cy = cell / _terrain.Columns;
                long best = InGoal(cx, cy) ? 0 : Unreachable;
                for (int d = 0; d < 8 && best != 0; d++)
                {
                    long w = StepCost(costs, cx, cy, d);
                    if (w < 0)
                    {
                        continue;
                    }
                    long nd = _distance[_terrain.Index(cx + Offsets[d].Dx, cy + Offsets[d].Dy)];
                    if (nd != Unreachable && nd + w < best)
                    {
                        best = nd + w;
                    }
                }
                if (best != Unreachable)
                {
                    _distance[cell] = best;
                    _heap.Push(best, cell);
                }
            }
            int cleared = count;
            count = Propagate(costs, cleared);

            // 3. Directions change where a cell's own distance or a neighbour's distance or step cost changed.
            int directionStamp = NextStamp();
            for (int k = 0; k < count; k++)
            {
                int cell = _list[k];
                int cx = cell % _terrain.Columns;
                int cy = cell / _terrain.Columns;
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = cx + dx;
                        int ny = cy + dy;
                        if (!_terrain.InBounds(nx, ny))
                        {
                            continue;
                        }
                        int n = _terrain.Index(nx, ny);
                        if (_mark[n] != directionStamp)
                        {
                            _mark[n] = directionStamp;
                            _direction[n] = ComputeDirection(costs, nx, ny);
                        }
                    }
                }
            }
            BuiltFromVersion = _costs.BuiltFromVersion;
        }

        // Dijkstra from whatever is on the heap (repairs, whose seeds lie at many distances; a full build uses the
        // bucket ring instead). Ties pop in cell order, so the run is the same everywhere (the distances would be the
        // same anyway; the fixed order keeps the work, and the changed list, identical too).
        // Every cell whose distance drops is appended to _list after listStart; returns the list length.
        private int Propagate(ReadOnlySpan<ushort> costs, int listStart)
        {
            int count = listStart;
            while (_heap.Count > 0)
            {
                _heap.Pop(out long dist, out int cell);
                if (dist != _distance[cell])
                {
                    continue; // a stale entry: the cell was reached more cheaply after this was pushed
                }
                int cx = cell % _terrain.Columns;
                int cy = cell / _terrain.Columns;
                bool interior = IsInterior(cx, cy);
                for (int d = 0; d < 8; d++)
                {
                    long w = interior
                        ? InteriorStepCost(costs, cell, d)
                        : StepCost(costs, cx, cy, d);
                    if (w < 0)
                    {
                        continue;
                    }
                    int n = cell + _delta[d];
                    long nd = dist + w;
                    if (nd < _distance[n])
                    {
                        _distance[n] = nd;
                        _heap.Push(nd, n);
                        count = Append(count, n);
                    }
                }
            }
            return count;
        }

        private byte ComputeDirection(ReadOnlySpan<ushort> costs, int x, int y)
        {
            int i = _terrain.Index(x, y);
            if (_distance[i] == Unreachable)
            {
                return NoDirection;
            }
            if (InGoal(x, y))
            {
                return AtGoal;
            }
            long best = Unreachable;
            byte bestDirection = NoDirection;
            bool interior = IsInterior(x, y);
            for (int d = 0; d < 8; d++)
            {
                long w = interior ? InteriorStepCost(costs, i, d) : StepCost(costs, x, y, d);
                if (w < 0)
                {
                    continue;
                }
                long nd = _distance[i + _delta[d]];
                if (nd != Unreachable && nd + w < best)
                {
                    best = nd + w;
                    bestDirection = (byte)d;
                }
            }
            return bestDirection;
        }

        private bool IsInterior(int x, int y) =>
            x > 0 && y > 0 && x < _terrain.Columns - 1 && y < _terrain.Rows - 1;

        /// <summary><see cref="StepCost"/> for a cell off the map edge, by index arithmetic alone.</summary>
        private long InteriorStepCost(ReadOnlySpan<ushort> costs, int cell, int d)
        {
            int from = costs[cell];
            int to = costs[cell + _delta[d]];
            if (from == CostFields.Impassable || to == CostFields.Impassable)
            {
                return -1;
            }
            if ((d & 1) == 0)
            {
                return (from + to) * 5L;
            }
            if (
                costs[cell + Offsets[d].Dx] == CostFields.Impassable
                || costs[cell + Offsets[d].Dy * _terrain.Columns] == CostFields.Impassable
            )
            {
                return -1;
            }
            return (from + to) * 7L;
        }

        /// <summary>The cost of the step from (x, y) in direction d, or -1 when it leaves the map or either end (or a cut corner) is impassable. Symmetric.</summary>
        private long StepCost(ReadOnlySpan<ushort> costs, int x, int y, int d)
        {
            int nx = x + Offsets[d].Dx;
            int ny = y + Offsets[d].Dy;
            if (!_terrain.InBounds(nx, ny))
            {
                return -1;
            }
            int from = costs[_terrain.Index(x, y)];
            int to = costs[_terrain.Index(nx, ny)];
            if (from == CostFields.Impassable || to == CostFields.Impassable)
            {
                return -1;
            }
            if ((d & 1) == 0)
            {
                return (from + to) * 5L;
            }
            if (
                costs[_terrain.Index(nx, y)] == CostFields.Impassable
                || costs[_terrain.Index(x, ny)] == CostFields.Impassable
            )
            {
                return -1;
            }
            return (from + to) * 7L;
        }

        private bool InGoal(int x, int y) =>
            x >= Goal.MinX && x <= Goal.MaxX && y >= Goal.MinY && y <= Goal.MaxY;

        private int NextStamp()
        {
            _stamp++;
            if (_stamp == int.MaxValue)
            {
                Array.Clear(_mark, 0, _mark.Length);
                _stamp = 1;
            }
            return _stamp;
        }

        private int Append(int count, int cell)
        {
            if (count == _list.Length)
            {
                Array.Resize(ref _list, _list.Length * 2);
            }
            _list[count] = cell;
            return count + 1;
        }

        /// <summary>A binary min-heap of (distance, cell), ordered by distance and then cell index: a total order.</summary>
        private sealed class CellHeap
        {
            private long[] _keys;
            private int[] _cells;

            public CellHeap(int capacity)
            {
                _keys = new long[capacity];
                _cells = new int[capacity];
            }

            public int Count { get; private set; }

            public void Clear() => Count = 0;

            public void Push(long key, int cell)
            {
                if (Count == _keys.Length)
                {
                    Array.Resize(ref _keys, _keys.Length * 2);
                    Array.Resize(ref _cells, _cells.Length * 2);
                }
                int i = Count++;
                while (i > 0)
                {
                    int parent = (i - 1) >> 1;
                    if (!Less(key, cell, _keys[parent], _cells[parent]))
                    {
                        break;
                    }
                    _keys[i] = _keys[parent];
                    _cells[i] = _cells[parent];
                    i = parent;
                }
                _keys[i] = key;
                _cells[i] = cell;
            }

            public void Pop(out long key, out int cell)
            {
                key = _keys[0];
                cell = _cells[0];
                int last = --Count;
                if (last == 0)
                {
                    return;
                }
                long k = _keys[last];
                int c = _cells[last];
                int i = 0;
                while (true)
                {
                    int child = 2 * i + 1;
                    if (child >= last)
                    {
                        break;
                    }
                    if (
                        child + 1 < last
                        && Less(_keys[child + 1], _cells[child + 1], _keys[child], _cells[child])
                    )
                    {
                        child++;
                    }
                    if (!Less(_keys[child], _cells[child], k, c))
                    {
                        break;
                    }
                    _keys[i] = _keys[child];
                    _cells[i] = _cells[child];
                    i = child;
                }
                _keys[i] = k;
                _cells[i] = c;
            }

            private static bool Less(long ka, int ca, long kb, int cb) =>
                ka < kb || (ka == kb && ca < cb);
        }
    }
}
