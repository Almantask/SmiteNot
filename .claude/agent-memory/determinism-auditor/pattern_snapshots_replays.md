---
name: pattern-snapshots-replays
description: Checks for Snapshot/ByteBuffer/Replay/Battle, RestoreSnapshot and the godsim CLI/CI hash compare - shallow pool validation, no battle identity in snapshot headers, compare ignoring hash-file headers, test-local vs real systems in thread tests, concurrent agents breaking the build
metadata:
  type: project
---

Check first when snapshots, replays, Battle or the CLI/CI hash steps change (first audited 2026-11-01, M1 items 6-7; no High):
- Snapshot covers a superset of StateHash (every slot to capacity, stale free-stack tail included); hash covers SimVersion, tick, seed, live slots. Re-verify both lists when a World array or pool field is added: the new field must go in SpawnUnit, DespawnUnit reset, AppendTo, WriteTo and ReadFrom (five places).
- EntityPool.ReadFrom only checks counts. Want: alive-flag count == aliveCount, no alive flag at >= highWater, free stack entries distinct/in range/dead, live generations > 0. World.ReadFrom wants UnitRegiment -> live regiment, RegimentAlive == recount, dead slots zero, known flag bits. Cheapest catch-all: store StateHash (or xxHash of the payload) in the snapshot and verify after reading the scratch world.
- Snapshot header has seed but no rules/map/plan hash; wander bounds come from Battle, not World. Same-seed, same-capacity snapshots from an edited plan restore silently (Unity scrub cache trap).
- Restore reads into a scratch World and then parses again into the live one: safe, but allocates a full World per scrub.
- `godsim compare` skips '#' headers, so files from other rules/map/plan compare "identical" if state matches; CI `diff` of whole files does check headers.
- Thread-count tests (TickingTests) use a test-local wander system; only one 5 s replay test runs the real Battle serial vs ThreadedRunner(4). CI runs threads 4 on both OSes only.
- CI checkout has no `lfs: true` while data/maps/**/*.bin is LFS: a future height.bin arrives as a pointer file on both OSes, identically.
- Other agents edit the tree mid-audit (2026-11-01: Goldens.cs, RepoFiles.cs, GoldenReplays.cs appeared; Hosting stopped building on `Math.Min`). Run tests on a copy without the out-of-scope files and say so.
- .meta files still missing (Snapshots/, PlaceholderWanderSystem.cs, State/, Ticking/): seventh time. See [[pattern-overflow-assert-side-effects]], [[pattern-world-soa-pools]].

**Why:** The scrubbable timeline and PvP replays depend on restore being exact and refusing foreign keyframes.
**How to apply:** diff WriteTo/ReadFrom/AppendTo/SpawnUnit field lists, check what the snapshot header identifies, and check what compare/CI actually compare.
