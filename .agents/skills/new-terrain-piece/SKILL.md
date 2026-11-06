---
name: new-terrain-piece
description: Implement a terrain piece (channel, ridge, boulders, wall, bridge, dam, and similar features) as rules data plus minimal simulation and UI code with buffer, reachability, navigation, counter, and scenario tests. Use when adding a terrain piece from docs/design/power-palette.md or when invoked via /new-terrain-piece.
---

# Add a Terrain Piece

## Inputs

Extract `<piece name>` from the prompt.

## Checklist

1. **Design & counter rules.** Read its entry in `docs/design/power-palette.md` and its wave in `docs/design/content-roadmap.md` (plus any spec pass such as `docs/design/spec-s1-water.md`). Document its visible warning (rule 6), its field/power counter, and its unit-side answer (rule 7).
2. **Footprint & rules data (`data/rules/terrain.json`).** Define shape, size limits, cost, material (`wood`, `stone`, `earth`), load strength, and world fields written, using documented integer units only.
3. **100 m deployment buffer.** Refuse player placement closer than 100 m to either army's deployment zone (level-designer map pieces are exempt per `docs/implementation-plan.md` section 10); indirect consequences (such as water routed through a channel) may still flow into the buffer.
4. **Reachability guard (`sim/src/GodGame.Sim/Navigation/Reachability.cs`, `data/rules/routes.json`).** Ensure the coarse breadth-first fill still finds a legal path for every side and land class within `2x` straight-line distance; refuse full traps interactively while dragging (`<= 5–10 ms`).
5. **Navigation & field stamping (`Terrain`, `CostFields`, `FlowFieldSet`).** Stamp per-class cell costs/blockers, wake affected field tiles (`DirtyAround`), and trigger incremental flow-field and `LethalDistance` repairs on touched rectangles.
6. **Materials, load & counters.** Wire material interactions (fire burns wood, rot weakens wood, tremors topple stone, sappers undermine) and load strain stages (`creak`, `crack`, `sag`) when units cross load-bearing pieces.
7. **Placement UI, overlays & art (`unity/`, `art/`).** Add mouse drag/shape placement, route/reachability preview feedback, and art props/damage states (`docs/implementation-plan.md` section 7.7).
8. **Tests (`sim/tests/`).** Verify with `run_command`:
   ```powershell
   dotnet test sim/GodGame.Sim.sln
   python tools/ci/check_sim_analyzers.py
   ```
   Include: buffer refusal at `99 m`, reachability trap refusal, scenario tests via `/scenario-test` for each brief decision, and counter-web tests.
