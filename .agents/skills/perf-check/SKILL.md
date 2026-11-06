---
name: perf-check
description: Run simulation benchmarks in Release, compare results against sim/bench/GodGame.Sim.Bench/budgets.json, and report regressions. Use after touching per-unit or per-cell simulation code or when invoked via /perf-check.
---

# Simulation Performance Check

## Inputs

Extract an optional BenchmarkDotNet filter pattern from the prompt (for example, `*Tick*` or `*Water*`). Empty defaults to `*` (all benchmarks).

## Workflow

1. **Run Release benchmarks.** Launch BenchmarkDotNet with JSON export via `run_command`:
   ```powershell
   dotnet run -c Release --project sim/bench/GodGame.Sim.Bench -- --filter "<filter or *>" --exporters json
   ```
   BenchmarkDotNet runs often exceed `WaitMsBeforeAsync` and move to a background task; end your turn and wait for Antigravity's completion notification rather than polling in a loop.
2. **Read results and budgets.** Use `read_file` to inspect the generated report under `BenchmarkDotNet.Artifacts/results/*-report-full-compressed.json` alongside `sim/bench/GodGame.Sim.Bench/budgets.json` (and `docs/implementation-plan.md` section 8 if context is needed).
3. **Report budget comparison.** Present a markdown table with:
   - Benchmark name
   - Mean time and managed allocation per operation
   - Target budget columns (**floor laptop** and **recommended desktop**)
   - Margin against each budget
   State clearly which hardware ran the benchmark: passing on a fast desktop CPU does not prove compliance on the floor laptop.
4. **Flag regressions.** Highlight any benchmark that is **>10% slower** than the recorded baseline in `budgets.json` or introduces **any non-zero per-tick allocation**, and point to the specific code path responsible.
5. **Protect baselines.** Never modify `sim/bench/GodGame.Sim.Bench/budgets.json` unless explicitly asked to record a new baseline.
