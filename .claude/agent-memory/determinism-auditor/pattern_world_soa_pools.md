---
name: pattern-world-soa-pools
description: Checks for the SoA World and EntityPool (slots, generations, LIFO free stack) - dead-slot stale data vs snapshot/restore, despawn order as state, shared regiment counters under parallelism, get-only array properties blocking double buffering
metadata:
  type: project
---

Check first when World/EntityPool or any per-unit array changes (first audited 2026-11-01, M1 item 2):
- Every new per-unit/per-regiment array must be (a) written in SpawnUnit/AddRegiment and (b) hashed in World.AppendTo. Dead slots are skipped in the hash, so a field that spawn forgets to reset is unhashed future-affecting state.
- Dead slots keep stale data. Harmless only while nothing reads them; it breaks snapshot/restore (M1 item 6) if restore copies only live slots or into a fresh World. Preferred fix: clear dead slots on despawn to a canonical value, plus a debug "hash all slots to HighWater" mode.
- The LIFO free stack makes despawn *order* part of state. Any parallel system that kills units must collect deaths and despawn serially in a fixed order (ascending index), never in completion order.
- DespawnUnit writes RegimentAlive[regiment] (shared counter) and the pool: never callable from parallel chunks.
- Arrays are get-only properties: parallel systems that read neighbours' Position while writing their own chunk need a separate output buffer; check that nobody writes Position in place in a parallel pass.
- Exception messages built with `"..." + int` use the current culture (negative sign). Low, but flag it; EntityId.ToString is invariant.
- Thread-count tests exist since M1 item 3 (TickingTests, 1/2/8 threads); see [[pattern-runner-kernels]].
- Missing .meta files for the World/ folder and its files came up again (third time). See [[pattern-overflow-assert-side-effects]].

**Why:** These came up in the World/EntityPool review; none diverge today but each is a trap for the systems that land in M1-M2.
**How to apply:** for every new system, grep for writes to World arrays inside parallel loops, for DespawnUnit calls in parallel code, and for fields missing from SpawnUnit or AppendTo.
