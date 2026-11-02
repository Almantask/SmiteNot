namespace GodGame.Benchmarks
{
    /// <summary>
    /// The Burst decision's benchmark kernel (plan, M2 item 10): the hot core of the movement step, neighbour
    /// separation over a dense crowd with integer square roots, in plain C# so the identical source runs on .NET (the
    /// sim bench links this file), Unity's Mono and IL2CPP. <see cref="BurstSeparation"/> holds the same arithmetic as
    /// a Burst job. Every implementation must produce the same <see cref="Checksum"/>.
    /// </summary>
    public static class SeparationKernel
    {
        public const int Units = 10_000;
        public const long Radius = 22_938; // 0.35 m in Fix raw units (16 fractional bits)
        public const long CellRaw = 131_072; // 2 m grid cells

        /// <summary>A crowd packed at about 1.3 soldiers per m² in a 100 × 77 m block, from a fixed integer sequence.</summary>
        public static void MakeCrowd(long[] x, long[] y)
        {
            ulong state = 0x9E3779B97F4A7C15UL;
            for (int i = 0; i < x.Length; i++)
            {
                state = state * 6364136223846793005UL + 1442695040888963407UL;
                x[i] = (long)((state >> 33) % (100UL << 16));
                state = state * 6364136223846793005UL + 1442695040888963407UL;
                y[i] = (long)((state >> 33) % (77UL << 16));
            }
        }

        /// <summary>A counting-sort grid over the crowd: cellStart has columns × rows + 1 entries.</summary>
        public static void BuildGrid(
            long[] x,
            long[] y,
            int columns,
            int rows,
            int[] cellStart,
            int[] cellUnits
        )
        {
            System.Array.Clear(cellStart, 0, cellStart.Length);
            for (int i = 0; i < x.Length; i++)
            {
                cellStart[Cell(x[i], y[i], columns, rows) + 1]++;
            }
            for (int c = 0; c < columns * rows; c++)
            {
                cellStart[c + 1] += cellStart[c];
            }
            var cursor = new int[columns * rows];
            System.Array.Copy(cellStart, cursor, cursor.Length);
            for (int i = 0; i < x.Length; i++)
            {
                cellUnits[cursor[Cell(x[i], y[i], columns, rows)]++] = i;
            }
        }

        public static int Cell(long x, long y, int columns, int rows)
        {
            long cx = x / CellRaw;
            long cy = y / CellRaw;
            cx =
                cx < 0 ? 0
                : cx >= columns ? columns - 1
                : cx;
            cy =
                cy < 0 ? 0
                : cy >= rows ? rows - 1
                : cy;
            return (int)(cy * columns + cx);
        }

        /// <summary>Separation pushes and pressure for units [start, end), from the 3 × 3 cells around each.</summary>
        public static void Run(
            long[] x,
            long[] y,
            int columns,
            int rows,
            int[] cellStart,
            int[] cellUnits,
            long[] pushX,
            long[] pushY,
            long[] pressure,
            int start,
            int end
        )
        {
            long contact = 2 * Radius;
            for (int i = start; i < end; i++)
            {
                long px = x[i];
                long py = y[i];
                int cx = (int)(Cell(px, py, columns, rows) % columns);
                int cy = (int)(Cell(px, py, columns, rows) / columns);
                long sx = 0;
                long sy = 0;
                long sp = 0;
                for (int ny = cy - 1; ny <= cy + 1; ny++)
                {
                    if (ny < 0 || ny >= rows)
                    {
                        continue;
                    }
                    for (int nx = cx - 1; nx <= cx + 1; nx++)
                    {
                        if (nx < 0 || nx >= columns)
                        {
                            continue;
                        }
                        int cell = ny * columns + nx;
                        for (int k = cellStart[cell]; k < cellStart[cell + 1]; k++)
                        {
                            int j = cellUnits[k];
                            if (j == i)
                            {
                                continue;
                            }
                            long dx = x[j] - px;
                            long dy = y[j] - py;
                            long d2 = dx * dx + dy * dy;
                            if (d2 >= contact * contact)
                            {
                                continue;
                            }
                            long dist = Sqrt((ulong)d2);
                            long overlap = contact - dist;
                            sp += overlap;
                            long share = overlap * 26_214 >> 16; // 0.4 of the overlap
                            if (dist > 0)
                            {
                                sx -= dx * share / dist;
                                sy -= dy * share / dist;
                            }
                        }
                    }
                }
                pushX[i] = sx;
                pushY[i] = sy;
                pressure[i] = sp;
            }
        }

        /// <summary>Bit-by-bit integer square root (the simulation's IntMath.Sqrt).</summary>
        public static long Sqrt(ulong value)
        {
            ulong result = 0;
            ulong bit = 1UL << 62;
            while (bit > value)
            {
                bit >>= 2;
            }
            while (bit != 0)
            {
                if (value >= result + bit)
                {
                    value -= result + bit;
                    result = (result >> 1) + bit;
                }
                else
                {
                    result >>= 1;
                }
                bit >>= 2;
            }
            return (long)result;
        }

        /// <summary>An order-fixed fold of the outputs, to compare implementations.</summary>
        public static ulong Checksum(long[] pushX, long[] pushY, long[] pressure)
        {
            ulong h = 1469598103934665603UL;
            for (int i = 0; i < pushX.Length; i++)
            {
                h = (h ^ (ulong)pushX[i]) * 1099511628211UL;
                h = (h ^ (ulong)pushY[i]) * 1099511628211UL;
                h = (h ^ (ulong)pressure[i]) * 1099511628211UL;
            }
            return h;
        }
    }
}
