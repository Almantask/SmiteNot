---
name: pattern-timeline-keyframes
description: Checks for Timeline/KeyframeStore/Edit rebasing (M4 item 1, first audited 2026-11-07; one High) - kept keyframes keep the previous plan's layout and id, Edit not transactional, Rebase skips header checks, chained-edit fuzz probe
metadata:
  type: project
---

Check first when Timeline, KeyframeStore, a spill, Rebase or CastStates/CauseLog layout change:
- HIGH found 2026-11-07: Edit rebases only the keyframe at `at`; keyframes before it keep the previous plan's battle id and cast layout. A second edit that resumes from one of them reads it with the wrong plan's layout: FormatException when cast counts differ, and a silent wrong world when the counts match but ids shift (repro: p0 {c:200, d:1500} -> p1 {a:1500, c:200} -> p2 adds e:1400; a comes out Fired with c's state). Plain RestoreSnapshot of those keyframes also fails on battle id. Want: rebase every kept keyframe, or record each keyframe's plan and rebase from that one.
- Single edits were sound in the fuzz (about 26 first edits, fizzles and fires, keyframe periods 1/37/100/250, budget drop and spill): the world at `at` hashed equal to a fresh run. Cast at tick == k is safe (Check runs when context.Tick == cast.Tick, the step after the keyframe).
- Edit sets Battle = edited before Rebase/spill reads can throw, and DropAfter has already run, so a failed edit leaves the new plan paired with the old head and keyframes.
- Rebase ignores Snapshot.Read's battle id, seed and tick, so a foreign (shared spill dir) keyframe gets stamped with the new id. Spill files are named by tick only.
- SameCast/SameTerrain compare Power.Id / Rules.Id; a plan loaded with another Ruleset instance would run its own PowerRules under old.Rules' id.
- Probe: scratch console app (fresh subfolder, references Hosting), m3-exit + m3-exit-rain army, random casts, 3 chained edits per Timeline, compare hash at `at` and at a random target vs a fresh ThreadedRunner(3) run. About 10 s per edit; run 4 seeds in parallel in the background.
- .meta missing for Timeline/ and its files (fourteenth time). See [[pattern-snapshots-replays]], [[pattern-casts-powers]], [[pattern-cause-log]].

**Why:** the scrubbable preview's whole promise is that an edit resumes bit-identical to a fresh run; tests with one edit can't see chained-edit bugs.
**How to apply:** always test chains of edits, including one that resumes from before the previous edit's resume tick; check what each kept keyframe's header and layout belong to.
