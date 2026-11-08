---
name: pattern-combat-morale
description: Checks for Combat/ (CombatSystem swing kernel, volleys, MoraleSystem, RegimentOrderSystem, RegimentSummary) and ArmyState - goldens with no combat, single-chunk thread tests, 64-neighbour truncation in melee, empty-outline sentinel overflow, probe recipe
metadata:
  type: project
---

Check first when Combat/, ArmyState or the morale/rout parts of Movement/Navigation change (first audited 2026-11-08, M5 item 1, SimVersion 11; no High).
Fixed after that audit, before the push: the m5-melee golden (m5-field map, 2,400 soldiers, 437 killed and 828 fled in 180 s) now covers combat on CI; CombatDeterminismSlowTests runs 2,400 soldiers (5 chunks) on 1/3/8 threads and restores snapshots every 15 s through routs and fleeing; Gap/GapFrom return long.MaxValue for an empty regiment; the swing buffer is 256; volley flight is clamped to 4x flightTicksAtRange; World.CheckInvariants checks dead ArmyState slots are clear. Melee and the engaged slowdown now run only for regiments whose outline is within contact range of an enemy (RegimentSummary.NearEnemy), so a battle with no contact skips the swing pass and the second spatial hash entirely.
Notes from the audit itself (some now superseded, above):
- The m1-ten-thousand golden and the CLI default run have NO combat (killed 0, fled 0 at 240 s: the armies never meet). m2-bridge-ten-thousand has no defenders. So the goldens and 10k thread probes say nothing about combat. CombatTests' thread test is 350 units = one 512 chunk (same trap as [[pattern-movement-steering]]).
- Probe recipe that works (no repo edits): scratch map 300 x 300 m, zones x 0-135 / 165-300 m, 12 + 12 regiments of 200 (infantry, infantry, cavalry, ranged) at x 115/185 m (ranged 80/225 m), plan needs "terrain": [] and "casts": []. `godsim hash --map <dir> --plan <file> --threads N` 150 s: 945 killed, 2616 fled, 10 chunks. Matched on 1/3/8 threads (last 0e2c2d336d24675b). Restore probe: scratch console referencing GodGame.Sim + Hosting (RepoFiles.LoadRules, MapData.Load, BattlePlan.Load, new Battle), snapshots every 15 s, restore into fresh ThreadedRunner(3) sims and in place on the advanced sim: all matched 2026-11-08 (routs from 45 s, fleeing from 75 s, 19 regiments routing).
- Shape that keeps it safe: swing kernel writes only _strikeTarget/_strikeDamage/_engaged[u]; Hurt serial in striker order (first to cross 0 is the killer); volleys land then shoot in regiment order; despawn ascending after all damage; the spatial hash is not queried after a despawn (Debug.Assert in QueryRadius). Re-check that order if a new damage source lands between them.
- RegimentSummary empty regiment: MinX = long.MaxValue, MaxX = long.MinValue, so Gap/GapFrom on a regiment with Count 0 overflows (UB in IL2CPP, garbage in .NET). Every caller must guard Count > 0; re-grep callers each audit.
- Melee and engaged checks use the 64-neighbour buffer filled in row-major cell order, query radius includes the largest body (siege 1.5 m): in a packed melee the first 64 can be all friends, so a soldier misses an enemy in reach. Deterministic but biased.
- Navigation retreat fields are added to FlowFieldSet mid-battle (GetOrAdd); restore correctness rests on repair == rebuild ([[pattern-terrain-navigation]]).
- Scratch arrays in Combat/Morale/Orders/Movement were all rebuilt per run (no IRestoreAware needed). New systems: grep fields, confirm each is overwritten before read.

**Why:** Combat is where outcomes get decided; the existing goldens would not catch a combat desync.
**How to apply:** run the probe recipe whenever combat, morale or orders change; count chunks; check Count guards before Gap.
