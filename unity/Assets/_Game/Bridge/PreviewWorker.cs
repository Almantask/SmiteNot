using System;
using System.Diagnostics;
using System.Threading;
using GodGame.Sim;

namespace GodGame.Bridge
{
    /// <summary>
    /// The preview (plan, sections 3.6 and 9, M4): one background thread that owns a <see cref="Timeline"/> and fills the
    /// battle's future forward to <see cref="EndTick"/>, like a video's buffering bar. The main thread posts plan edits
    /// with <see cref="Edit"/> and reads <see cref="ReadProgress"/>; it never waits on the simulation. An edit is picked up
    /// at the next chunk boundary (<see cref="ChunkTicks"/>), re-simulates from the earliest tick it changes, and fills
    /// the 30 s after that point first (they are simply the next ticks the worker runs), reporting when they are ready.
    /// Unity reads only; nothing here decides an outcome.
    /// </summary>
    public sealed class PreviewWorker : IDisposable
    {
        /// <summary>Plan section 10's working default: about 3 minutes per campaign battle.</summary>
        public const int DefaultEndTick = 180 * SimConstants.TicksPerSecond;

        /// <summary>The first window after an edit that the player sees fill first (plan section 8: ≤ 1.5 s for a cast edit).</summary>
        public const int FirstWindowTicks = 30 * SimConstants.TicksPerSecond;

        /// <summary>Ticks between looks at the edit mailbox: a second of battle.</summary>
        public const int ChunkTicks = SimConstants.TicksPerSecond;

        /// <summary>What the main thread may read, copied whole under a lock.</summary>
        public readonly struct Progress
        {
            public Progress(
                int edits,
                int resumedAt,
                int filled,
                int endTick,
                double firstWindowMs,
                double fillMs
            )
            {
                Edits = edits;
                ResumedAt = resumedAt;
                Filled = filled;
                EndTick = endTick;
                FirstWindowMs = firstWindowMs;
                FillMs = fillMs;
            }

            /// <summary>How many posted edits the worker has applied (the main thread compares with what it posted).</summary>
            public int Edits { get; }

            /// <summary>The tick the latest edit resumed from.</summary>
            public int ResumedAt { get; }

            /// <summary>How far the future is computed: the buffer bar's end.</summary>
            public int Filled { get; }

            public int EndTick { get; }

            /// <summary>Wall time from picking up the latest edit until the 30 s after its resume tick were filled, or -1.</summary>
            public double FirstWindowMs { get; }

            /// <summary>Wall time from picking up the latest edit until the end was filled, or -1.</summary>
            public double FillMs { get; }

            public bool FirstWindowReady => FirstWindowMs >= 0;

            public bool Complete => Filled >= EndTick;

            /// <summary>The buffer bar, 0 to 1.</summary>
            public float Fraction => EndTick <= 0 ? 1f : Math.Min(1f, Filled / (float)EndTick);
        }

        /// <summary>
        /// A cast's outcome in the latest filled future (plan, M4: red markers with reasons): pending until the fill
        /// passes its tick, then fired or fizzled, with each army's nearest regiment and its distance from the area.
        /// </summary>
        public readonly struct CastResult
        {
            public CastResult(
                string id,
                int tick,
                CastStatus status,
                FizzleReasons reasons,
                int nearestAttacker,
                int attackerMm,
                int nearestDefender,
                int defenderMm
            )
            {
                Id = id;
                Tick = tick;
                Status = status;
                Reasons = reasons;
                NearestAttacker = nearestAttacker;
                AttackerDistanceMm = attackerMm;
                NearestDefender = nearestDefender;
                DefenderDistanceMm = defenderMm;
            }

            public string Id { get; }

            public int Tick { get; }

            public CastStatus Status { get; }

            public FizzleReasons Reasons { get; }

            public int NearestAttacker { get; }

            public int AttackerDistanceMm { get; }

            public int NearestDefender { get; }

            public int DefenderDistanceMm { get; }

            /// <summary>The red marker: the range rule failed at the cast's tick.</summary>
            public bool Fizzled => Status == CastStatus.Fizzled;
        }

        private readonly Timeline _timeline;
        private PlaybackTrack _track;
        private CastResult[] _casts = Array.Empty<CastResult>();
        private CauseEvent[] _causes = Array.Empty<CauseEvent>();
        private BattlePlan _plan;
        private readonly object _gate = new object();
        private readonly AutoResetEvent _wake = new AutoResetEvent(false);
        private BattlePlan _pending;
        private int _posted;
        private Progress _progress;
        private Thread _worker;
        private volatile bool _stop;
        private Exception _failure;

        public PreviewWorker(
            Battle battle,
            IParallelRunner runner,
            KeyframeStore keyframes,
            int endTick = DefaultEndTick
        )
        {
            if (endTick < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(endTick));
            }
            EndTick = endTick;
            _timeline = new Timeline(battle, runner, keyframes);
            _track = _timeline.Track;
            _plan = battle.Plan;
            _progress = new Progress(0, 0, 0, endTick, -1, -1);
            _editStarted = Stopwatch.GetTimestamp(); // the first fill is timed from here
        }

