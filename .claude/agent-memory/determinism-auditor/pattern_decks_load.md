---
name: pattern-decks-load
description: Checks for decks/load/strain/Rot (M5 item 4 - Decks.cs DeckLayout, StructureStates, LoadSystem, Navigation.SegmentFailed, Terrain broken flags, EffectiveDepthUm) - empty segments fail unloaded, lethal/footing ignore decks, restore/thread probe recipe
metadata:
  type: project
---

Check first when Decks.cs, LoadSystem, StructureStates, Terrain's deck API or bridge pieces change (first audited 2026-11-08, uncommitted, SimVersion 13, snapshot format 10; restore/threads sound):
- Sound shape: Terrain._broken is derived; RebuildFromWorld resets it from world.Structures.Broken BEFORE RebuildAll. SegmentFailed writes the failed cells' nav_speed codes straight into the hashed stored field (no hysteresis), so live and restore read the same stored codes and later refreshes compare against the same value. Dirty = DirtyAround(segment cells) covers every cell whose cost reads deck state (DeckClasses is radius 0; ApproachMask static); `_routes?.Update(dirty)` is there. OverChecks/Stage/LoadPermille/Rot* live in StructureStates; LoadSystem _load/_failed are scratch. Re-check if any cost/lethal/footing rule reads a NEIGHBOUR's deck state, or if Terrain.DeckVersion gets a reader (a ran-ahead sim's value differs after restore).
- AddBridge "cells already taken stay with the first": a plan bridge over a frozen pond, a stone bridge or another bridge gets 0-cell segments (MaxStrengthKg 0). `_load >= strength` (0 >= 0) fails them after FailChecks with nobody on them: SegmentFailed events, counter, and real neighbours weakened (probe: 163 segments, 9 empty, 9 failures in 40 ticks). CheckFootprints doesn't refuse bridge overlaps.
- LethalDistance.IsLethal reads heights only: over a gorge >= lethalDropMm (4 m) the bridge's end deck cells are cliff tops (LoadTests' gorge is 6 m). EffectiveDepthUm is 0 under a standing deck, so swept soldiers regain footing on it and FindFooting paddles them toward it.
- CostFields deck branch runs before IsBlocked (a bridge over a boulder opens it). Classes the deck doesn't carry get Impassable on its deck AND approach cells (a footbridge over a ford closes the ford to horses; a future water class can't pass under bridges). ColdDrowningTicks[(int)cls] has 3 entries, so a Water-class unit would throw.
- RotStart 0 is the "none" sentinel: a rot with windUpTicks 0 cast at tick 0 is lost (the loader allows both).
- Overflow bounds hold because stone/plan bridge points are validated on the map (px*dx <= ~1e15). Re-derive the ice MaxStrength bound (cells * cell^2 * thickness * kg) if patchCells (<= 64), thickness or cell-size caps rise.
- Repo LoadSlowTests: 250 units (one 512-unit movement chunk), snapshots only at multiples of 400 ticks (always a load-check tick). Probe recipe: copy sim/ data/ tools/ci into the scratchpad (output goes to sim/.build, so it's isolated from a repo run), add ProbeLoad.cs (gorge + wooden bridge + rot with 6x200 infantry + siege; 70 mm ice pond with 5x200 infantry + 100 cavalry), keep a 12-tick snapshot ring until first failure +6 (a snapshot every tick passed 750 MB), restore at failure -5..+6 into fresh and ran-ahead (4321-tick) sims, 1/3/8 threads. Gorge: first failure at tick 3581, all equal.
See [[pattern-field-navigation]], [[pattern-specialists-ground-edits]], [[pattern-hazards]], [[pattern-terrain-pieces]].

**Why:** decks make derived navigation depend on hashed structure state, and collapses are the M5 showcase consequence.
**How to apply:** for each deck-aware reader, check that restore rebuilds it from Broken; check CheckFootprints' overlap rules for every new piece kind; probe restores at non-check ticks around the first failure.
