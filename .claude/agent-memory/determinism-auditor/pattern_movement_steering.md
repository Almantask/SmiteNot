---
name: pattern-movement-steering
description: Checks for MovementSystem/Navigation (flow-field steering, separation, crowd pressure) - single-chunk thread tests, 64-neighbour truncation bias, fixed 2 m spatial hash vs map caps, stale Pressure scratch, tunnelling past thin walls
metadata:
  type: project
---

Check first when Movement/ or Navigation.cs changes (first audited 2026-11-02, M2 item 3, commit fc69e50; no High):
- Thread-count tests must exceed ChunkSize (512) by several chunks. MovementTests' test had 360 units = one chunk, so ThreadedRunner never split it. A scratch probe with 3000 units (6 chunks, cavalry+siege, 60 s at a gap) matched on 1/3/8 threads. Count chunks in every new thread-count test.
- Neighbour buffer (MaxNeighbours 64) is filled in row-major cell order and the query reach includes the LARGEST radius in the ruleset (siege 1.5 m), so infantry query 2.35 m. 3000 infantry at a 6 m gap reached 61/64 (self included) and was still rising; the 10k golden peaks near 30. Truncation drops NE neighbours: one-sided pushes, under-read pressure. Deterministic, but it decides crush deaths later.
- MovementSystem hard-codes a 2 m SpatialHash over the whole map: anything over ~16M hash cells (about 8 x 8 km) throws on tick 1 although MapData allows 30 km sides. Map caps and hash cell counts must agree.
- Raw-long margins (Fix 16 bits): QueryRadius rejects per axis, so dx,dy <= reach (<= 5.5 m, 3.6e5 raw); products stay below 1e12. Re-derive if MaxRadiusMm, BrakeDistance or MaxQueryRadius grows.
- Derived scratch (Pressure, means, directions, hash) is safe while nothing reads it before Movement in the tick; Pressure keeps stale values for dead slots and after a restore until the next run.
- Only the target cell is checked for passability: a step of push cap (one radius) plus speed can cross a one-cell wall when radius is about the cell size (siege 1.5 m at 1.5 m cells).
- Movement/ and Navigation/ folders and files still have no .meta files (tenth time). See [[pattern-overflow-assert-side-effects]].
- Formations (audited 2026-11-02, commit 660c642; no High): the serial prepass (UpdateRegiments) is the only writer of RegimentAnchor/RegimentFacing; the kernel reads copies (_anchorPosition/_anchorVelocity) and writes _newPosition, applied serially. A scratch probe killing every other officer twice in the 3000-unit crowd, snapshotting with anchor == -1, matched on 1/8 threads and across a restore. Re-check this shape whenever a new per-regiment field is written during Movement.
- Data-reachable long overflow: straggler reach = ReformDistance + rankSpacing x ranks, squared. Loader allows files 1, rankSpacing 20 m, 10,000 soldiers (reach about 200 km, square about 1.7e20). Any "spacing x count" product squared needs a loader cap or a clamp to the map diagonal (IL2CPP signed overflow is UB).
- Fixed in 0eb4a84: loader caps formations at 1 km (Ranks(max, files) x rankSpacing), MapData checks the exact hash ceil. Residual: snapshots carry RegimentFiles/StartingSize unchecked against rules (files=1, size 10,000 restores a 200 km reach), and CheckInvariants allocates slot blocks before rejecting negative starting sizes (Overflow/IndexOutOfRange instead of FormatException).
- Map area caps vs hash cells: an area cap (m²) is not a cell cap; ceil per axis overshoots (8001 x 7999 m passes 64 km², hash needs 16,004,000 > 16M cells). Compare with the exact ceil formula of SpatialHash.

**Why:** These are the traps once crush, pushes over edges and battle-time terrain arrive (M2-M5).
**How to apply:** count chunks in the thread tests, measure peak neighbour counts in the densest scenario, compare hash cell size with the map caps, and grep for readers of Pressure.
