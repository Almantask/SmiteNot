using System;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// The movement step (tech notes, sections 3 and 4): each regiment's officer follows its flow field and the others
    /// keep their formation slots behind him (or follow the field, blending in the regiment's mean velocity, while
    /// their slot is out of reach); every soldier brakes for whoever stands in its way (queueing), is pushed apart from
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
        /// <summary>
        /// At most this many neighbours per soldier are considered (a fixed buffer, never machine-dependent). The
        /// search reaches about 2.4 m for infantry; the tightest crush seen in tests holds about 60 in that circle, and
        /// past the buffer the north-east neighbours would be dropped first (cell order), so keep it well above that.
        /// </summary>
        public const int MaxNeighbours = 128;

        public const int ChunkSize = 512;

        /// <summary>Spatial-hash cell edge.</summary>
        public static readonly Fix HashCellSize = Fix.FromRatio(MapData.NeighbourGridCellMm, 1000);

        /// <summary>The gap at which a soldier walks at full speed behind someone standing still; it brakes in proportion below (placeholder).</summary>
        public static readonly Fix BrakeDistance = Fix.FromRatio(3, 10);

        /// <summary>How many flow-field steps ahead a soldier aims (placeholder).</summary>
        public const int LookAheadCells = 4;

        /// <summary>Share of its speed a soldier keeps when someone stands right in front (placeholder).</summary>
        public static readonly Fix CreepShare = Fix.FromRatio(1, 8);

        /// <summary>A soldier already overlapping its neighbours by this much in total stops creeping (placeholder).</summary>
        public static readonly Fix CreepPressureLimit = Fix.FromRatio(1, 4);

        /// <summary>Share of an overlap each of the two soldiers moves apart per tick (placeholder).</summary>
        public static readonly Fix PushShare = Fix.FromRatio(2, 5);

        /// <summary>The officer walks at this share of the type's speed, so the ranks can keep up (placeholder).</summary>
        public static readonly Fix OfficerPace = Fix.FromRatio(9, 10);

        /// <summary>
        /// The officer's pace while his regiment straggles: slow enough for the stragglers to catch up, never a halt,
        /// so a regiment re-forming beyond a gap or a bridge doesn't block the way out for the ones behind (placeholder).
        /// </summary>
        public static readonly Fix WaitingPace = Fix.FromRatio(1, 2);

        /// <summary>Share of the way to its slot a soldier closes per tick, on top of the officer's velocity (placeholder).</summary>
        public static readonly Fix SlotGain = Fix.FromRatio(1, 8);

        /// <summary>
        /// A soldier further from its slot than this, or whose slot lies in a closed cell (a narrow bridge, a gap in a
        /// wall), follows the flow field instead and rejoins once the slot is in reach again (placeholder).
        /// </summary>
        public static readonly Fix ReformDistance = Fix.FromInt(20);

        /// <summary>How fast the formation turns toward the officer's heading: 1.5° per tick, 30° per second (placeholder).</summary>
        public const int MaxTurnUnitsPerTick = Angle.UnitsPerTurn * 15 / 3600;

        private readonly Navigation _navigation;
        private readonly Fix[] _typeSpeed; // metres per tick
        private readonly long[] _typeRadiusRaw;
        private readonly MovementClass[] _typeClass;
        private readonly Fix[] _typeFileSpacing;
        private readonly Fix[] _typeRankSpacing;
        private readonly long _maxRadiusRaw;
        private readonly long _quarterCellRaw;
        private readonly long _mapDiagonalRaw;

        // Scratch: overwritten every run before it is read.
        private SpatialHash? _hash;
        private FixVec2[] _newPosition = new FixVec2[0];
        private FixVec2[] _newVelocity = new FixVec2[0];
        private int[] _pressure = new int[0];
        private FixVec2[] _regimentMean = new FixVec2[0];
        private long[] _sumX = new long[0];
        private long[] _sumY = new long[0];
        private byte[]?[] _regimentDirections = new byte[]?[0];
        private long[]?[] _regimentDistances = new long[]?[0];
        private FixVec2[] _anchorPosition = new FixVec2[0];
        private FixVec2[] _anchorVelocity = new FixVec2[0];
        private FixVec2[] _formationOrigin = new FixVec2[0]; // where slot 0 would stand: the officer, less his own slot's offset
        private bool[] _leaderless = new bool[0];
        private bool[] _officerWaits = new bool[0];
        private int[] _stragglers = new int[0];
        private ushort[][] _typeCosts = new ushort[0][];

        public MovementSystem(Navigation navigation)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            var types = navigation.Rules.UnitTypes;
            _typeSpeed = new Fix[types.Count];
            _typeRadiusRaw = new long[types.Count];
            _typeClass = new MovementClass[types.Count];
            _quarterCellRaw = Fix.FromRatio(navigation.Terrain.CellSizeMm, 4000).Raw;
            long w = navigation.Map.Width.Raw;
            long d = navigation.Map.Depth.Raw;
            _mapDiagonalRaw = IntMath.Sqrt((ulong)(w * w + d * d)) + 1; // sides at most 30 km: the square fits
            _typeFileSpacing = new Fix[types.Count];
            _typeRankSpacing = new Fix[types.Count];
            for (int t = 0; t < types.Count; t++)
            {
                _typeFileSpacing[t] = Fix.FromRatio(types[t].Formation.FileSpacingMm, 1000);
                _typeRankSpacing[t] = Fix.FromRatio(types[t].Formation.RankSpacingMm, 1000);
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
            UpdateRegiments(world);
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

        /// <summary>
        /// Serial, before the parallel pass: promotes a new officer where the old one fell (the soldier with the lowest
        /// formation slot, then the lowest unit slot), turns each formation toward its officer's heading, and gathers
        /// what the soldiers read: the regiment's mean velocity, its officer's position and velocity, its flow field.
        /// </summary>
        private void UpdateRegiments(World world)
        {
            int regiments = world.Regiments.Capacity;
            if (_regimentMean.Length < regiments)
            {
                _regimentMean = new FixVec2[regiments];
                _regimentDirections = new byte[]?[regiments];
                _regimentDistances = new long[]?[regiments];
                _anchorPosition = new FixVec2[regiments];
                _anchorVelocity = new FixVec2[regiments];
                _formationOrigin = new FixVec2[regiments];
                _sumX = new long[regiments];
                _sumY = new long[regiments];
            }
            var sumX = _sumX;
            var sumY = _sumY;
            Array.Clear(sumX, 0, regiments);
            Array.Clear(sumY, 0, regiments);
            if (_leaderless.Length < regiments)
            {
                _leaderless = new bool[regiments];
                _officerWaits = new bool[regiments];
                _stragglers = new int[regiments];
            }
            bool promote = false;
            for (int r = 0; r < regiments; r++)
            {
                _leaderless[r] =
                    r < world.Regiments.HighWater
                    && world.Regiments.IsAlive(r)
                    && world.RegimentAnchor[r] < 0
                    && world.RegimentAlive[r] > 0;
                promote |= _leaderless[r];
            }
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (world.Units.IsAlive(u))
                {
                    int r = world.UnitRegiment[u];
                    sumX[r] += world.Velocity[u].X.Raw;
                    sumY[r] += world.Velocity[u].Y.Raw;
                    if (promote && _leaderless[r])
                    {
                        int current = world.RegimentAnchor[r];
                        if (current < 0 || world.FormationSlot[u] < world.FormationSlot[current])
                        {
                            world.RegimentAnchor[r] = u; // ascending slots: ties keep the lower unit slot
                        }
                    }
                }
            }
            for (int r = 0; r < regiments; r++)
            {
                int alive = world.RegimentAlive[r];
                _regimentMean[r] =
                    alive > 0
                        ? new FixVec2(Fix.FromRaw(sumX[r] / alive), Fix.FromRaw(sumY[r] / alive))
                        : FixVec2.Zero;
                bool live = world.Regiments.IsAlive(r);
                var field = live ? _navigation.FieldOf(r) : null;
                _regimentDirections[r] = field?.FreshDirections();
                _regimentDistances[r] = field?.FreshDistances();
                int anchor = live ? world.RegimentAnchor[r] : -1;
                if (anchor < 0)
                {
                    _anchorPosition[r] = FixVec2.Zero;
                    _anchorVelocity[r] = FixVec2.Zero;
                    _formationOrigin[r] = FixVec2.Zero;
                    continue;
                }
                FixVec2 v = world.Velocity[anchor];
                _anchorPosition[r] = world.Position[anchor];
                _anchorVelocity[r] = v;
                // Turn toward the officer's heading once he is clearly walking (a quarter of his speed).
                long quarter = _typeSpeed[world.RegimentUnitType[r]].Raw / 4;
                long vx = v.X.Raw;
                long vy = v.Y.Raw;
                if (vx * vx + vy * vy > quarter * quarter)
                {
                    Angle facing = world.RegimentFacing[r];
                    int delta = facing.DeltaTo(v.Direction);
                    delta = System.Math.Max(
                        -MaxTurnUnitsPerTick,
                        System.Math.Min(MaxTurnUnitsPerTick, delta)
                    );
                    world.RegimentFacing[r] = facing + Angle.FromUnits(delta);
                }
                // A promoted officer keeps his own slot's place: the formation forms on where slot 0 would be, so
                // nobody's slot jumps when the standard changes hands.
                int type = world.RegimentUnitType[r];
                _formationOrigin[r] =
                    _anchorPosition[r]
                    - Formation.SlotOffset(
                        world.FormationSlot[anchor],
                        world.RegimentFiles[r],
                        _typeFileSpacing[type],
                        _typeRankSpacing[type],
                        world.RegimentFacing[r]
                    );
            }

            // Stragglers: soldiers further from their officer than the formation's depth plus ReformDistance. While
            // more than a quarter of a regiment straggles (squeezed through a gap, held up at a bridge), its officer
            // slows to WaitingPace so they can rejoin. Counted from this tick's positions, so a restored run counts the same.
            Array.Clear(_stragglers, 0, regiments);
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (!world.Units.IsAlive(u))
                {
                    continue;
                }
                int r = world.UnitRegiment[u];
                int anchor = world.RegimentAnchor[r];
                if (anchor < 0 || anchor == u)
                {
                    continue;
                }
                int type = world.RegimentUnitType[r];
                int ranks = Formation.Ranks(world.RegimentStartingSize[r], world.RegimentFiles[r]);
                // Clamped to the map's diagonal, so even a crafted snapshot's formation can't overflow the square.
                long reach = System.Math.Min(
                    _mapDiagonalRaw,
                    ReformDistance.Raw + _typeRankSpacing[type].Raw * ranks
                );
                long dx = world.Position[u].X.Raw - world.Position[anchor].X.Raw;
                long dy = world.Position[u].Y.Raw - world.Position[anchor].Y.Raw;
                // Axis test first: a soldier flung far away can't overflow the square.
                if (
                    dx > reach
                    || dx < -reach
                    || dy > reach
                    || dy < -reach
                    || dx * dx + dy * dy > reach * reach
                )
                {
                    _stragglers[r]++;
                }
            }
            for (int r = 0; r < regiments; r++)
            {
                _officerWaits[r] = _stragglers[r] * 4 > world.RegimentAlive[r];
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
                    var cls = _s._typeCosts[type];

                    // Where to go: the officer follows the flow field; everyone else walks to their formation slot
                    // when it is open and in reach, or follows the flow field until it is (flow-followers also blend
                    // in the regiment's mean velocity: alignment).
                    FixVec2 heading;
                    long cap; // the most this soldier wants to walk along its heading, raw m/tick
                    bool follower = false;
                    int anchor = _world.RegimentAnchor[regiment];
                    if (anchor == u || anchor < 0)
                    {
                        heading = directions == null ? FixVec2.Zero : Heading(directions, p);
                        cap =
                            speed.Raw * (_s._officerWaits[regiment] ? WaitingPace : OfficerPace).Raw
                            >> Fix.FractionalBits;
                    }
                    else
                    {
                        FixVec2 slot =
                            _s._formationOrigin[regiment]
                            + Formation.SlotOffset(
                                _world.FormationSlot[u],
                                _world.RegimentFiles[regiment],
                                _s._typeFileSpacing[type],
                                _s._typeRankSpacing[type],
                                _world.RegimentFacing[regiment]
                            );
                        long ox = slot.X.Raw - p.X.Raw;
                        long oy = slot.Y.Raw - p.Y.Raw;
                        long reform = ReformDistance.Raw;
                        bool inReach =
                            ox < reform
                            && ox > -reform
                            && oy < reform
                            && oy > -reform
                            && ox * ox + oy * oy <= reform * reform;
                        if (
                            directions == null
                            || (inReach && Passable(cls, slot) && Direct(regiment, p, slot, ox, oy))
                        )
                        {
                            FixVec2 seek =
                                _s._anchorVelocity[regiment]
                                + new FixVec2(Fix.FromRaw(ox), Fix.FromRaw(oy)) * SlotGain;
                            long seekSq = seek.X.Raw * seek.X.Raw + seek.Y.Raw * seek.Y.Raw;
                            if (seekSq == 0)
                            {
                                heading = FixVec2.Zero;
                                cap = 0;
                            }
                            else
                            {
                                cap = System.Math.Min(speed.Raw, IntMath.Sqrt((ulong)seekSq));
                                heading = seek.WithLength(Fix.One);
                            }
                        }
                        else
                        {
                            heading = Heading(directions, p);
                            cap = speed.Raw;
                            follower = true;
                        }
                    }

                    // Neighbours: overlaps push apart; anyone in the path ahead makes this soldier brake.
                    long reach = radius + _s._maxRadiusRaw + BrakeDistance.Raw;
                    int found = _hash.QueryRadius(_world, p, Fix.FromRaw(reach), neighbours);
                    int count = System.Math.Min(found, MaxNeighbours);
                    long pushX = 0;
                    long pushY = 0;
                    long pressure = 0;
                    long limit = cap; // lowered by whoever stands in the path
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

                    // Steering: the heading at the allowed pace. A blocked soldier still creeps forward at CreepShare
                    // of its wish: the crowd behind presses on (crowd pressure), and a jam where everyone waits for
                    // everyone can always unwind.
                    if (pressure < CreepPressureLimit.Raw)
                    {
                        // Only while not already pressed: in a deep crowd the creeps would add up along the chain.
                        limit = System.Math.Max(limit, cap * CreepShare.Raw >> Fix.FractionalBits);
                    }
                    FixVec2 desired = heading * Fix.FromRaw(limit);
                    if (follower)
                    {
                        desired = (desired * 3 + _s._regimentMean[regiment]) / 4;
                    }
                    FixVec2 v = velocity[u] + (desired - velocity[u]) / 4;

                    // The push is capped at a quarter of a terrain cell per tick (and at one body radius), so a dense
                    // crowd can't fling anyone, and a step (walk at most 0.2 m plus the push) never jumps a closed cell.
                    var push = new FixVec2(Fix.FromRaw(pushX), Fix.FromRaw(pushY));
                    long pushCap = System.Math.Min(radius, _s._quarterCellRaw);
                    long pushSq = pushX * pushX + pushY * pushY;
                    if (pushSq > pushCap * pushCap)
                    {
                        push = push.WithLength(Fix.FromRaw(pushCap));
                    }

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

            /// <summary>
            /// Whether the slot can be walked to more or less straight: by the flow field's distances, the soldier is
            /// no more than twice the straight line (plus two cells) further from the goal than its slot. A slot on the
            /// far side of a wall or a river fails, and the soldier follows the field around instead of pressing
            /// against the obstacle.
            /// </summary>
            private bool Direct(int regiment, FixVec2 p, FixVec2 slot, long ox, long oy)
            {
                var distances = _s._regimentDistances[regiment]!;
                long from = distances[CellIndex(p)];
                long to = distances[CellIndex(slot)];
                if (from == FlowField.Unreachable || to == FlowField.Unreachable)
                {
                    return false;
                }
                // A flat step costs 1000 (FlowField), so the straight line in field units is metres × 1000 / cell.
                long straight =
                    (long)IntMath.Sqrt((ulong)(ox * ox + oy * oy))
                    * 1000
                    * 1000
                    / Fix.OneRaw
                    / _terrain.CellSizeMm;
                long apart = from > to ? from - to : to - from;
                return apart <= 2 * straight + 2000;
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
