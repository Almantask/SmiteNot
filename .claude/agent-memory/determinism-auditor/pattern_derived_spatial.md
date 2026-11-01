---
name: pattern-derived-spatial
description: Checks for derived per-tick structures (SpatialHash and later fields/flow grids) - Fix square overflow far off-map, staleness vs spawn/despawn after Build, shared result buffers in parallel kernels, buffer-full truncation bias
metadata:
  type: project
---

Check first when SpatialHash or any rebuilt-per-tick acceleration structure changes (first audited 2026-11-01, M1 item 4):
- DistanceSquared = (a-b).LengthSquared multiplies raw*raw in long: wraps once one axis delta exceeds ~46,341 m. Off-map units are clamped into edge cells, so a far-flung unit (launch/swept) can be tested; in Release the wrapped square goes negative and passes `<= r^2`, in Debug the Fix assert fires. Want a per-axis |dx|,|dy| <= r reject before squaring.
- Cell clamp with truncating `raw / cellRaw` is correct: all negatives clamp to 0 anyway and CellX is monotone, so the query box always covers the unit's cell. Re-check if the clamp ever changes to wrap or to "skip off-map".
- Derived state is only safe if Build runs after the last Position/spawn/despawn and before every reader, every tick a reader runs. QueryRadius does not check IsAlive, so despawn-after-Build returns dead or reused slots. Want a build stamp (tick or pool version) asserted in queries.
- Results buffers: a kernel struct holding an int[] field shares that array across all chunks/threads even with per-chunk struct copies (see [[pattern-runner-kernels]]). Buffers must be stackalloc or per-chunk.
- Early stop on a full buffer keeps the first N in row-major (south-west first) order, not the nearest: deterministic but biased, and the caller cannot tell "exactly full" from "truncated". Buffer size must be a constant, never derived from thread count or platform.
- Constructor: Columns*Rows in int can overflow for tiny cells; width.Raw + cellRaw - 1 can overflow for huge widths.
- .meta files: State/ and Ticking/ folders and Angle/FixVec2/FixVec3/Trig still had none on 2026-11-01 (fifth time). See [[pattern-overflow-assert-side-effects]].

**Why:** Nothing diverged in the first audit, but these are the traps for movement/separation/melee in M1-M2.
**How to apply:** grep readers of the hash for where Build runs relative to movement and despawn, and kernels for array fields used as query buffers.
