---
name: new-unit-type
description: Implement a unit type or specialist as rules data and simulation behaviour covering movement class, load, launch response, regiment AI, specialist actions, charges, preview markers, art specs, and scenario tests. Use when adding a unit or specialist or when invoked via /new-unit-type.
---

# Add a Unit Type or Specialist

## Inputs

Extract `<unit or specialist name>` from the prompt.

## Checklist

1. **Design & role check.** Read `docs/design/unit-spec.md` (and `docs/design/roster.md` once spec passes `S2`/`S4` land) plus its wave in `docs/design/content-roadmap.md`. Remember rule 7: specialists answer *part* of a god setup, never all of it.
2. **Rules data (`data/rules/unit-types.json` / `specialists.json`).** Define in integer units:
   - Regiment size bounds (`50–200`), formation files, spacing, radius, and mass/load weight
   - Movement class (`foot`, `horse`, `wheeled`, `water` — at most four classes at launch) and base speed (`speedMmPerS`)
   - Melee/volley stats, charge and launch response, morale, swept/burn thresholds, and price in bodies
3. **Specialist action (if applicable).** Route through the shared specialist action executor as data: trigger condition, work rate, finite `charges` on the regiment, seat lock, and per-action `Reachability` validation. Remaining charges are lost when the regiment falls.
4. **Regiment AI (`sim/src/`).** Add staggered regiment-level decision rules (when it acts, how it is escorted or hunted from M7) with hysteresis so tiny plan edits do not flip behaviour unpredictably.
5. **Timeline & map preview (`unity/`).** Add its regiment-lane marker and landing-spread overlays if it launches projectiles or can be launched.
6. **Art pipeline checklist (`art/`).** Verify rig (`RIG_humanoid`, `RIG_quadruped`, or `RIG_siege`), LOD triangle budgets, work-loop and movement clips, `TeamMask` vertex-colour regions, and cartoon death gags per cause (`.claude/rules/art-pipeline.md`).
7. **Tests (`sim/tests/`).** Run and verify:
   ```powershell
   dotnet test sim/GodGame.Sim.sln
   python tools/ci/check_sim_analyzers.py
   ```
   Include scenario tests via `/scenario-test` for each brief decision, a test proving the specialist counters part (not all) of its target god setup, snapshot/hash coverage, and multi-thread determinism.
