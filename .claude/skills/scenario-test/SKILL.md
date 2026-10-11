---
name: scenario-test
description: Turn a brief decision into an executable scenario test with the map and plan builders, named after the decision. Use whenever a design decision is implemented or needs protecting.
argument-hint: "<decision date and name, e.g. 2026-10-08 weak ice breaks under crowds>"
---

Decision: $ARGUMENTS

1. Find the decision in docs/design/game-brief.md (quote it with its date) and its row in docs/implementation-plan.md Appendix B, if any.
2. Name the test `Decision_YYYY_MM_DD_<Name>_<Expectation>` in sim/tests/GodGame.Sim.Scenarios, category `Fast` if it runs in under a second or two, otherwise `Slow`.
3. Build the smallest world that shows the decision: `MapBuilder` and `PlanBuilder` (from M1; until then, the placeholder battle), one variable at a time, and a control case that shows the opposite outcome.
4. Assert on cause events and statistics (casualties by cause, swept, launched, routs, arrival times), never on internals.
5. Run it twice and with 1 and N threads (from M1); it must be deterministic.
6. If it matters, save it as a golden replay through /golden-replay.
