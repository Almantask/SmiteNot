---
name: pattern-launches-ballistics
description: Checks for launched soldiers (M5 item 5 - Ballistics.cs, BallisticState, LoadSystem collapse launches, LandingSpread) - spread seed keyed on cause-log index, launched-only hash invariant, knock-down hash rebuild, probe recipe
metadata:
  type: project
---

First audited 2026-11-14 (uncommitted, SimVersion 19, snapshot format 13). Sound at that point:
- "Hashed only for launched" holds because CheckInvariants forces every non-launched slot (live or dead) to an all-zero arc, and the only writers are LaunchWith, Land (Clear) and DespawnUnit (Clear). Re-check whenever a new path clears UnitFlags.Launched (any `Flags[u] = ...` assignment that doesn't preserve the bit) or writes BallisticState.
- Knock-downs are order-independent within a tick: everyone who has already landed is Stunned, Swept or dead, and launched units are skipped. So the hash built at the first landing tick only needs rebuilding after a despawn (Debug.Assert on Units.Version). Re-check if a landing outcome ever leaves a soldier on his feet, or if Ballistics starts moving standing units.
- Movement and combat kernels only read world.Flags; writes go to _newFlags (double-buffered). Launch is called only from serial code (LoadSystem.Fail, tests). Flag it if a parallel kernel ever calls Ballistics.Launch.

Open design-stability issue (Medium, not a desync): the spread stream is SimRandom.Stream(seed, 6001, sourceEvent, unitSlot). sourceEvent is a cause-log INDEX, so any edit that adds or removes one earlier cause event re-rolls every later launch. The spec's "source event id" was taken literally. Every test passes CauseLog.None, so CI can't see this. Suggested fix: key on a stable source identity (kind + segment/herd/regiment index), or on the source event's tick plus its subject.

Coverage gaps: LaunchDeterminismTests and the collapse test use at most 400 units (one 512-unit movement chunk), and their restore test compares only the final hash. Probe recipe: 3x200 infantry (two chunks) on the LaunchDeterminismTests map; launch with collapse at tick 100 and tick 103 (surface 5-7 m, so Fell and Killed despawns happen) and stampede at tick 140; compare per-tick hashes at 1/2/3/8 threads; restore every snapshot from tick 95 to 260 into a ran-ahead sim. All passed.

See [[pattern-decks-load]], [[pattern-world-soa-pools]], [[pattern-snapshot-validators-vs-live-state]], [[pattern-movement-steering]].
