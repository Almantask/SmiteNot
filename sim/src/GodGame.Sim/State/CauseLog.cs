using System;
using System.Collections.Generic;
using GodGame.Sim.Math;

namespace GodGame.Sim
{
    /// <summary>What a cause event records (spec S1, section 12; plan, section 3: the causality trace).</summary>
    public enum CauseKind : byte
    {
        /// <summary>A cast passed the range rule. Subject: the cast's index in the plan.</summary>
        CastFired = 1,

        /// <summary>A cast fizzled. Subject: the cast's index; detail: its <see cref="FizzleReasons"/>.</summary>
        CastFizzled = 2,

        /// <summary>A water tile first held water that sweeps foot soldiers. Subject: the water tile.</summary>
        RiverOverflowed = 3,

        /// <summary>Soldiers of a regiment lost their footing (once per regiment per 5 s). Subject: the regiment.</summary>
        RegimentSwept = 4,

        /// <summary>The crowd pushed a soldier over a lethal edge. Subject: his regiment; detail: his unit slot.</summary>
        SoldierPushedOver = 5,

        /// <summary>A swept soldier's drowning timer ran out. Subject: his regiment; detail: his unit slot.</summary>
        SoldierDrowned = 6,

        /// <summary>A soldier died in combat (melee or a volley). Subject: his regiment; detail: the regiment that struck him.</summary>
        SoldierKilled = 7,

        /// <summary>A routing soldier ran off the map. Subject: his regiment; detail: his unit slot.</summary>
        SoldierFled = 8,

        /// <summary>A regiment's morale broke. Subject: the regiment; detail: 1 for a rout it may rally from, 2 for a final one.</summary>
        RegimentRouted = 9,

        /// <summary>A routing regiment re-formed. Subject: the regiment.</summary>
        RegimentRallied = 10,

        /// <summary>A regiment fell below about 10% of its starting size. Subject: the regiment.</summary>
        RegimentWiped = 11,
    }

    /// <summary>One entry in the cause log: what, when, and the (up to two) earlier events that caused it.</summary>
    public readonly struct CauseEvent
    {
        public CauseEvent(CauseKind kind, int tick, int subject, int detail, int cause, int cause2)
        {
            Kind = kind;
            Tick = tick;
            Subject = subject;
            Detail = detail;
            Cause = cause;
            Cause2 = cause2;
        }

        public CauseKind Kind { get; }

        public int Tick { get; }

        public int Subject { get; }

        public int Detail { get; }

        /// <summary>The event that caused this one, or <see cref="CauseLog.None"/> (the map itself: river water, a push).</summary>
        public int Cause { get; }

        /// <summary>A second cause (a soldier pushed in, then drowned), or <see cref="CauseLog.None"/>.</summary>
        public int Cause2 { get; }
    }

    /// <summary>
    /// The battle's cause log v1 (spec S1, section 12; M3 item 9): an append-only list of cause events, each linked to
    /// what caused it, so a result can be walked back to the plan (<see cref="Trace"/>: soldier drowned → regiment swept →
    /// cast). Water carries its cause with it: every water tile has a tag, the newest event whose water reached it (rain
    /// stamps its cast's event; tags spread downstream with the flow, <see cref="CauseSystem"/>); the map's own river
    /// water has none. Events are added only by serial code in tick order, so their indices are the same everywhere.
    /// Hashed and snapshotted with the world.
    /// </summary>
    public sealed class CauseLog
    {
        /// <summary>No event: the map's own water, or a cause the log doesn't track.</summary>
        public const int None = -1;

        private readonly List<CauseEvent> _events = new List<CauseEvent>();

        public CauseLog(int waterTiles, int regimentCapacity, int unitCapacity)
        {
            TileTag = Filled(waterTiles);
            TileOverflowed = Filled(waterTiles);
            RegimentSwept = Filled(regimentCapacity);
            RegimentSweptTick = new int[regimentCapacity];
            UnitSwept = Filled(unitCapacity);
            UnitPushed = Filled(unitCapacity);
        }

        private static int[] Filled(int length)
        {
            var array = new int[length];
            for (int i = 0; i < length; i++)
            {
                array[i] = None;
            }
            return array;
        }

        public IReadOnlyList<CauseEvent> Events => _events;

        /// <summary>Per water tile: the newest event whose water reached it, or <see cref="None"/>.</summary>
        public int[] TileTag { get; }

        /// <summary>Per water tile: its <see cref="CauseKind.RiverOverflowed"/> event, or <see cref="None"/>.</summary>
        public int[] TileOverflowed { get; }

        /// <summary>Per regiment: its latest <see cref="CauseKind.RegimentSwept"/> event and that event's tick.</summary>
        public int[] RegimentSwept { get; }

        public int[] RegimentSweptTick { get; }

        /// <summary>Per unit slot: the sweep event covering the soldier while it is swept, or <see cref="None"/>.</summary>
        public int[] UnitSwept { get; }

