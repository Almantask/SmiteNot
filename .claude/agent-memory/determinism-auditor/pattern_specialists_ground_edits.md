---
name: pattern-specialists-ground-edits
description: Checks for SpecialistExecutor / GroundEdits / Navigation.ApplyGroundEdit (M5 item 3, first audited 2026-11-08; three High) - per-tick derived nav data read before Navigation after restore, cached Reachability not updated by other cost rebuilds, Timeline SameArmy missing new plan fields
metadata:
  type: project
---

Check first when specialists, battle-time terrain edits, Navigation restore or PlannedRegiment change:
- HIGH 2026-11-08: RegimentAi (step 3) runs before Navigation (step 4), and NavigationSystem.AfterRestore only RebuildFromWorld()s; it does NOT AssignRegiments. So Navigation.FieldOf(r) right after a restore is empty (fresh sim, as Timeline.Edit does) or the ran-ahead sim's mapping. Probe: restore into a fresh sim at the fascine trigger tick (1455 in the SpecialistTests fill scenario) diverged; all other ticks matched. Keyframes are multiples of 100 ticks, so the first restored tick is always an AI tick. Any AI-step reader of per-tick derived Navigation data has this bug.
- HIGH 2026-11-08: Navigation._routes (Reachability, coarse _open cache) was created on the first checked edit and then only Update(dirty)'d by checked edits. RefreshFieldCosts (water/mud codes -> Impassable) and unchecked edits (washout) rebuild costs without updating it; restore nulls it and rebuilds fresh. Reflection probe (moat-in-rain scenario, rain at tick 100): cache stale on 2080 of 3675 ticks. Rule: every Costs.Rebuild must be followed by `_routes?.Update(dirty)` (or compare CostFields.RebuildCount).
- HIGH 2026-11-08: Timeline.SameArmy compares Id/Side/UnitType/Soldiers/X/Y/Facing only; PlannedRegiment.DitchMm was added but not compared, so moving a ditch keeps the head under the new plan. Probe: timeline edit at 90 s vs fresh run at 150 s differed (ground edits 54 vs 53). Every new PlannedRegiment/PlannedPiece field must go in SameArmy/SameTerrain: grep the ctor params against those methods each audit.
- Skip path of ApplyGroundEdit (set, rebuild, check, revert, rebuild, recheck) is exact: radius-1 cost stencil, flow fields were never repaired with the edited costs, Lethal reads heights, water cache refreshes fully on any Version change.
- Ditch cache keyed by regiment slot == plan.Army index (Deploy spawns in plan order); derived from PlayedMap heights, so restore-safe.
- Direct mud Values write from the AI step is safe like Rain (FieldSystem PrepareStep copies Values next field step; ground model non-staggered at FieldsRate).
- SegmentDistanceSq's "q/r split" still has dx*r up to L^3; ditch length rests on charges (soldiers x mmPer10Soldiers), not a loader length cap.
- Probe recipe: copy sim/ + data/ (exclude bin/obj/.build) into scratchpad, add an NUnit file to the copied test project (TestBattles.Map, RepoData.Rules are available), reflect Simulation._systems -> NavigationSystem._navigation -> Navigation._routes. About 1-2 min per probe.
See [[pattern-terrain-navigation]], [[pattern-timeline-keyframes]], [[pattern-casts-powers]].

**Why:** battle-time terrain edits make every derived navigation structure state-dependent; the scrubbable preview restores at AI ticks.
**How to apply:** for any new reader in RegimentAi or earlier, list what derived data it reads and whether AfterRestore rebuilds exactly that; for every cached derived structure, list every writer of its inputs.
