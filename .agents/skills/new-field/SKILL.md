---
name: new-field
description: Implement a world field (water, temperature, fuel, wind, fog, and similar grids) in the simulation with storage, update order, interaction rows, snapshots, GPU upload, shader sampling, tests, and benchmarks. Use when a power or mechanic requires a new world field or when invoked via /new-field.
---

# Add a World Field

## Inputs

Extract `<field name>` from the prompt.

## Checklist

1. **Justify against the field budget.** Content roadmap rule 4 (`docs/design/content-roadmap.md`): a new field ships only when at least **three** strong mechanics need it. Name all three and verify the target milestone/chapter.
2. **Storage and integer units.** Define an integer grid in natural units (`docs/implementation-plan.md` section 3.3 table) inside `sim/src/GodGame.Sim/Fields/` (`FieldGrid` / `FieldStore`); calculate bytes per cell and total memory on the largest map, keeping 10,000-unit snapshots within the 4 MB budget.
3. **Update order and scheduling.** Slot the model into `FieldSystem` in the fixed step order:
   ```text
   water -> wetness and mud -> temperature -> ice and snow -> fuel and fire -> wind plumes -> fog and smoke
   ```
   Define its sub-rate (e.g., 5 Hz) and 16-cell active-tile wake/sleep rules. Changing step order alters outcomes and requires a `SimVersion` bump via `/golden-replay`.
4. **Interaction table (`data/rules/fields.json`).** Specify what fields it reads, what fields it writes, and integer interaction strengths (plus schema updates in `data/schema/` if applicable).
5. **Unit reactions.** Wire how regiments and soldiers read it (`sim/src/GodGame.Sim/Navigation/` and movement/combat/morale): speed codes, cost field penalties, lethal cells, hazards, morale effects, and which movement classes (`foot`, `horse`, `wheeled`, `water`) care.
6. **Snapshots and hashing.** Include the field in sparse tile snapshots (`Snapshot`) and `StateHash` (`SimHash`); verify `IRestoreAware` rebuilds derived navigation data on restore.
7. **GPU upload and shaders (`unity/`).** Upload the field grid as an integer texture, add/update the HLSL shader include that decodes it, and ensure its strategic-zoom warning visual is clear.
8. **Tests (`sim/tests/`).** Add kernel unit tests, conservation or bound invariants, scenario tests via `/scenario-test` for each brief decision, snapshot/restore round-trip tests, and 1 vs 4 vs 8 thread determinism tests:
   ```powershell
   dotnet test sim/GodGame.Sim.sln
   python tools/ci/check_sim_analyzers.py
   ```
9. **Performance budget.** Run `/perf-check` with the new field active on the largest test map and verify tick and allocation budgets.