        /// <summary>Per unit slot: the push that put the soldier in the water, or <see cref="None"/>.</summary>
        public int[] UnitPushed { get; }

        /// <summary>
        /// Renumbers the cast events' subjects after a plan edit: <paramref name="oldToNew"/>[old cast index] is the new
        /// index + 1, or 0 for a cast the edit removed or changed (which can't have acted yet when this is called).
        /// </summary>
        internal void RemapCastSubjects(int[] oldToNew)
        {
            for (int i = 0; i < _events.Count; i++)
            {
                var e = _events[i];
                if (e.Kind != CauseKind.CastFired && e.Kind != CauseKind.CastFizzled)
                {
                    continue;
                }
                int to = e.Subject < oldToNew.Length ? oldToNew[e.Subject] : 0;
                if (to == 0)
                {
                    throw new InvalidOperationException(
                        "A cast event belongs to a cast the edit changed."
                    );
                }
                _events[i] = new CauseEvent(e.Kind, e.Tick, to - 1, e.Detail, e.Cause, e.Cause2);
            }
        }

        /// <summary>Appends an event and returns its index.</summary>
        public int Add(
            CauseKind kind,
            int tick,
            int subject,
            int detail,
            int cause,
            int cause2 = None
        )
        {
            if (cause >= _events.Count || cause2 >= _events.Count)
            {
                throw new ArgumentException("A cause must come before what it causes.");
            }
            _events.Add(new CauseEvent(kind, tick, subject, detail, cause, cause2));
            return _events.Count - 1;
        }

        /// <summary>
        /// The causality trace: the event and everything it links back to, each once, depth first in a fixed order
        /// (cause before second cause). The last entries are the roots: casts, pushes, the map's own water.
        /// </summary>
        public List<CauseEvent> Trace(int index)
        {
            var trace = new List<CauseEvent>();
            var seen = new bool[_events.Count];
            var stack = new Stack<int>();
            stack.Push(index);
            while (stack.Count > 0)
            {
                int e = stack.Pop();
                if (e < 0 || seen[e])
                {
                    continue;
                }
                seen[e] = true;
                var ev = _events[e];
                trace.Add(ev);
                stack.Push(ev.Cause2);
                stack.Push(ev.Cause);
            }
            return trace;
        }

        internal void WriteTo(ByteWriter writer)
        {
            writer.WriteInt32(_events.Count);
            foreach (var e in _events)
            {
                writer.WriteByte((byte)e.Kind);
                writer.WriteInt32(e.Tick);
                writer.WriteInt32(e.Subject);
                writer.WriteInt32(e.Detail);
                writer.WriteInt32(e.Cause);
                writer.WriteInt32(e.Cause2);
            }
            foreach (var array in Arrays())
            {
                writer.WriteInt32(array.Length);
                foreach (int v in array)
                {
                    writer.WriteInt32(v);
                }
            }
        }

        internal void ReadFrom(ByteReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > 50_000_000)
            {
                throw new FormatException("Snapshot cause log is implausibly large.");
            }
            _events.Clear();
            for (int i = 0; i < count; i++)
            {
                byte kind = reader.ReadByte();
                int tick = reader.ReadInt32();
                int subject = reader.ReadInt32();
                int detail = reader.ReadInt32();
                int cause = reader.ReadInt32();
                int cause2 = reader.ReadInt32();
                if (
                    kind < (byte)CauseKind.CastFired
                    || kind > (byte)CauseKind.RegimentWiped
                    || cause < None
                    || cause >= i
                    || cause2 < None
                    || cause2 >= i
                )
                {
                    throw new FormatException("Snapshot cause event is invalid.");
                }
                _events.Add(new CauseEvent((CauseKind)kind, tick, subject, detail, cause, cause2));
            }
            foreach (var array in Arrays())
            {
                if (reader.ReadInt32() != array.Length)
                {
                    throw new FormatException("Snapshot cause log has another size.");
                }
                for (int i = 0; i < array.Length; i++)
                {
                    int v = reader.ReadInt32();
                    // Every array holds event indices except the sweep ticks.
                    if (!ReferenceEquals(array, RegimentSweptTick) && (v < None || v >= count))
                    {
                        throw new FormatException("Snapshot cause log links to a missing event.");
                    }
                    array[i] = v;
                }
            }
        }

        internal void AppendTo(SimHash hash)
        {
            hash.AppendInt32(_events.Count);
            foreach (var e in _events)
            {
                hash.AppendInt32((int)e.Kind);
                hash.AppendInt32(e.Tick);
                hash.AppendInt32(e.Subject);
                hash.AppendInt32(e.Detail);
                hash.AppendInt32(e.Cause);
                hash.AppendInt32(e.Cause2);
            }
            foreach (var array in Arrays())
            {
                foreach (int v in array)
                {
                    hash.AppendInt32(v);
                }
            }
        }

        private int[][] Arrays() =>
            new[]
            {
                TileTag,
                TileOverflowed,
                RegimentSwept,
                RegimentSweptTick,
                UnitSwept,
                UnitPushed,
            };
    }
}
