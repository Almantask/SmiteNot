using System;
using System.Collections.Generic;

namespace GodGame.Sim
{
    /// <summary>
    /// The scrubbable future's engine (plan, M4; tech notes, section 6): one battle simulated forward with a keyframe
    /// (a full snapshot) every <see cref="KeyframeTicks"/>, and plan edits that re-simulate from the earliest tick they
    /// can change. Army and terrain edits change tick 0, so the battle starts over; a cast edit changes nothing before
    /// the earlier of its old and new ticks, so the timeline resumes from the last keyframe at or before that tick,
    /// rebased onto the edited plan. A resumed battle is identical, hash for hash, to the edited plan run from the start.
    /// <para>
    /// Rebasing: a keyframe belongs to its plan's battle (its id covers the plan). Before the change tick the two plans'
    /// worlds are the same except for the casts layer's shape, so the keyframe is read with the old plan's layout, its
    /// cast states and cast events are carried over by cast id (casts that changed haven't acted yet, so they start
    /// pending), and it is written again under the new battle's id.
    /// </para>
    /// </summary>
    public sealed class Timeline
    {
        /// <summary>Plan, section 3: a keyframe every 5 s.</summary>
        public const int DefaultKeyframeTicks = 5 * SimConstants.TicksPerSecond;

        private readonly IParallelRunner _runner;

        // The battle each held keyframe was written by, parallel to Keyframes.Ticks: keyframes kept across an edit still
        // have their own plan's cast layout and battle id, and are rebased from it when resumed.
        private readonly List<int> _sourceTicks = new List<int>();
        private readonly List<Battle> _sources = new List<Battle>();

