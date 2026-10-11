---
name: new-terrain-piece
description: Add a terrain piece (channel, ridge, boulders, wall, bridge, dam and so on) as data plus the minimum code, with buffer and reachability checks, navigation stamping, counters, placement UI and tests.
argument-hint: "<piece name>"
---

Piece: $ARGUMENTS

Stub (M0): the checklist is final; fill in file paths as terrain pieces land in M3.

1. **Design:** read its entry in docs/design/power-palette.md and its wave in docs/design/content-roadmap.md; write its warning and its counter (content roadmap, rule 6) and its unit-side answer (rule 7).
2. **Footprint and data** (data/rules/terrain.json): shape, size limits, cost, material (wood, stone, earth), strength for load, what fields it writes.
3. **Buffer:** at least 100 m from both deployment zones for the player (designer-placed pieces are exempt, plan section 10); consequences may still flow into the buffer.
4. **Reachability:** the coarse fill must still find a route, at most twice the straight-line distance; a piece that would fully trap an army is refused while dragging (≤ 5–10 ms).
5. **Navigation stamping:** cost and blocked cells per movement class; incremental flow-field rebuild of the touched tiles.
6. **Materials and counters:** fire burns wood, rot weakens it, tremors topple stone, sappers undermine; strain stages if it carries load.
7. **Placement UI and preview overlay;** the art props and states (plan section 7.7).
8. **Tests:** buffer refusal at 99 m, reachability refusal, a scenario test per decision, a counter-web test.
