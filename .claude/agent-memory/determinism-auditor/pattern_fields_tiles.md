---
name: pattern-fields-tiles
description: Checks for FieldGrid/FieldStore/FieldSystem/IFieldModel (M3 fields) - unhashed Next buffer and _changed flags, unclamped Commit vs snapshot range check, public Values/Set bypassing wake or called from kernels, keyframe size when fields are dense
metadata:
  type: project
---

Check first when a field model or the field framework changes (first audited 2026-11-02, commit 59e592f; no High):
- Next is neither hashed nor snapshotted and starts as zeros (not Initial). Commit copies whole tiles from Next, so any cell a model forgets to write leaks stale data (from before a restore, or another tick) into Values. Want: Commit/FieldSystem pre-fills Next from Values for the due tiles before Step, or a debug check.
- _changed is cleared only for stepped tiles; a MarkChanged on a tile outside the step stays set, unhashed, and ReadFrom clears it, so restore diverges. Better: Commit derives "changed" by comparing Next with Values per tile (no model flag at all).
- Commit does not clamp; ReadFrom rejects out-of-range values, so a model overshooting Max makes the live state unsnapshottable.
- Values is a public int[] and Set/Add write _quiet of 9 tiles: both are serial-only. Any unit action or cast calling Set from a parallel kernel races on _quiet. Grep new models/systems for Set/Add inside Execute.
- Field data dominates keyframes once rain makes fields dense (3 fields x 800x534 x 4 B = 5 MB); the size test only covers untouched fields.
- Multiple models on one field: the second sees the first's commit and new awake set (deterministic, but order is state).
- .meta files missing for Fields/ and Rules/FieldRules.cs (eighth time). See [[pattern-overflow-assert-side-effects]], [[pattern-snapshots-replays]], [[pattern-runner-kernels]].

**Why:** No field models exist yet; these are contract-only traps that the first water/wetness models (M3 items 3-4) will hit.
**How to apply:** for each new IFieldModel, check it writes every cell of every given tile, marks only its own tiles, stays within Min/Max, and never calls Set from a kernel.
