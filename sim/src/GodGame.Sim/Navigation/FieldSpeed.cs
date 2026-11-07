using System;
using GodGame.Sim.Math;
using GodGame.Sim.Rules;

namespace GodGame.Sim
{
    /// <summary>
    /// How water and mud slow each movement class (spec S1, sections 3 and 6), packed one byte per class into the
    /// hashed <see cref="FieldId"/> field: <see cref="Swept"/> (the water sweeps the class away; for foot, a lethal
    /// cell), <see cref="Closed"/> (too deep to wade in), or the speed in ‰ divided by 4 (2 to 250). Navigation computes
    /// it from the water and mud fields once per refresh (S1: costs refresh at most at 1 Hz) and builds costs, flow
    /// fields and lethal distances from it; movement reads the speed of the cell a soldier stands in. Because the codes
    /// live in the world's fields they are hashed and saved, so a restored battle rebuilds exactly the costs it had.
    /// The water class has no byte: it stays impassable on land and water alike until boats exist.
    /// </summary>
    public static class FieldSpeed
    {
        public const string FieldId = "nav_speed";

        public const int Swept = 0;
        public const int Closed = 1;

        /// <summary>Full speed: one class's byte on dry, firm ground.</summary>
        public const int AllDryCode = 250;

        /// <summary>Full speed for foot, horse and wheeled: the field's initial value.</summary>
        public const int AllDry = 250 | 250 << 8 | 250 << 16;

        /// <summary>The classes with a byte, in byte order.</summary>
        public const int Classes = 3;

        public static int Code(int codes, MovementClass movementClass) =>
            movementClass == MovementClass.Water
                ? Closed
                : codes >> ((int)movementClass * 8) & 0xFF;

        /// <summary>The speed a code allows, in ‰ (0 for swept and closed cells).</summary>
        public static int Permille(int code) =>
            code <= Closed ? 0
            : code >= AllDryCode ? 1000 // a byte above 250 (bad data) still never speeds anyone up
            : code * 4;

        /// <summary>
        /// Whether new codes differ enough from the stored ones to rebuild navigation (spec S1, section 3: cost
        /// hysteresis): some class crosses swept or closed, or its speed moves by at least
        /// <paramref name="hysteresisPermille"/> of full speed. Smaller drifts keep the stored codes, so a rising flood
        /// doesn't rebuild the whole river every second.
        /// </summary>
        public static bool Significant(int stored, int fresh, int hysteresisPermille)
        {
            if (stored == fresh)
            {
                return false;
            }
            int step = System.Math.Max(1, hysteresisPermille / 4); // codes are speed / 4
            for (int c = 0; c < Classes; c++)
            {
                int a = stored >> (c * 8) & 0xFF;
                int b = fresh >> (c * 8) & 0xFF;
                if (a == b)
                {
                    continue;
                }
                if (a <= Closed || b <= Closed || System.Math.Abs(a - b) >= step)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The code for one class in water <paramref name="depthMm"/> deep, flowing at <paramref name="flowMmPerS"/>, on mud <paramref name="mud"/>.</summary>
        public static int CodeFor(MovementClassRules rules, long depthMm, long flowMmPerS, int mud)
        {
            var water = rules.Water;
            if (water.Sweeps(depthMm, flowMmPerS))
            {
                return Swept;
            }
            if (depthMm > water.MaxEnterDepthMm)
            {
                return Closed;
            }
            int speed = water.Speed.At(depthMm) * rules.MudSpeed.At(mud) / 1000;
            return System.Math.Max(2, speed / 4);
        }

        /// <summary>The packed codes for one cell of <paramref name="fields"/>.</summary>
        public static int CellCodes(
            FieldStore fields,
            FieldGrid depth,
            FieldGrid mudGrid,
            Ruleset rules,
            int cellSizeMm,
            int dryDepthUm,
            int x,
            int y
        )
        {
            long depthUm = depth.Get(x, y);
            int mud = mudGrid.Get(x, y);
            long depthMm = 0;
            long flow = 0;
            if (depthUm >= dryDepthUm)
            {
                depthMm = depthUm / 1000;
                WaterModel.Velocity(fields, cellSizeMm, x, y, out int east, out int north);
                flow = (long)IntMath.Sqrt((ulong)((long)east * east + (long)north * north));
            }
            else if (mud == 0)
            {
                return AllDry;
            }
            int codes = 0;
            for (int c = 0; c < Classes; c++)
            {
                codes |= CodeFor(rules.MovementClasses[c], depthMm, flow, mud) << (c * 8);
            }
            return codes;
        }
    }
}
