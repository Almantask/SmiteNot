---
name: desync-hunt
description: Find the first diverging tick between two simulation runs (threads, OS, runtime), bisect to the system and entity, and report likely causes.
argument-hint: "<what diverged, e.g. linux vs windows hashes, or 1 vs 8 threads>"
---

Divergence: $ARGUMENTS

Stub (M0): steps 3–4 need the per-system hashing and snapshots from M1.

1. **Reproduce** both runs with the same map, plan, seed, SimVersion and ruleset hash: `godsim hash --out <file>` per side (CI artifacts hold the Linux and Windows files).
2. **First diverging second:** diff the hash files; then rerun that second hashing every tick.
3. **Bisect by system:** hash state after each step of the tick order (plan section 3.4) to find the first system whose output differs.
4. **Bisect by entity:** hash per regiment, then per soldier, inside that system; dump both values.
5. **Likely causes,** checked in this order: floating point slipping in (GG0001 should stop it), unordered iteration, sort ties without an id tie-breaker, shared or reused RNG streams, parallel reductions in completion order, static state, culture-dependent parsing, uninitialised memory, Fix overflow, a field missing from StateHash.
6. Report the tick, system, entity, both values and the suspected line; propose a fix and a regression test. Ask determinism-auditor to review the fix.
