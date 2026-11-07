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
            public UnitSnapshot(int capacity)
            {
                X = new float[capacity];
                Y = new float[capacity];
                Z = new float[capacity];
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

        /// <summary>The exception that stopped the worker, if any (shown by the debug view).</summary>
        public Exception Failure => Volatile.Read(ref _failure);

        private Exception _failure;

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
            lock (_gate)
            {
                // The oldest snapshot becomes the spare. A main-thread frame longer than a tick may still be
                // drawing from it while it refills: a visual glitch at worst, never a simulation effect.
                _spare = _previous;
                _previous = _current;
                _current = next;
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
            int count = world.Units.HighWater;
            s.Tick = _simulation.Tick;
            s.Count = count;
            for (int u = 0; u < count; u++)
            {
                bool alive = world.Units.IsAlive(u);
                s.Alive[u] = alive;
                if (!alive)
                {
                    continue;
                }
                FixVec2 p = world.Position[u];
                s.X[u] = p.X.Raw / (float)Fix.OneRaw;
                s.Z[u] = p.Y.Raw / (float)Fix.OneRaw;
                s.Y[u] = _terrain.HeightMmAt(p) / 1000f;
                s.Regiment[u] = world.UnitRegiment[u];
                s.UnitType[u] = world.UnitType[u];
            }
            s.PublishedTicks = Stopwatch.GetTimestamp();
        }
    }
}
