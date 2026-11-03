---
name: pattern-water-model
description: Checks for WaterModel (M3 virtual pipes, first audited 2026-11-03 at b879acc) - activity keyed on depth only so steady flow sleeps (source tile slept in a real run), terrain edits don't wake water, long overflow at loader-legal extremes, clamps that break conservation
metadata:
  type: project
---

Check first when WaterModel, FieldSystem activity or water rules change:
- Activity follows the depth grid only (AlsoWrites outflows don't count). A tile whose depth is integer-steady sleeps even with nonzero outflows; AddSources skips sleeping tiles, so a source tile can sleep and the source stops. Reproduced with a scratch harness (20 L/s channel: source tile slept from ~2100 s). Want: source tiles never sleep (pin awake each step) and/or count nonzero outflow as activity.
- Terrain.SetHeight wakes no field tiles; the ground cache refreshes on Version (monotonic, never restored, same Terrain instance, so the cache itself is safe) but sleeping water next to a new ditch never steps.
- Pipe-term overflow: (head diff x gain/1000) x depth, then f x 1e6 before the 1e9 clamp, then ppm x speed x dt. Safe at shipped numbers; overflows at loader-legal extremes (gain 10000, cell 0.1 m or 100 m, ppm 1e6, 20 km step). Recheck when ranges or cell caps change.
- Non-negative outflow holds only implicitly through the deadbands (no Max(0, f)).
- Conservation is exact except: AddSources Min(Max) and Commit's clamp to depth Max (1e8) drop water; (int) cast of depth before Clamp.
- Thread safety was fine: kernels write only own cells, read neighbours only if stepped; scratch reloaded each Step.
- .meta files missing again (Fields/ folder has none). See [[pattern-fields-tiles]], [[pattern-overflow-assert-side-effects]].

**Why:** Deterministic, but these make rivers stop or water appear frozen, and overflows would be UB under IL2CPP.
**How to apply:** for water changes, rerun a long low-discharge river and check IsAwake on the source tile; recompute worst-case products against loader ranges, not shipped values.
