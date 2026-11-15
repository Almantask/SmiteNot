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

        /// <summary>
        /// Two bank points and a width (spec S2 13.4): a deck of segments at the higher bank's height, carrying the
        /// classes its structure kind carries, over water that flows beneath it.
        /// </summary>
        Bridge = 6,

        /// <summary>
        /// Trees (spec S2 13.1): a grove (a circle of trees with woodland between) or a row along a polyline. Trunk cells
        /// close every class; everything is fuel for Lightning v0's fire.
        /// </summary>
        Trees = 7,

        /// <summary>A herd grazing in a circle (spec S2 11.1): animals that move, so the route check ignores them.</summary>
        Herd = 8,
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

        /// <summary>Whether the piece is drawn as a polyline (channel, ridge, hollow, line, tree row) rather than placed at a point.</summary>
        public bool IsPolyline =>
            Kind <= PieceKind.Hollow
            || Kind == PieceKind.Line
            || (Kind == PieceKind.Trees && !IsCircle);

        /// <summary>A grove or a herd: a circle the player places with a radius.</summary>
        public bool IsCircle { get; internal set; }

        public int MinRadiusMm { get; internal set; }

        public int MaxRadiusMm { get; internal set; }

        /// <summary>A grove's tree spacing: one tree per block this many cells square.</summary>
        public int TreeEveryCells { get; internal set; }

        /// <summary>A row's tree spacing along it.</summary>
        public int TreeSpacingMm { get; internal set; }

        public int TreeMinHeightMm { get; internal set; }

        public int TreeMaxHeightMm { get; internal set; }

        /// <summary>A trunk cell's crossing speed per land class (‰; 0 closed).</summary>
        public int[] TrunkPermille { get; internal set; } = new int[3];

        /// <summary>A grove's woodland cells' crossing speed per land class, or null for a row.</summary>
        public int[]? WoodlandPermille { get; internal set; }

        public int MinAnimals { get; internal set; }

        public int MaxAnimals { get; internal set; }

        public int DefaultAnimals { get; internal set; }

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

        /// <summary>A bridge's structure kind (<c>load.json</c>): its segments, strength and classes.</summary>
        public StructureKindRules? Structure { get; internal set; }

        public int MinSpanMm { get; internal set; }

        public int MaxSpanMm { get; internal set; }

        /// <summary>A bank point stands on ground at least this far above its tick-0 water (dry ground).</summary>
        public int BankMinAboveWaterMm { get; internal set; }

        /// <summary>Approach cells beyond each end, flat for the classes the bridge carries.</summary>
        public int FootprintEndCells { get; internal set; }

        /// <summary>A line's material (hedge, stone): what burns or topples it once fire and tremors exist (M6).</summary>
        public string Material { get; internal set; } = "";

        /// <summary>A line's height, for presentation (height bonuses arrive with cover in M6).</summary>
        public int LineHeightMm { get; internal set; }

        /// <summary>A line's crossing speed per land class (foot, horse, wheeled), in ‰ of the class's speed; 0 is closed.</summary>
        public int[] CrossingPermille { get; internal set; } = new int[3];
    }

    /// <summary>
    /// A battle-made blocker (spec S2 13.2: a wreck, a felled tree): how each land class crosses its cells, and how long
    /// clearing it takes Engineers, or any foot regiment when it closes its army's last route (D14). Battle-made blockers
    /// skip the lock-in route check (brief, 2026-10-09).
    /// </summary>
    public sealed class BlockerRules
    {
        public BlockerRules(
            int[] crossingPermille,
            int engineerClearTicks,
            int footRegimentClearTicks
        )
        {
            if (crossingPermille == null || crossingPermille.Length != 3)
            {
                throw new ArgumentException("Foot, horse and wheeled.", nameof(crossingPermille));
            }
            CrossingPermille = crossingPermille;
            EngineerClearTicks = engineerClearTicks;
            FootRegimentClearTicks = footRegimentClearTicks;
        }

        /// <summary>Crossing speed per land class (foot, horse, wheeled), ‰ of the class's speed; 0 is closed.</summary>
        public int[] CrossingPermille { get; }

        public int EngineerClearTicks { get; }

        public int FootRegimentClearTicks { get; }

        /// <summary>A felled tree's crown cells (profile 1), per land class (‰; 0 closed); null for a one-profile blocker.</summary>
        public int[]? CrownPermille { get; internal set; }

        public int CrownLengthMm { get; internal set; }

        public int CrownWidthCells { get; internal set; }

        /// <summary>Soldiers within this of a felled tree's cells when it lands are launched (those in them crushed).</summary>
        public int CrushEdgeMm { get; internal set; }
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

        /// <summary>A wrecked or abandoned engine's stamp (spec S2 13.2), or null in rules without one.</summary>
        public BlockerRules? Wreck { get; internal set; }

        /// <summary>A felled tree's trunk and crown (spec S2 13.2), or null.</summary>
        public BlockerRules? FelledTree { get; internal set; }

        /// <summary>A burning cell (fire v0, S2 12.2), or null.</summary>
        public BlockerRules? Fire { get; internal set; }

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
