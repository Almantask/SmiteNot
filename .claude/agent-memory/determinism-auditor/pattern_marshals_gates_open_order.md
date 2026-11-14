---
name: pattern-marshals-gates-open-order
description: Checks for Crossing Marshals (SpecialistExecutor.Marshals.cs gate pre-pass, RegimentOrder.Hold, ArmyState.Open, mid-battle RegimentFiles) - multi-marshal JobRef sharing, FallBack point never reset, null JobCells on crafted Active meter jobs, single-chunk thread test, probe recipe
metadata:
  type: project
---

First audited 2026-11-14 (uncommitted, SimVersion 16, snapshot format 12). No determinism or restore High found.
Why it is restore-safe (re-check if any of these change):
- Gate scratch (_held/_onSpan/_toSpan/_inSpan/_queue) is written and read inside one RegimentOrders.Run; nothing crosses AI steps. Holds() is only read by RegimentOrders.Hold() in the same Run.
- Persistent gate state is all in ArmyState (JobRef = crossing regiment SLOT, WorkProgress = elapsed, JobCells = sorted segment ids, JobTargets = [entry cell, width mm], Point, Order Hold, Open) plus World.RegimentFiles (snapshotted, hashed, validated 1..max(1,StartingSize)).
- _wasHold is derived from Order at the start of the step, so the Hold point is kept across steps from state, not scratch.
- MarchField from the gate is by goal object (see [[pattern-pontoon-work-fields]]); Hold is a March-field order in AssignRegiments, a point order in Movement.
Gameplay/robustness notes found:
- JobRef is a regiment slot for meter jobs but other job kinds use it as segment/boulder ids; validator only checks >= -1, all uses guarded by HighWater/IsAlive.
- Two marshal regiments: each gate only excludes its OWN crossing regiment from its queue, so gates on different spans/timings can hold each other's crossing regiment (deadlock to crossingEndsTicks); one gate's Restore() closes order on a regiment another gate still meters.
- Threatened marshals FallBack overwrite Point; Meter never resets the post (FindSpan sets it once), so they stand at the fall-back spot afterwards.
- Crafted snapshot: Job Active + JobKind Meter + JobCells null passes the validator, then Gate NREs (MarkSpan foreach). Live can't produce it.
- CrossingMarshalTests thread test is 174 units = one movement chunk (same trap as [[pattern-movement-steering]]).
Probe recipe: ProbeMarshals.cs in a scratch copy of the test project (TestBattles.Map gorge 240x100, wooden-bridge, 2 marshals + 3 x 200 infantry = 648 units, 2 chunks), serial live run with StateHash every 20 ticks and snapshots every 100 ticks, restore into fresh ThreadedRunner(3) and a ran-ahead ThreadedRunner(8), compare 600 ticks later; second case sets Order[0]=Rout mid-crossing. Result 2026-11-14: two marshals (2 chunks, 84 snapshots, fresh + ran-ahead, 8 threads) all equal, about 7 min in Debug; the two gates ran in lockstep (same JobRef). Pitfall: an injected world write (the rout) must be replayed in every compared run, or every restore from before it shows a false MISMATCH.
- The crossing regiment takes open order at selection (tick 6 in the probe, about 100 m short), not on the span.

**Why:** gate pre-pass is a new pattern (serial AI pre-pass writing World.RegimentFiles and Order for other regiments).
**How to apply:** when gates, open order or Hold change, re-verify the scratch-is-same-step rule and run the probe; check multi-marshal interaction.
