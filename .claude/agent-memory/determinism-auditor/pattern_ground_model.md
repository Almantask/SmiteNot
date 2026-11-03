---
name: pattern-ground-model
description: Checks for GroundModel (M3 wetness/mud, first audited 2026-11-03 at 0553bc2) - AlsoWrites commit double-counts the shared water grid's quiet steps, churn scratch, unit reads in BeforeStep, thin-film drying branch
metadata:
  type: project
---

Check first when GroundModel, FieldSystem's AlsoWrites handling or ground rules change:
- A second model listing another model's primary field in AlsoWrites (ground lists water depth) makes FieldSystem call water.Commit(groundTiles), and Commit counts a quiet step for every unchanged tile. Ground steps every awake water tile, so still water gets +2 quiet per field step and sleeps after 3 steps instead of sleepAfterSteps (5). Deterministic but silently changes water's activity contract. Want: Commit for AlsoWrites grids wakes on change but never counts quiet.
- Churn scratch (_churn long[] per cell) is cleared via a touched list after the kernel; safe across restores as long as nothing throws between tally and clear. Kernel only reads it.
- Unit reads (Position, Velocity) are safe because Fields (step 1) runs before Movement (step 5); both are hashed. Recheck if the tick order or a launched/airborne state arrives (airborne units should not churn).
- Header-only golden was right: m1-flat initial wetness 8192 < mudCapStart 16384, so ground never wakes; old rulesets (no ground section) build no GroundModel.
- Overflow fine at loader ranges (worst per-unit churn ~6.6e9, x10k units ~6.6e13; span >= 1 because wet>0 needs w>CapStart).
- Drying branch runs under water thinner than dryDepth when soil is full, so thin films leak away via soak/dry oscillation (design note). Soak rounds room up, losing up to UnitsPerUm-1 units per cell per step.
- Fields/ still has no .meta files. See [[pattern-fields-tiles]], [[pattern-water-model]].

**Why:** first multi-model field coupling; the AlsoWrites-on-a-foreign-primary-field trap will recur with temperature/ice/fire.
**How to apply:** for any model whose AlsoWrites names another model's primary field, check the other field's sleep timing in a test with and without the second model.
