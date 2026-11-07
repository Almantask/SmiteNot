---
name: pattern-terrain-pieces
description: Checks for plan terrain pieces (TerrainPieces stamping, TerrainPlacement, MapData.WithTerrain/Blocked, Battle.PlayedMap) - SegmentDistanceSq overflow bound rests on maxLengthMm, PlayedMap reuses base Id/Hash, Blocked has no setter/version, tiny-map clamps
metadata:
  type: project
---

Check first when Data/TerrainPieces.cs, TerrainPlacement.cs, terrain.json or Battle.PlayedMap change (first audited 2026-11-07, M3 item 8; no High):
- SegmentDistanceSq middle branch computes dx*t with t < lengthSq: bound is L^3 (L = one segment's length), NOT map size. Safe only because the loader caps maxLengthMm at 1e6 (1e18 vs 9.2e18). Raising the cap past ~2.09e6, or building PlannedPiece directly (public ctor, public TerrainPieces.Apply) skips the guard. Comment there wrongly says "on the map" suffices.
- Order independence holds: kinds stamped in fixed order, channel/hollow by min vs original, ridge by max vs original, boulders disjoint (CheckFootprints refuses overlap) so their read-then-write of heights commutes. Re-check if boulders may ever overlap or ridges read stamped heights.
- PlayedMap keeps the base map's Id and Hash, so passing it back into new Battle / BattlePlan.Load / Replay.ToBattle stamps twice and every hash check (Replay map hash, Battle.Id, snapshot header) still passes. BattleHost.Map now returns PlayedMap. Want a distinct hash or an IsPlayed flag that those entry points reject.
- Battle.Id = rules+map+plan hash fully determines PlayedMap; heights are not in StateHash/snapshots, which is fine only while nothing edits terrain during the battle. Terrain.IsBlocked has no setter and no Version bump: when tremor (M6) moves boulders, Blocked becomes state (hash, snapshot, DirtyAround rebuild).
- Stamped heights may exceed ±MapData.MaxHeightMm (channel depth up to 100 m in loader, boulder height up to 100 m, unlike ridge/hollow which are capped below LethalDropMm); Terrain.SetHeight would refuse such values later; Step/HeightMmAt still fit.
- BoulderCells/SpringArea go negative when the block is wider than the map (x = Columns - side < 0): wrong indices, not a clamp.
- Blocked is consistent: CostFields has one kernel for full and partial rebuilds; Reachability, flow fields and Movement read costs. LethalDistance and water read heights only.

**Why:** pieces are planning data that every derived structure is built from; a double-stamped or wrongly-hashed played map would diverge with all identity checks passing.
**How to apply:** re-derive the dx*t bound against loader caps, check every entry point that accepts a MapData for a played map, and ask where Blocked/heights are hashed once battle-time terrain edits arrive.
