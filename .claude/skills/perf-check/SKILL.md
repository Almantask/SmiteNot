---
name: perf-check
description: Run the simulation benchmarks in Release, compare them with sim/bench/GodGame.Sim.Bench/budgets.json and report regressions. Use after touching per-unit or per-cell code.
argument-hint: "[benchmark filter, e.g. *Tick*]"
allowed-tools: Bash(dotnet run *), Read
---

Filter: $ARGUMENTS (empty means all benchmarks)

1. Run `dotnet run -c Release --project sim/bench/GodGame.Sim.Bench -- --filter '<filter or *>' --exporters json`.
2. Read the results (BenchmarkDotNet.Artifacts/results/*-report-full-compressed.json) and sim/bench/GodGame.Sim.Bench/budgets.json.
3. Report a table: benchmark, mean and allocation per operation, the budget it maps to (both columns: minimum and recommended), and the margin. Say which machine this ran on; budgets are set for the floor laptop and the recommended machine (plan section 8), so a fast desktop result is not proof.
4. Flag regressions against the recorded baseline (more than 10% slower, or any new allocation in a per-tick path) and point at the likely code.
5. Never edit budgets.json unless the user asks to record a new baseline.
