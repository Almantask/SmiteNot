---
name: pattern-snapshot-validators-vs-live-state
description: Check first in every audit - snapshot ReadFrom validators that reject states the live sim can legally reach (duplicates, ranges), and validators missing map-dependent upper bounds
metadata:
  type: project
---

Two recurring snapshot bugs in sim/src/GodGame.Sim/State/*.ReadFrom:

1. **A validator rejects a state the live sim can reach.** Found 2026-11-14: GroundEdits.ReadFrom rejects duplicate cleared boulders, but two Engineer regiments that pick the same boulder both clear it and AddCleared twice. Live hashes stay consistent, but every later keyframe fails to restore, so the timeline breaks.
   Probe: two specialist regiments of the same type on ONE target (boulder, gap, weak span), then CaptureSnapshot and RestoreSnapshot after both finish.

2. **A validator has no map- or rules-dependent upper bound** (cell index < Columns*Rows, kind < Kinds.Count, boulder < Map.Boulders.Count, JobRef < segment count). Simulation.RestoreSnapshot validates into a scratch world first, but these bounds are only hit later in AfterRestore (Navigation.RebuildFromWorld). By then World has been overwritten, so a bad snapshot leaves the sim half-restored.

**Why:** the tests only restore single-regiment scenarios, so neither bug shows up in CI.
**How to apply:** for every new list or index in world state, ask: can two actors append the same value? Is the index bounded before World is overwritten? See also [[pattern-shared-map-objects]].
