using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace GodGame.Benchmarks
{
    /// <summary>
    /// <see cref="SeparationKernel.Run"/> as a Burst job: the same integer arithmetic line for line, on NativeArrays
    /// (Burst compiles only unmanaged data). Benchmark only; the simulation never runs through it.
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct BurstSeparation : IJob
    {
        [ReadOnly]
        public NativeArray<long> X;

        [ReadOnly]
        public NativeArray<long> Y;

        [ReadOnly]
        public NativeArray<int> CellStart;

        [ReadOnly]
        public NativeArray<int> CellUnits;
        public NativeArray<long> PushX;
        public NativeArray<long> PushY;
        public NativeArray<long> Pressure;
        public int Columns;
        public int Rows;

        public void Execute()
        {
            long contact = 2 * SeparationKernel.Radius;
            for (int i = 0; i < X.Length; i++)
            {
                long px = X[i];
                long py = Y[i];
                int cell0 = Cell(px, py);
                int cx = cell0 % Columns;
                int cy = cell0 / Columns;
                long sx = 0;
                long sy = 0;
                long sp = 0;
                for (int ny = cy - 1; ny <= cy + 1; ny++)
                {
                    if (ny < 0 || ny >= Rows)
                    {
                        continue;
                    }
                    for (int nx = cx - 1; nx <= cx + 1; nx++)
                    {
                        if (nx < 0 || nx >= Columns)
                        {
                            continue;
                        }
                        int cell = ny * Columns + nx;
                        for (int k = CellStart[cell]; k < CellStart[cell + 1]; k++)
                        {
                            int j = CellUnits[k];
                            if (j == i)
                            {
                                continue;
                            }
                            long dx = X[j] - px;
                            long dy = Y[j] - py;
                            long d2 = dx * dx + dy * dy;
                            if (d2 >= contact * contact)
                            {
                                continue;
                            }
                            long dist = SeparationKernel.Sqrt((ulong)d2);
                            long overlap = contact - dist;
                            sp += overlap;
                            long share = overlap * 26_214 >> 16;
                            if (dist > 0)
                            {
                                sx -= dx * share / dist;
                                sy -= dy * share / dist;
                            }
                        }
                    }
                }
                PushX[i] = sx;
                PushY[i] = sy;
                Pressure[i] = sp;
            }
        }

        private int Cell(long x, long y)
        {
            long cx = x / SeparationKernel.CellRaw;
            long cy = y / SeparationKernel.CellRaw;
            cx =
                cx < 0 ? 0
                : cx >= Columns ? Columns - 1
                : cx;
            cy =
                cy < 0 ? 0
                : cy >= Rows ? Rows - 1
                : cy;
            return (int)(cy * Columns + cx);
        }
    }
}
