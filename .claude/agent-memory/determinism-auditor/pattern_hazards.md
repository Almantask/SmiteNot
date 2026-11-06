---
name: pattern-hazards
description: Checks for HazardSystem / swept-stunned-teetering state, HazardTimer, BattleCounters - kernel flag scratch, movement->hazards hand-off of Teetering timer 0, nav-code (1 Hz) vs live-depth mismatch, counter misattribution, thin thread tests
metadata:
  type: project
---

Check first when Hazards/, UnitFlags hazard bits, HazardTimer or the teeter code in MovementSystem change (first audited 2026-11-06, M3 item 6, SimVersion 8, snapshot format 5; no High, no Medium):
- Shape that is safe today: the movement kernel copies Flags/HazardTimer into _newFlags/_newTimer for its own slot first thing (before any `continue`), commits serially; HazardSystem is serial in slot order, collects drowned slots and despawns ascending after the loop. Re-check that every new early `continue` in the kernel comes after the scratch copy, or the commit writes stale scratch.
- Hand-off invariant: movement leaves "Teetering, timer 0" (went over); only the Hazards step (TickStep 8, after Movement 5) resolves it (swept, or flag cleared). Any harness that registers Movement without Hazards, or a snapshot taken mid-tick, would carry that state. CheckInvariants allows it (hazard != None), so it would not be caught.
- Teeter/go-over decide from FieldSpeed codes (refreshed at 1 Hz) and _typeCosts; Hazards decides from live depth (um/1000 truncated) and WaterModel.Velocity. They disagree between refreshes: a soldier can be pushed into a code-Swept cell the live water no longer sweeps. Deterministic, but watch the test assertion Swept == PushedOver.
- Counters are hashed: PushedOver++ fires for any Teetering soldier the Sweeps branch catches, including when water rose under him (misattribution becomes state). Cause log (M3 item 9) should own attribution.
- HazardTests' runner test is 600 units = 2 chunks only. Scratch probe (8 x 150 infantry at the moat, 3000 ticks, serial vs 3 vs 8 threads, snapshot -> serial restore every 37 ticks) matched: last BD119354C975F065, 375 swept/pushed, 374 bank, 0 drowned. Drowning/despawn is covered only serially (basin test).
- Loader allows sweptDepthMm 0: RegainsFooting(0,0) is false, so a swept soldier never regains footing even on dry ground.
- Hazards/ folder, HazardSystem.cs, BattleCounters.cs had no .meta files (eleventh time). See [[pattern-overflow-assert-side-effects]].

**Why:** crush, ice, bridges and fire (M3-M5) will reuse this swept/stunned/teeter state machine and the counters.
**How to apply:** trace each flag/timer pair through movement then hazards, compare what decides go-over against what decides swept, and count chunks in the thread test. See [[pattern-movement-steering]], [[pattern-world-soa-pools]].
