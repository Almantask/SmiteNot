using System;
using GodGame.Sim.Math;

namespace GodGame.Sim
{
    /// <summary>Where a herd is in its life (spec S2 11.2–11.3).</summary>
    public enum HerdMode : byte
    {
        /// <summary>Walking between seeded waypoints inside its grazing circle.</summary>
        Grazing = 0,

        /// <summary>Fear reached the threshold: milling and raising dust, the warning, until the stampede.</summary>
        Agitated = 1,

        /// <summary>Running from the fear, steered by the ground.</summary>
        Stampede = 2,

        /// <summary>After its one stampede: wandering where it stopped; it never stampedes again.</summary>
        Scattered = 3,
    }

    /// <summary>
    /// Per herd (spec S2 11): the regiment slot its animals belong to, its mode and since when, its current fear, the run's
    /// heading and start, the point it scattered at, what drives it (a drovers' regiment and where they stand), and the
    /// cause events of its stampede. Plus, per animal slot (its formation slot), the tick it last hit a soldier.
    /// Hashed and snapshotted with the world.
    /// </summary>
    public sealed class HerdStates
    {
        public const int MaxHerds = 64;

        public int Count { get; private set; }

        public int[] Regiment { get; private set; } = new int[0];

        public HerdMode[] Mode { get; private set; } = new HerdMode[0];

        /// <summary>The tick the current mode began.</summary>
        public int[] Since { get; private set; } = new int[0];

        /// <summary>The fear from the last update (0-1,000).</summary>
        public int[] Fear { get; private set; } = new int[0];

        /// <summary>The run's heading (stampede), in angle units.</summary>
        public int[] Heading { get; private set; } = new int[0];

        /// <summary>Where the run began, then where the herd scattered.</summary>
        public FixVec2[] Anchor { get; private set; } = new FixVec2[0];

        /// <summary>The drovers' regiment driving it this update, or -1.</summary>
        public int[] DrivenBy { get; private set; } = new int[0];

        /// <summary>Where the driving drovers stand (the fear comes from there).</summary>
        public FixVec2[] DrivenFrom { get; private set; } = new FixVec2[0];

        /// <summary>The cause event of the agitation, then of the stampede, or none.</summary>
        public int[] Event { get; private set; } = new int[0];

        /// <summary>Per herd, per animal (formation slot): the tick it last hit a soldier, or int.MinValue / 2.</summary>
        public int[]?[] LastHit { get; private set; } = new int[]?[0];

        public const int NeverHit = int.MinValue / 2;

        /// <summary>Sets the herds up at deployment: one per herd regiment, grazing.</summary>
        public void Init(int[] regiments, int[] animals)
        {
            int n = regiments.Length;
            if (n > MaxHerds)
            {
                throw new ArgumentOutOfRangeException(nameof(regiments));
            }
            Count = n;
            Regiment = (int[])regiments.Clone();
            Mode = new HerdMode[n];
            Since = new int[n];
            Fear = new int[n];
            Heading = new int[n];
            Anchor = new FixVec2[n];
            DrivenBy = new int[n];
            DrivenFrom = new FixVec2[n];
            Event = new int[n];
            LastHit = new int[]?[n];
            for (int h = 0; h < n; h++)
            {
                DrivenBy[h] = -1;
                Event[h] = CauseLog.None;
                var hits = new int[animals[h]];
                for (int k = 0; k < hits.Length; k++)
                {
                    hits[k] = NeverHit;
                }
                LastHit[h] = hits;
            }
        }

        /// <summary>The herd whose animals regiment <paramref name="r"/> is, or -1.</summary>
        public int HerdOf(int r)
        {
            for (int h = 0; h < Count; h++)
            {
                if (Regiment[h] == r)
                {
                    return h;
                }
            }
            return -1;
        }

