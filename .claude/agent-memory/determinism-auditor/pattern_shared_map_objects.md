---
name: pattern-shared-map-objects
description: PlayedMap-owned objects (DeckLayout segments, BoulderSpec arrays, SegmentOfCell, Blocked) are shared by live and preview sims; check that runtime code never mutates them
metadata:
  type: project
---

The Battle's PlayedMap (DeckLayout.Segments with their mutable Neighbours setter, SegmentOfCell, ApproachMask, Blocked, Boulders) is shared by every Simulation of that map: the live head, previews and timeline rebases. Terrain clones the arrays (ResetDecks), and battle-made DeckSegments are per-Terrain. Only static segments are shared, and AddDeck links neighbours only when previous >= StaticCount. As of 2026-11-14 this was clean.

Also: Terrain.ResetDecks copies Map.Blocked over _blocked on every restore. Any future runtime blocker (felled trees, wrecks, M5 item 7) must be re-applied from world state in RebuildFromWorld after ResetDecks, or it vanishes on restore.

**How to apply:** grep for assignments to DeckSegment/BoulderSpec members and for writes into map arrays outside the data/ build path. Related: [[pattern-snapshot-validators-vs-live-state]].
