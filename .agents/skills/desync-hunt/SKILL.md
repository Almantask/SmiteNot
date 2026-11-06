---
name: desync-hunt
description: Find the first diverging tick between two simulation runs across threads, operating systems, or runtimes, bisect to the offending system and entity, and report root causes. Use when simulation hashes or replays diverge or when invoked via /desync-hunt.
---

# Simulation Desync Hunt

## Inputs

Take the divergence target from the prompt (for example, `Linux vs Windows CI hashes`, `1 vs 8 threads`, `snapshot/restore`, or `.NET vs IL2CPP`).

## Workflow

1. **Reproduce both sides.** Run both configurations with identical map, plan, seed, `SimVersion`, and ruleset hash using `run_command`:
   ```powershell
   dotnet run --project sim/tools/GodGame.Sim.Cli -- hash --map <dir> --plan <file> --seed <n> --threads <t> --out hashes-a.txt
   dotnet run --project sim/tools/GodGame.Sim.Cli -- compare hashes-a.txt hashes-b.txt
   ```
   (For CI cross-OS divergences, compare the downloaded Linux and Windows hash artifacts.)
2. **Isolate the first diverging second and tick.** Use `compare` output to locate the first differing second, then hash every tick inside that one-second window (20 ticks/s) to pinpoint the exact first diverging tick.
3. **Bisect by system.** Hash state after each step in `TickOrder.cs` (`docs/implementation-plan.md` section 3.4: plan events -> fields -> structures and load -> regiment AI -> navigation -> movement -> ballistics and impacts -> combat -> hazards -> morale -> bookkeeping) to find the first system whose post-step hash differs.
4. **Bisect by entity.** Inside that system, hash per regiment (and per active field tile if in fields), then per soldier slot, and dump both runs' field/entity values at that tick.
5. **Check likely causes in order:**
   - Floating-point arithmetic slipping past `GG0001` or `BannedSymbols.txt`
   - Unordered `Dictionary` or `HashSet` iteration affecting simulation state
   - Sort ties without a deterministic entity/slot ID tie-breaker
   - Shared, carried-over, or mis-keyed `SimRandom` streams
   - Parallel writes to shared memory or parallel reductions in completion order rather than chunk/index order
   - Static mutable state across runs
   - Culture-dependent parsing or formatting (missing `CultureInfo.InvariantCulture`)
   - Uninitialised pooled slot or scratch buffer memory
   - Unchecked `Fix` or `int` multiplication overflow
   - State affecting future ticks omitted from `StateHash` or `Snapshot`
6. **Report and audit.** Report the exact tick, system, entity/cell ID, both diverging values, and the offending `file:line`. Propose a minimal fix plus a deterministic regression test.
   - Audit the fix using the `determinism-auditor` role: if `determinism-auditor` is not listed in `<subagents>`, register it with `define_subagent` (`EnableWriteTools: false`) using [determinism-auditor.md](./references/determinism-auditor.md), invoke it via `invoke_subagent` (`Model: "inherit"`), and end your turn to await its review.
