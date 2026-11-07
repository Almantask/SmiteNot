---
name: pattern-cause-log
description: Checks for CauseLog/CauseSystem/cause emitters and map field layers (water.bin, M3 item 9, first audited 2026-11-07; no High) - emitter order, tag tie rule, stale per-slot links, rain restamping older tags, backwater gets no cause, shallow link validation
metadata:
  type: project
---

Check first when CauseLog, CauseSystem, a new cause emitter, or MapData.InitialFields / ApplyInitialFields change:
- Sound on 2026-11-07: all emitters serial, fixed order within a tick (PlanEvents casts in plan order -> Navigation refresh in cell order -> Hazards in slot order, drownings after the loop ascending); CauseSystem decides from a copy of pre-step tags and post-step outflows, so tile order is irrelevant. Its "newest, then larger inflow, then lower tile" tie rule is dead code: `tag <= best` returns first, so the outcome is just the max qualifying tag (fine, but the comment over-promises).
- Probe that passed (scratch console app referencing Hosting, in a fresh scratch subfolder: the old scratchpad/probe has a sim/ copy the SDK globs): m3-exit-rain 200 s, serial vs 3/8 threads, restores at ticks 0, 7, 1501, 1517, 1623, 2333, 3001 from ThreadedRunner(4) into a serial sim stepped 613: all identical. Repo tests have no thread or restore test of the log.
- Every per-slot log array (UnitSwept/UnitPushed, RegimentSwept/Tick) is cleared only on the paths that exist now (bank exit, drowning). DespawnUnit/RemoveRegiment don't reset them; harmless while slots are only reused after a fresh sweep, but every new despawn path (combat, M7) must clear them (or do it in DespawnUnit, the five-places rule).
- CastSystem.Write stamps TileTag = cause for every tile of the circle's bbox each field step, overwriting a newer tag with an older cast's (plan order, not fire order) and tagging for any written field. Want Max(tag, cause) and only for water writes.
- Tags follow net inflow, so backwater (river rising upstream of a storm) overflows with cause None: seen at ty 23-24 on m3-exit-rain.
- ReadFrom checks links are < count only, not kinds (TileTag -> CastFired, UnitSwept -> RegimentSwept), nor CastStates.Event against the log's count; CastStates.ReadFrom rejects Fired with Event None, so a world with casts but no log can't restore.
- water.bin: hashed into map hash (no count prefix), range-checked at apply only, duplicate ids allowed (last wins), non-ASCII ids decode to '?'. Outflows are only momentum (WaterModel zeroes closed/non-stepped edges and caps by depth), so inconsistent outflows can't break conservation. nav_speed is not in the layer; settled m3-exit river isn't foot-swept so no tick-0 overflow events, but a deeper settled river would log RiverOverflowed for every river tile at tick 0 and pre-empt real overflows.
- .meta missing for CauseLog.cs and CauseSystem.cs (thirteenth time). See [[pattern-casts-powers]], [[pattern-hazards]], [[pattern-field-navigation]], [[pattern-snapshots-replays]].

**Why:** the trace is what players and side quests read; links must stay right as despawn paths and powers multiply.
**How to apply:** for each new emitter check it is serial and placed in the tick order; for each new despawn/reuse path check the per-slot log arrays are cleared; rerun the probe with restores at non-refresh ticks.
