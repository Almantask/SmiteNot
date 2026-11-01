---
name: pattern-runner-kernels
description: Checks for IParallelRunner / IChunkKernel / ISimSystem / TickRate - struct kernel shared via lambda capture, ban-list holes (PLINQ, ProcessorCount, Timer), system-held state outside StateHash, TickRate overflow
metadata:
  type: project
---

Check first when the tick scheduler, a runner or any chunk kernel changes (first audited 2026-11-01, M1 item 3):
- Struct kernels are NOT copied per chunk unless the runner does `var k = kernel;` per chunk. A lambda capturing `kernel` hoists it into one closure field shared by all threads; SerialRunner's parameter carries field mutations from chunk to chunk. Any kernel with a mutable field (counter, Pcg32 RNG, cache) then differs by thread count. Want: per-chunk copy in every runner and kernels declared `readonly struct` (ideally analyzer-enforced).
- Kernels must not call Runner.For (nested parallelism can deadlock a fixed-pool Unity runner) and must not call DespawnUnit (see [[pattern-world-soa-pools]]).
- BannedSymbols.txt bans Parallel/Thread/Task/ThreadPool but (as of 2026-11-01) not Task`1, TaskFactory, System.Threading.Timer, System.Timers.Timer, PLINQ (System.Linq.ParallelEnumerable), Environment.ProcessorCount. Re-check the list each audit.
- ISimSystem instances are reference types and may hold fields; anything persisting across ticks there is outside StateHash and snapshots. Persistent state must live in World; check new systems for instance fields that are not scratch.
- TickRate.IsDueFor computes tick - Phase - index in int: wraps for huge index (deterministic in C#, UB in IL2CPP C++). default(TickRate) has Period 0 -> divide by zero; Validate should check rates.
- Thread-count tests now exist (TickingTests ThreadCountDeterminismTests, 1/2/8 threads). The Unity runner (GodGame.Bridge) needs the same conformance test.
- .meta files missing again for Ticking/ and State/ (fourth time). See [[pattern-overflow-assert-side-effects]].

**Why:** No divergence today, but each is a trap for the first real parallel systems in M1-M2.
**How to apply:** read every runner's For body for per-chunk copies, grep kernels for non-readonly fields, grep systems for instance state, diff the ban list.
