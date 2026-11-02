# ADR 0002: The simulation stays managed C#; no Burst for now

- **Status:** Accepted
- **Date:** 2026-11-02
- **Source:** implementation plan, M2 item 10 ("The Burst decision: benchmark the hot kernels on .NET, IL2CPP and Burst; record it in an ADR"); ADR 0001 (an engine-free simulation that also runs on .NET).

## Context

The simulation is one C# source that Unity compiles (Mono in the editor, IL2CPP in players) and .NET runs for tests, the `godsim` CLI and the future PvP verifier. Burst could speed up the hot per-unit kernels in Unity players, but it compiles only unmanaged code: structs, `NativeArray`, no classes, no managed arrays, no virtual calls. The simulation's systems are classes over managed arrays, so using Burst means a second implementation of each kernel that only Unity runs.

## Measurements (2026-11-02, dev desktop: AMD Ryzen 9 5900X, one thread)

The movement step's hot core, neighbour separation with integer square roots over a dense 10,000-soldier crowd, from one source file (`unity/Assets/_Game/Benchmarks/SeparationKernel.cs`; the .NET bench links it), plus the same arithmetic as a Burst job (`BurstSeparation.cs`):

| Runtime | Separation kernel | Whole tick, 10,000 units on m1-flat |
|---|---|---|
| .NET 10 (BenchmarkDotNet) | 2.94 ms | 2.8 ms |
| Unity IL2CPP player | 2.90 ms | 4.5 ms |
| Unity IL2CPP player, Burst job | 1.92 ms | — |
| Unity Mono editor | 4.17 ms | 11.0 ms |
| Unity Mono editor, Burst job | 3.65 ms | — |

All five kernel runs produce the same checksum (`9DEA2B9CD064A8A7`): integer arithmetic under Burst matches the managed runtimes bit for bit. The golden replay (SimVersion 4) also matches in the IL2CPP player.

## Decision

1. **The simulation stays managed C# with one source for every runtime.** No Burst, no `NativeArray` and no Unity types in `sim/src`.
2. **Unity players get their speed from threads, not Burst:** a Unity-side `IParallelRunner` (M4, with the preview worker), so a player uses its cores the way the .NET hosts' `ThreadedRunner` does. IL2CPP runs the kernel as fast as .NET; its whole tick is 1.6× slower, which threads cover within the recommended budget.
3. **Revisit** when a measured budget fails on the floor laptop (M5, combat; or M8, presentation), and only for the one kernel that fails: a Burst port behind the same interface, with the managed kernel as the reference, the goldens run through both, and the .NET hosts keeping the managed one.

## Why

- **Burst gains 1.5× on the kernel in a player** (1.92 against 2.90 ms). That is real but small next to what threads give (the .NET tick runs 2× faster on 4 threads, ADR-free), and the tick has budget left: 1.4 ms on 4 threads on the working map, 2.4 ms in the worst bridge crush, against 3.3 ms.
- **A second implementation per kernel is a desync risk** across the runtimes that must agree (.NET server, Mono editor, IL2CPP player), and doubles the work of every steering change; the M2 tuning alone changed the movement kernel a dozen times.
- **The managed path is already fast where it matters:** IL2CPP equals .NET on the kernel. The Mono editor is slow (11 ms ticks), which affects the preview while developing, not players.

## Consequences

- The M2 exit criterion on the tick budget is met with threads on .NET; Unity players need the Unity runner from M4 to reach it in-game (the debug view runs the battle on one worker thread for now, 4.5 to 4.8 ms a tick).
- The benchmark stays in the repo (`unity/Assets/_Game/Benchmarks`, `GodGame → Benchmarks → Burst Decision`, the determinism player's `-run-bench <file>`, and `SeparationKernelBench` in the .NET bench), so the decision can be re-measured on the floor laptop.
