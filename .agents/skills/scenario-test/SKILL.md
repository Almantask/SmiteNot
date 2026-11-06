---
name: scenario-test
description: Create an executable scenario test from a game-brief decision using MapBuilder and PlanBuilder, named after the dated decision. Use when a design decision is implemented or needs regression protection, or when invoked via /scenario-test.
---

# Write a Decision Scenario Test

## Inputs

Extract the target decision date and summary from the prompt (for example, `2026-10-08 weak ice breaks under crowds`).

## Workflow

1. **Locate the source decision.** Read `docs/design/game-brief.md` (and any relevant spec pass in `docs/design/`) with `read_file` to quote the exact decision and date, plus its entry in `docs/implementation-plan.md` Appendix B if listed.
2. **Name and categorise the test.** Place the test in `sim/tests/GodGame.Sim.Scenarios`:
   ```csharp
   // Category("Fast") when runtime <= ~1-2 s; otherwise Category("Slow")
   public void Decision_YYYY_MM_DD_<Name>_<Expectation>()
   ```
3. **Build the minimal world.** Construct the smallest reproducible setup with `MapBuilder` and `PlanBuilder` isolating one variable at a time, paired with a **control case** proving the opposite outcome when that variable is absent.
4. **Assert on observable outcomes, never internals.** Assert on cause events, causality traces, and battle statistics (casualties by cause, swept, launched, routs, arrival times)—never private buffers or implementation details (`.claude/rules/sim-tests.md`).
5. **Verify determinism.** Run the scenario twice and across `1`, `4`, and `8` threads (`ThreadedRunner`) plus snapshot/restore where applicable:
   ```powershell
   dotnet test sim/GodGame.Sim.sln --filter "FullyQualifiedName~Decision_YYYY_MM_DD"
   ```
6. **Optional golden promotion.** If the scenario protects a milestone-critical outcome chain, record a golden replay for it via `/golden-replay` (`add`).
