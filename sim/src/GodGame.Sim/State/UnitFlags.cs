using System;

namespace GodGame.Sim
{
    /// <summary>
    /// Per-soldier state bits named in the tick order (plan, section 3.4). The systems that set and clear them
    /// arrive in later milestones; M1 only stores and hashes them.
    /// </summary>
    [Flags]
    public enum UnitFlags : uint
    {
        None = 0,

        /// <summary>A ballistic body after a charge, stampede, falling tree or siege stone (brief, 2026-10-09).</summary>
        Launched = 1 << 0,

        /// <summary>Down after a landing or a knock-down.</summary>
        Stunned = 1 << 1,

        /// <summary>Pressed against a lethal edge, arms out: the warning before going over.</summary>
        Teetering = 1 << 2,

        /// <summary>Carried by water.</summary>
        Swept = 1 << 3,

        /// <summary>Reached by a fire front.</summary>
        Burning = 1 << 4,

        /// <summary>Part of a routing regiment.</summary>
        Routing = 1 << 5,

        /// <summary>In a fixed slot on a tower or wall (tech notes, section 4).</summary>
        Garrisoned = 1 << 6,

        /// <summary>A rider who has made his charge's contact (spec S2 10.3): he takes no more targets until his regiment charges again.</summary>
        Charged = 1 << 7,
    }

    /// <summary>The two seats in every mode, and the herds, which pick no side (brief 2026-10-08).</summary>
    public enum Side : byte
    {
        Attacker = 0,
        Defender = 1,

        /// <summary>A herd (spec S2 11): neither army's, never a target, never fighting, never counted for either.</summary>
        Neutral = 2,
    }

    public static class Sides
    {
        /// <summary>Whether regiments of these sides fight each other: the two armies do; herds fight nobody.</summary>
        public static bool Hostile(Side a, Side b) =>
            a != b && a != Side.Neutral && b != Side.Neutral;
    }
}
