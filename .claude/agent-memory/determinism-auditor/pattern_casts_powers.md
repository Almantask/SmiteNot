---
name: pattern-casts-powers
description: Checks for CastSystem/CastStates/PowerRules/plan casts (M3 item 7, Rain; first audited 2026-11-07, no High) - power writes to any field id, duration not a multiple of the field period, shallow CastStates validation, direct Values writes before FieldSystem, missing .meta
metadata:
  type: project
---

Check first when casts, powers.json or the plan's casts layer change:
- Ordering was sound: casts sorted by unique ordinal id in both BattlePlan and canonical JSON; CastStates index = sorted order; the per-(regiment, coarse cell) Dictionary is cleared and only looked up; nearest outline ties go to lower unit slot (deterministic). Re-verify if anyone iterates the dictionary or sorts casts by tick.
- Rain writes grid.Values directly in PlanEvents on FieldsRate ticks, then WakeAround on the circle's tiles; FieldSystem's PrepareStep copies Values to Next afterwards, so it is safe ONLY while the target field's model is non-staggered at FieldsRate. A staggered or other-rate model would see the write on tiles not due, or miss it. Ground wakes wetness from awake water tiles.
- Loader accepts any fields.json id as a power write target (nav_speed, water outflows, mud) - bypasses the owning model's invariants. Want a whitelist/flag.
- durationTicks not a multiple of FieldsRate.Period: number of writes depends on cast-tick phase (deterministic, but the total changes by one step when the cast moves 1 tick).
- CastStates.ReadFrom checks status/reasons/carry ranges only: Pending with a nearest regiment, Fired with NoRegiment distances etc. are not rejected.
- No-cast plans append nothing to StateHash (no count prefix), which is why adding casts kept per-second hashes and SimVersion 8.
- .meta missing for Casts/, CastStates.cs, PowerRules.cs (ninth time). See [[pattern-fields-tiles]], [[pattern-water-model]], [[pattern-snapshots-replays]].

**Why:** casts are the first external writer into fields and the first plan-driven per-tick state; scrubbing edits them constantly.
**How to apply:** for each new power, check its target field's model rate/stagger, the write's overflow against loader ranges (rate 1e8 x period fits long; Clamp saturates), and that CastStates covers any new per-cast state in WriteTo/ReadFrom/AppendTo/CreateBlank.
