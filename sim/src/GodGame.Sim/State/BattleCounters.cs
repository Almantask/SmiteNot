using System;
using GodGame.Sim.Math;

namespace GodGame.Sim
{
    /// <summary>
    /// How many hazard events the battle has had: swept soldiers, drownings, soldiers the crowd pushed over an edge and
    /// soldiers who reached a bank. Hashed and saved with the world, so statistics read the same after a restore; the
    /// cause log (M3 item 9) records who and why.
    /// </summary>
    public sealed class BattleCounters
    {
        /// <summary>Soldiers who lost their footing in water (each sweep counts, so one soldier can count twice).</summary>
        public int Swept { get; internal set; }

        /// <summary>Soldiers whose drowning timer ran out.</summary>
        public int Drowned { get; internal set; }

        /// <summary>Soldiers the crowd pushed over a lethal edge after teetering.</summary>
        public int PushedOver { get; internal set; }

        /// <summary>Swept soldiers who regained their footing.</summary>
        public int ReachedBank { get; internal set; }

        /// <summary>Soldiers killed in combat (melee and volleys).</summary>
        public int Killed { get; internal set; }

        /// <summary>Routing soldiers who ran off the map (they count as lost).</summary>
        public int Fled { get; internal set; }

        internal void WriteTo(ByteWriter writer)
        {
            writer.WriteInt32(Swept);
            writer.WriteInt32(Drowned);
            writer.WriteInt32(PushedOver);
            writer.WriteInt32(ReachedBank);
            writer.WriteInt32(Killed);
            writer.WriteInt32(Fled);
        }

        internal void ReadFrom(ByteReader reader)
        {
            Swept = reader.ReadInt32();
            Drowned = reader.ReadInt32();
            PushedOver = reader.ReadInt32();
            ReachedBank = reader.ReadInt32();
            Killed = reader.ReadInt32();
            Fled = reader.ReadInt32();
            if (
                Swept < 0
                || Drowned < 0
                || PushedOver < 0
                || ReachedBank < 0
                || Killed < 0
                || Fled < 0
            )
            {
                throw new FormatException("Snapshot counters are negative.");
            }
        }

        internal void AppendTo(SimHash hash)
        {
            hash.AppendInt32(Swept);
            hash.AppendInt32(Drowned);
            hash.AppendInt32(PushedOver);
            hash.AppendInt32(ReachedBank);
            hash.AppendInt32(Killed);
            hash.AppendInt32(Fled);
        }
    }
}
