---
name: new-power
description: Implement a god power as rules data plus minimal simulation and preview code, including 100 m range-rule checks, timeline bars, field counters, and scenario tests. Use when building a power from docs/design/power-palette.md or when invoked via /new-power.
---

# Add a God Power

## Inputs

Extract `<power name>` from the prompt.

## Workflow

1. **Check design readiness.** Read the power's entry in `docs/design/power-palette.md`, its wave in `docs/design/content-roadmap.md`, and every dated decision it touches in `docs/design/game-brief.md` (plus any spec pass such as `docs/design/spec-s1-water.md`).
   - If the palette marks it `seed` or `open`, or required systems do not exist at the current milestone, consult `design-lead` before coding: register `design-lead` with `define_subagent` using [design-lead.md](./references/design-lead.md) and call `invoke_subagent` (`Model: "inherit"`), or surface the open rule directly with a recommended answer.
2. **State the consequence chain.** Document the 2–3 hop chain in the commit message:
   ```text
   Power -> World field/terrain change -> Army behaviour change -> Outcome
   ```
3. **Author rules data (`data/rules/powers.json`).** Add `id`, area shape, wind-up, duration, cost, target fields written and integer amounts, and mode-specific tuning (campaign vs PvP) in documented integer units only.
4. **Minimal simulation code (`sim/src/`).** Add C# code only when a new field operation is required; otherwise let the generic cast executor apply the data. Enforce the **100 m range rule** at the **firing tick** against regiment outlines, recording an explicit fizzle reason and cause event.
5. **Timeline & placement preview (`unity/`).** Wire the timeline bar (wind-up to peak), mouse area/shape placement, and map overlays needed to read the predicted consequence while scrubbing.
6. **Counters & unit answers.** Add or verify field-interaction rows in `data/rules/fields.json` matching the `power-palette.md` counter-web table, and confirm a unit-side answer exists (content roadmap rule 7).
7. **Tests (`sim/tests/`).** Add via `/scenario-test`:
   - One scenario test per brief decision touched
   - One test per counter-web row where the power appears
   - A 100 m range-rule fizzle test at the firing tick
   - Determinism check (`dotnet test sim/GodGame.Sim.sln` and `python tools/ci/check_sim_analyzers.py`)
8. **Sync design docs.** If implementation settled a previously `seed` detail, record it in `docs/design/power-palette.md` and `docs/design/game-brief.md` following `/game-design`.