        internal void WriteTo(ByteWriter writer)
        {
            writer.WriteInt32(Count);
            for (int h = 0; h < Count; h++)
            {
                writer.WriteInt32(Regiment[h]);
                writer.WriteByte((byte)Mode[h]);
                writer.WriteInt32(Since[h]);
                writer.WriteInt32(Fear[h]);
                writer.WriteInt32(Heading[h]);
                writer.WriteInt64(Anchor[h].X.Raw);
                writer.WriteInt64(Anchor[h].Y.Raw);
                writer.WriteInt32(DrivenBy[h]);
                writer.WriteInt64(DrivenFrom[h].X.Raw);
                writer.WriteInt64(DrivenFrom[h].Y.Raw);
                writer.WriteInt32(Event[h]);
                var hits = LastHit[h]!;
                writer.WriteInt32(hits.Length);
                foreach (int t in hits)
                {
                    writer.WriteInt32(t);
                }
            }
        }

        internal void ReadFrom(ByteReader reader, int regimentCapacity)
        {
            int n = reader.ReadInt32();
            if (n < 0 || n > MaxHerds)
            {
                throw new FormatException("Snapshot herd count is out of range.");
            }
            var regiments = new int[n];
            var animals = new int[n];
            var modes = new HerdMode[n];
            var since = new int[n];
            var fear = new int[n];
            var heading = new int[n];
            var anchor = new FixVec2[n];
            var by = new int[n];
            var from = new FixVec2[n];
            var events = new int[n];
            var hits = new int[]?[n];
            for (int h = 0; h < n; h++)
            {
                regiments[h] = reader.ReadInt32();
                byte mode = reader.ReadByte();
                if (
                    regiments[h] < 0
                    || regiments[h] >= regimentCapacity
                    || mode > (byte)HerdMode.Scattered
                )
                {
                    throw new FormatException("Snapshot herd is invalid.");
                }
                modes[h] = (HerdMode)mode;
                since[h] = reader.ReadInt32();
                fear[h] = reader.ReadInt32();
                heading[h] = reader.ReadInt32();
                if (heading[h] < 0 || heading[h] >= Angle.UnitsPerTurn)
                {
                    throw new FormatException("Snapshot herd heading is out of range.");
                }
                anchor[h] = new FixVec2(
                    Fix.FromRaw(reader.ReadInt64()),
                    Fix.FromRaw(reader.ReadInt64())
                );
                by[h] = reader.ReadInt32();
                if (by[h] < -1 || by[h] >= regimentCapacity)
                {
                    throw new FormatException("Snapshot herd driver is out of range.");
                }
                from[h] = new FixVec2(
                    Fix.FromRaw(reader.ReadInt64()),
                    Fix.FromRaw(reader.ReadInt64())
                );
                events[h] = reader.ReadInt32();
                int count = reader.ReadInt32();
                if (count < 0 || count > 100_000)
                {
                    throw new FormatException("Snapshot herd size is out of range.");
                }
                animals[h] = count;
                var row = new int[count];
                for (int k = 0; k < count; k++)
                {
                    row[k] = reader.ReadInt32();
                }
                hits[h] = row;
            }
            Count = n;
            Regiment = regiments;
            Mode = modes;
            Since = since;
            Fear = fear;
            Heading = heading;
            Anchor = anchor;
            DrivenBy = by;
            DrivenFrom = from;
            Event = events;
            LastHit = hits;
        }

        internal void AppendTo(SimHash hash)
        {
            hash.AppendInt32(Count);
            for (int h = 0; h < Count; h++)
            {
                hash.AppendInt32(Regiment[h]);
                hash.AppendInt32((int)Mode[h]);
                hash.AppendInt32(Since[h]);
                hash.AppendInt32(Fear[h]);
                hash.AppendInt32(Heading[h]);
                hash.AppendInt64(Anchor[h].X.Raw);
                hash.AppendInt64(Anchor[h].Y.Raw);
                hash.AppendInt32(DrivenBy[h]);
                hash.AppendInt64(DrivenFrom[h].X.Raw);
                hash.AppendInt64(DrivenFrom[h].Y.Raw);
                hash.AppendInt32(Event[h]);
                foreach (int t in LastHit[h]!)
                {
                    hash.AppendInt32(t);
                }
            }
        }
    }
}
