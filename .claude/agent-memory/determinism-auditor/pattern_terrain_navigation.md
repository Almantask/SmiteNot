---
name: pattern-terrain-navigation
description: Checks for Terrain heightfield, CostFields and later flow fields - integer margins at map limits, dirty radius vs stencil, unbuilt/stale derived fields, heights becoming state with battle-time edits, header-only golden regeneration
metadata:
  type: project
---

Check first when sim/src/GodGame.Sim/Navigation changes (first audited 2026-11-02, M2 item 1; no High):
- Margins at the limits (±10 km heights, 100 mm cells, 30 km sides): bilinear HeightMmAt peaks near 1e17 in long (about 100x headroom); Step peaks at 2e8 in int; CellCost multiplies only after the max-slope reject, so it stays at or below 1e9. Re-derive these if MaxHeightMm, the minimum cell size or the fraction bits change.
- Truncating division on negative mm is harmless only because every off-map coordinate clamps to the edge cell, so the value is constant there. Re-check if clamping ever changes.
- Partial rebuild == full rebuild only while each cell's cost reads a radius-1 stencil (4-neighbour slope). Mud, water and flow fields with a wider stencil need a wider DirtyAround, one per input.
- CostFields arrays are all zero until RebuildAll, and nothing records that SetHeight happened without a rebuild. Want a build or version stamp that readers assert.
- When battle-time height edits arrive, Terrain heights become state: they go in StateHash (or a running edit hash) and in snapshots, restore needs a full rebuild, edits are applied serially in a fixed order, and planning terrain edits must be covered by the plan hash.
- Adding a rules file changes RulesetHash, which forces a header-only golden regeneration with no SimVersion bump. That is correct under CLAUDE.md rule 3, but sim-tests.md and the godsim help say "with a bump". Point out the mismatch rather than calling it a violation.
- RulesetHash sorts by file name (ordinal), so host enumeration order is fine.
- Navigation/ .meta files were missing (eighth time). See [[pattern-overflow-assert-side-effects]].

**Why:** Flow fields (M2 item 2) and terrain powers will read these fields every tick; staleness or a too-narrow dirty rect diverges silently.
**How to apply:** re-derive the products, compare each dirty radius with each stencil, and ask where heights are hashed and snapshotted once anything edits them during a battle.
