using System;
using GodGame.Sim.Math;

namespace GodGame.Sim
{
    /// <summary>
    /// Launched soldiers' arcs (spec S2 9.1), structure-of-arrays by unit slot, in <see cref="World"/>. A slot holds an arc
    /// only while its soldier is <see cref="UnitFlags.Launched"/>; otherwise every value is zero. The arc's horizontal
    /// velocity is the soldier's <see cref="World.Velocity"/> (metres per tick) and its ground position his
    /// <see cref="World.Position"/>; this holds the vertical half and what the landing needs. Height follows the closed
    /// form z(n) = start + vz × n / 20 − g × n² / 800 (mm, n ticks flown), so an arc's flight time on level ground is exactly
    /// 2 × vz / g. Hashed (launched slots only, so a battle without launches hashes as before) and snapshotted with the world.
    /// </summary>
    public sealed class BallisticState
    {
        public BallisticState(int unitCapacity)
        {
            HeightMm = new int[unitCapacity];
            StartMm = new int[unitCapacity];
            VerticalMmPerS = new int[unitCapacity];
            Ticks = new int[unitCapacity];
            SpinCdPerS = new int[unitCapacity];
            LaunchSurfaceMm = new int[unitCapacity];
            LaunchEvent = new int[unitCapacity];
        }

        /// <summary>Height above the map datum now, mm (for drawing and the landing test).</summary>
        public int[] HeightMm { get; }

        /// <summary>Height at launch, mm.</summary>
        public int[] StartMm { get; }

        /// <summary>Upward speed at launch, mm/s.</summary>
        public int[] VerticalMmPerS { get; }

        /// <summary>Ticks flown so far.</summary>
        public int[] Ticks { get; }

        /// <summary>The tumble, seeded per soldier, centi-degrees per second (the playback track redraws it).</summary>
        public int[] SpinCdPerS { get; }

        /// <summary>The surface he was launched from (ground or deck), mm: the cliff rule measures drops from it.</summary>
        public int[] LaunchSurfaceMm { get; }

        /// <summary>His <see cref="CauseKind.SoldierLaunched"/> event, or <see cref="CauseLog.None"/> in a world without a log.</summary>
        public int[] LaunchEvent { get; }

        internal void Clear(int u)
        {
            HeightMm[u] = 0;
            StartMm[u] = 0;
            VerticalMmPerS[u] = 0;
            Ticks[u] = 0;
            SpinCdPerS[u] = 0;
            LaunchSurfaceMm[u] = 0;
            LaunchEvent[u] = 0;
        }

        internal bool IsClear(int u) =>
            HeightMm[u] == 0
            && StartMm[u] == 0
            && VerticalMmPerS[u] == 0
            && Ticks[u] == 0
            && SpinCdPerS[u] == 0
            && LaunchSurfaceMm[u] == 0
            && LaunchEvent[u] == 0;

        private int[][] Arrays() =>
            new[]
            {
                HeightMm,
                StartMm,
                VerticalMmPerS,
                Ticks,
                SpinCdPerS,
                LaunchSurfaceMm,
                LaunchEvent,
            };

        /// <summary>Only the airborne slots (every other slot is zero, which the world's invariants enforce): their count, then each slot and its arc, in slot order.</summary>
        internal void WriteTo(ByteWriter writer)
        {
            int count = 0;
            for (int u = 0; u < HeightMm.Length; u++)
            {
                count += IsClear(u) ? 0 : 1;
            }
            writer.WriteInt32(count);
            for (int u = 0; u < HeightMm.Length; u++)
            {
                if (IsClear(u))
                {
                    continue;
                }
                writer.WriteInt32(u);
                foreach (var array in Arrays())
                {
                    writer.WriteInt32(array[u]);
                }
            }
        }

        internal void ReadFrom(ByteReader reader)
        {
            foreach (var array in Arrays())
            {
                Array.Clear(array, 0, array.Length);
            }
            int count = reader.ReadInt32();
            if (count < 0 || count > HeightMm.Length)
            {
                throw new FormatException("Snapshot holds more arcs than unit slots.");
            }
            int previous = -1;
            for (int k = 0; k < count; k++)
            {
                int u = reader.ReadInt32();
                if (u <= previous || u >= HeightMm.Length)
                {
                    throw new FormatException("Snapshot arcs are out of order or out of range.");
                }
                previous = u;
                foreach (var array in Arrays())
                {
                    array[u] = reader.ReadInt32();
                }
            }
        }

        /// <summary>One launched soldier's arc, in a fixed order.</summary>
        internal void AppendTo(SimHash hash, int u)
        {
            hash.AppendInt32(HeightMm[u]);
            hash.AppendInt32(StartMm[u]);
            hash.AppendInt32(VerticalMmPerS[u]);
            hash.AppendInt32(Ticks[u]);
            hash.AppendInt32(SpinCdPerS[u]);
            hash.AppendInt32(LaunchSurfaceMm[u]);
            hash.AppendInt32(LaunchEvent[u]);
        }

        /// <summary>Throws when a launched soldier's arc can't be real (it would flow into overflow or a missing event).</summary>
        internal void Check(int u, int events)
        {
            if (
                Ticks[u] < 0
                || Ticks[u] > Ballistics.MaxFlightTicks
                || VerticalMmPerS[u] < -Ballistics.MaxSpeedMmPerS
                || VerticalMmPerS[u] > Ballistics.MaxSpeedMmPerS
                || LaunchEvent[u] < CauseLog.None
                || LaunchEvent[u] >= events
                || StartMm[u] < -Ballistics.MaxHeightMm
                || StartMm[u] > Ballistics.MaxHeightMm
                || HeightMm[u] < -2 * Ballistics.MaxHeightMm
                || HeightMm[u] > 2 * Ballistics.MaxHeightMm
                || LaunchSurfaceMm[u] < -Ballistics.MaxHeightMm
                || LaunchSurfaceMm[u] > Ballistics.MaxHeightMm
            )
            {
                throw new FormatException("A launched soldier's arc is invalid.");
            }
        }
    }
}
