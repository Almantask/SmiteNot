using System;
using System.Collections.Generic;

namespace GodGame.Sim.Rules
{
    /// <summary>One kind of deck (spec S2, section 8.1): a bridge, a pontoon, a trestle or ice.</summary>
    public sealed class StructureKindRules
    {
        public StructureKindRules(int index, string id)
        {
            Index = index;
            Id = id;
        }

        public int Index { get; }

        public string Id { get; }

        /// <summary>wood, stone or ice: Rot weakens wood; M6's fire and tremors read it too.</summary>
        public string Material { get; internal set; } = "";

        public bool IsWood => string.Equals(Material, "wood", StringComparison.Ordinal);

        public bool IsIce => string.Equals(Material, "ice", StringComparison.Ordinal);

        /// <summary>Cells along the span per segment (bridges).</summary>
        public int SegmentCellsAlong { get; internal set; }

        /// <summary>Ice: patches this many cells square, on a grid aligned to the map.</summary>
        public int PatchCells { get; internal set; }

        public int MinWidthMm { get; internal set; }

        public int MaxWidthMm { get; internal set; }

        /// <summary>Strength per m² of deck when sound (ice: per mm of thickness, see <see cref="IceKgPerM2PerMm"/>).</summary>
        public int StrengthKgPerM2 { get; internal set; }

        public int IceKgPerM2PerMm { get; internal set; }

        public int MinThicknessMm { get; internal set; }

        public int MaxThicknessMm { get; internal set; }

        /// <summary>Movement classes it carries, as bits (1 &lt;&lt; class).</summary>
        public int ClassMask { get; internal set; }
    }

    /// <summary>
    /// Load, strength, the strain stages and collapse (<c>data/rules/load.json</c>, spec S2 section 8): a segment fails
    /// when its load reaches its strength at <see cref="FailChecks"/> checks in a row; its neighbours lose part of their
    /// strength; the strain stages are for the player only.
    /// </summary>
    public sealed class StructureRules
    {
        public int FailChecks { get; internal set; }

        public int CreakPermille { get; internal set; }

        public int CrackPermille { get; internal set; }

        public int SagPermille { get; internal set; }

        public int StageHysteresisPermille { get; internal set; }

        public int NeighbourLossPermille { get; internal set; }

        public int IceNeighbourLossPermille { get; internal set; }

        /// <summary>Speed on intact ice per land class (foot, horse, wheeled), ‰ (S2 3.2).</summary>
        public int[] IceSpeedPermille { get; internal set; } = new int[3];

        /// <summary>Drowning timer in a broken-ice hole per land class (until S3 owns cold).</summary>
        public int[] ColdDrowningTicks { get; internal set; } = new int[3];

        public IReadOnlyList<StructureKindRules> Kinds { get; internal set; } =
            Array.Empty<StructureKindRules>();

        public StructureKindRules? Kind(string id)
        {
            foreach (var k in Kinds)
            {
                if (string.Equals(k.Id, id, StringComparison.Ordinal))
                {
                    return k;
                }
            }
            return null;
        }
    }

    /// <summary>Rot v0 (spec S2 12.3, D12): weakens wooden segments in its circle.</summary>
    public sealed class RotRules
    {
        /// <summary>Ticks over which a segment falls to its new strength.</summary>
        public int RotTicks { get; internal set; }

        /// <summary>A first Rot takes a segment to this share of its maximum.</summary>
        public int FloorPermille { get; internal set; }

        /// <summary>Each further Rot lowers it by this much more…</summary>
        public int StackPermille { get; internal set; }

        /// <summary>…but never below this.</summary>
        public int StackFloorPermille { get; internal set; }
    }
}
