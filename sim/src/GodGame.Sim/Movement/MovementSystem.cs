using System;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// The movement step (tech notes, section 3): every soldier samples its regiment's flow field, blends in its
    /// regiment's mean velocity (alignment), brakes for whoever stands in its way (queueing), is pushed apart from
    /// overlapping neighbours (separation; the overlap it feels is its crowd pressure) and never steps into a cell its
    /// movement class can't enter (obstacle avoidance). Per soldier this is O(1): one field sample and one
    /// spatial-hash query.
    /// <para>
    /// Determinism: the spatial hash is built here, at the start of the step, from the positions the tick started
    /// with; every soldier reads only those and writes its own slot of scratch arrays, which are committed after the
    /// parallel pass. Sums over neighbours are integer sums, so their order doesn't matter, and the neighbour list
    /// itself comes in a fixed order. Numbers in this class (acceleration, braking distance, push share) are
    /// placeholders no spec has set; they are tuned for queues that read well, not for realism.
    /// </para>
    /// </summary>
    public sealed class MovementSystem : ISimSystem
    {
        /// <summary>At most this many neighbours per soldier are considered (a fixed buffer, never machine-dependent).</summary>
        public const int MaxNeighbours = 64;

        public const int ChunkSize = 512;

        /// <summary>Spatial-hash cell edge.</summary>
        public static readonly Fix HashCellSize = Fix.FromInt(2);

        /// <summary>The gap at which a soldier walks at full speed behind someone standing still; it brakes in proportion below (placeholder).</summary>
        public static readonly Fix BrakeDistance = Fix.FromRatio(1, 2);

        /// <summary>How many flow-field steps ahead a soldier aims (placeholder).</summary>
        public const int LookAheadCells = 4;

        /// <summary>Share of its speed a soldier keeps when someone stands right in front (placeholder).</summary>
        public static readonly Fix CreepShare = Fix.FromRatio(1, 8);

        /// <summary>Share of an overlap each of the two soldiers moves apart per tick (placeholder).</summary>
        public static readonly Fix PushShare = Fix.FromRatio(2, 5);

        private readonly Navigation _navigation;
        private readonly Fix[] _typeSpeed; // metres per tick
        private readonly long[] _typeRadiusRaw;
        private readonly MovementClass[] _typeClass;
        private readonly long _maxRadiusRaw;

        // Scratch: overwritten every run before it is read.
        private SpatialHash? _hash;
        private FixVec2[] _newPosition = new FixVec2[0];
        private FixVec2[] _newVelocity = new FixVec2[0];
        private int[] _pressure = new int[0];
        private FixVec2[] _regimentMean = new FixVec2[0];
        private long[] _sumX = new long[0];
        private long[] _sumY = new long[0];
        private byte[]?[] _regimentDirections = new byte[]?[0];
        private ushort[][] _typeCosts = new ushort[0][];

        public MovementSystem(Navigation navigation)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            var types = navigation.Rules.UnitTypes;
            _typeSpeed = new Fix[types.Count];
            _typeRadiusRaw = new long[types.Count];
            _typeClass = new MovementClass[types.Count];
            for (int t = 0; t < types.Count; t++)
            {
                _typeSpeed[t] = Fix.FromRatio(
                    types[t].SpeedMmPerSecond,
                    1000L * SimConstants.TicksPerSecond
                );
                _typeRadiusRaw[t] = Fix.FromRatio(types[t].RadiusMm, 1000).Raw;
                _typeClass[t] = types[t].MovementClass;
                _maxRadiusRaw = System.Math.Max(_maxRadiusRaw, _typeRadiusRaw[t]);
            }
        }

        public TickStep Step => TickStep.Movement;

        public TickRate Rate => TickRate.EveryTick;

        /// <summary>
        /// Each soldier's crowd pressure from the last run: the summed overlap with its neighbours, in mm. Derived (from
        /// the positions this tick started with); later steps of the same tick may read it (crush, pushes over edges).
        /// </summary>
        public ReadOnlySpan<int> Pressure => _pressure;

        public void Run(TickContext context)
        {
            var world = context.World;
            int capacity = world.Units.Capacity;
            if (_newPosition.Length < capacity)
            {
                _newPosition = new FixVec2[capacity];
                _newVelocity = new FixVec2[capacity];
                _pressure = new int[capacity];
            }
            if (_hash == null)
            {
                _hash = new SpatialHash(
                    _navigation.Map.Width,
                    _navigation.Map.Depth,
                    HashCellSize,
                    capacity
                );
            }
            _hash.Build(world);
            ComputeRegimentMeans(world);
            if (_typeCosts.Length != _typeClass.Length)
            {
                _typeCosts = new ushort[_typeClass.Length][];
            }
            for (int t = 0; t < _typeClass.Length; t++)
            {
                _typeCosts[t] = _navigation.Costs.FreshArray(_typeClass[t]);
            }

            int highWater = world.Units.HighWater;
            context.Runner.For(
                highWater,
                ChunkSize,
                new Kernel(this, world, _hash, _navigation.Terrain)
            );
            for (int u = 0; u < highWater; u++)
            {
                if (world.Units.IsAlive(u))
                {
                    world.Position[u] = _newPosition[u];
                    world.Velocity[u] = _newVelocity[u];
                }
            }
        }

        // Serial: integer sums per regiment, then one division each.
        private void ComputeRegimentMeans(World world)
        {
            int regiments = world.Regiments.Capacity;
            if (_regimentMean.Length < regiments)
            {
                _regimentMean = new FixVec2[regiments];
                _regimentDirections = new byte[]?[regiments];
                _sumX = new long[regiments];
                _sumY = new long[regiments];
            }
            var sumX = _sumX;
            var sumY = _sumY;
            Array.Clear(sumX, 0, regiments);
            Array.Clear(sumY, 0, regiments);
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (world.Units.IsAlive(u))
                {
                    int r = world.UnitRegiment[u];
                    sumX[r] += world.Velocity[u].X.Raw;
                    sumY[r] += world.Velocity[u].Y.Raw;
                }
            }
            for (int r = 0; r < regiments; r++)
            {
                int alive = world.RegimentAlive[r];
                _regimentMean[r] =
                    alive > 0
                        ? new FixVec2(Fix.FromRaw(sumX[r] / alive), Fix.FromRaw(sumY[r] / alive))
                        : FixVec2.Zero;
                var field = world.Regiments.IsAlive(r) ? _navigation.FieldOf(r) : null;
                _regimentDirections[r] = field?.FreshDirections();
            }
        }

        private readonly struct Kernel : IChunkKernel
        {
            private readonly MovementSystem _s;
            private readonly World _world;
            private readonly SpatialHash _hash;
            private readonly Terrain _terrain;

            public Kernel(MovementSystem system, World world, SpatialHash hash, Terrain terrain)
            {
                _s = system;
                _world = world;
                _hash = hash;
                _terrain = terrain;
            }

            public void Execute(int start, int end)
            {
                Span<int> neighbours = stackalloc int[MaxNeighbours];
                var pool = _world.Units;
                var position = _world.Position;
                var velocity = _world.Velocity;
                var unitType = _world.UnitType;
                var unitRegiment = _world.UnitRegiment;
                Fix maxX = _s._navigation.Map.Width - Fix.FromRaw(1);
                Fix maxY = _s._navigation.Map.Depth - Fix.FromRaw(1);
                for (int u = start; u < end; u++)
                {
                    if (!pool.IsAlive(u))
                    {
                        continue;
                    }
                    int type = unitType[u];
                    int regiment = unitRegiment[u];
                    FixVec2 p = position[u];
                    Fix speed = _s._typeSpeed[type];
                    long radius = _s._typeRadiusRaw[type];
                    var directions = _s._regimentDirections[regiment];
                    FixVec2 heading = directions == null ? FixVec2.Zero : Heading(directions, p);

                    // Neighbours: overlaps push apart; anyone in the path ahead makes this soldier brake.
                    long reach = radius + _s._maxRadiusRaw + BrakeDistance.Raw;
                    int found = _hash.QueryRadius(_world, p, Fix.FromRaw(reach), neighbours);
                    int count = System.Math.Min(found, MaxNeighbours);
                    long pushX = 0;
                    long pushY = 0;
                    long pressure = 0;
                    long limit = speed.Raw; // the fastest this soldier may walk along its heading, raw m/tick
                    for (int k = 0; k < count; k++)
                    {
                        int j = neighbours[k];
                        if (j == u)
                        {
                            continue;
                        }
                        long dx = position[j].X.Raw - p.X.Raw;
                        long dy = position[j].Y.Raw - p.Y.Raw;
                        long contact = radius + _s._typeRadiusRaw[unitType[j]];
                        long distSq = dx * dx + dy * dy; // within the query radius (a few metres), far inside a long
                        if (distSq < contact * contact)
                        {
                            long dist = IntMath.Sqrt((ulong)distSq);
                            long overlap = contact - dist;
                            pressure += overlap;
                            long share = overlap * PushShare.Raw >> Fix.FractionalBits;
                            if (dist == 0)
                            {
                                // Standing on the same spot: split along one of eight directions picked from the pair's
                                // slots (so a heap of soldiers fans out in 2D), the lower slot one way, the higher the
                                // other, the same on every machine.
                                int low = System.Math.Min(u, j);
                                int high = System.Math.Max(u, j);
                                var split = FlowField.DirectionVector(
                                    (byte)((low * 31 + high) & 7)
                                );
                                long sign = u == low ? -1 : 1;
                                pushX += sign * (split.X.Raw * share >> Fix.FractionalBits);
                                pushY += sign * (split.Y.Raw * share >> Fix.FractionalBits);
                            }
                            else
                            {
                                pushX -= dx * share / dist;
                                pushY -= dy * share / dist;
                            }
                        }
                        if (heading != FixVec2.Zero)
                        {
                            // Distance along the heading and across it, in raw metres (heading is a unit vector).
                            long along =
                                (dx * heading.X.Raw + dy * heading.Y.Raw) >> Fix.FractionalBits;
                            long across =
                                (dx * heading.Y.Raw - dy * heading.X.Raw) >> Fix.FractionalBits;
                            if (along > 0 && across < contact && across > -contact)
                            {
                                // Car-following: keep pace with the one ahead, plus what the gap allows.
                                long gap = System.Math.Max(0, along - contact);
                                long ahead =
                                    (
                                        velocity[j].X.Raw * heading.X.Raw
                                        + velocity[j].Y.Raw * heading.Y.Raw
                                    ) >> Fix.FractionalBits;
                                long allowed =
                                    System.Math.Max(0, ahead) + speed.Raw * gap / BrakeDistance.Raw;
                                limit = System.Math.Min(limit, allowed);
                            }
                        }
                    }

                    // Steering: the field's heading at full speed, slowed by the gap ahead, blended with the regiment.
                    // A blocked soldier still creeps forward at CreepShare of its speed: the crowd behind presses on
                    // (crowd pressure), and a jam where everyone waits for everyone can always unwind.
                    limit = System.Math.Max(
                        limit,
                        speed.Raw * CreepShare.Raw >> Fix.FractionalBits
                    );
                    FixVec2 desired = heading * Fix.FromRaw(limit);
                    desired = (desired * 3 + _s._regimentMean[regiment]) / 4;
                    FixVec2 v = velocity[u] + (desired - velocity[u]) / 4;

                    // The push is capped at one body radius per tick, so a dense crowd can't fling anyone.
                    var push = new FixVec2(Fix.FromRaw(pushX), Fix.FromRaw(pushY));
                    long pushSq = pushX * pushX + pushY * pushY;
                    if (pushSq > radius * radius)
                    {
                        push = push.WithLength(Fix.FromRaw(radius));
                    }

                    var cls = _s._typeCosts[type];
                    FixVec2 target = Clamp(p + v + push, maxX, maxY);
                    if (Passable(cls, p) && !Passable(cls, target))
                    {
                        // Blocked: nobody is shoved or walks into a closed cell. Slide the whole move along whichever
                        // axis is open (so a soldier pressed against a wall still gets out of the crush), else walk
                        // without the push, sliding the same way, and stop only the blocked part of the walk. (A
                        // soldier already standing in a closed cell is let walk out.)
                        FixVec2 walk = Clamp(p + v, maxX, maxY);
                        if (Passable(cls, new FixVec2(target.X, p.Y)))
                        {
                            target = new FixVec2(target.X, p.Y);
                            v = new FixVec2(v.X, Fix.Zero);
                        }
                        else if (Passable(cls, new FixVec2(p.X, target.Y)))
                        {
                            target = new FixVec2(p.X, target.Y);
                            v = new FixVec2(Fix.Zero, v.Y);
                        }
                        else if (Passable(cls, walk))
                        {
                            target = walk;
                        }
                        else if (Passable(cls, new FixVec2(walk.X, p.Y)))
                        {
                            target = new FixVec2(walk.X, p.Y);
                            v = new FixVec2(v.X, Fix.Zero);
                        }
                        else if (Passable(cls, new FixVec2(p.X, walk.Y)))
                        {
                            target = new FixVec2(p.X, walk.Y);
                            v = new FixVec2(Fix.Zero, v.Y);
                        }
                        else
                        {
                            target = p;
                            v = FixVec2.Zero;
                        }
                    }
                    _s._newPosition[u] = target;
                    _s._newVelocity[u] = v;
                    _s._pressure[u] = (int)
                        System.Math.Min(int.MaxValue, pressure * 1000 >> Fix.FractionalBits);
                }
            }

            /// <summary>
            /// Toward the centre of the cell <see cref="LookAheadCells"/> steps down the flow: straighter than the
            /// cell's own 8-way direction, so a crowd fans out across a gap instead of filing along its edges.
            /// </summary>
            private FixVec2 Heading(byte[] directions, FixVec2 p)
            {
                _terrain.CellOf(p, out int x, out int y);
                byte first = directions[_terrain.Index(x, y)];
                if (first >= FlowField.AtGoal)
                {
                    return FixVec2.Zero;
                }
                for (int step = 0; step < LookAheadCells; step++)
                {
                    byte d = directions[_terrain.Index(x, y)];
                    if (d >= FlowField.AtGoal)
                    {
                        break;
                    }
                    var (dx, dy) = FlowField.Offset(d);
                    x += dx;
                    y += dy;
                }
                long cell = Fix.FromRatio(_terrain.CellSizeMm, 1000).Raw;
                var aim = new FixVec2(
                    Fix.FromRaw(x * cell + cell / 2) - p.X,
                    Fix.FromRaw(y * cell + cell / 2) - p.Y
                );
                return aim == FixVec2.Zero
                    ? FlowField.DirectionVector(first)
                    : aim.WithLength(Fix.One);
            }

            private static FixVec2 Clamp(FixVec2 p, Fix maxX, Fix maxY) =>
                new FixVec2(Fix.Clamp(p.X, Fix.Zero, maxX), Fix.Clamp(p.Y, Fix.Zero, maxY));

            private bool Passable(ushort[] costs, FixVec2 position) =>
                costs[CellIndex(position)] != CostFields.Impassable;

            private int CellIndex(FixVec2 position)
            {
                _terrain.CellOf(position, out int x, out int y);
                return _terrain.Index(x, y);
            }
        }
    }
}
