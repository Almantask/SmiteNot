---
name: pattern-field-navigation
description: Checks for units reading fields (M3 item 5, FieldSpeed nav_speed codes, RefreshFieldCosts, IRestoreAware/RebuildFromWorld) - start-of-battle derived data built dry vs stored initial codes, unvalidated code bytes, partial-refresh exactness, probe recipe
metadata:
  type: project
---

Check first when FieldSpeed, Navigation.RefreshFieldCosts/RebuildFromWorld, IRestoreAware or movement-class water/mud rules change (first audited 2026-11-03, uncommitted M3 item 5, SimVersion 7; no High):
- Design: derived nav data (costs, flow, lethal) is a pure function of hashed nav_speed codes + static terrain; refresh writes changed codes serially and rebuilds over their bounding rect; restore calls RebuildFromWorld. Partial == full holds because cost and IsLethal read only their own cell's code (radius 0), FlowField.Repair takes dirty+1 and falls back to Rebuild unless exactly one cost rebuild passed, and costs stay >= 1 (dry >= 1, speed <= 1000). Re-prove if a cost or lethal rule reads a neighbour's code (widen dirty) or speed can exceed 1000.
- Start-of-battle gap: Navigation is built with null codes (dry) while the world's nav_speed starts at the fields.json "initial". Refresh only rebuilds cells whose fresh code differs from stored, so a non-AllDry initial leaves cells whose computed code equals the initial dry-costed live, but restore rebuilds them from stored: divergence. Loader does not pin the initial. Fix: pin initial == AllDry in the loader, or RebuildFromWorld at Battle.Start.
- Bytes 251-255 pass the field range check (0..0xFFFFFF): Permille up to 1020 makes CellCost < dry, 0 when dry = 1, which breaks repair exactness. Validate bytes <= 250 or clamp Permille/CellCost.
- Lethal uses the foot byte only; horse is swept in water foot is not (horse sweptDepthSpeed 450 < foot 600). Design note, not determinism.
- Perf (not determinism): whole-map CodeKernel each second with 5 string field lookups per wet cell in WaterModel.Velocity; bounding rect of all changes can span the map; Dial Rebuild walks every distance value, and mud costs up to ~12,500/cell make RebuildAll after restore slow. Fast test set took 1 m 37 s on 2026-11-03.
- Probe recipe that passed: scratch NUnit project linking TestBattles.cs, RulesetHashTests.cs (RepoData), PinnedHashes.cs, with data/rules copied beside it (RepoData walks up from the test dir); 120x100 map (7 code chunks), 22 regiments x 150 (3300 units, 7 movement chunks), pond + poured lines; 1/3/8 threads equal serial; restore at ticks 0, 7, 313, 600, 999 into a sim stepped 517 ticks, plus double restore, all equal. Repo test only restores at tick 300 (a refresh multiple) with 130 units (one movement chunk).
- FieldSpeed.cs had no .meta (twelfth time). See [[pattern-overflow-assert-side-effects]], [[pattern-terrain-navigation]], [[pattern-fields-tiles]].

**Why:** first time derived navigation data depends on battle-time state; restore and live must agree exactly.
**How to apply:** for each new field-driven cost/lethal input, check its stencil vs the dirty rect, how start-of-battle derived data matches the stored initial, and run the probe with restores at non-refresh ticks.
