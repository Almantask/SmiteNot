namespace GodGame.Sim
{
    /// <summary>
    /// Work on one chunk of indices, [start, end). Write kernels as <c>struct</c>s (ideally <c>readonly struct</c>)
    /// that hold references to the arrays they read and write. Every runner hands each chunk its own copy of the
    /// kernel, so a field the kernel changes is lost when the chunk ends: results go in arrays, never in fields.
    /// Each chunk writes only its own index range; anything that sums across chunks writes per-chunk partials and
    /// reduces them afterwards, in chunk order, on one thread. A kernel never calls <see cref="IParallelRunner.For"/>
    /// (no nesting) and never spawns or despawns: those stay serial, in ascending slot order (see <see cref="World"/>).
    /// </summary>
    public interface IChunkKernel
    {
        void Execute(int start, int end);
    }

    /// <summary>
    /// Runs a kernel over [0, count) in fixed chunks (plan, section 3.6). Chunk c always covers
    /// [c × chunkSize, min(count, (c + 1) × chunkSize)), whatever the thread count, and chunks may run in any order
    /// and on any thread; because chunks write disjoint ranges, the result is the same on 1, 2 or N threads.
    /// If a kernel throws, the exception of the lowest-numbered failing chunk is rethrown and the world is invalid.
    /// The simulation never creates threads itself: the host passes a runner in.
    /// </summary>
    public interface IParallelRunner
    {
        /// <summary>Threads this runner may use, for reports and tests; results never depend on it.</summary>
        int ThreadCount { get; }

        void For<TKernel>(int count, int chunkSize, TKernel kernel)
            where TKernel : struct, IChunkKernel;
    }

    /// <summary>Runs every chunk in order on the calling thread: the reference every other runner must match.</summary>
    public sealed class SerialRunner : IParallelRunner
    {
        public int ThreadCount => 1;

        public void For<TKernel>(int count, int chunkSize, TKernel kernel)
            where TKernel : struct, IChunkKernel
        {
            int chunks = ParallelChunks.Count(count, chunkSize);
            for (int chunk = 0; chunk < chunks; chunk++)
            {
                TKernel copy = kernel; // a fresh copy per chunk, as on every other runner
                copy.Execute(
                    ParallelChunks.Start(chunk, chunkSize),
                    ParallelChunks.End(chunk, count, chunkSize)
                );
            }
        }
    }

    /// <summary>The fixed chunking every runner uses.</summary>
    public static class ParallelChunks
    {
        /// <summary>Chunks needed for <paramref name="count"/> indices; validates the arguments.</summary>
        public static int Count(int count, int chunkSize)
        {
            if (count < 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(count));
            }
            if (chunkSize < 1)
            {
                throw new System.ArgumentOutOfRangeException(nameof(chunkSize));
            }
            // Long arithmetic, so counts near int.MaxValue can't overflow into a negative chunk count.
            return (int)(((long)count + chunkSize - 1) / chunkSize);
        }

        public static int Start(int chunk, int chunkSize) => (int)((long)chunk * chunkSize);

        public static int End(int chunk, int count, int chunkSize) =>
            (int)System.Math.Min(count, ((long)chunk + 1) * chunkSize);
    }
}
