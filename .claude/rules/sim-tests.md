---
paths:
  - "sim/tests/**"
  - "sim/bench/**"
---

# Simulation tests

- **Frameworks:** NUnit 4 (`[Test]`, `Assert.That`), with NUnit.Analyzers; warnings are errors.
- **Categories** on every fixture or test: `Fast` (whole set under about 30 s; the Stop hook runs it), `Slow`, `Golden`, `Perf`, `Fuzz`. CI runs everything except `Perf` and `Fuzz` on every push; nightly runs all.
- **Scenario test names** follow the brief decision they protect: `Decision_YYYY_MM_DD_<Name>_<Expectation>`, for example `Decision_2026_10_08_WeakIceBreaksUnderCrowds_ThinFileCrosses`. Appendix B of the plan lists the first ones; the scenario-test skill writes them.
- **Builders:** build maps and plans with `MapBuilder` and `PlanBuilder` (from M1), so tests read like the design. Assert on cause events and statistics, not on internals.
- **Never edit `sim/tests/golden/`** by hand (a hook denies it). Goldens change only through /golden-replay, with a SimVersion bump. Until M1, pinned hashes live in `GodGame.Sim.Tests/PinnedHashes.cs` under the same rule.
- **Determinism tests** compare hash sequences: same plan twice, snapshot and restore, 1 vs N threads.
- Unit tests may use floats and LINQ freely; the simulation may not.
- Benchmarks (BenchmarkDotNet) run in Release and are compared with `sim/bench/GodGame.Sim.Bench/budgets.json` by /perf-check.
