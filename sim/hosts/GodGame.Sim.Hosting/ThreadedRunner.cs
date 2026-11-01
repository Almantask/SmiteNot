using System.Runtime.ExceptionServices;

namespace GodGame.Sim.Hosting;

/// <summary>
/// Runs chunks on up to <see cref="ThreadCount"/> pool threads. Chunk boundaries are fixed by
/// <see cref="ParallelChunks"/>, never by the thread count, so results match <see cref="SerialRunner"/> bit for bit;
/// only which thread runs which chunk varies. Each chunk gets its own copy of the kernel.
/// </summary>
public sealed class ThreadedRunner : IParallelRunner
{
    private readonly ParallelOptions _options;

    public ThreadedRunner(int threadCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(threadCount, 1);
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
        var failures = new Exception?[chunks];
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
