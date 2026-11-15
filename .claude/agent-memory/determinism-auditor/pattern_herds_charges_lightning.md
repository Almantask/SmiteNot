---
name: pattern-herds-charges-lightning
description: Checks for herds (HerdSystem, HerdStates, animal steering), cavalry charges (Charges, ArmyState Charge*), Lightning v0/fire v0 (LightningStrikes, StrikeStates, Terrain._burning) and Impacts - plan-compare gaps, hash queried after despawn, single-chunk thread tests
metadata:
  type: project
---

First audited 2026-11-15 (M5 item 7, uncommitted). State coverage was complete: ArmyState Charge*, HerdStates (incl. Anchor, LastHit), StrikeStates, Blockers ClosesRoute/Clearer/Progress, UnitFlags.Charged all written, read and hashed. Scratch (_herd*, _herdOf, _ends, _targets, _cells) is overwritten each run. Kernel reads of Herds.LastHit / Army.ChargeSince are safe: their only writers are serial steps (RegimentAi, Impacts). RNG ids 8001/6101/6102 unique; launch sourceKey is stable (herd index, regiment, tree index) - unlike 6001 collapse ([[pattern-launches-ballistics]]).

Recurring trap, found again: every new PlannedCast/PlannedPiece/PlannedRegiment field must be in Timeline.SameCast/SameTerrain/SameArmy. 2026-11-15: HeadingCd (lightning fall), PlannedPiece.RadiusMm and Animals, and PlannedRegiment.Open were all missing, so an edit keeps stale keyframes. Grep the constructors against those methods every audit ([[pattern-timeline-keyframes]], [[pattern-specialists-ground-edits]]).

Other notes:
- Impacts.Stampedes and Charges.Contact despawn (Trample kills, rider deaths) and then query the same SpatialHash again: breaks SpatialHash's Debug.Assert (Units.Version). Release is deterministic (IsAlive filter, no spawns mid-step). Repo tests never hit a trample death followed by another query.
- ChargeEvent == CauseLog.None doubles as "has never charged": game logic depends on the cause log being attached / not full.
- Terrain.AnyBurning is sticky (stays true after fires go out; restore resets it). Safe only while it is a fast-path gate.
- Thread tests: herd test 160 units, charge test 260 units = one 512-unit chunk; herd test has no stampede contact. Neither exercises parallel kernels with herds/charges.
- AnimalHeading `(uint)(radius*radius)` overflows for herd radius > ~66.5 m (loader allows 1,000 km); data caps at 40 m.

**How to apply:** grep plan constructors vs Timeline compares; check every despawn inside a loop that queries a hash; count chunks in new thread tests.
