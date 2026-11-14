using System;
using System.Collections.Generic;

namespace GodGame.Sim
{
    /// <summary>One preview cell of a landing spread: how many landed there, and how many of those landings were lethal.</summary>
    public readonly struct LandingCell
    {
        public LandingCell(int x, int y, int stunned, int lethal)
        {
            X = x;
            Y = y;
            Stunned = stunned;
            Lethal = lethal;
        }

        /// <summary>The preview cell, counted from the map's south-west corner in preview cells.</summary>
        public int X { get; }

        public int Y { get; }

        /// <summary>Landings that hurt or stunned (drawn in the stun colour).</summary>
        public int Stunned { get; }

        /// <summary>Landings in deep water, off a cliff or killed by the blow (drawn red).</summary>
        public int Lethal { get; }
    }

    /// <summary>
    /// The landing-spread preview (spec S2 9.4; brief, 2026-10-09: "the preview shows where soldiers will land"): per launch
    /// source, by its stable identity (one failed segment, one rider's charge, one herd's stampede), the landings its
    /// launches led to, gathered into
    /// cells of <see cref="Rules.LaunchRules.PreviewCellMm"/>, each with a stun and a lethal count. Built from the cause log
    /// the preview's own simulation wrote, so it is exact: the landings shown are the landings that happen.
    /// Presentation data: never read by the simulation.
    /// </summary>
    public sealed class LandingSpread
    {
        private LandingSpread(int source, int[] events, LandingCell[] cells)
        {
            Source = source;
            Events = events;
            Cells = cells;
        }

        /// <summary>
        /// The source's stable identity (<see cref="Ballistics.SourceId"/>: the launch source and its key, such as the failed
        /// segment), which doesn't move when an unrelated event is logged earlier, so the preview can match a spread across
        /// edits.
        /// </summary>
        public int Source { get; }

        /// <summary>The source events whose launches landed here (the cause links), ascending.</summary>
        public IReadOnlyList<int> Events { get; }

        /// <summary>The cells landed in, ordered by row, then column.</summary>
        public IReadOnlyList<LandingCell> Cells { get; }

        public int Stunned
        {
            get
            {
                int n = 0;
                foreach (var c in Cells)
                {
                    n += c.Stunned;
                }
                return n;
            }
        }

        public int Lethal
        {
            get
            {
                int n = 0;
                foreach (var c in Cells)
                {
                    n += c.Lethal;
                }
                return n;
            }
        }

        /// <summary>
        /// Every source's spread in <paramref name="log"/>, ordered by stable source identity. <paramref name="columns"/> and
        /// <paramref name="cellSizeMm"/> are the terrain grid's (landing details name terrain cells); each landing counts in
        /// the preview cell holding its terrain cell's centre.
        /// </summary>
        public static List<LandingSpread> FromLog(
            CauseLog log,
            int columns,
            int cellSizeMm,
            int previewCellMm
        )
        {
            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }
            if (columns < 1 || cellSizeMm < 1 || previewCellMm < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(columns));
            }
            var events = log.Events;
            // (source, preview cell) → (stunned, lethal), in a sorted list so the order never depends on hashing.
            var counts =
                new SortedDictionary<(int Source, int Y, int X), (int Stunned, int Lethal)>();
            var sourceEvents = new SortedDictionary<(int Source, int Event), bool>();
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (e.Kind != CauseKind.SoldierLanded || e.Cause < 0)
                {
                    continue;
                }
                var launch = events[e.Cause]; // landed → launched → its source
                int source = launch.Detail;
                sourceEvents[(source, launch.Cause)] = true;
                int cell = Landing.Cell(e.Detail);
                long cx = (long)(cell % columns) * cellSizeMm + cellSizeMm / 2;
                long cy = (long)(cell / columns) * cellSizeMm + cellSizeMm / 2;
                var key = (source, (int)(cy / previewCellMm), (int)(cx / previewCellMm));
                counts.TryGetValue(key, out var c);
                counts[key] = Landing.IsLethal(Landing.Outcome(e.Detail))
                    ? (c.Stunned, c.Lethal + 1)
                    : (c.Stunned + 1, c.Lethal);
            }
            var result = new List<LandingSpread>();
            int[] EventsOf(int source)
            {
                var list = new List<int>();
                foreach (var pair in sourceEvents)
                {
                    if (pair.Key.Source == source && pair.Key.Event >= 0)
                    {
                        list.Add(pair.Key.Event);
                    }
                }
                return list.ToArray();
            }
            var cells = new List<LandingCell>();
            int current = int.MinValue;
            foreach (var pair in counts)
            {
                if (pair.Key.Source != current && cells.Count > 0)
                {
                    result.Add(new LandingSpread(current, EventsOf(current), cells.ToArray()));
                    cells.Clear();
                }
                current = pair.Key.Source;
                cells.Add(
                    new LandingCell(pair.Key.X, pair.Key.Y, pair.Value.Stunned, pair.Value.Lethal)
                );
            }
            if (cells.Count > 0)
            {
                result.Add(new LandingSpread(current, EventsOf(current), cells.ToArray()));
            }
            return result;
        }
    }
}
