using System;
using System.Collections.Generic;

namespace GodGame.Sim.Rules
{
    /// <summary>The kinds of terrain piece in ruleset v0 (spec S1, section 9).</summary>
    public enum PieceKind
    {
        /// <summary>A dug polyline: bottom cells at −depth, one side cell each way at half that.</summary>
        Channel = 0,

        /// <summary>A raised polyline: a crest, then sides sloping down to the original ground.</summary>
        Ridge = 1,

        /// <summary>The ridge's negative (S1 D6): a floor, then sides sloping up to the original ground.</summary>
        Hollow = 2,

        /// <summary>A point: a 3 × 3-cell source block flowing all battle.</summary>
        Spring = 3,

        /// <summary>A point: a 2 × 2-cell stone, closed to every class and solid to water below its top.</summary>
        Boulder = 4,

        /// <summary>
        /// A polyline one cell wide that every class crosses at its own speed or not at all (spec S2 13.3: hedgerow,
        /// drystone wall). It edits no height; it stamps the map's piece speed layer after the height edits.
        /// </summary>
        Line = 5,
    }

    /// <summary>
    /// One terrain piece from <c>data/rules/terrain.json</c>. Fields a kind doesn't use are 0. Lengths in mm, slopes in ‰,
    /// discharge in L/s.
    /// </summary>
    public sealed class PieceRules
    {
        internal PieceRules(string id, PieceKind kind)
        {
            Id = id;
            Kind = kind;
        }

        public string Id { get; }

        public PieceKind Kind { get; }

        /// <summary>Whether the piece is drawn as a polyline (channel, ridge, hollow) rather than placed at a point.</summary>
        public bool IsPolyline => Kind <= PieceKind.Hollow || Kind == PieceKind.Line;

        public int MinPoints { get; internal set; }

        public int MaxPoints { get; internal set; }

        public int MinLengthMm { get; internal set; }

        public int MaxLengthMm { get; internal set; }

        /// <summary>Channel: the bottom width the player picks.</summary>
        public int MinWidthMm { get; internal set; }

        public int MaxWidthMm { get; internal set; }

        /// <summary>Channel and hollow: how deep below the original ground.</summary>
        public int MinDepthMm { get; internal set; }

        public int MaxDepthMm { get; internal set; }

        /// <summary>Ridge: how high above the original ground.</summary>
        public int MinHeightMm { get; internal set; }

        public int MaxHeightMm { get; internal set; }

        /// <summary>Ridge crest or hollow floor width.</summary>
        public int TopWidthMm { get; internal set; }

        /// <summary>Ridge and hollow sides.</summary>
        public int SideSlopePermille { get; internal set; }

        /// <summary>Channel: side cells on each edge, at <see cref="SideDepthPermille"/> of the depth.</summary>
        public int SideCells { get; internal set; }

        public int SideDepthPermille { get; internal set; }

        /// <summary>Spring: the source block's side in cells.</summary>
        public int SourceBlockCells { get; internal set; }

        public int MinDischargeLPerS { get; internal set; }

        public int MaxDischargeLPerS { get; internal set; }

        public int DefaultDischargeLPerS { get; internal set; }

        /// <summary>Boulder: its square footprint's side.</summary>
        public int SizeMm { get; internal set; }

        /// <summary>Boulder: how far it stands above the highest ground under it.</summary>
        public int BoulderHeightMm { get; internal set; }

        /// <summary>A line's material (hedge, stone): what burns or topples it once fire and tremors exist (M6).</summary>
        public string Material { get; internal set; } = "";

        /// <summary>A line's height, for presentation (height bonuses arrive with cover in M6).</summary>
        public int LineHeightMm { get; internal set; }

        /// <summary>A line's crossing speed per land class (foot, horse, wheeled), in ‰ of the class's speed; 0 is closed.</summary>
        public int[] CrossingPermille { get; internal set; } = new int[3];
    }

    /// <summary>The terrain pieces and the rules every placement shares (spec S1, section 9.1).</summary>
    public sealed class TerrainRules
    {
        internal TerrainRules(int bufferFromDeploymentMm, IReadOnlyList<PieceRules> pieces)
        {
            BufferFromDeploymentMm = bufferFromDeploymentMm;
            Pieces = pieces ?? throw new ArgumentNullException(nameof(pieces));
        }

        /// <summary>Every piece's whole footprint stays at least this far from both deployment zones (brief, 2026-10-08).</summary>
        public int BufferFromDeploymentMm { get; }

        public IReadOnlyList<PieceRules> Pieces { get; }

        /// <summary>The piece with this id, or null.</summary>
        public PieceRules? Piece(string id)
        {
            foreach (var piece in Pieces)
            {
                if (string.Equals(piece.Id, id, StringComparison.Ordinal))
                {
                    return piece;
                }
            }
            return null;
        }
    }
}
