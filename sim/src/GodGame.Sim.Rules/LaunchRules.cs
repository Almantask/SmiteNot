using System;
using System.Collections.Generic;

namespace GodGame.Sim.Rules
{
    /// <summary>
    /// Which way a launch source throws (spec S2 9.2): its caller passes the heading, or the spread picks any. Validated at
    /// load but not read by the simulation yet: the collapse (the only source so far) passes no heading; charges,
    /// stampedes, trees and stones (M5 items 6 and 7) will pass theirs.
    /// </summary>
    public enum LaunchHeading
    {
        /// <summary>The charging rider's heading.</summary>
        Rider = 0,

        /// <summary>The herd's run heading.</summary>
        HerdRun = 1,

        /// <summary>Perpendicular away from the falling trunk.</summary>
        AwayFromTrunk = 2,

        /// <summary>Away from where the stone came down.</summary>
        AwayFromImpact = 3,

        /// <summary>Any direction, seeded per soldier (a collapse).</summary>
        Seeded = 4,
    }

    /// <summary>One launch source (S2 9.2): the launch velocity before the spread, and where its heading comes from.</summary>
    public sealed class LaunchSourceRules
    {
        public LaunchSourceRules(
            int index,
            string id,
            int horizontalMmPerS,
            int verticalMmPerS,
            LaunchHeading heading
        )
        {
            Index = index;
            Id = id;
            HorizontalMmPerS = horizontalMmPerS;
            VerticalMmPerS = verticalMmPerS;
            Heading = heading;
        }

        public int Index { get; }

        public string Id { get; }

        public int HorizontalMmPerS { get; }

        public int VerticalMmPerS { get; }

        public LaunchHeading Heading { get; }
    }

    /// <summary>What one kind of landing does to the soldier (S2 9.3).</summary>
    public readonly struct LandingRules
    {
        public LandingRules(int damage, int stunTicks)
        {
            Damage = damage;
            StunTicks = stunTicks;
        }

        public int Damage { get; }

        public int StunTicks { get; }
    }

    /// <summary>
    /// Launched soldiers (<c>data/rules/launches.json</c>, spec S2 section 9): ballistic bodies under reduced gravity, a
    /// seeded spread per (source event, soldier), and what each landing surface does.
    /// </summary>
    public sealed class LaunchRules
    {
        public int GravityMmPerS2 { get; internal set; } = 4000;

        public int SpinMinCdPerS { get; internal set; }

        public int SpinMaxCdPerS { get; internal set; }

        /// <summary>The heading spread either side, centi-degrees.</summary>
        public int SpreadHeadingCd { get; internal set; }

        /// <summary>The speed spread either side, ‰ (both components).</summary>
        public int SpreadSpeedPermille { get; internal set; }

        public IReadOnlyList<LaunchSourceRules> Sources { get; internal set; } =
            Array.Empty<LaunchSourceRules>();

        public LandingRules Ground { get; internal set; }

        /// <summary>Intact ice: as ground (S2 9.3), with its own numbers.</summary>
        public LandingRules Ice { get; internal set; }

        /// <summary>Water that doesn't sweep the class.</summary>
        public LandingRules ShallowWater { get; internal set; }

        /// <summary>Deep water within <see cref="FringeMm"/> of a cell the class can stand in.</summary>
        public LandingRules Fringe { get; internal set; }

        public int FringeMm { get; internal set; }

        /// <summary>A landing <see cref="HardFallDropMm"/> or more below the surface he was launched from.</summary>
        public LandingRules HardFall { get; internal set; }

        public int HardFallDropMm { get; internal set; }

        /// <summary>Thrown into a hard blocker: moved back along the arc to the nearest open cell.</summary>
        public LandingRules HardBlocker { get; internal set; }

        /// <summary>Soldiers on their feet within this of a landing are knocked down.</summary>
        public int KnockDownRadiusMm { get; internal set; }

        public int KnockDownTicks { get; internal set; }

        /// <summary>The landing-spread preview's cell (S2 9.4).</summary>
        public int PreviewCellMm { get; internal set; } = 1500;

        /// <summary>The source with this id, or null.</summary>
        public LaunchSourceRules? Source(string id)
        {
            foreach (var source in Sources)
            {
                if (string.Equals(source.Id, id, StringComparison.Ordinal))
                {
                    return source;
                }
            }
            return null;
        }
    }
}
