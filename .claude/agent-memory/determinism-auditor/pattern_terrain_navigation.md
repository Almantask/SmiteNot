---
name: pattern-terrain-navigation
description: Checks for Terrain heightfield, CostFields and FlowField/FlowFieldSet - integer margins at map limits, dirty radius vs stencil, unbuilt/stale derived fields, heights becoming state with battle-time edits, header-only golden regeneration
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
- Navigation/ .meta files were missing (eighth time; ninth on 2026-11-02 for FlowField.cs, FlowFieldSet.cs; eleventh for LethalDistance.cs). See [[pattern-overflow-assert-side-effects]].

Flow fields (audited 2026-11-02, M2 item 2, commit a581895; no High):
- Repair == Rebuild proof rests on: every changed edge has both ends in dirty+1 (diagonal corner cells included); old direction pointers trace exact old distances; all costs >= 1 so pointer paths strictly decrease (no zero-weight cycles). baseCost >= 1 is enforced only by RulesetLoader, not Ruleset/CellCost: a cost-0 source (future congestion, mud) silently breaks repair exactness. Re-check whenever a new cost input arrives.
- Repair(dirty) is exact only if the field was fresh against the costs right before that one cost rebuild. Nothing checks it (BuiltFromVersion is a terrain version, not a rebuild counter): two cost rebuilds then one Repair, or CostFields.RebuildAll then Repair(small), leaves stale cells. Live run repairs, snapshot restore rebuilds: divergence. Want a cost rebuild counter + previous-version assert, or dirty-union accumulation.
- Versions are terrain versions only: when costs gain non-terrain inputs, the staleness asserts stop seeing changes.
- Dial ring = maxCost*14+1; Rebuild walks every distance value (O(max distance)), so high slope costs cost time, not correctness.
- FlowField.Offsets is a public static readonly array (mutable contents). FlowFieldSet indices depend on GetOrAdd order: never hash or snapshot them unless restore re-adds in the same order.

Lethal distance (audited 2026-11-02, commit 8af20fc; no High):
- Two-pass 3x3 chamfer is exact on an unobstructed grid when orth <= diag <= 2*orth (optimal paths are monotone; each quadrant's path can be reordered so one pass does it). Windowed rebuild is exact because a window is convex (a monotone path enters once) and cells beyond cap/orth cells can't feel the edit. Breaks if lethal cells ever block propagation (obstacles) or the cap/window margin changes: re-prove then.
- Same freshness gap as flow fields: Rebuild(dirty) sets BuiltFromVersion to the current version whatever rect it got, so a missed edit is invisible. Want dirty-union accumulation per tick.
- Map edge cells are lethal (distance 0) and deployment zones may touch the edge: check once teeter/landing rules read it.
- Header-only golden check: git show <commit> -- sim/tests/golden must show only the rulesetHash line; StateHash excludes RulesetHash.

**Why:** Flow fields (M2 item 2) and terrain powers will read these fields every tick; staleness or a too-narrow dirty rect diverges silently.
**How to apply:** re-derive the products, compare each dirty radius with each stencil, and ask where heights are hashed and snapshotted once anything edits them during a battle.

Reachability (audited 2026-11-02, commit 8bc3078; one High, correctness not determinism):
- Coarse "closed when under half its field cells are open" loses thin blockers: a 2-field-cell cliff line straddling a coarse boundary leaves both coarse cells open, so a full trap passes (verified: step at x=42 with coarseCells 3 -> Ok=True; 40/41/43 refused). Tests only use walls that happen to align. Any coarse or downsampled validation grid: test every alignment offset (0..k-1) of a minimal blocker, and want "closed if any blocking field cell line crosses it" (or edge-based closure) rather than a majority vote.
- Dial early exit is exact here: the for-condition is checked only after a bucket drains, every tentative value <= current has been popped, and Trace only follows values below current, so lengths, tie-breaks (lowest cell) and traces match a full fill. Re-check if the exit moves inside the bucket loop or steps become 0 or > ring size.
- Per-fill scratch (distance, bucket stacks incl. Array.Resize) is indexed by fill, clearance by class: thread-safe. The instance is not reentrant (two concurrent Check calls race).
- Coarse cell size = coarseCells x map cellSizeMm, so clearance widths in metres vary per map.
- No zone/usable-seed validation at construction; Zone() throws inside the kernel.
- Same freshness gap as costs/flow: Update(rect) is trusted, no RebuildCount stamp.
