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
    /// movement class can't enter (obstacle avoidance). Water and mud slow it by the speed code of the cell it stands
    /// in (<see cref="FieldSpeed"/>, refreshed with the costs), and water too deep or too fast for its class is closed
    /// like any other impassable cell. Per soldier this is O(1): one field sample and one spatial-hash query.
    /// <para>
    /// Hazards (spec S1, section 4): a swept soldier doesn't steer. It drifts with the water and paddles toward the
    /// nearest cell where its class has footing; a stunned one lies still. A soldier the crowd presses toward water that
    /// would sweep it teeters for <see cref="HazardRules.TeeterTicks"/> and, if the pressure keeps up, goes over (the
    /// hazards step then finds it swept). An officer who is swept or stunned hands the standard on.
    /// </para>
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

        /// <summary>How far, in cells, a swept soldier looks for footing to paddle toward (9 m: S1's 15 s paddle).</summary>
        public const int PaddleSearchCells = 6;

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

        /// <summary>
        /// An engaging regiment whose outline is this close to its target breaks formation and every soldier closes on
        /// the enemy, so the fight wraps around the line instead of touching only where the two blocks meet (placeholder).
        /// </summary>
        public const int CloseInMm = 8_000;

        /// <summary>The officer walks straight at his order's point only if the line there is open and at most this long (placeholder).</summary>
        public const int MaxDirectMm = 80_000;

        private readonly Navigation _navigation;
        private readonly Fix[] _typeSpeed; // metres per tick
        private readonly Fix[] _typeRun; // metres per tick
        private readonly long[] _typeReachRaw; // melee reach beyond contact, or -1
        private readonly long _maxReachRaw;
        private readonly int _engagedPermille;
        private readonly RegimentSummary _summary = new RegimentSummary();
        private RegimentOrder[] _order = new RegimentOrder[0];
        private bool[] _hasPoint = new bool[0];
        private FixVec2[] _point = new FixVec2[0];
        private bool[] _closeIn = new bool[0];
        private FixVec2[] _chargeDir = new FixVec2[0]; // a charging regiment's line of advance, a unit vector
        private bool[] _near = new bool[0]; // an enemy outline within contact range: only these soldiers check for melee
        private readonly long _contactMm;
        private readonly long[] _typeRadiusRaw;
        private readonly MovementClass[] _typeClass;
        private readonly Fix[] _typeFileSpacing;
        private readonly Fix[] _typeRankSpacing;
        private readonly EngineCrews _crews;
        private readonly long[] _typeMass; // kg, for the mass-weighted push (S2 4.1)
        private readonly EngineRules?[] _typeEngine; // an engine type's crew rules, or null
        private readonly Fix[] _crewRing; // a crew type's ring around its engine
        private readonly Fix[] _openFileSpacing; // open order (S2 2.2): the type's spacing × the order's factor
        private readonly Fix[] _openRankSpacing;
        private readonly long _maxRadiusRaw;
        private readonly long _quarterCellRaw;
        private readonly long _mapDiagonalRaw;

        // Herds (spec S2 11): per regiment, filled serially each run by UpdateHerds; -1 where the regiment isn't a herd.
        private readonly HerdRules? _herdRules;
        private readonly Fix _grazeSpeed; // metres per tick
        private readonly Fix _drivenSpeed;
        private readonly Fix _stampedeSpeed;
        private readonly bool[] _typeCharges;
        private readonly Fix _chargeSpeed; // metres per tick (S2 10.1)
        private readonly Fix _afterContactSpeed;
        private readonly long _chargeAccelRaw; // metres per tick, per tick
        private int[] _herdOf = new int[0];
        private HerdMode[] _herdMode = new HerdMode[0];
        private FixVec2[] _herdCentre = new FixVec2[0];
        private int[] _herdRadiusMm = new int[0];
        private FixVec2[] _herdRun = new FixVec2[0]; // the stampede's heading, a unit vector
        private bool[] _herdDriven = new bool[0];
        private FixVec2[] _herdDrivenFrom = new FixVec2[0]; // the drive's direction, a unit vector
        private ulong _seed;
        private int _tick;

        /// <summary>The animals' wander waypoints (<see cref="SimRandom.Stream"/>: entity = unit slot, tick = the waypoint's period).</summary>
        public const int HerdWanderSystemId = 8_001;

        /// <summary>An agitated herd mills: each animal picks a new point this often, within <see cref="MillRadiusMm"/> (placeholder).</summary>
        public const int MillTicks = 20;

        /// <summary>How far from the herd's centre a milling animal wanders (placeholder).</summary>
        public const int MillRadiusMm = 6_000;

        /// <summary>A scattered herd wanders in place, within this of where it stopped (S2 11.3 sets no circle; placeholder).</summary>
        public const int ScatterWanderMm = 12_000;

        /// <summary>An animal this close to its waypoint stands and grazes.</summary>
        public const int WaypointReachedMm = 500;

        // Scratch: overwritten every run before it is read.
        private SpatialHash? _hash;
        private FixVec2[] _newPosition = new FixVec2[0];
        private FixVec2[] _newVelocity = new FixVec2[0];
        private int[] _pressure = new int[0];
        private UnitFlags[] _newFlags = new UnitFlags[0];
        private int[] _newTimer = new int[0];
        private readonly int[] _typeCarriedPermille;
        private readonly long[] _typePaddleRaw; // metres per tick
        private readonly long[] _typeFootingUm; // water shallower than this gives footing back (regain share of swept depth)
        private FieldGrid? _depth;
        private readonly int _teeterTicks;
        private readonly int _teeterPressureMm;
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
        private readonly int[] _typeDeepPermille; // the wading speed at the deepest water the class enters
        private int[]? _codes;

        public MovementSystem(Navigation navigation)
        {
            _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            var types = navigation.Rules.UnitTypes;
            _typeSpeed = new Fix[types.Count];
            _typeRun = new Fix[types.Count];
            _typeReachRaw = new long[types.Count];
            _engagedPermille = navigation.Rules.Combat.EngagedSpeedPermille;
            _typeRadiusRaw = new long[types.Count];
            _typeClass = new MovementClass[types.Count];
            _typeDeepPermille = new int[types.Count];
            _typeCarriedPermille = new int[types.Count];
            _typePaddleRaw = new long[types.Count];
            _typeFootingUm = new long[types.Count];
            _teeterTicks = navigation.Rules.Hazards.TeeterTicks;
            _teeterPressureMm = navigation.Rules.Hazards.TeeterPressureMm;
            _quarterCellRaw = Fix.FromRatio(navigation.Terrain.CellSizeMm, 4000).Raw;
            long w = navigation.Map.Width.Raw;
            long d = navigation.Map.Depth.Raw;
            _mapDiagonalRaw = IntMath.Sqrt((ulong)(w * w + d * d)) + 1; // sides at most 30 km: the square fits
            _crews = new EngineCrews(navigation.Rules);
            _herdRules = navigation.Rules.Herds;
            _typeCharges = new bool[types.Count];
            for (int t = 0; t < types.Count; t++)
            {
                _typeCharges[t] = types[t].Charges;
            }
            var charge = navigation.Rules.Combat.Charge;
            if (charge != null)
            {
                long ticks = 1000L * SimConstants.TicksPerSecond;
                _chargeSpeed = Fix.FromRatio(charge.SpeedMmPerS, ticks);
                _afterContactSpeed = Fix.FromRatio(charge.AfterContactMmPerS, ticks);
                _chargeAccelRaw = Fix.FromRatio(
                    charge.AccelMmPerS2,
                    1000L * SimConstants.TicksPerSecond * SimConstants.TicksPerSecond
                ).Raw;
            }
            if (_herdRules != null)
            {
                long perTick = 1000L * SimConstants.TicksPerSecond;
                _grazeSpeed = Fix.FromRatio(_herdRules.GrazeMmPerS, perTick);
                _drivenSpeed = Fix.FromRatio(_herdRules.DrivenMmPerS, perTick);
                _stampedeSpeed = Fix.FromRatio(_herdRules.StampedeMmPerS, perTick);
            }
            _typeMass = new long[types.Count];
            _typeEngine = new EngineRules?[types.Count];
            _crewRing = new Fix[types.Count];
            _typeFileSpacing = new Fix[types.Count];
            _typeRankSpacing = new Fix[types.Count];
            _openFileSpacing = new Fix[types.Count];
            _openRankSpacing = new Fix[types.Count];
            int open = navigation.Rules.Combat.OpenSpacingPermille;
            for (int t = 0; t < types.Count; t++)
            {
                _typeEngine[t] = types[t].Engine;
                _typeMass[t] = System.Math.Max(1, types[t].MassKg);
                _crewRing[t] = Fix.FromRatio(types[t].SlotRadiusMm, 1000);
                _typeFileSpacing[t] = Fix.FromRatio(types[t].Formation.FileSpacingMm, 1000);
                _typeRankSpacing[t] = Fix.FromRatio(types[t].Formation.RankSpacingMm, 1000);
                _openFileSpacing[t] = Fix.FromRatio(
                    (long)types[t].Formation.FileSpacingMm * open,
                    1_000_000
                );
                _openRankSpacing[t] = Fix.FromRatio(
                    (long)types[t].Formation.RankSpacingMm * open,
                    1_000_000
                );
                _typeSpeed[t] = Fix.FromRatio(
                    types[t].SpeedMmPerSecond,
                    1000L * SimConstants.TicksPerSecond
                );
                _typeRun[t] = Fix.FromRatio(
                    types[t].RunSpeedMmPerSecond,
                    1000L * SimConstants.TicksPerSecond
                );
                _typeReachRaw[t] =
                    types[t].Melee == null ? -1 : Fix.FromRatio(types[t].Melee!.ReachMm, 1000).Raw;
                _maxReachRaw = System.Math.Max(_maxReachRaw, _typeReachRaw[t]);
                _typeRadiusRaw[t] = Fix.FromRatio(types[t].RadiusMm, 1000).Raw;
                _typeClass[t] = types[t].MovementClass;
                var water = navigation.Rules.Movement(_typeClass[t]).Water;
                _typeDeepPermille[t] = water.Speed.At(water.MaxEnterDepthMm);
                _typeCarriedPermille[t] = water.CarriedPermille;
                _typeFootingUm[t] = (long)water.SweptDepthMm * water.RegainPermille; // mm × ‰ = µm
                _typePaddleRaw[t] = Fix.FromRatio(
                    water.PaddleMmPerS,
                    1000L * SimConstants.TicksPerSecond
                ).Raw;
                _maxRadiusRaw = System.Math.Max(_maxRadiusRaw, _typeRadiusRaw[t]);
            }
            // Two of the largest bodies plus the longest reach, a metre to spare (as the combat step's contact range).
            _contactMm = RegimentSummary.Mm(2 * _maxRadiusRaw + _maxReachRaw) + 1_000;
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
                _newFlags = new UnitFlags[capacity];
                _newTimer = new int[capacity];
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
            UpdateOrders(world);
            _seed = context.Seed;
            _tick = context.Tick;
            UpdateHerds(world);
            if (_typeCosts.Length != _typeClass.Length)
            {
                _typeCosts = new ushort[_typeClass.Length][];
            }
            for (int t = 0; t < _typeClass.Length; t++)
            {
                _typeCosts[t] = _navigation.Costs.FreshArray(_typeClass[t]);
            }
            var fields = world.Fields;
            _codes =
                fields != null && fields.Rules.IndexOf(FieldSpeed.FieldId) >= 0
                    ? fields.Get(FieldSpeed.FieldId).Values
                    : null;
            _depth =
                fields != null && fields.Rules.IndexOf(WaterModel.Depth) >= 0
                    ? fields.Get(WaterModel.Depth)
                    : null;

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
                    world.Flags[u] = _newFlags[u];
                    world.HazardTimer[u] = _newTimer[u];
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
            // Engines and their crews (S2 2.1): who walks beside which engine, and how many crew each has beside it.
            _crews.Gather(world);
            bool promote = false;
            for (int r = 0; r < regiments; r++)
            {
                int officer =
                    r < world.Regiments.HighWater && world.Regiments.IsAlive(r)
                        ? world.RegimentAnchor[r]
                        : -1;
                if (officer >= 0 && Down(world.Flags[officer]))
                {
                    world.RegimentAnchor[r] = -1; // swept or stunned: someone on his feet takes the standard
                }
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
                    if ((world.Flags[u] & UnitFlags.Launched) == 0)
                    {
                        // An airborne body's arc isn't the regiment's walk.
                        sumX[r] += world.Velocity[u].X.Raw;
                        sumY[r] += world.Velocity[u].Y.Raw;
                    }
                    if (promote && _leaderless[r] && !Down(world.Flags[u]))
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
                        world.Army.Open[r] ? _openFileSpacing[type] : _typeFileSpacing[type],
                        world.Army.Open[r] ? _openRankSpacing[type] : _typeRankSpacing[type],
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
                if (_typeEngine[type] != null && world.UnitType[u] != type)
                {
                    continue; // crews keep to their engines, not to the formation
                }
                int ranks = Formation.Ranks(
                    _navigation.Rules.UnitTypes[type].EnginesIn(world.RegimentStartingSize[r]),
                    world.RegimentFiles[r]
                );
                // Clamped to the map's diagonal, so even a crafted snapshot's formation can't overflow the square.
                long reach = System.Math.Min(
                    _mapDiagonalRaw,
                    ReformDistance.Raw
                        + (world.Army.Open[r] ? _openRankSpacing[type] : _typeRankSpacing[type]).Raw
                            * ranks
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

        /// <summary>
        /// Serial: what each regiment's order asks of movement this tick (spec S2, section 5's targeting default). An
        /// Engage order's point is its target's centroid, a FallBack order's the point it walks to; an engaging
        /// regiment within <see cref="CloseInMm"/> of its target closes in soldier by soldier.
        /// </summary>
        private void UpdateOrders(World world)
        {
            int n = world.Regiments.Capacity;
            if (_order.Length != n)
            {
                _order = new RegimentOrder[n];
                _hasPoint = new bool[n];
                _point = new FixVec2[n];
                _closeIn = new bool[n];
                _chargeDir = new FixVec2[n];
                _near = new bool[n];
            }
            var army = world.Army;
            _summary.Gather(world);
            _summary.NearEnemy(world, _contactMm, _near);
            bool any = false;
            for (int r = 0; r < n; r++)
            {
                _order[r] = world.Regiments.IsAlive(r) ? army.Order[r] : RegimentOrder.March;
                any |=
                    _order[r] == RegimentOrder.Engage
                    || _order[r] == RegimentOrder.FallBack
                    || _order[r] == RegimentOrder.Work
                    || _order[r] == RegimentOrder.Hold;
                _hasPoint[r] = false;
                _closeIn[r] = false;
                _chargeDir[r] = FixVec2.Zero;
            }
            if (!any)
            {
                return;
            }
            for (int r = 0; r < n; r++)
            {
                if (
                    _order[r] == RegimentOrder.FallBack
                    || _order[r] == RegimentOrder.Work
                    || _order[r] == RegimentOrder.Hold
                )
                {
                    _hasPoint[r] = true;
                    _point[r] = army.Point[r];
                    continue;
                }
                int target = army.Target[r];
                if (
                    _order[r] != RegimentOrder.Engage
                    || target < 0
                    || !world.Regiments.IsAlive(target)
                    || _summary.Count[target] == 0
                    || _summary.Count[r] == 0
                )
                {
                    continue;
                }
                _hasPoint[r] = true;
                _point[r] = _summary.Centroid[target];
                // A charging regiment keeps its line (S2 10.3: the front ranks meet the front ranks).
                _closeIn[r] = _summary.Gap(r, target) <= CloseInMm && army.ChargeSince[r] == 0;
                if (army.ChargeSince[r] > 0)
                {
                    var line = _summary.Centroid[target] - _summary.Centroid[r];
                    _chargeDir[r] = line == FixVec2.Zero ? FixVec2.Zero : line.WithLength(Fix.One);
                }
            }
        }

        /// <summary>
        /// Serial: what each herd asks of its animals this tick (spec S2 11.1–11.3), from the herd step's state. Grazing,
        /// the animals walk between seeded waypoints in the grazing circle around the herd's anchor; driven, away from the
        /// drovers at walking pace; agitated, they mill near the herd's centre; stampeding, along the run's heading at
        /// stampede speed; scattered, they wander where the run stopped.
        /// </summary>
        private void UpdateHerds(World world)
        {
            int n = world.Regiments.Capacity;
            if (_herdOf.Length != n)
            {
                _herdOf = new int[n];
                _herdMode = new HerdMode[n];
                _herdCentre = new FixVec2[n];
                _herdRadiusMm = new int[n];
                _herdRun = new FixVec2[n];
                _herdDriven = new bool[n];
                _herdDrivenFrom = new FixVec2[n];
            }
            for (int r = 0; r < n; r++)
            {
                _herdOf[r] = -1;
            }
            var herds = world.Herds;
            if (_herdRules == null || herds.Count == 0)
            {
                return;
            }
            var specs = _navigation.Map.Herds;
            for (int h = 0; h < herds.Count; h++)
            {
                int r = herds.Regiment[h];
                if (r < 0 || r >= n || !world.Regiments.IsAlive(r))
                {
                    continue;
                }
                _herdOf[r] = h;
                var mode = herds.Mode[h];
                _herdMode[r] = mode;
                _herdDriven[r] = herds.DrivenBy[h] >= 0;
                _herdDrivenFrom[r] = herds.DrivenFrom[h];
                if (_herdDriven[r] && _summary.Count[r] > 0)
                {
                    // The whole herd walks one way, away from the drovers through its centre, so an animal on the
                    // drovers' far side isn't sent back the way they came.
                    var away = _summary.Centroid[r] - herds.DrivenFrom[h];
                    _herdDrivenFrom[r] =
                        away == FixVec2.Zero ? FixVec2.Zero : away.WithLength(Fix.One);
                }
                else
                {
                    _herdDriven[r] = false;
                }
                _herdRun[r] = FixVec2.FromAngle(Angle.FromUnits(herds.Heading[h]), Fix.One);
                switch (mode)
                {
                    case HerdMode.Agitated:
                        _herdCentre[r] =
                            _summary.Count[r] > 0 ? _summary.Centroid[r] : herds.Anchor[h];
                        _herdRadiusMm[r] = MillRadiusMm;
                        break;
                    case HerdMode.Scattered:
                        _herdCentre[r] = herds.Anchor[h];
                        _herdRadiusMm[r] = ScatterWanderMm;
                        break;
                    default:
                        _herdCentre[r] = herds.Anchor[h];
                        _herdRadiusMm[r] =
                            h < specs.Count
                                ? System.Math.Max(1_000, specs[h].RadiusMm - 1_000)
                                : ScatterWanderMm;
                        break;
                }
            }
        }

        /// <summary>
        /// An animal's heading and pace this tick (<see cref="UpdateHerds"/>): <paramref name="stampeding"/> when it runs
        /// with the stampede (it then ploughs on rather than braking for soldiers). Reads only state the tick started with.
        /// </summary>
        private FixVec2 AnimalHeading(
            World world,
            int u,
            FixVec2 p,
            int r,
            out long cap,
            out bool stampeding
        )
        {
            var mode = _herdMode[r];
            stampeding = mode == HerdMode.Stampede;
            if (stampeding)
            {
                cap = _stampedeSpeed.Raw;
                var hits = world.Herds.LastHit[_herdOf[r]];
                int slot = world.FormationSlot[u];
                if (
                    hits != null
                    && slot >= 0
                    && slot < hits.Length
                    && _tick - hits[slot] < _herdRules!.SlowAfterHitTicks
                )
                {
                    cap = cap * _herdRules.SlowAfterHitPermille / 1000;
                }
                return _herdRun[r];
            }
            if (_herdDriven[r] && mode != HerdMode.Agitated)
            {
                cap = _drivenSpeed.Raw;
                return _herdDrivenFrom[r]; // the drive's direction (UpdateHerds)
            }
            int period = mode == HerdMode.Agitated ? MillTicks : _herdRules!.WaypointTicks;
            // Each animal's own phase, so the herd doesn't all set off on the same tick.
            int phase = (int)((uint)(u * 2654435761u) % (uint)period);
            var rng = SimRandom.Stream(_seed, HerdWanderSystemId, u, (_tick + phase) / period);
            var angle = Angle.FromUnits((int)rng.NextUInt(Angle.UnitsPerTurn));
            long radius = _herdRadiusMm[r];
            // At most uint.MaxValue (a circle of about 65 m; herds are at most 40 m, S2 11.1), never a zero bound.
            long bound = System.Math.Min(radius * radius, uint.MaxValue);
            long off = bound <= 0 ? 0 : IntMath.Sqrt(rng.NextUInt((uint)bound));
            var waypoint = _herdCentre[r] + FixVec2.FromAngle(angle, Fix.FromRatio(off, 1000));
            long dx = waypoint.X.Raw - p.X.Raw;
            long dy = waypoint.Y.Raw - p.Y.Raw;
            long reached = Fix.FromRatio(WaypointReachedMm, 1000).Raw;
            if (dx * dx + dy * dy <= reached * reached)
            {
                cap = 0;
                return FixVec2.Zero;
            }
            cap = mode == HerdMode.Agitated ? _drivenSpeed.Raw : _grazeSpeed.Raw;
            return Kernel.TowardPoint(p, waypoint);
        }

        /// <summary>Swept, stunned or airborne: not on its feet, so it neither steers nor leads.</summary>
        private static bool Down(UnitFlags flags) =>
            (flags & (UnitFlags.Swept | UnitFlags.Stunned | UnitFlags.Launched)) != 0;

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
                    var flags = _world.Flags[u];
                    int timer = _world.HazardTimer[u];
                    _s._newFlags[u] = flags;
                    _s._newTimer[u] = timer;
                    _s._pressure[u] = 0;
                    if ((flags & UnitFlags.Swept) != 0)
                    {
                        FixVec2 drift = Drift(type, p);
                        _s._newPosition[u] = Clamp(p + drift, maxX, maxY);
                        _s._newVelocity[u] = drift;
                        continue;
                    }
                    if ((flags & UnitFlags.Launched) != 0)
                    {
                        // Airborne: the ballistics step flies him; nobody pushes him and he pushes nobody.
                        _s._newPosition[u] = p;
                        _s._newVelocity[u] = velocity[u];
                        continue;
                    }
                    if ((flags & UnitFlags.Stunned) != 0)
                    {
                        _s._newPosition[u] = p;
                        _s._newVelocity[u] = FixVec2.Zero;
                        continue;
                    }
                    Fix speed = _s._typeSpeed[type];
                    Fix run = _s._typeRun[type];
                    // A charging rider (S2 10.1) rides at the charge speed until his contact, then slows to fight.
                    bool charging = _s._typeCharges[type] && _world.Army.ChargeSince[regiment] > 0;
                    if (charging)
                    {
                        bool contacted = (flags & UnitFlags.Charged) != 0;
                        speed = contacted ? _s._afterContactSpeed : _s._chargeSpeed;
                        run = speed;
                        charging = !contacted;
                    }
                    if (_s._codes != null || _terrain.HasPieceSpeed)
                    {
                        // Wading, mud and climbing a hedge or wall: the speed the cell's codes allow. A soldier caught
                        // in water that closed around it wades out at the class's deepest wading speed.
                        int cell = CellIndex(p);
                        int codes = FieldSpeed.Combine(
                            _s._codes == null ? FieldSpeed.AllDry : _s._codes[cell],
                            _terrain.PieceCodes(cell)
                        );
                        int code = FieldSpeed.Code(codes, _s._typeClass[type]);
                        int permille =
                            code <= FieldSpeed.Closed
                                ? _s._typeDeepPermille[type]
                                : FieldSpeed.Permille(code);
                        if (permille < 1000)
                        {
                            speed = Fix.FromRaw(speed.Raw * permille / 1000);
                            run = Fix.FromRaw(run.Raw * permille / 1000);
                        }
                    }
                    var engineRules = _s._typeEngine[type];
                    if (engineRules != null)
                    {
                        // An engine moves at full speed with enough crew beside it, crawls with fewer, stands with none.
                        int share = EngineCrews.SpeedPermille(engineRules, _s._crews.CrewNear(u));
                        speed = Fix.FromRaw(speed.Raw * share / 1000);
                        run = Fix.FromRaw(run.Raw * share / 1000);
                    }
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
                    var order = _s._order[regiment];
                    bool routing = order == RegimentOrder.Rout;
                    bool stampeding = false;
                    if (_s._herdOf[regiment] >= 0)
                    {
                        // A herd animal (S2 11): grazing, driven, milling or running, never in formation.
                        heading = _s.AnimalHeading(_world, u, p, regiment, out cap, out stampeding);
                    }
                    else if (charging && _s._chargeDir[regiment] != FixVec2.Zero)
                    {
                        // Charging (S2 10.1): every rider still to make contact rides the regiment's line at the charge
                        // speed, side by side, so the front rank meets the enemy's front rank.
                        heading = _s._chargeDir[regiment];
                        cap = speed.Raw;
                    }
                    else if (routing)
                    {
                        // Routed: every soldier runs down the retreat field for himself (S2 7.2).
                        heading = directions == null ? FixVec2.Zero : Heading(directions, p);
                        cap = run.Raw;
                        follower = true;
                    }
                    else if (_s._closeIn[regiment])
                    {
                        // Close combat: out of the ranks and at the enemy.
                        heading = Toward(p, _s._point[regiment]);
                        cap = speed.Raw;
                    }
                    else if (anchor == u || anchor < 0)
                    {
                        if (_s._hasPoint[regiment])
                        {
                            // Engage or fall back: straight for the point while the way there is open, else the field.
                            var point = _s._point[regiment];
                            heading =
                                Clear(cls, p, point) ? Toward(p, point)
                                : directions == null ? FixVec2.Zero
                                : Heading(directions, p);
                        }
                        else
                        {
                            heading =
                                order == RegimentOrder.Shoot || directions == null
                                    ? FixVec2.Zero
                                    : Heading(directions, p);
                        }
                        cap =
                            speed.Raw * (_s._officerWaits[regiment] ? WaitingPace : OfficerPace).Raw
                            >> Fix.FractionalBits;
                    }
                    else
                    {
                        FixVec2 slot;
                        FixVec2 slotVelocity = _s._anchorVelocity[regiment];
                        bool hasSlot = true;
                        int regimentType = _world.RegimentUnitType[regiment];
                        var crewOf = _s._typeEngine[regimentType];
                        bool crew = crewOf != null && type != regimentType;
                        if (crew)
                        {
                            // A crew soldier: his place on the ring around his engine (S2 2.1, D4).
                            int engine = _s._crews.EngineOf(_world, u);
                            hasSlot = engine >= 0;
                            slot = hasSlot
                                ? position[engine]
                                    + EngineCrews.CrewOffset(
                                        _s._crews.CrewIndex(_world, u),
                                        crewOf!.CrewPerEngine,
                                        _s._crewRing[type],
                                        _world.RegimentFacing[regiment]
                                    )
                                : p;
                            if (hasSlot)
                            {
                                slotVelocity = velocity[engine];
                            }
                        }
                        else
                        {
                            slot =
                                _s._formationOrigin[regiment]
                                + Formation.SlotOffset(
                                    _world.FormationSlot[u],
                                    _world.RegimentFiles[regiment],
                                    _world.Army.Open[regiment]
                                        ? _s._openFileSpacing[type]
                                        : _s._typeFileSpacing[type],
                                    _world.Army.Open[regiment]
                                        ? _s._openRankSpacing[type]
                                        : _s._typeRankSpacing[type],
                                    _world.RegimentFacing[regiment]
                                );
                        }
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
                            || (
                                hasSlot
                                && inReach
                                && Passable(cls, slot)
                                && Direct(regiment, p, slot, ox, oy)
                            )
                            // A crew soldier left behind walks straight back to his engine when his own class can (the
                            // engine's wheeled field may lead him into the jam he was left in).
                            || (crew && hasSlot && Passable(cls, slot) && Clear(cls, p, slot))
                        )
                        {
                            FixVec2 seek =
                                slotVelocity
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
                    // Only a soldier whose regiment is near an enemy looks out to melee reach.
                    long swing = routing || !_s._near[regiment] ? -1 : _s._typeReachRaw[type];
                    long reach =
                        radius
                        + _s._maxRadiusRaw
                        + System.Math.Max(BrakeDistance.Raw, swing < 0 ? 0 : _s._maxReachRaw);
                    var side = _world.RegimentSide[regiment];
                    bool engaged = false;
                    int found = _hash.QueryRadius(_world, p, Fix.FromRaw(reach), neighbours);
                    int count = System.Math.Min(found, MaxNeighbours);
                    long pushX = 0;
                    long pushY = 0;
                    long pressure = 0;
                    long limit = cap; // lowered by whoever stands in the path
                    for (int k = 0; k < count; k++)
                    {
                        int j = neighbours[k];
                        if (j == u || (_world.Flags[j] & UnitFlags.Launched) != 0)
                        {
                            continue;
                        }
                        long dx = position[j].X.Raw - p.X.Raw;
                        long dy = position[j].Y.Raw - p.Y.Raw;
                        long contact = radius + _s._typeRadiusRaw[unitType[j]];
                        // Weighted by mass (S2 4.1): 2 m_j / (m_u + m_j), exactly 1 between equals, so a cavalry
                        // block shoves infantry hard and a crowd barely moves an engine.
                        long massSelf = _s._typeMass[type];
                        long massOther = _s._typeMass[unitType[j]];
                        long distSq = dx * dx + dy * dy; // within the query radius (a few metres), far inside a long
                        if (
                            swing >= 0
                            && !engaged
                            && distSq <= (contact + swing) * (contact + swing)
                            && Sides.Hostile(_world.RegimentSide[unitRegiment[j]], side)
                        )
                        {
                            engaged = true; // an enemy within reach: this soldier is fighting
                        }
                        if (distSq < contact * contact)
                        {
                            long dist = IntMath.Sqrt((ulong)distSq);
                            long overlap = contact - dist;
                            pressure += overlap * 2 * massOther / (massSelf + massOther);
                            long share =
                                (overlap * PushShare.Raw >> Fix.FractionalBits)
                                * 2
                                * massOther
                                / (massSelf + massOther);
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
                        if (
                            heading != FixVec2.Zero
                            // A stampede doesn't wait for soldiers: it runs into them (S2 11.4).
                            && !(stampeding && unitRegiment[j] != regiment)
                            // Nor does a charge wait for the enemy it rides into (S2 10.3).
                            && !(
                                charging
                                && Sides.Hostile(_world.RegimentSide[unitRegiment[j]], side)
                            )
                            && !(
                                engineRules != null
                                && unitRegiment[j] == regiment
                                && unitType[j] != type
                            )
                        )
                        {
                            // (An engine doesn't wait for its own crew: they step aside, S2 4.1's weighted push.)
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
                    if (engaged && !charging) // a charging rider rides into the line (S2 10.3)
                    {
                        // A soldier in melee moves at a fifth of his march speed (S2 5).
                        limit = System.Math.Min(limit, speed.Raw * _s._engagedPermille / 1000);
                    }
                    if (charging)
                    {
                        // The charge builds up: at most the acceleration on top of the speed he has.
                        long now = IntMath.Sqrt(
                            (ulong)(
                                velocity[u].X.Raw * velocity[u].X.Raw
                                + velocity[u].Y.Raw * velocity[u].Y.Raw
                            )
                        );
                        limit = System.Math.Min(limit, now + _s._chargeAccelRaw);
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
                    long pressureMm = pressure * 1000 >> Fix.FractionalBits;
                    bool shovedToward =
                        Passable(cls, p)
                        && !Passable(cls, target)
                        && pressureMm >= _s._teeterPressureMm
                        && SweepsHere(type, target);
                    if (shovedToward && (flags & UnitFlags.Teetering) != 0 && timer <= 1)
                    {
                        // Teetered long enough with the crowd still pressing: over the edge. The hazards step finds
                        // him swept this tick and counts the push.
                        _s._newTimer[u] = 0;
                    }
                    else if (Passable(cls, p) && !Passable(cls, target))
                    {
                        if (shovedToward)
                        {
                            // At a lethal edge with the crowd pressing: arms out, the warning before going over.
                            _s._newFlags[u] = flags | UnitFlags.Teetering;
                            _s._newTimer[u] =
                                (flags & UnitFlags.Teetering) != 0 ? timer - 1 : _s._teeterTicks;
                        }
                        else if ((flags & UnitFlags.Teetering) != 0)
                        {
                            _s._newFlags[u] = flags & ~UnitFlags.Teetering;
                            _s._newTimer[u] = 0;
                        }
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
                    else if ((flags & UnitFlags.Teetering) != 0)
                    {
                        // The crowd eased off, or he stepped back from the edge.
                        _s._newFlags[u] = flags & ~UnitFlags.Teetering;
                        _s._newTimer[u] = 0;
                    }
                    _s._newPosition[u] = target;
                    _s._newVelocity[u] = v;
                    _s._pressure[u] = (int)System.Math.Min(int.MaxValue, pressureMm);
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

            internal static FixVec2 TowardPoint(FixVec2 p, FixVec2 to) => Toward(p, to);

            /// <summary>A unit vector from <paramref name="p"/> to <paramref name="to"/>, or zero within a few centimetres.</summary>
            private static FixVec2 Toward(FixVec2 p, FixVec2 to)
            {
                var d = to - p;
                long dx = d.X.Raw;
                long dy = d.Y.Raw;
                long near = Fix.OneRaw / 20;
                return dx < near && dx > -near && dy < near && dy > -near
                    ? FixVec2.Zero
                    : d.WithLength(Fix.One);
            }

            /// <summary>
            /// Whether the straight line to <paramref name="to"/> is open for this soldier's class, sampled every half
            /// cell; anything further than <see cref="MaxDirectMm"/> on either axis counts as not open.
            /// </summary>
            private bool Clear(ushort[] costs, FixVec2 p, FixVec2 to)
            {
                long dx = to.X.Raw - p.X.Raw;
                long dy = to.Y.Raw - p.Y.Raw;
                long max = Fix.FromRatio(MaxDirectMm, 1000).Raw;
                if (dx > max || dx < -max || dy > max || dy < -max)
                {
                    return false;
                }
                long length = IntMath.Sqrt((ulong)(dx * dx + dy * dy));
                long step = Fix.FromRatio(_terrain.CellSizeMm, 2000).Raw;
                long steps = length / step + 1;
                for (long k = 1; k <= steps; k++)
                {
                    var at = new FixVec2(
                        Fix.FromRaw(p.X.Raw + dx * k / steps),
                        Fix.FromRaw(p.Y.Raw + dy * k / steps)
                    );
                    if (!Passable(costs, at))
                    {
                        return false;
                    }
                }
                return true;
            }

            /// <summary>Whether the water in the cell at <paramref name="at"/> sweeps this type's class (from the speed codes).</summary>
            private bool SweepsHere(int type, FixVec2 at) =>
                _s._codes != null
                && FieldSpeed.Code(_s._codes[CellIndex(at)], _s._typeClass[type])
                    == FieldSpeed.Swept;

            /// <summary>
            /// A swept soldier's move this tick: the water's velocity at its cell times the class's carried share, plus
            /// its paddle toward the centre of the nearest cell (rings out to <see cref="PaddleSearchCells"/>) where its
            /// class has footing.
            /// </summary>
            private FixVec2 Drift(int type, FixVec2 p)
            {
                var terrain = _terrain;
                terrain.CellOf(p, out int cx, out int cy);
                long east = 0;
                long north = 0;
                var fields = _world.Fields;
                if (fields != null && _s._depth != null)
                {
                    WaterModel.Velocity(fields, terrain.CellSizeMm, cx, cy, out int e, out int n);
                    east = e;
                    north = n;
                }
                // mm/s × share / 1000 → mm/s; to metres per tick: × 2^16 / (1000 × ticks per second).
                long perTick = 1000L * SimConstants.TicksPerSecond;
                int carried = _s._typeCarriedPermille[type];
                var v = new FixVec2(
                    Fix.FromRaw(east * carried / 1000 * Fix.OneRaw / perTick),
                    Fix.FromRaw(north * carried / 1000 * Fix.OneRaw / perTick)
                );
                long paddle = _s._typePaddleRaw[type];
                if (
                    paddle > 0
                    && _s._depth != null
                    && FindFooting(type, p, cx, cy, out FixVec2 footing)
                )
                {
                    var toward = footing - p;
                    if (toward != FixVec2.Zero)
                    {
                        v += toward.WithLength(Fix.FromRaw(paddle));
                    }
                }
                return v;
            }

            /// <summary>
            /// The centre of the nearest cell, ring by ring, that the class can stand in: open terrain shallow enough to
            /// regain its footing (by depth alone: the current there is the hazards step's to judge). Within a ring the
            /// closest centre wins, ties to the first in scan order.
            /// </summary>
            private bool FindFooting(int type, FixVec2 p, int cx, int cy, out FixVec2 footing)
            {
                long shallow = _s._typeFootingUm[type];
                var depth = _s._depth!;
                var costs = _s._typeCosts[type];
                var terrain = _terrain;
                footing = default;
                for (int ring = 1; ring <= PaddleSearchCells; ring++)
                {
                    long best = long.MaxValue;
                    for (int dy = -ring; dy <= ring; dy++)
                    {
                        for (int dx = -ring; dx <= ring; dx++)
                        {
                            if (System.Math.Max(System.Math.Abs(dx), System.Math.Abs(dy)) != ring)
                            {
                                continue;
                            }
                            int x = cx + dx;
                            int y = cy + dy;
                            if (!terrain.InBounds(x, y))
                            {
                                continue;
                            }
                            int i = terrain.Index(x, y);
                            if (
                                terrain.FootingDepthUm(i, depth.Values[i]) >= shallow
                                || costs[i] == CostFields.Impassable
                            )
                            {
                                continue;
                            }
                            var centre = terrain.CellCentre(x, y);
                            long ex = centre.X.Raw - p.X.Raw;
                            long ey = centre.Y.Raw - p.Y.Raw;
                            long d = ex * ex + ey * ey; // within a few cells: far inside a long
                            if (d < best)
                            {
                                best = d;
                                footing = centre;
                            }
                        }
                    }
                    if (best != long.MaxValue)
                    {
                        return true;
                    }
                }
                return false;
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