        public int EndTick { get; }

        /// <summary>The exception that stopped the worker, if any.</summary>
        public Exception Failure => Volatile.Read(ref _failure);

        /// <summary>Edits posted so far; the worker has caught up when <see cref="Progress.Edits"/> equals it.</summary>
        public int Posted
        {
            get
            {
                lock (_gate)
                {
                    return _posted;
                }
            }
        }

        public void Start()
        {
            if (_worker != null)
            {
                return;
            }
            _stop = false;
            _worker = new Thread(Run) { IsBackground = true, Name = "GodGame preview" };
            _worker.Start();
        }

        public void Stop()
        {
            if (_worker == null)
            {
                return;
            }
            _stop = true;
            _wake.Set();
            _worker.Join();
            _worker = null;
        }

        public void Dispose()
        {
            Stop();
            _wake.Dispose();
        }

        /// <summary>Posts an edited plan; a newer post replaces one the worker hasn't picked up yet. Never blocks.</summary>
        public void Edit(BattlePlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }
            lock (_gate)
            {
                _pending = plan;
                _posted++;
            }
            _wake.Set();
        }

        /// <summary>The playback track of the plan the worker is filling (replaced, never changed, by an edit).</summary>
        public PlaybackTrack Track => Volatile.Read(ref _track);

        /// <summary>The plan the published casts and causes belong to.</summary>
        public BattlePlan Plan
        {
            get
            {
                lock (_gate)
                {
                    return _plan;
                }
            }
        }

        /// <summary>Each cast's outcome as far as the future is filled, in the plan's cast order.</summary>
        public CastResult[] ReadCasts()
        {
            lock (_gate)
            {
                return _casts;
            }
        }

        /// <summary>The cause log as far as the future is filled (a copy).</summary>
        public CauseEvent[] ReadCauses()
        {
            lock (_gate)
            {
                return _causes;
            }
        }

        public Progress ReadProgress()
        {
            lock (_gate)
            {
                return _progress;
            }
        }

        /// <summary>Runs the worker's loop on the calling thread until the future is full and no edit waits (tests).</summary>
        public void RunUntilIdle()
        {
            if (_worker != null)
            {
                throw new InvalidOperationException(
                    "The worker thread owns the timeline while it runs."
                );
            }
            while (StepOnce()) { }
        }

        private void Run()
        {
            try
            {
                while (!_stop)
                {
                    if (!StepOnce())
                    {
                        _wake.WaitOne();
                    }
                }
            }
            catch (Exception e)
            {
                Volatile.Write(ref _failure, e);
            }
        }

        private long _editStarted;
        private int _applied;

        /// <summary>Applies a waiting edit or fills one chunk. False when there is nothing to do.</summary>
        private bool StepOnce()
        {
            BattlePlan plan;
            int posted;
            lock (_gate)
            {
                plan = _pending;
                posted = _posted;
                _pending = null;
            }
            if (plan != null)
            {
                _editStarted = Stopwatch.GetTimestamp();
                int resumed = _timeline.Edit(plan);
                _applied = posted;
                Publish(resumed, -1, -1);
                return true;
            }
            int filled = _timeline.Tick;
            if (filled >= EndTick)
            {
                return false;
            }
            _timeline.AdvanceTo(Math.Min(EndTick, filled + ChunkTicks));
            var last = ReadProgress();
            double elapsed =
                (Stopwatch.GetTimestamp() - _editStarted) * 1000.0 / Stopwatch.Frequency;
            double first = last.FirstWindowMs;
            if (first < 0 && _timeline.Tick >= Math.Min(EndTick, last.ResumedAt + FirstWindowTicks))
            {
                first = elapsed;
            }
            double fill = _timeline.Tick >= EndTick ? elapsed : -1;
            Publish(last.ResumedAt, first, fill);
            return true;
        }

        private void Publish(int resumedAt, double firstWindowMs, double fillMs)
        {
            var progress = new Progress(
                _applied,
                resumedAt,
                _timeline.Tick,
                EndTick,
                firstWindowMs,
                fillMs
            );
            var world = _timeline.Simulation.World;
            var plan = _timeline.Battle.Plan;
            var states = world.Casts;
            var casts = new CastResult[plan.Casts.Count];
            for (int c = 0; c < casts.Length; c++)
            {
                casts[c] = new CastResult(
                    plan.Casts[c].Id,
                    plan.Casts[c].Tick,
                    states.Status[c],
                    states.Reasons[c],
                    states.NearestAttacker[c],
                    states.AttackerDistanceMm[c],
                    states.NearestDefender[c],
                    states.DefenderDistanceMm[c]
                );
            }
            var log = world.Causes;
            var causes = _causes;
            if (log != null && (causes.Length != log.Events.Count || !ReferenceEquals(plan, _plan)))
            {
                causes = new CauseEvent[log.Events.Count];
                for (int i = 0; i < causes.Length; i++)
                {
                    causes[i] = log.Events[i];
                }
            }
            lock (_gate)
            {
                _progress = progress;
                _casts = casts;
                _causes = causes;
                _plan = plan;
            }
            Volatile.Write(ref _track, _timeline.Track);
        }
    }
}
