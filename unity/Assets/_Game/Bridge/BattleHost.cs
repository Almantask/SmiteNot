using System;
using System.Diagnostics;
using System.Threading;
using GodGame.Sim;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Bridge
{
    /// <summary>
    /// Runs one battle for the presentation layer: a worker thread steps the simulation at real time (times a speed
    /// factor) and publishes a snapshot of every unit after each tick; the main thread reads the two newest snapshots
    /// and blends between them, so it never waits on the simulation (rule from M4, adopted early for the 10,000-capsule
    /// debug view). Unity reads only; nothing here decides an outcome.
    /// <para>
    /// The worker owns the <see cref="Simulation"/> exclusively while running; everything the main thread sees goes
    /// through <see cref="UnitSnapshot"/> copies swapped under a lock, and after every field step (5 Hz) through a
    /// <see cref="FieldUpload.Snapshot"/> of the fields.
    /// </para>
    /// </summary>
    public sealed class BattleHost : IDisposable
    {
        /// <summary>One tick's worth of drawing data, in Unity units (metres; y up, the map's y is Unity's z).</summary>
        public sealed class UnitSnapshot
        {
            public readonly float[] SpinRad;
            public readonly byte[] Flags;

            public UnitSnapshot(int capacity)
            {
                X = new float[capacity];
                Y = new float[capacity];
                Z = new float[capacity];
                SpinRad = new float[capacity];
                Flags = new byte[capacity];
                Regiment = new int[capacity];
                UnitType = new int[capacity];
                Alive = new bool[capacity];
            }

            public int Tick;
            public long PublishedTicks; // Stopwatch timestamp
            public int Count; // slots in use (the pool's high-water mark)
            public readonly float[] X;
            public readonly float[] Y;
            public readonly float[] Z;
            public readonly int[] Regiment;
            public readonly int[] UnitType;
            public readonly bool[] Alive;
        }

        private readonly Simulation _simulation;
        private readonly Terrain _terrain;
        private readonly object _gate = new object();
        private UnitSnapshot _previous;
        private UnitSnapshot _current;
        private UnitSnapshot _spare;
        private FieldUpload.Snapshot _fields;
        private FieldUpload.Snapshot _spareFields;
        private Thread _worker;
        private volatile bool _stop;
        private double _speed = 1.0;
        private double _lastTickMs;

        public BattleHost(Battle battle)
        {
            Battle = battle ?? throw new ArgumentNullException(nameof(battle));
            _simulation = battle.Start(new SerialRunner());
            _terrain = new Terrain(battle.PlayedMap);
            int capacity = _simulation.World.Units.Capacity;
            _previous = new UnitSnapshot(capacity);
            _current = new UnitSnapshot(capacity);
            _spare = new UnitSnapshot(capacity);
            Fill(_previous);
            Fill(_current);
            _fields = new FieldUpload.Snapshot(battle.Map.Columns, battle.Map.Rows);
            _spareFields = new FieldUpload.Snapshot(battle.Map.Columns, battle.Map.Rows);
            _fields.Fill(_simulation.World, 0, battle.Map.CellSizeMm);
            _blockersSnap = new BlockerSnapshot(32);
            _blockersSpare = new BlockerSnapshot(32);
            _blockersSnap.FillFrom(_simulation.World, 0);
            _strikesSnap = new StrikeSnapshot();
            _strikesSpare = new StrikeSnapshot();
            _strikesSnap.FillFrom(_simulation.World, 0);
            _herdsSnap = new HerdSnapshot();
            _herdsSpare = new HerdSnapshot();
            _herdsSnap.FillFrom(_simulation.World, 0);
            RegimentSides = new Side[_simulation.World.Regiments.Capacity];
            for (int r = 0; r < RegimentSides.Length; r++)
            {
                RegimentSides[r] = _simulation.World.RegimentSide[r];
            }
        }

        public Battle Battle { get; }

        public Ruleset Rules => Battle.Rules;

        public MapData Map => Battle.PlayedMap;

        /// <summary>Each regiment's side, copied at the start (sides never change in a battle).</summary>
        public Side[] RegimentSides { get; }

        /// <summary>Game seconds per real second; 0 pauses. Read by the worker between ticks.</summary>
        public double Speed
        {
            get => Volatile.Read(ref _speed);
            set => Volatile.Write(ref _speed, Math.Max(0, value));
        }

        /// <summary>How long the last tick took on the worker, in ms.</summary>
        public double LastTickMs => Volatile.Read(ref _lastTickMs);

        public bool IsRunning => _worker != null;

        public void Start()
        {
            if (_worker != null)
            {
                return;
            }
            _stop = false;
            _worker = new Thread(Run) { IsBackground = true, Name = "GodGame battle" };
            _worker.Start();
        }

        public void Stop()
        {
            if (_worker == null)
            {
                return;
            }
            _stop = true;
            _worker.Join();
            _worker = null;
        }

        public void Dispose() => Stop();

        /// <summary>Steps on the calling thread (tests and tools); only while the worker is stopped.</summary>
        public void StepNow(int ticks)
        {
            if (_worker != null)
            {
                throw new InvalidOperationException(
                    "The worker owns the simulation while it runs."
                );
            }
            for (int i = 0; i < ticks; i++)
            {
                StepAndPublish();
            }
        }

        /// <summary>The two newest snapshots and how far real time is from the older to the newer, 0 to 1.</summary>
        public void Read(out UnitSnapshot previous, out UnitSnapshot current, out float alpha)
        {
            lock (_gate)
            {
                previous = _previous;
                current = _current;
            }
            double sinceMs =
                (Stopwatch.GetTimestamp() - current.PublishedTicks) * 1000.0 / Stopwatch.Frequency;
            if (Speed <= 0)
            {
                alpha = 1f; // paused: show the newest tick, not one behind
                return;
            }
            double tickMs = 1000.0 / SimConstants.TicksPerSecond / Speed;
            alpha = (float)Math.Min(1.0, Math.Max(0.0, sinceMs / tickMs));
        }

        /// <summary>
        /// The newest field snapshot (main thread). It is replaced, never changed, while the main thread holds it, except
        /// when an upload takes longer than two field steps: a visual glitch at worst.
        /// </summary>
        public FieldUpload.Snapshot ReadFields()
        {
            lock (_gate)
            {
                return _fields;
            }
        }

        /// <summary>Copies the latest deck/ice segment strain snapshot.</summary>
        public void ReadStructures(StructureSnapshot into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }
            lock (_gate)
            {
                if (_structuresSnap == null)
                {
                    into.Count = 0;
                    return;
                }
                _structuresSnap.CopyTo(into);
            }
        }

        /// <summary>Copies the latest regiment morale/order snapshot.</summary>
        public void ReadRegiments(RegimentSnapshot into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }
            lock (_gate)
            {
                if (_regimentsSnap == null)
                {
                    into.Count = 0;
                    return;
                }
                _regimentsSnap.CopyTo(into);
            }
        }

        /// <summary>Copies the simulation's cause log thread-safely.</summary>
        public CauseEvent[] ReadCauses()
        {
            lock (_gate)
            {
                return _causesSnap;
            }
        }

        /// <summary>Copies the latest battle-made blocker snapshot thread-safely.</summary>
        public void ReadBlockers(BlockerSnapshot into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }
            lock (_gate)
            {
                _blockersSnap?.CopyTo(into);
            }
        }

        /// <summary>Copies the latest lightning, felled-tree, and fire state snapshot thread-safely.</summary>
        public void ReadStrikes(StrikeSnapshot into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }
            lock (_gate)
            {
                _strikesSnap?.CopyTo(into);
            }
        }

        /// <summary>Copies the latest herd mood and stampede snapshot thread-safely.</summary>
        public void ReadHerds(HerdSnapshot into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }
            lock (_gate)
            {
                _herdsSnap?.CopyTo(into);
            }
        }

        /// <summary>The exception that stopped the worker, if any (shown by the debug view).</summary>
        public Exception Failure => Volatile.Read(ref _failure);

        private Exception _failure;
        private StructureSnapshot _structuresSnap;
        private StructureSnapshot _structuresSpare;
        private RegimentSnapshot _regimentsSnap;
        private RegimentSnapshot _regimentsSpare;
        private BlockerSnapshot _blockersSnap;
        private BlockerSnapshot _blockersSpare;
        private StrikeSnapshot _strikesSnap;
        private StrikeSnapshot _strikesSpare;
        private HerdSnapshot _herdsSnap;
        private HerdSnapshot _herdsSpare;
        private CauseEvent[] _causesSnap = Array.Empty<CauseEvent>();

        private void Run()
        {
            try
            {
                Loop();
            }
            catch (Exception e)
            {
                Volatile.Write(ref _failure, e);
            }
        }

        private void Loop()
        {
            var clock = Stopwatch.StartNew();
            double simulatedMs = 0;
            double lastWall = 0;
            while (!_stop)
            {
                double wall = clock.Elapsed.TotalMilliseconds;
                simulatedMs += (wall - lastWall) * Speed;
                lastWall = wall;
                double tickMs = 1000.0 / SimConstants.TicksPerSecond;
                if (simulatedMs >= tickMs)
                {
                    simulatedMs = Math.Min(simulatedMs - tickMs, 4 * tickMs); // never spiral after a hitch
                    StepAndPublish();
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
        }

        private void StepAndPublish()
        {
            long start = Stopwatch.GetTimestamp();
            bool fieldStep = TickOrder.FieldsRate.IsDue(_simulation.Tick);
            _simulation.Step();
            Volatile.Write(
                ref _lastTickMs,
                (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency
            );
            var next = _spare;
            Fill(next);
            var nextStructs = _structuresSpare ?? new StructureSnapshot(64);
            FillStructures(nextStructs);
            var nextRegs = _regimentsSpare ?? new RegimentSnapshot(32);
            FillRegiments(nextRegs);
            var nextBlockers = _blockersSpare ?? new BlockerSnapshot(32);
            nextBlockers.FillFrom(_simulation.World, _simulation.Tick);
            var nextStrikes = _strikesSpare ?? new StrikeSnapshot();
            nextStrikes.FillFrom(_simulation.World, _simulation.Tick);
            var nextHerds = _herdsSpare ?? new HerdSnapshot();
            nextHerds.FillFrom(_simulation.World, _simulation.Tick);
            var causes = CopyCauses();
            lock (_gate)
            {
                // The oldest snapshot becomes the spare. A main-thread frame longer than a tick may still be
                // drawing from it while it refills: a visual glitch at worst, never a simulation effect.
                _spare = _previous;
                _previous = _current;
                _current = next;
                _structuresSpare = _structuresSnap;
                _structuresSnap = nextStructs;
                _regimentsSpare = _regimentsSnap;
                _regimentsSnap = nextRegs;
                _blockersSpare = _blockersSnap;
                _blockersSnap = nextBlockers;
                _strikesSpare = _strikesSnap;
                _strikesSnap = nextStrikes;
                _herdsSpare = _herdsSnap;
                _herdsSnap = nextHerds;
                _causesSnap = causes;
            }
            if (fieldStep)
            {
                var fields = _spareFields;
                fields.Fill(_simulation.World, _simulation.Tick, Battle.Map.CellSizeMm);
                lock (_gate)
                {
                    _spareFields = _fields;
                    _fields = fields;
                }
            }
        }

        private void Fill(UnitSnapshot s)
        {
            var world = _simulation.World;
            var map = Battle.PlayedMap;
            var structs = world.Structures;
            var ballistic = world.Ballistic;
            int count = world.Units.HighWater;
            s.Tick = _simulation.Tick;
            s.Count = count;
            for (int u = 0; u < count; u++)
            {
                bool alive = world.Units.IsAlive(u);
                s.Alive[u] = alive;
                byte flags = (byte)world.Flags[u];
                s.Flags[u] = flags;
                FixVec2 p = world.Position[u];
                s.X[u] = p.X.Raw / (float)Fix.OneRaw;
                s.Z[u] = p.Y.Raw / (float)Fix.OneRaw;
                float groundY = _terrain.HeightMmAt(p) / 1000f;
                if (alive && (flags & (byte)UnitFlags.Launched) != 0 && ballistic != null)
                {
                    s.Y[u] = groundY + ballistic.HeightMm[u] / 1000f;
                    float elapsedS = ballistic.Ticks[u] / (float)SimConstants.TicksPerSecond;
                    s.SpinRad[u] =
                        (ballistic.SpinCdPerS[u] / 100f) * (float)(Math.PI / 180.0) * elapsedS;
                }
                else
                {
                    float y = groundY;
                    if (alive && structs != null && map != null)
                    {
                        int cell = CellIndexAt(map, p);
                        int deckSeg =
                            cell >= 0 && map.Decks != null ? map.Decks.SegmentOfCell[cell] : -1;
                        if (deckSeg >= 0 && deckSeg < structs.Count && !structs.Broken[deckSeg])
                        {
                            float deckY = map.Decks.Segments[deckSeg].DeckHeightMm / 1000f;
                            if (deckY > y)
                            {
                                y = deckY;
                            }
                        }
                        else if (cell >= 0 && structs.Count > structs.StaticCount)
                        {
                            for (int ds = structs.StaticCount; ds < structs.Count; ds++)
                            {
                                if (structs.Broken[ds] || structs.BattleCells[ds] == null)
                                {
                                    continue;
                                }
                                var bCells = structs.BattleCells[ds];
                                for (int k = 0; k < bCells.Length; k++)
                                {
                                    if (bCells[k] == cell)
                                    {
                                        float dy = structs.BattleDeckHeight[ds] / 1000f;
                                        if (dy > y)
                                        {
                                            y = dy;
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    s.Y[u] = y;
                    s.SpinRad[u] = 0f;
                }
                s.Regiment[u] = world.UnitRegiment[u];
                s.UnitType[u] = world.UnitType[u];
            }
            s.PublishedTicks = Stopwatch.GetTimestamp();
        }

        private void FillStructures(StructureSnapshot into)
        {
            var world = _simulation.World;
            var structs = world.Structures;
            var map = Battle.PlayedMap;
            var rules = Battle.Rules;
            into.Tick = _simulation.Tick;
            if (structs == null || structs.Count == 0)
            {
                into.Count = 0;
                return;
            }
            into.EnsureCapacity(structs.Count);
            into.Count = structs.Count;
            int staticCount = map?.Decks != null ? map.Decks.Segments.Count : 0;
            for (int s = 0; s < structs.Count; s++)
            {
                into.LoadPermille[s] = (ushort)
                    Math.Max(0, Math.Min(ushort.MaxValue, structs.LoadPermille[s]));
                into.StrengthPermille[s] = (ushort)
                    Math.Max(0, Math.Min(ushort.MaxValue, structs.Strength[s]));
                into.Stage[s] = structs.Stage[s];
                into.Broken[s] = structs.Broken[s];
                if (s < staticCount)
                {
                    var seg = map.Decks.Segments[s];
                    into.DeckHeightM[s] = seg.DeckHeightMm / 1000f;
                    into.KindIndex[s] = seg.Kind != null ? seg.Kind.Index : -1;
                    into.KindIds[s] = seg.Kind != null ? seg.Kind.Id : string.Empty;
                    into.Cells[s] = seg.Cells;
                }
                else
                {
                    int kindIdx = structs.BattleKind[s];
                    into.DeckHeightM[s] = structs.BattleDeckHeight[s] / 1000f;
                    into.KindIndex[s] = kindIdx;
                    into.KindIds[s] =
                        kindIdx >= 0 && kindIdx < rules.Structures.Kinds.Count
                            ? rules.Structures.Kinds[kindIdx].Id
                            : string.Empty;
                    into.Cells[s] = structs.BattleCells[s] ?? Array.Empty<int>();
                }
            }
        }

        private void FillRegiments(RegimentSnapshot into)
        {
            var world = _simulation.World;
            int count = world.Regiments.HighWater;
            into.Tick = _simulation.Tick;
            into.EnsureCapacity(count);
            into.Count = count;
            for (int r = 0; r < count; r++)
            {
                into.Morale[r] = (ushort)
                    Math.Max(0, Math.Min(ushort.MaxValue, world.RegimentMorale[r]));
                into.Order[r] = world.Army.Order[r];
                into.Alive[r] = world.Regiments.IsAlive(r);
                into.Wiped[r] = world.Army.Wiped[r];
                into.Routing[r] = world.Army.Order[r] == RegimentOrder.Rout;
                into.AliveCount[r] = world.RegimentAlive[r];
                into.StartingSize[r] = world.RegimentStartingSize[r];
                into.Side[r] = world.RegimentSide[r];
            }
        }

        private CauseEvent[] CopyCauses()
        {
            var log = _simulation.World.Causes;
            if (log == null || log.Events.Count == 0)
            {
                return Array.Empty<CauseEvent>();
            }
            var arr = new CauseEvent[log.Events.Count];
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = log.Events[i];
            }
            return arr;
        }

        private static int CellIndexAt(MapData map, FixVec2 p)
        {
            long xMm = (p.X.Raw * 1000L) >> Fix.FractionalBits;
            long yMm = (p.Y.Raw * 1000L) >> Fix.FractionalBits;
            if (xMm < 0 || yMm < 0)
            {
                return -1;
            }
            int col = (int)(xMm / map.CellSizeMm);
            int row = (int)(yMm / map.CellSizeMm);
            if ((uint)col >= (uint)map.Columns || (uint)row >= (uint)map.Rows)
            {
                return -1;
            }
            return row * map.Columns + col;
        }
    }

    /// <summary>Snapshot of deck/ice segments' load, strain stage, strength and broken state for Unity rendering.</summary>
    public sealed class StructureSnapshot
    {
        public int Tick;
        public int Count;
        public ushort[] LoadPermille;
        public ushort[] StrengthPermille;
        public StrainStage[] Stage;
        public bool[] Broken;
        public float[] DeckHeightM;
        public int[] KindIndex;
        public string[] KindIds;
        public int[][] Cells;

        public StructureSnapshot(int capacity = 64)
        {
            int cap = Math.Max(16, capacity);
            LoadPermille = new ushort[cap];
            StrengthPermille = new ushort[cap];
            Stage = new StrainStage[cap];
            Broken = new bool[cap];
            DeckHeightM = new float[cap];
            KindIndex = new int[cap];
            KindIds = new string[cap];
            Cells = new int[cap][];
        }

        public void EnsureCapacity(int required)
        {
            if (required <= LoadPermille.Length)
            {
                return;
            }
            int cap = Math.Max(required, LoadPermille.Length * 2);
            LoadPermille = new ushort[cap];
            StrengthPermille = new ushort[cap];
            Stage = new StrainStage[cap];
            Broken = new bool[cap];
            DeckHeightM = new float[cap];
            KindIndex = new int[cap];
            KindIds = new string[cap];
            Cells = new int[cap][];
        }

        public void CopyTo(StructureSnapshot target)
        {
            target.EnsureCapacity(Count);
            target.Tick = Tick;
            target.Count = Count;
            Array.Copy(LoadPermille, target.LoadPermille, Count);
            Array.Copy(StrengthPermille, target.StrengthPermille, Count);
            Array.Copy(Stage, target.Stage, Count);
            Array.Copy(Broken, target.Broken, Count);
            Array.Copy(DeckHeightM, target.DeckHeightM, Count);
            Array.Copy(KindIndex, target.KindIndex, Count);
            Array.Copy(KindIds, target.KindIds, Count);
            Array.Copy(Cells, target.Cells, Count);
        }
    }

    /// <summary>Snapshot of regiment morale, order and casualty state for strategic-zoom tinting and HUDs.</summary>
    public sealed class RegimentSnapshot
    {
        public int Tick;
        public int Count;
        public ushort[] Morale;
        public RegimentOrder[] Order;
        public bool[] Alive;
        public bool[] Wiped;
        public bool[] Routing;
        public int[] AliveCount;
        public int[] StartingSize;
        public Side[] Side;

        public RegimentSnapshot(int capacity = 32)
        {
            int cap = Math.Max(8, capacity);
            Morale = new ushort[cap];
            Order = new RegimentOrder[cap];
            Alive = new bool[cap];
            Wiped = new bool[cap];
            Routing = new bool[cap];
            AliveCount = new int[cap];
            StartingSize = new int[cap];
            Side = new Side[cap];
        }

        public void EnsureCapacity(int required)
        {
            if (required <= Morale.Length)
            {
                return;
            }
            int cap = Math.Max(required, Morale.Length * 2);
            Morale = new ushort[cap];
            Order = new RegimentOrder[cap];
            Alive = new bool[cap];
            Wiped = new bool[cap];
            Routing = new bool[cap];
            AliveCount = new int[cap];
            StartingSize = new int[cap];
            Side = new Side[cap];
        }

        public void CopyTo(RegimentSnapshot target)
        {
            target.EnsureCapacity(Count);
            target.Tick = Tick;
            target.Count = Count;
            Array.Copy(Morale, target.Morale, Count);
            Array.Copy(Order, target.Order, Count);
            Array.Copy(Alive, target.Alive, Count);
            Array.Copy(Wiped, target.Wiped, Count);
            Array.Copy(Routing, target.Routing, Count);
            Array.Copy(AliveCount, target.AliveCount, Count);
            Array.Copy(StartingSize, target.StartingSize, Count);
            Array.Copy(Side, target.Side, Count);
        }
    }

    /// <summary>Snapshot of battle-made blockers (<see cref="Blockers"/>) read from the simulation.</summary>
    public sealed class BlockerSnapshot
    {
        public int Tick;
        public int Count;
        public BlockerKind[] Kind;
        public int[] EventIndex;
        public bool[] Cleared;
        public int[] Clearer;
        public int[] Progress;
        public bool[] ClosesRoute;
        public int[][] Cells;
        public byte[][] Profile;

        public BlockerSnapshot(int capacity = 32)
        {
            int cap = Math.Max(8, capacity);
            Kind = new BlockerKind[cap];
            EventIndex = new int[cap];
            Cleared = new bool[cap];
            Clearer = new int[cap];
            Progress = new int[cap];
            ClosesRoute = new bool[cap];
            Cells = new int[cap][];
            Profile = new byte[cap][];
        }

        public void EnsureCapacity(int required)
        {
            if (required <= Kind.Length)
            {
                return;
            }
            int oldCap = Kind.Length;
            int cap = Math.Max(required, oldCap * 2);
            Array.Resize(ref Kind, cap);
            Array.Resize(ref EventIndex, cap);
            Array.Resize(ref Cleared, cap);
            Array.Resize(ref Clearer, cap);
            Array.Resize(ref Progress, cap);
            Array.Resize(ref ClosesRoute, cap);
            Array.Resize(ref Cells, cap);
            Array.Resize(ref Profile, cap);
        }

        public void FillFrom(World world, int tick)
        {
            Tick = tick;
            var blockers = world?.Blockers;
            int count = blockers != null ? blockers.Count : 0;
            Count = count;
            EnsureCapacity(count);
            for (int i = 0; i < count; i++)
            {
                Kind[i] = blockers.Kind(i);
                EventIndex[i] = blockers.Event(i);
                Cleared[i] = blockers.IsCleared(i);
                Clearer[i] = blockers.Clearer(i);
                Progress[i] = blockers.Progress(i);
                ClosesRoute[i] = blockers.ClosesRoute(i);

                int[] srcCells = blockers.Cells(i);
                int len = srcCells != null ? srcCells.Length : 0;
                if (Cells[i] == null || Cells[i].Length != len)
                {
                    Cells[i] = new int[len];
                }
                if (len > 0)
                {
                    Array.Copy(srcCells, Cells[i], len);
                }

                byte[] srcProf = blockers.Profile(i);
                int plen = srcProf != null ? srcProf.Length : 0;
                if (Profile[i] == null || Profile[i].Length != plen)
                {
                    Profile[i] = new byte[plen];
                }
                if (plen > 0)
                {
                    Array.Copy(srcProf, Profile[i], plen);
                }
            }
        }

        public void CopyTo(BlockerSnapshot target)
        {
            target.EnsureCapacity(Count);
            target.Tick = Tick;
            target.Count = Count;
            Array.Copy(Kind, target.Kind, Count);
            Array.Copy(EventIndex, target.EventIndex, Count);
            Array.Copy(Cleared, target.Cleared, Count);
            Array.Copy(Clearer, target.Clearer, Count);
            Array.Copy(Progress, target.Progress, Count);
            Array.Copy(ClosesRoute, target.ClosesRoute, Count);
            for (int i = 0; i < Count; i++)
            {
                int[] srcCells = Cells[i];
                int len = srcCells != null ? srcCells.Length : 0;
                if (target.Cells[i] == null || target.Cells[i].Length != len)
                {
                    target.Cells[i] = new int[len];
                }
                if (len > 0)
                {
                    Array.Copy(srcCells, target.Cells[i], len);
                }

                byte[] srcProf = Profile[i];
                int plen = srcProf != null ? srcProf.Length : 0;
                if (target.Profile[i] == null || target.Profile[i].Length != plen)
                {
                    target.Profile[i] = new byte[plen];
                }
                if (plen > 0)
                {
                    Array.Copy(srcProf, target.Profile[i], plen);
                }
            }
        }
    }
}
