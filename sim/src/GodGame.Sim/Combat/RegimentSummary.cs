using System;
using GodGame.Sim.Math;

namespace GodGame.Sim
{
    /// <summary>
    /// Each regiment's centroid, outline box and mean velocity, gathered serially in slot order from the world as it
    /// stands. Scratch for the systems that read it (orders, volleys, morale): rebuilt by every caller, never kept.
    /// The outline is the axis-aligned box around the regiment's soldiers, a cheap stand-in for the cast range
    /// rule's coarse outline that the M5 targeting default and morale's "within 60 m" both measure from.
    /// </summary>
    public sealed class RegimentSummary
    {
        private long[] _sumX = new long[0];
        private long[] _sumY = new long[0];
        private long[] _sumVx = new long[0];
        private long[] _sumVy = new long[0];

        public int[] Count { get; private set; } = new int[0];

        /// <summary>Soldiers swept by water: they count as half a loss for morale (S2 7.1).</summary>
        public int[] Swept { get; private set; } = new int[0];

        public FixVec2[] Centroid { get; private set; } = new FixVec2[0];

        public FixVec2[] MeanVelocity { get; private set; } = new FixVec2[0];

        /// <summary>The outline box, in mm.</summary>
        public long[] MinX { get; private set; } = new long[0];

        public long[] MinY { get; private set; } = new long[0];

        public long[] MaxX { get; private set; } = new long[0];

        public long[] MaxY { get; private set; } = new long[0];

        public void Gather(World world)
        {
            int n = world.Regiments.Capacity;
            if (Count.Length != n)
            {
                Count = new int[n];
                Swept = new int[n];
                Centroid = new FixVec2[n];
                MeanVelocity = new FixVec2[n];
                MinX = new long[n];
                MinY = new long[n];
                MaxX = new long[n];
                MaxY = new long[n];
                _sumX = new long[n];
                _sumY = new long[n];
                _sumVx = new long[n];
                _sumVy = new long[n];
            }
            Array.Clear(Count, 0, n);
            Array.Clear(Swept, 0, n);
            Array.Clear(_sumX, 0, n);
            Array.Clear(_sumY, 0, n);
            Array.Clear(_sumVx, 0, n);
            Array.Clear(_sumVy, 0, n);
            for (int r = 0; r < n; r++)
            {
                MinX[r] = long.MaxValue;
                MinY[r] = long.MaxValue;
                MaxX[r] = long.MinValue;
                MaxY[r] = long.MinValue;
            }
            for (int u = 0; u < world.Units.HighWater; u++)
            {
                if (!world.Units.IsAlive(u))
                {
                    continue;
                }
                int r = world.UnitRegiment[u];
                var p = world.Position[u];
                Count[r]++;
                if ((world.Flags[u] & UnitFlags.Swept) != 0)
                {
                    Swept[r]++;
                }
                _sumX[r] += p.X.Raw;
                _sumY[r] += p.Y.Raw;
                _sumVx[r] += world.Velocity[u].X.Raw;
                _sumVy[r] += world.Velocity[u].Y.Raw;
                long x = Mm(p.X.Raw);
                long y = Mm(p.Y.Raw);
                MinX[r] = System.Math.Min(MinX[r], x);
                MinY[r] = System.Math.Min(MinY[r], y);
                MaxX[r] = System.Math.Max(MaxX[r], x);
                MaxY[r] = System.Math.Max(MaxY[r], y);
            }
            for (int r = 0; r < n; r++)
            {
                int c = Count[r];
                if (c == 0)
                {
                    Centroid[r] = FixVec2.Zero;
                    MeanVelocity[r] = FixVec2.Zero;
                    continue;
                }
                Centroid[r] = new FixVec2(Fix.FromRaw(_sumX[r] / c), Fix.FromRaw(_sumY[r] / c));
                MeanVelocity[r] = new FixVec2(
                    Fix.FromRaw(_sumVx[r] / c),
                    Fix.FromRaw(_sumVy[r] / c)
                );
            }
        }

        /// <summary>
        /// Marks each regiment with an enemy regiment's outline within <paramref name="withinMm"/> (live regiments with
        /// soldiers only) and returns whether any is: the melee and the engaged slowdown skip every other regiment.
        /// </summary>
        public bool NearEnemy(World world, long withinMm, bool[] near)
        {
            int high = world.Regiments.HighWater;
            Array.Clear(near, 0, near.Length);
            bool any = false;
            for (int a = 0; a < high; a++)
            {
                if (Count[a] == 0)
                {
                    continue;
                }
                for (int b = a + 1; b < high; b++)
                {
                    if (
                        Count[b] > 0
                        && world.RegimentSide[a] != world.RegimentSide[b]
                        && Within(a, b, withinMm)
                    )
                    {
                        near[a] = true;
                        near[b] = true;
                        any = true;
                    }
                }
            }
            return any;
        }

        /// <summary>Whether two non-empty regiments' outline boxes are at most <paramref name="mm"/> apart (no square root).</summary>
        public bool Within(int a, int b, long mm)
        {
            long dx = System.Math.Max(0, System.Math.Max(MinX[b] - MaxX[a], MinX[a] - MaxX[b]));
            long dy = System.Math.Max(0, System.Math.Max(MinY[b] - MaxY[a], MinY[a] - MaxY[b]));
            return dx <= mm && dy <= mm && dx * dx + dy * dy <= mm * mm;
        }

        /// <summary>Raw Fix metres to mm.</summary>
        public static long Mm(long raw) => raw * 1000 >> Fix.FractionalBits;

        /// <summary>The gap between two regiments' outline boxes, in mm (0 when they touch or overlap).</summary>
        public long Gap(int a, int b)
        {
            if (Count[a] == 0 || Count[b] == 0)
            {
                return long.MaxValue; // an empty regiment has no outline
            }
            long dx = System.Math.Max(0, System.Math.Max(MinX[b] - MaxX[a], MinX[a] - MaxX[b]));
            long dy = System.Math.Max(0, System.Math.Max(MinY[b] - MaxY[a], MinY[a] - MaxY[b]));
            return Length(dx, dy);
        }

        /// <summary>From a point (raw Fix) to a regiment's outline box, in mm.</summary>
        public long GapFrom(FixVec2 point, int r)
        {
            if (Count[r] == 0)
            {
                return long.MaxValue;
            }
            long x = Mm(point.X.Raw);
            long y = Mm(point.Y.Raw);
            long dx = System.Math.Max(0, System.Math.Max(MinX[r] - x, x - MaxX[r]));
            long dy = System.Math.Max(0, System.Math.Max(MinY[r] - y, y - MaxY[r]));
            return Length(dx, dy);
        }

        /// <summary>Map distances (under 30 km a side) square safely inside a long.</summary>
        public static long Length(long dx, long dy) =>
            (long)IntMath.Sqrt((ulong)(dx * dx + dy * dy));
    }
}
