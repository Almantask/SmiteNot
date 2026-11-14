---
name: pattern-pontoon-work-fields
description: Checks for the Pontoon Train (FindWater/MarchField), Work-order block flow fields, floating-deck Surge chain breaks (LoadSystem) - FlowFieldSet growth, AfterRestore AssignRegiments window, broken decks still occupying SegmentAt, slow-scenario probe recipe
metadata:
  type: project
---

First audited 2026-11-14 (uncommitted on top of 8e69a8d, SimVersion 14, snapshot format 11; no High found):
- FlowFieldSet grows mid-battle from the RegimentAi step (Navigation.MarchField, AssignRegiments' Work branch: one field per 8x8-cell map-aligned block a specialist works in). Sound for determinism ONLY because (a) fields are only reached through FieldOf/MarchField objects, never by index; (b) every Costs.Rebuild is followed by FlowFields.Repair over ALL fields, and Repair falls back to a full Rebuild when more than one cost rebuild passed, so a field built fresh after restore equals one repaired live; (c) NavigationSystem.AfterRestore now runs AssignRegiments (fixed the 2026-11-08 High). Re-check (a)-(c) whenever a field index is stored, a cost writer skips Repair, or fields get evicted.
- AfterRestore AssignRegiments reads Order/Point/costs at END of tick T-1; live used them at T-1's Navigation step. Only MoraleSystem (rout/rally) writes Order after Navigation today; it is unobservable only because the executor returns early for routed/Lost specialists and RegimentOrders never reads FieldOf. Any new post-Navigation writer of Order/Point/costs, or a non-specialist AI reader of FieldOf, breaks this.
- Fields are never evicted: Spademen along a long ditch add a full-map field per 8 cells; each costs ~21 bytes/cell, a Repair on every cost change and a RebuildAll on every restore. Perf/memory, not outcome.
- Surge: loose chain computed before any Fail (index order, BattlePrevious < s validated), so in-loop Broken mutation is harmless. A broken (non-loose) middle segment cuts the chain: segments beyond it survive a later upstream surge.
- Terrain.SegmentAt still returns broken segments, but since the audit FindWater and the pontoon/trestle pickers use StandingSegment and AddSegment claims every recorded cell, so a failed deck's cells can be bridged again (a restore lays the lists in the same order). Consequence: a train re-lays into a flood still at the surge line and spends its charges (no lay-time current check yet).
- sweepsAll sweeps units on the dry bank-end cells too.
- Repo determinism coverage: PontoonTests lake is still water (no surge, one movement chunk); M5PontoonScenario's thread test runs 300 s but the train lays and surges around 6 min, so CI's thread/restore checks never cover Surge. Probe recipe: copy sim/data/tools to scratch, ProbePontoon.cs in GodGame.Sim.Scenarios (m3-exit + m5-pontoon.json), one serial live run with an 8-tick snapshot ring flushed at started/built/first-fail(+6), end = fail+160; restore each into a fresh 3-thread sim AND one reused ran-ahead 8-thread sim; run in -c Release. Full PerSecondHashes x3 plus ~50 restores timed out at 25 min in Debug; the ring version took 3 m 48 s. Result 2026-11-14: train started 4166, first deck 7306, segments break one by one as laid (7344..7504), 30 restores fresh+ahead and 8 threads all equal. The multi-segment loose chain was NOT exercised (each segment broke alone); a probe needs a bridge finished before the surge.
See [[pattern-decks-load]], [[pattern-specialists-ground-edits]], [[pattern-terrain-navigation]].

**Why:** first time flow fields are added from the AI step and first chain-break failure in LoadSystem.
**How to apply:** for any new FlowFieldSet consumer, check it is reached by object, repaired on every cost change, and rebuilt on restore; for chain failures, compute the set before mutating.
