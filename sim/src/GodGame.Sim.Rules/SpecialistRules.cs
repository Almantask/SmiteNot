using System;
using System.Collections.Generic;

namespace GodGame.Sim.Rules
{
    /// <summary>Rules every specialist action shares (spec S2, section 14.1): when a threat pauses the work, and for how long.</summary>
    public sealed class SpecialistCommonRules
    {
        /// <summary>An enemy regiment's outline this close pauses the action, and the specialists fall back.</summary>
        public int PauseWhenEnemyWithinMm { get; internal set; }

        public int FallBackMm { get; internal set; }

        /// <summary>The action resumes after this long with no enemy that close.</summary>
        public int ResumeAfterClearTicks { get; internal set; }
    }

    /// <summary>Spademen (S2 14.3, D5): a ditch and bank along a line the player draws in the army layer.</summary>
    public sealed class DigDitchRules
    {
        public int MaxPoints { get; internal set; }

        /// <summary>The line stays within this distance of the regiment's own deployment zone (the zone or its buffer).</summary>
        public int WithinZoneMm { get; internal set; }

        /// <summary>The line's first point is within this distance of the regiment's deployment point.</summary>
        public int StartWithinMm { get; internal set; }

        /// <summary>Ditch length the regiment can dig, per 10 soldiers.</summary>
        public int MmPer10Soldiers { get; internal set; }

        public int DitchDepthMm { get; internal set; }

        /// <summary>Untouched cells between the ditch and the bank.</summary>
        public int BermCells { get; internal set; }

        public int BankHeightMm { get; internal set; }

        /// <summary>Cells along the line dug per section.</summary>
        public int SectionCellsAlong { get; internal set; }

        /// <summary>Ticks per section with the whole regiment.</summary>
        public int TicksPerSection { get; internal set; }

        /// <summary>A depleted regiment digs at living ÷ starting soldiers, never slower than this.</summary>
        public int MinRatePermille { get; internal set; }
    }

    /// <summary>Fascine Carriers (S2 14.4): fill one crossing where the army's path meets a channel, ditch, mud or ford.</summary>
    public sealed class FillCrossingRules
    {
        /// <summary>How far ahead of the officer along the regiment's path a fillable run triggers the action.</summary>
        public int AheadMm { get; internal set; }

        /// <summary>A cell at least this far below the run's rim is fillable.</summary>
        public int BelowRimMm { get; internal set; }

        /// <summary>…or mud at least this deep (0–65,535)…</summary>
        public int MudAtLeast { get; internal set; }

        /// <summary>…or water this deep (a ford).</summary>
        public int WaterMinMm { get; internal set; }

        public int WaterMaxMm { get; internal set; }

        /// <summary>The crossing's width across the path, in cells.</summary>
        public int WidthCells { get; internal set; }

        /// <summary>At most this many cells along the path.</summary>
        public int MaxLengthCells { get; internal set; }

        /// <summary>Each bundle raises one cell's bed by this much, up to the lower rim.</summary>
        public int BundleRaiseMm { get; internal set; }

        /// <summary>Ticks between bundles (2 a second).</summary>
        public int TicksPerBundle { get; internal set; }

        /// <summary>A filled cell where depth × flow speed reaches this (mm·m/s)…</summary>
        public int WashoutDepthSpeed { get; internal set; }

        /// <summary>…for this long…</summary>
        public int WashoutTicks { get; internal set; }

        /// <summary>…loses this much fill.</summary>
        public int WashoutLossMm { get; internal set; }
    }

    /// <summary>Engineers' bridgework (spec S2 14.7): trestles over short gaps and repairs to weakened wooden spans.</summary>
    public sealed class BridgeworkRules
    {
        /// <summary>The longest gap a trestle spans, along the path.</summary>
        public int MaxGapMm { get; internal set; }

        /// <summary>How far ahead of the officer along the regiment's path the trestle trigger looks.</summary>
        public int AheadMm { get; internal set; }

        public int TicksPerSegment { get; internal set; }

        public int TimberMmPerSegment { get; internal set; }

        /// <summary>A wooden segment within this distance…</summary>
        public int RepairWithinMm { get; internal set; }

        /// <summary>…below this share of its maximum strength gets repaired…</summary>
        public int RepairBelowPermille { get; internal set; }

        /// <summary>…at this many ‰ a second…</summary>
        public int RepairPermillePerS { get; internal set; }

        /// <summary>…up to this…</summary>
        public int RepairUpToPermille { get; internal set; }

        /// <summary>…for this much timber per 250‰ restored.</summary>
        public int TimberMmPer250Permille { get; internal set; }

        /// <summary>Timber a regiment carries per 40 soldiers.</summary>
        public int TimberMmPer40Soldiers { get; internal set; }
    }

    /// <summary>Engineers' clearing (spec S2 14.7): blockers on the army's path within reach (M5: placed boulders).</summary>
    public sealed class ClearBlockerRules
    {
        public int WithinMm { get; internal set; }

        /// <summary>A blocker counts as on the path when it lies this close to the path's cells.</summary>
        public int NearPathMm { get; internal set; }

        public int BoulderTicks { get; internal set; }

        /// <summary>Clearing time a regiment carries per 40 soldiers.</summary>
        public int TicksPer40Soldiers { get; internal set; }
    }

    /// <summary>
    /// The shared specialist executor's rules (<c>data/rules/specialists.json</c>, spec S2 section 14). Ruleset v0 holds
    /// the actions whose targets exist in M5 so far; the Pontoon Train, Crossing Marshals, Engineers and Drovers join
    /// with bridges (M5 item 4) and herds (item 7).
    /// </summary>
    public sealed class SpecialistRules
    {
        public SpecialistCommonRules Common { get; internal set; } = new SpecialistCommonRules();

        public DigDitchRules? DigDitch { get; internal set; }

        public FillCrossingRules? FillCrossing { get; internal set; }

        public BridgeworkRules? Bridgework { get; internal set; }

        public ClearBlockerRules? ClearBlocker { get; internal set; }

        /// <summary>The action ids the file defines, for unit types to name.</summary>
        public IReadOnlyList<string> ActionIds { get; internal set; } = Array.Empty<string>();
    }
}