        public Timeline(
            Battle battle,
            IParallelRunner runner,
            KeyframeStore keyframes,
            int keyframeTicks = DefaultKeyframeTicks
        )
        {
            Battle = battle ?? throw new ArgumentNullException(nameof(battle));
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));
            Keyframes = keyframes ?? throw new ArgumentNullException(nameof(keyframes));
            if (keyframeTicks < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(keyframeTicks));
            }
            KeyframeTicks = keyframeTicks;
            Simulation = battle.Start(runner);
            DropFrom(0);
            AddKeyframe(0, Simulation.CaptureSnapshot(), battle);
        }

        public Battle Battle { get; private set; }

        /// <summary>The head: the simulation the timeline steps. Replaced by edits; don't keep a reference across one.</summary>
        public Simulation Simulation { get; private set; }

        public KeyframeStore Keyframes { get; }

        public int KeyframeTicks { get; }

        /// <summary>How far the future has been simulated.</summary>
        public int Tick => Simulation.Tick;

        /// <summary>Steps the head to <paramref name="tick"/>, keeping a keyframe at every multiple of <see cref="KeyframeTicks"/>.</summary>
        public void AdvanceTo(int tick)
        {
            while (Simulation.Tick < tick)
            {
                int next = (Simulation.Tick / KeyframeTicks + 1) * KeyframeTicks;
                Simulation.Step(System.Math.Min(tick, next) - Simulation.Tick);
                if (Simulation.Tick % KeyframeTicks == 0)
                {
                    AddKeyframe(Simulation.Tick, Simulation.CaptureSnapshot(), Battle);
                }
            }
        }

        /// <summary>
        /// Switches to an edited plan (same rules, map and seed) and returns the tick the head now stands at: the last
        /// keyframe at or before the earliest change, from which the caller re-simulates with <see cref="AdvanceTo"/>.
        /// </summary>
        public int Edit(BattlePlan plan)
        {
            if (plan == null)
            {
                throw new ArgumentNullException(nameof(plan));
            }
            var old = Battle;
            var edited = new Battle(old.Rules, old.Map, plan, old.Seed);
            int change = EarliestChange(old.Plan, plan);
            if (change == 0)
            {
                Restart(edited);
                return 0;
            }
            // All-or-nothing: the new head is built before anything the timeline holds changes.
            int at;
            byte[] rebased;
            if (change >= Simulation.Tick)
            {
                // Nothing the head has simulated changes: keep it, under the edited plan.
                at = Simulation.Tick;
                rebased = Rebase(old, edited, Simulation.CaptureSnapshot(), at);
            }
            else if (Keyframes.TryGetAtOrBefore(change, out at, out byte[] keyframe) && at > 0)
            {
                rebased = Rebase(SourceOf(at), edited, keyframe, at);
            }
            else
            {
                Restart(edited);
                return 0;
            }
            var head = edited.Start(_runner);
            head.RestoreSnapshot(rebased);
            Battle = edited;
            Simulation = head;
            DropFrom(at);
            if (at % KeyframeTicks == 0)
            {
                AddKeyframe(at, rebased, edited);
            }
            return at;
        }

        private void Restart(Battle battle)
        {
            var head = battle.Start(_runner);
            Battle = battle;
            Simulation = head;
            DropFrom(0);
            AddKeyframe(0, head.CaptureSnapshot(), battle);
        }

        private void AddKeyframe(int tick, byte[] keyframe, Battle source)
        {
            Keyframes.Add(tick, keyframe);
            _sourceTicks.Add(tick);
            _sources.Add(source);
        }

        private void DropFrom(int tick)
        {
            Keyframes.DropFrom(tick);
            for (int i = _sourceTicks.Count - 1; i >= 0 && _sourceTicks[i] >= tick; i--)
            {
                _sourceTicks.RemoveAt(i);
                _sources.RemoveAt(i);
            }
        }

        private Battle SourceOf(int tick)
        {
            for (int i = _sourceTicks.Count - 1; i >= 0; i--)
            {
                if (_sourceTicks[i] == tick)
                {
                    return _sources[i];
                }
            }
            throw new InvalidOperationException("No battle is recorded for the keyframe at tick " + tick + ".");
        }

        /// <summary>
        /// The first tick at which two plans for the same map can differ: 0 when the army or terrain layers differ, else
        /// the earliest tick of a cast added, removed or changed (old or new tick, whichever is earlier), or
        /// <see cref="int.MaxValue"/> when nothing that matters differs.
        /// </summary>
        public static int EarliestChange(BattlePlan a, BattlePlan b)
        {
            if (!SameArmy(a, b) || !SameTerrain(a, b))
            {
                return 0;
            }
            int change = int.MaxValue;
            foreach (var cast in a.Casts)
            {
                var other = FindCast(b, cast.Id);
                if (other == null || !SameCast(cast, other))
                {
                    change = System.Math.Min(change, cast.Tick);
                    if (other != null)
                    {
                        change = System.Math.Min(change, other.Tick);
                    }
                }
            }
            foreach (var cast in b.Casts)
            {
                if (FindCast(a, cast.Id) == null)
                {
                    change = System.Math.Min(change, cast.Tick);
                }
            }
            return change;
        }

        private static PlannedCast? FindCast(BattlePlan plan, string id)
        {
            foreach (var cast in plan.Casts)
            {
                if (string.Equals(cast.Id, id, StringComparison.Ordinal))
                {
                    return cast;
                }
            }
            return null;
        }

        private static bool SameCast(PlannedCast a, PlannedCast b) =>
            string.Equals(a.Power.Id, b.Power.Id, StringComparison.Ordinal)
            && a.Tick == b.Tick
            && a.XMm == b.XMm
            && a.YMm == b.YMm
            && a.RadiusMm == b.RadiusMm;

        private static bool SameArmy(BattlePlan a, BattlePlan b)
        {
            if (a.Army.Count != b.Army.Count)
            {
                return false;
            }
            for (int i = 0; i < a.Army.Count; i++)
            {
                var x = a.Army[i];
                var y = b.Army[i];
                if (
                    !string.Equals(x.Id, y.Id, StringComparison.Ordinal)
                    || x.Side != y.Side
                    || !string.Equals(x.UnitType, y.UnitType, StringComparison.Ordinal)
                    || x.Soldiers != y.Soldiers
                    || x.XMm != y.XMm
                    || x.YMm != y.YMm
                    || x.FacingCentiDegrees != y.FacingCentiDegrees
                )
                {
                    return false;
                }
            }
            return true;
        }

        private static bool SameTerrain(BattlePlan a, BattlePlan b)
        {
            if (a.Terrain.Count != b.Terrain.Count)
            {
                return false;
            }
            for (int i = 0; i < a.Terrain.Count; i++)
            {
                var x = a.Terrain[i];
                var y = b.Terrain[i];
                if (
                    !string.Equals(x.Id, y.Id, StringComparison.Ordinal)
                    || !string.Equals(x.Rules.Id, y.Rules.Id, StringComparison.Ordinal)
                    || x.WidthMm != y.WidthMm
                    || x.DepthMm != y.DepthMm
                    || x.HeightMm != y.HeightMm
                    || x.LitresPerSecond != y.LitresPerSecond
                    || !SamePoints(x.PointsMm, y.PointsMm)
                )
                {
                    return false;
                }
            }
            return true;
        }

        private static bool SamePoints(int[] a, int[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>A keyframe of <paramref name="old"/>'s battle, as the same moment of <paramref name="edited"/>'s.</summary>
        private static byte[] Rebase(Battle old, Battle edited, byte[] keyframe, int expectedTick)
        {
            // The old plan's world layout (deployment only: no systems, no navigation to build).
            var world = BattleSetup.Deploy(old.PlayedMap, old.Plan, old.Rules).CreateBlank();
            var (id, tick, seed) = Snapshot.Read(keyframe, world);
            if (id != old.Id || seed != old.Seed || tick != expectedTick)
            {
                throw new FormatException("The keyframe doesn't belong to this timeline's battle at that tick.");
            }

            // Casts: carried over by id; a cast that isn't in the old plan unchanged starts pending (it hasn't acted).
            var oldCasts = old.Plan.Casts;
            var newCasts = edited.Plan.Casts;
            var states = CastSystem.StatesFor(edited.Plan);
            var from = world.Casts!;
            var oldToNew = new int[oldCasts.Count];
            int newWrite = 0;
            for (int c = 0; c < newCasts.Count; c++)
            {
                int o = IndexOf(oldCasts, newCasts[c].Id);
                if (o >= 0 && SameCast(oldCasts[o], newCasts[c]))
                {
                    oldToNew[o] = c + 1; // +1: 0 marks "gone"
                    states.Status[c] = from.Status[o];
                    states.Reasons[c] = from.Reasons[o];
                    states.NearestAttacker[c] = from.NearestAttacker[o];
                    states.AttackerDistanceMm[c] = from.AttackerDistanceMm[o];
                    states.NearestDefender[c] = from.NearestDefender[o];
                    states.DefenderDistanceMm[c] = from.DefenderDistanceMm[o];
                    states.Event[c] = from.Event[o];
                    int oldWrite = WriteOffset(oldCasts, o);
                    for (int w = 0; w < newCasts[c].Power.Writes.Count; w++)
                    {
                        states.Carry[newWrite + w] = from.Carry[oldWrite + w];
                    }
                }
                else if (o >= 0 && from.Status[o] != CastStatus.Pending)
                {
                    throw new InvalidOperationException(
                        "A changed cast had already acted at the keyframe; the edit's earliest change is wrong."
                    );
                }
                newWrite += newCasts[c].Power.Writes.Count;
            }
            world.ReplaceCasts(states);
            world.Causes?.RemapCastSubjects(oldToNew);
            return Snapshot.Write(edited.Id, tick, seed, world);
        }

        private static int IndexOf(IReadOnlyList<PlannedCast> casts, string id)
        {
            for (int i = 0; i < casts.Count; i++)
            {
                if (string.Equals(casts[i].Id, id, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        private static int WriteOffset(IReadOnlyList<PlannedCast> casts, int index)
        {
            int offset = 0;
            for (int i = 0; i < index; i++)
            {
                offset += casts[i].Power.Writes.Count;
            }
            return offset;
        }
    }
}
