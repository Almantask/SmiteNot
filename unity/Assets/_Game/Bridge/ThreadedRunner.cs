using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using GodGame.Sim;

namespace GodGame.Bridge
{
    /// <summary>
    /// The parallel runner for Unity's simulation threads (the preview worker), the twin of GodGame.Sim.Hosting's: chunk
    /// boundaries come from <see cref="ParallelChunks"/>, never from the thread count, so results match
    /// <see cref="SerialRunner"/> bit for bit; only which thread runs which chunk varies. Each chunk gets its own copy of
    /// the kernel. (Unity can't reference the hosting library, and the simulation itself may not start threads.)
    /// </summary>
    public sealed class ThreadedRunner : IParallelRunner
    {
        private readonly ParallelOptions _options;

        public ThreadedRunner(int threadCount)
        {
            if (threadCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(threadCount));
            }
            ThreadCount = threadCount;
            _options = new ParallelOptions { MaxDegreeOfParallelism = threadCount };
        }

        public int ThreadCount { get; }

        public void For<TKernel>(int count, int chunkSize, TKernel kernel)
            where TKernel : struct, IChunkKernel
        {
            int chunks = ParallelChunks.Count(count, chunkSize);
            if (chunks <= 1 || ThreadCount == 1)
            {
                new SerialRunner().For(count, chunkSize, kernel);
                return;
            }
            // One slot per chunk, so the lowest failing chunk can be rethrown, as the serial runner would.
            var failures = new Exception[chunks];
            Parallel.For(
                0,
                chunks,
                _options,
                chunk =>
                {
                    TKernel copy = kernel;
                    try
                    {
                        copy.Execute(
                            ParallelChunks.Start(chunk, chunkSize),
                            ParallelChunks.End(chunk, count, chunkSize)
                        );
                    }
                    catch (Exception e)
                    {
                        failures[chunk] = e;
                    }
                }
            );
            foreach (var failure in failures)
            {
                if (failure != null)
                {
                    ExceptionDispatchInfo.Capture(failure).Throw();
                }
            }
        }
    }
}
