using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// Where each soldier stands in a regiment's formation (tech notes, section 4): the officer or standard-bearer
    /// (slot 0) in front, then ranks of <c>files</c> soldiers behind, rank after rank, the first rank one rank
    /// spacing behind the officer. Offsets are from the officer, turned by the regiment's facing.
    /// </summary>
    public static class Formation
    {
        /// <summary>The files a regiment of this size forms: the type's width, or fewer for a small regiment.</summary>
        public static int FilesFor(FormationRules rules, int soldiers) =>
            System.Math.Max(1, System.Math.Min(rules.Files, soldiers - 1));

        /// <summary>Ranks behind the officer.</summary>
        public static int Ranks(int soldiers, int files) =>
            soldiers <= 1 ? 0 : (soldiers - 1 + files - 1) / files;

        /// <summary>A slot's offset from the officer, with the regiment facing <paramref name="facing"/>.</summary>
        public static FixVec2 SlotOffset(
            int slot,
            int files,
            Fix fileSpacing,
            Fix rankSpacing,
            Angle facing
        )
        {
            if (slot <= 0)
            {
                return FixVec2.Zero;
            }
            int i = slot - 1;
            int file = i % files;
            int rank = i / files;
            // Files are counted from the left end of the rank; offsets in half-spacings keep the rank centred.
            Fix left = fileSpacing * (files - 1 - 2 * file) / 2;
            Fix back = rankSpacing * (rank + 1);
            return FixVec2.FromAngle(facing + Angle.QuarterTurn, left)
                + FixVec2.FromAngle(facing + Angle.HalfTurn, back);
        }
    }
}
